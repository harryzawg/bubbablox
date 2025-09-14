using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Roblox.Logging;
using System.Net;
using System.Xml;
using Roblox;

namespace Roblox.Rendering
{
    public static class CommandHandler
    {
        private static System.Threading.Mutex mux { get; set; } = new();
        private static ClientWebSocket? ws { get; set; }
        private static Dictionary<string, Func<RenderResponse<Stream>,int>> resultListeners { get; } = new();
        private static Uri wsUrl { get; set; }
		private static Dictionary<int, Process> rccProcesses { get; } = new();
        private static Random random { get; } = new();
        private static object rccLock { get; } = new();
		private static SemaphoreSlim R15RenderLock { get; } = new(1, 1);
		private static Process? rccProcess;
		private static int? rccPort;

        public static void Configure(string baseUrl, string authorization)
        {
            var url = new Uri(baseUrl + "?key=" + HttpUtility.UrlEncode(authorization));
            wsUrl = url;

            Task.Run(async () =>
            {
                await ConnectionManager();
            });
        }
		
		private static bool IsPortInUse(int port)
		{
			var TCP = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
			try
			{
				TCP.Start();
				return true;
			}
			catch (System.Net.Sockets.SocketException)
			{
				return false;
			}
			finally
			{
				try
				{
					TCP.Stop();
				}
				catch { }
			}
		}
		
		private static int GetRandomPortRCC2020()
		{
			lock (rccLock)
			{
				int port;
				bool available;

				do
				{
					port = random.Next(20000, 40000);
					available = IsPortInUse(port);
				} while (!available);

				return port;
			}
		}

        private static async Task ListenForMessages()
        {
            // allocate 8mb
            using var memory = MemoryPool<byte>.Shared.Rent(1024 * 1024 * 8);

            while (true)
            {
                try
                {
                    var result = await ws.ReceiveAsync(memory.Memory, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("[info] Render websocket closed. Re-opening...");
                        continue;
                    }

                    var msg = Encoding.UTF8.GetString(memory.Memory.Span.Slice(0, result.Count));

                    Console.WriteLine("Received WS message, result={0}", msg.Substring(0,100)+"...");
                    var decoded = JsonSerializer.Deserialize<RenderResponse<string>>(msg);
                    if (decoded == null)
                    {
                        Console.WriteLine("Got invalid WS message - it was null");
                        continue;
                    }
                    var newResponse = new RenderResponse<Stream>()
                    {
                        id = decoded.id,
                        status = decoded.status,
                        data = null,
                    };

                    // data is null when statusCode != 200
                    if (decoded.data != null)
                    {
                        var bytes = Convert.FromBase64String(decoded.data);
                        newResponse.data = new MemoryStream(bytes);
                    }

                    mux.WaitOne();
                    try
                    {
                        var hasListener = resultListeners.ContainsKey(decoded.id);
                        if (hasListener)
                        {
                            resultListeners[decoded.id](newResponse);
                            resultListeners.Remove(decoded.id);
                        }
                        else
                        {
                            Console.WriteLine("[warning] got message for item without listener. id = {0}", decoded.id);
                        }
                    }
                    finally
                    {
                        mux.ReleaseMutex();
                    }
                }
                catch (System.Exception e)
                {
                    Console.WriteLine("Got error in ws connection {0}", e.Message);
                    throw;
                }
            }
        }
        
        private static async Task ConnectionManager()
        {
            while (true)
            {
                try
                {
                    mux.WaitOne();
                    ws ??= new ClientWebSocket();
                    var wsCurrentState = ws.State;
                    mux.ReleaseMutex();
                    
                    if (wsCurrentState is WebSocketState.Aborted or WebSocketState.Closed or WebSocketState.None or WebSocketState.CloseReceived or WebSocketState.CloseSent)
                    {
                        Console.WriteLine("[info] ws connection is in state {0}, so we are re-connecting (did you start the renderer?) state:", ws.State);
                        mux.WaitOne();
                        ws = new ClientWebSocket();
                        mux.ReleaseMutex();
                        await ws.ConnectAsync(wsUrl, CancellationToken.None);
                    }
                    await ListenForMessages();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[info] ConnectionManager error in WebSocket connection {0} error:", e.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }

        private static async Task<RenderResponse<Stream>> SendCommand(string command, IEnumerable<dynamic> arguments, CancellationToken? cancellationToken)
        {
            var id = Guid.NewGuid().ToString();
            var cmd = new RenderRequest()
            {
                command = command,
                args = arguments,
                id = id,
            };
            var res = new TaskCompletionSource<RenderResponse<Stream>>();
            var responseMutex = new Mutex();
            
            // Setup listener
            mux.WaitOne();
            resultListeners[id] = stream =>
            {
                //Console.WriteLine("[info] SendCommand() over");
                lock (responseMutex)
                {
                    res.SetResult(stream);
                }

                return 0; 
            };
            mux.ReleaseMutex();
            // Send message
            var bits = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmd));
            while (ws is not {State: WebSocketState.Open})
            {
                //Writer.Info(LogGroup.GeneralRender, "Ws not available, retry in a second");
#if DEBUG 
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken  ?? CancellationToken.None);
#else
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken  ?? CancellationToken.None);
#endif
                if (cancellationToken is {IsCancellationRequested: true})
                    throw new TaskCanceledException();
            }
            await ws.SendAsync(bits, WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);

            await using var register = cancellationToken?.Register(() =>
            {
                mux.WaitOne();
                resultListeners.Remove(id);
                mux.ReleaseMutex();
                // TODO: Would be nice if we could send a message to WS telling it to cancel the task
                // TrySetCanceled instead of SetCanceled due to WEB-35
                lock (responseMutex)
                {
                    if (res.TrySetCanceled(cancellationToken.Value) && command != "Cancel")
                    {
                        SendCommand("Cancel", new List<dynamic>()
                        {
                            id,
                        }, CancellationToken.None);
                    }
                }
            });
            var resp = await res.Task;
            return resp;
        }   
        
        private static async Task<Stream> SendCmdWithErrHandlingAsync(string cmd, IEnumerable<dynamic> arguments, CancellationToken? cancellationToken = null)
        {
            var result = await SendCommand(cmd, arguments, cancellationToken);
            if (result.status != 200) throw new Exception("Render failed with status = " + result.status);
            if (result.data == null) throw new Exception("Null stream returned from SendCommand");
            return result.data;
        }
		
		private static async Task SendCloseJobRequest(int port, string jobId)
		{
			var XML = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
		<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" 
						   xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/"" 
						   xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
						   xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
						   xmlns:ns2=""http://roblox.com/RCCServiceSoap"" 
						   xmlns:ns1=""http://roblox.com/"" 
						   xmlns:ns3=""http://roblox.com/RCCServiceSoap12"">
			<SOAP-ENV:Body>
				<ns1:CloseJob>
					<ns1:jobID>{jobId}</ns1:jobID>
				</ns1:CloseJob>
			</SOAP-ENV:Body>
		</SOAP-ENV:Envelope>";

			await SendSoapRequest(port, "http://roblox.com/CloseJob", XML);
		}  
		
		private static readonly HttpClient httpClient = new()
		{
			Timeout = TimeSpan.FromMinutes(1)
		};
		
		private static async Task<int> StartRccService()
		{
			lock (rccLock)
			{
				if (rccProcess != null && !rccProcess.HasExited && rccPort.HasValue)
					return rccPort.Value;

				rccPort = GetRandomPortRCC2020();

				var rccPath = Path.Combine(Roblox.Configuration.RccService2020Path, "RCCService.exe");
				if (string.IsNullOrEmpty(rccPath) || !File.Exists(rccPath))
					throw new Exception("RCC 2020 path not configured or RCC exe doesn't exist");

				var processStartInfo = new ProcessStartInfo
				{
					FileName = rccPath,
					Arguments = $"-console -verbose -port {rccPort.Value}",
					UseShellExecute = true,
					CreateNoWindow = false
				};

				rccProcess = new Process { StartInfo = processStartInfo };

				if (!rccProcess.Start())
					throw new Exception("Failed to start RCC 2020 process");

				return rccPort.Value;
			}
		}
		
		private static async Task<string> SendSoapRequest(int port, string soapAction, string xmlBody)
		{
			var url = $"http://localhost:{port}";
			using var request = new HttpRequestMessage(HttpMethod.Post, url);
			request.Headers.Add("SOAPAction", soapAction);
			request.Content = new StringContent(xmlBody, Encoding.UTF8, "text/xml");

			var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync();
		}

		private static async Task<Stream> RenderR15(long userId, string renderType, CancellationToken? cancellationToken = null)
		{
			var port = await StartRccService();
			var jobId = Guid.NewGuid().ToString();
			var baseUrl = Roblox.Configuration.BaseUrl;

			var charApp = $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={userId}";

			var Json = new
			{
				Mode = "Thumbnail",
				Settings = new
				{
					Type = renderType,
					Arguments = new object[]
					{
						Roblox.Configuration.BaseUrl,
						charApp,
						"Png",
						840,
						840,
					}
				}
			};

			var finalJson = JsonSerializer.Serialize(Json);

			var XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
		<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
		   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
		   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
			<soap:Body>
				<OpenJob xmlns=""http://roblox.com/"">
					<job>
						<id>{jobId}</id>
						<expirationInSeconds>60</expirationInSeconds>
						<category>0</category>
						<cores>1</cores>
					</job>
					<script>
						<name>GameServer</name>
						<script>{finalJson}</script>
					</script>
					<arguments>
						<LuaValue>
							<type>LUA_TNIL</type>
						</LuaValue>
					</arguments>
				</OpenJob>
			</soap:Body>
		</soap:Envelope>";

			try
			{
				var res = await SendSoapRequest(port, "http://roblox.com/OpenJob", XML);
				
				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(res);
				
				var NSManager = new XmlNamespaceManager(xmlDoc.NameTable);
				NSManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
				NSManager.AddNamespace("ns1", "http://roblox.com/");
				
				var resNodes = xmlDoc.SelectNodes("//soap:Envelope/soap:Body/ns1:OpenJobResponse/ns1:OpenJobResult", NSManager);
				using var httpClient = new HttpClient();
				foreach (XmlNode resultNode in resNodes)
				{
					var typeNode = resultNode.SelectSingleNode("ns1:type", NSManager);
					var valueNode = resultNode.SelectSingleNode("ns1:value", NSManager);
					
					if (typeNode != null && valueNode != null && 
						// tstring contains the actual b64 render
						typeNode.InnerText == "LUA_TSTRING" && 
						!string.IsNullOrEmpty(valueNode.InnerText))
					{
						try
						{
							var imgBytes = Convert.FromBase64String(valueNode.InnerText);
							await SendCloseJobRequest(port, jobId);								
							return new MemoryStream(imgBytes);						
						}
						catch (FormatException)
						{
							continue;
						}
					}
				}
				
				throw new Exception("no b64 found in RCC Response");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"R15 {renderType} rendering failed: {ex.Message}");
				throw;
			}
		}

		public static async Task<Stream> RequestPlayerThumbnailR15(long userId, CancellationToken? cancellationToken = null)
		{
			await R15RenderLock.WaitAsync(cancellationToken ?? CancellationToken.None);
			
			try
			{
				return await RenderR15(userId, "Avatar_R15_Action", cancellationToken);
			}
			finally
			{
				R15RenderLock.Release();
			}
		}

        public static async Task<Stream> RequestPlayerThumbnail(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");
            
            // todo: do we need to get assetTypeId here, or can we just expect caller to get it for us?
            var w = new Stopwatch();
            w.Start();
            
            var result = await SendCommand("GenerateThumbnail",
                new List<dynamic> {data}, cancellationToken);
            w.Stop();
            if (result.status != 200 || result.data == null)
            {
                if (result.data == null && result.status == 200)
                    Roblox.Metrics.RenderMetrics.ReportRenderAvatarThumbnailFailureDueToNullBody(data.userId);
                Roblox.Metrics.RenderMetrics.ReportRenderAvatarThumbnailFailure(data.userId);
                throw new Exception("Render failed with status = " + result.status);
            }
            Metrics.RenderMetrics.ReportRenderAvatarThumbnailTime(data.userId, w.ElapsedMilliseconds);
            return result.data;
        }

        public static async Task<Stream> RequestPlayerHeadshot(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");
            
            // todo: do we need to get assetTypeId here, or can we just expect caller to get it for us?
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailHeadshot", new List<dynamic> {data}, cancellationToken);
        }

        public static async Task<Stream> RequestTextureThumbnail(long assetId, int assetTypeId, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailTexture", new List<dynamic>
            {
                assetId, 
                assetTypeId
            }, cancellationToken);
        }
        
        public static async Task<Stream> RequestAssetThumbnail(long assetId, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailAsset", new List<dynamic>
            {
                assetId, 
            }, cancellationToken);
        }
        

        public static async Task<Stream> RequestHeadThumbnail(long assetId, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailHead", new List<dynamic>
            {
                assetId, 
            }, cancellationToken);
        }
		
        public static async Task<Stream> RequestAssetMesh(long assetId, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailMesh", new List<dynamic>
            {
                assetId, 
            }, cancellationToken);
        }

        public static async Task<Stream> RequestPlaceConversion(string base64EncodedPlace, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("ConvertRobloxPlace", new List<dynamic>
            {
                base64EncodedPlace, 
            }, cancellationToken);
        }

        public static async Task<Stream> RequestHatConversion(string base64EncodedHat,
            CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("ConvertHat", new List<dynamic>()
            {
                base64EncodedHat,
            });
        }
        
        public static async Task<Stream> RequestAssetGame(long assetId, int x, int y, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailGame", new List<dynamic>
            {
                assetId,
                x,
                y,
            }, cancellationToken);
        }

        public static async Task<Stream> RequestAssetTeeShirt(long assetId, long contentId, CancellationToken? cancellationToken = null)
        {
            return await SendCmdWithErrHandlingAsync("GenerateThumbnailTeeShirt", new List<dynamic>
            {
                assetId,
                contentId,
            }, cancellationToken);
        }
    }
}

