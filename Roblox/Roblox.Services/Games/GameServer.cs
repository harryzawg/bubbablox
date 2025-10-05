using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security;
using System.Collections.Concurrent;
using Dapper;
using Roblox.Dto.Games;
using Roblox.Libraries.EasyJwt;
using Roblox.Libraries.Password;
using Roblox.Logging;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.GameServer;
using Roblox.Rendering;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;

namespace Roblox.Services;

public class GameServerService : ServiceBase
{
    private const string ClientJoinTicketType = "GameJoinTicketV1.1";
    private const string ServerJoinTicketType = "GameServerTicketV2";
    private static HttpClient client { get; } = new();
    private static string jwtKey { get; set; } = string.Empty;
    private static EasyJwt jwt { get; } = new();
    private static Random RandomComponent = new Random();
    private static PasswordHasher hasher { get; } = new();
    private static Dictionary<long, long> gamePlayerCounts = new Dictionary<long, long>(); // placeid, playercount
    private static ConcurrentDictionary<string, Process> jobRccs = new ConcurrentDictionary<string, Process>(); // jobid, rcc process
    public static ConcurrentDictionary<string, int> currentGameServerPorts = new ConcurrentDictionary<string, int>(); // networkserver ports, jobid, port
    private static ConcurrentDictionary<long, ConcurrentBag<string>> currentPlaceIdsInUse = new ConcurrentDictionary<long, ConcurrentBag<string>>(); // placeid, jobid
    public static Dictionary<long, long> CurrentPlayersInGame = new Dictionary<long, long>() { }; // userid, placeid
    public static Dictionary<Process, int> mainRCCPortsInUse = new Dictionary<Process, int>(); // Process, main RCC soap port
	public ConcurrentDictionary<string, Process> JobRccs => jobRccs;
    public static void Configure(string newJwtKey)
    {
        jwtKey = newJwtKey;
    }

    private string HashIpAddress(string hashedIpAddress)
    {
        return hasher.Hash(hashedIpAddress);
    }

    private bool VerifyIpAddress(string hashedIpAddress, string providedIpAddress)
    {
        return hasher.Verify(hashedIpAddress, providedIpAddress);
    }

    /// <summary>
    /// Create a ticket for joining a game
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="placeId">The ID of the place</param>
    /// <param name="ipHash">The IP Address from ControllerBase.GetIP()</param>
    /// <returns></returns>
    public string CreateTicket(long userId, long placeId, string ipHash)
    {
        var entry = new GameServerJwt
        {
            t = ClientJoinTicketType,
            userId = userId,
            placeId = placeId,
            ip = HashIpAddress(ipHash),
            iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        return jwt.CreateJwt(entry, jwtKey);
    }

    public bool IsExpired(long issuedAt)
    {
        var createdAt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(issuedAt);
        var notExpired = createdAt.Add(TimeSpan.FromMinutes(5)) > DateTime.UtcNow;
        if (!notExpired)
        {
            return true;
        }

        return false;
    }

    public GameServerJwt DecodeTicket(string ticket, string? expectedIpAddress)
    {
        var value = jwt.DecodeJwt<GameServerJwt>(ticket, jwtKey);
        if (value.t != ClientJoinTicketType) throw new ArgumentException("Invalid ticket");
        if (IsExpired(value.iat))
        {
            throw new ArgumentException("Invalid ticket");
        }

        if (expectedIpAddress != null)
        {
            var ipOk = hasher.Verify(value.ip, expectedIpAddress);
            if (!ipOk)
            {
                throw new ArgumentException("Invalid ticket");
            }
        }

        return value;
    }

    public string CreateGameServerTicket(long placeId, string domain)
    {
        var ticket = new GameServerTicketJwt
        {
            t = ServerJoinTicketType,
            placeId = placeId,
            domain = domain,
            iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        return jwt.CreateJwt(ticket, jwtKey);
    }

    public GameServerTicketJwt DecodeGameServerTicket(string ticket)
    {
        var value = jwt.DecodeJwt<GameServerTicketJwt>(ticket, jwtKey);
        if (value.t != ServerJoinTicketType) throw new ArgumentException("Invalid ticket");
        if (IsExpired(value.iat))
        {
            throw new ArgumentException("Invalid ticket");
        }

        return value;
    }

    public async Task OnPlayerJoin(long userId, long placeId, string serverId)
    {
        CurrentPlayersInGame.Add(userId, placeId);
        await db.ExecuteAsync(
            "INSERT INTO asset_server_player (asset_id, user_id, server_id) VALUES (:asset_id, :user_id, :server_id::uuid)",
            new
            {
                asset_id = placeId,
                user_id = userId,
                server_id = serverId,
            });
        await InsertAsync("asset_play_history", new
        {
            asset_id = placeId,
            user_id = userId,
        });
        await db.ExecuteAsync("UPDATE asset_place SET visit_count = visit_count + 1 WHERE asset_id = :id", new
        {
            id = placeId,
        });
        // give ticket to creator
        await InTransaction(async _ =>
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
            var placeDetails = await assets.GetAssetCatalogInfo(placeId);
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            if (placeDetails.creatorType == CreatorType.Group)
            {
                await InsertAsync("user_transaction", new
                {
                    amount = 10,
                    currency_type = CurrencyType.Tickets,
                    user_id_one = (long?)null,
                    user_id_two = userId,
                    group_id_one = placeDetails.creatorTargetId,
                    type = PurchaseType.PlaceVisit,
                    // store id of the game as well
                    asset_id = placeId,
                });
            }
            else
            {
                await ec.IncrementCurrency(placeDetails.creatorTargetId, CurrencyType.Tickets, 1);
                await InsertAsync("user_transaction", new
                {
                    amount = 10,
                    currency_type = CurrencyType.Tickets,
                    user_id_one = placeDetails.creatorTargetId,
                    user_id_two = userId,
                    type = PurchaseType.PlaceVisit,
                    // store id of the game as well
                    asset_id = placeId,
                });
            }
			
			var CurrentVisits = await db.QueryFirstOrDefaultAsync<long>(
				"SELECT visit_count FROM asset_place WHERE asset_id = :id", 
				new { id = placeId });
				
			// make thus better
			using var users = ServiceProvider.GetOrCreate<UsersService>(this);
			if (CurrentVisits >= 100)
			{
				Console.WriteLine("Giving homestead");
				await users.GiveUserBadge(placeDetails.creatorTargetId, 6); // Homestead
			}
			
			if (CurrentVisits >= 1000)
			{
				Console.WriteLine("Giving bricksmith");
				await users.GiveUserBadge(placeDetails.creatorTargetId, 7); // Bricksmith
			}

            return 0;
        });
    }

    public async Task OnPlayerLeave(long userId, long placeId, string serverId)
    {
        CurrentPlayersInGame.Remove(userId);
        await db.ExecuteAsync(
            "DELETE FROM asset_server_player WHERE user_id = :user_id AND server_id = :server_id::uuid", new
            {
                server_id = serverId,
                user_id = userId,
            });
        Console.WriteLine("deleted from db, placer left (onplayerleave)");
        var latestSession = await db.QuerySingleOrDefaultAsync<AssetPlayEntry>(
            "SELECT id, created_at as createdAt FROM asset_play_history WHERE user_id = :user_id AND asset_id = :asset_id AND ended_at IS NULL ORDER BY asset_play_history.id DESC LIMIT 1",
            new
            {
                user_id = userId,
                asset_id = placeId,
            });
        if (latestSession != null)
        {
            await db.ExecuteAsync("UPDATE asset_play_history SET ended_at = now() WHERE id = :id", new
            {
                id = latestSession.id,
            });
            
            if (latestSession.createdAt.Year != DateTime.UtcNow.Year) return;
            
            var playTimeMinutes = (long)Math.Truncate((DateTime.UtcNow - latestSession.createdAt).TotalMinutes);
            var earnedTickets = Math.Min(playTimeMinutes * 10, 60); // temp cap, might reduce in the future?
            // cap is 10k tickets per 12 hours (about 1k robux)
            const long maxEarningsPerPeriod = 10000;
            using (var ec = ServiceProvider.GetOrCreate<EconomyService>(this))
            {
                var earningsToday =
                    await ec.CountTransactionEarningsOfType(userId, PurchaseType.PlayingGame, null, TimeSpan.FromHours(12));
                
                if (earningsToday >= maxEarningsPerPeriod)
                    return;
            }
            
            await InTransaction(async _ =>
            {
                using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
                await ec.IncrementCurrency(userId, CurrencyType.Tickets, earnedTickets);
                await InsertAsync("user_transaction", new
                {
                    amount = earnedTickets,
                    currency_type = CurrencyType.Tickets,
                    user_id_one = userId,
                    user_id_two = 1,
                    type = PurchaseType.PlayingGame,
                    // store id of the game they played as well
                    asset_id = placeId,
                });

                return 0;
            });
        }
    }

	// make this 1 call later
	public void ShutDownServer(string serverId)
	{
		if (string.IsNullOrEmpty(serverId))
		{
			Console.WriteLine("[GS] serverId is null or empty, cannot shut down");
			return;
		}

		try
		{
			Console.WriteLine($"[GS] shutting down server {serverId}");

			string placeJobId = serverId;
			long placeId = GetPlaceIdByJobId(serverId);
			if (placeId == 0)
			{
				Console.WriteLine($"[GS] could not find placeId for server: {serverId}");
				return;
			}

			if (!jobRccs.TryGetValue(placeJobId, out Process rccProcess))
			{
				Console.WriteLine($"[GS] process for server {serverId} not found");
				return;
			}

			if (!rccProcess.HasExited)
			{
				Console.WriteLine($"[GS] killing process for serverId: {serverId}");
				rccProcess.Kill();
			}

			// clean up
			if (currentPlaceIdsInUse.TryGetValue(placeId, out var jobList))
			{
				var DagLover34 = new ConcurrentBag<string>(jobList.Where(j => j != placeJobId));
				if (DagLover34.IsEmpty)
					currentPlaceIdsInUse.TryRemove(placeId, out _);
				else
					currentPlaceIdsInUse[placeId] = DagLover34;
			}
			currentGameServerPorts.TryRemove(placeJobId, out var removedNSport);
			jobRccs.TryRemove(placeJobId, out var removedRCCport);
			mainRCCPortsInUse.Remove(rccProcess);
			RemoveAllPlayersFromPlaceId(placeId);
			
			Task.Run(async () =>
			{
				try
				{
					await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new
					{
						id = serverId
					});
					Console.WriteLine($"[GS] deleted server {serverId} from DB");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[GS] failed to delete server from DB {serverId}: {ex.Message}");
				}
			});
			
			Console.WriteLine($"[GS] {placeJobId} (place {placeId}) was closed");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GS] failed to shutdown {serverId}: {ex.Message}");
			throw;
		}
	}
    
    public static void RemoveAllPlayersFromPlaceId(long placeId)
    {
        List<long> playersToRemove = CurrentPlayersInGame.Where(kvp => kvp.Value == placeId).Select(kvp => kvp.Key).ToList();
    
        foreach (var playerID in playersToRemove)
        {
            CurrentPlayersInGame.Remove(playerID);
        }
    }
	
	public async Task<string> GetJobIdByUserId(long userId)
    {
        var result = await db.QueryFirstOrDefaultAsync<Guid>(
            "SELECT server_id FROM asset_server_player WHERE user_id = :userId",
            new { userId }
        );
        
        return result == Guid.Empty ? "" : result.ToString();
    }
    
    public static long GetUserPlaceId(long userId) // get user game is in
    {
        bool isInGame = CurrentPlayersInGame.ContainsKey(userId);
        if (!isInGame)
            return 0;

        return CurrentPlayersInGame[userId];
    }
    
	public static long GetPlaceIdByJobId(string jobId)
	{
		foreach (var kvp in currentPlaceIdsInUse)
		{
			if (kvp.Value.Contains(jobId))
			{
				return kvp.Key;
			}
		}
		return 0;
	}

	public async Task<DateTime> GetLastServerPing(string serverId)
	{
		var result = await db.QuerySingleOrDefaultAsync("SELECT updated_at FROM asset_server WHERE id = :id::uuid", new
		{
			id = serverId,
		});

		if (result == null || result.updated_at == null)
		{
			throw new InvalidOperationException($"No server info found for serverId: {serverId}");
		}

		return (DateTime)result.updated_at;
	}

    public async Task SetServerPing(string serverId)
    {
        await db.ExecuteAsync("UPDATE asset_server SET updated_at = :u WHERE id = :id::uuid", new
        {
            u = DateTime.UtcNow,
            id = serverId,
        });
    }
	
	public async Task<dynamic> GetStatus()
	{
		var JobRCCList = new List<dynamic>();
		
		foreach (var kvp in jobRccs)
		{
			var process = kvp.Value;
			try
			{
				JobRCCList.Add(new
				{
					jobId = kvp.Key,
					processId = process.Id,
					processName = process.ProcessName,
					hasExited = process.HasExited,
					startTime = process.StartTime,
					memoryUsage = process.WorkingSet64 / 1024 / 1024 + " MB",
					memoryUsageMB = process.WorkingSet64 / 1024 / 1024,
					threads = process.Threads.Count,
					responding = !process.HasExited && process.Responding
				});
			}
			catch (Exception)
			{
				JobRCCList.Add(new
				{
					jobId = kvp.Key,
					processId = -1,
					processName = "RCCService",
					hasExited = true,
					startTime = DateTime.MinValue,
					memoryUsage = "Error",
					memoryUsageMB = 0,
					threads = 0,
					responding = false
				});
			}
		}

		var serverPlayers = new Dictionary<string, int>();
		foreach (var jobId in currentGameServerPorts.Keys)
		{
			try
			{
				var players = await GetGameServerPlayers(jobId);
				serverPlayers[jobId] = players.Count();
			}
			catch
			{
				serverPlayers[jobId] = 0;
			}
		}

		var gameServerPorts = currentGameServerPorts.Select(kvp => new
		{
			jobId = kvp.Key,
			port = kvp.Value,
			playerCount = serverPlayers.TryGetValue(kvp.Key, out var count) ? count : 0
		}).ToList();

		var placesInUse = currentPlaceIdsInUse.Select(kvp => new
		{
			placeId = kvp.Key,
			jobIds = kvp.Value.ToList(),
			totalServers = kvp.Value.Count
		}).ToList();

		var statistics = new
		{
			TotalRunningServers = JobRCCList.Count(p => !p.hasExited),
			TotalPlayersInGame = CurrentPlayersInGame.Count,
			TotalPortsUsed = currentGameServerPorts.Count,
			TotalPlacesRunning = currentPlaceIdsInUse.Count,
			TotalRCCs = jobRccs.Count
		};

		return new
		{
			jobRccs = JobRCCList,
			currentGameServerPorts = gameServerPorts,
			currentPlaceIdsInUse = placesInUse,
			mainRCCPortsInUse = mainRCCPortsInUse.Select(kvp => new
			{
				processId = kvp.Key.Id,
				port = kvp.Value,
				processName = kvp.Key.ProcessName
			}),
			currentPlayersInGame = CurrentPlayersInGame.Select(kvp => new
			{
				userId = kvp.Key,
				placeId = kvp.Value
			}),
			statistics = statistics
		};
	}
		
	private async Task<long> GetMaxPlayerCount(long placeId)
	{	
		using var gamesService = ServiceProvider.GetOrCreate<GamesService>();
		return await gamesService.GetMaxPlayerCount(placeId);
	}
	
	private async Task<GameServerEntry> GetAvailableServerDB(long placeId, long MaxPlayers)
	{
		return await db.QueryFirstOrDefaultAsync<GameServerEntry>(
			@"SELECT s.id::text, s.asset_id as assetId, s.port 
			FROM asset_server s 
			WHERE s.asset_id = :placeId 
			AND (
				SELECT COUNT(*) 
				FROM asset_server_player p 
				WHERE p.server_id = s.id
			) < :MaxPlayers
			ORDER BY (
				SELECT COUNT(*) 
				FROM asset_server_player p 
				WHERE p.server_id = s.id
			) ASC
			LIMIT 1",
			new
			{
				placeId,
				MaxPlayers
			});
	}
		
	public async Task<int> GetServerPortFromDatabase(string serverId)
	{
		var serverInfo = await db.QueryFirstOrDefaultAsync<GameServerEntry>(
			"SELECT port FROM asset_server WHERE id = :id::uuid",
			new { id = serverId });
		
		return serverInfo?.port ?? -1;
	}

	public async Task<GameServerGetOrCreateResponse> GetServerForPlace(long placeId, string year = "2016")
	{
		long MaxPlayers = await GetMaxPlayerCount(placeId);
		var AvailableServer = await GetAvailableServerDB(placeId, MaxPlayers);
		
		if (AvailableServer != null)
		{
			var ServerReady = await db.QueryFirstOrDefaultAsync<bool>(
				"SELECT created_at != updated_at FROM asset_server WHERE id = :id::uuid",
				new { id = AvailableServer.id });
			
			if (!ServerReady)
			{
				// if server hasn't pinged yet, return Loading until it has
				return new GameServerGetOrCreateResponse() 
				{ 
					status = JoinStatus.Loading
				};
			}
			
			if (!currentGameServerPorts.ContainsKey(AvailableServer.id))
			{
				currentGameServerPorts[AvailableServer.id] = AvailableServer.port;
			}
			
			return new GameServerGetOrCreateResponse()
			{
				job = AvailableServer.id,
				status = JoinStatus.Joining
			};
		}
		
		// create new server
		string jobId = Guid.NewGuid().ToString();
		int NSPort = -1;
		int RCCPort = -1;
		
		var RandomNSPort = Configuration.AllowedNetworkPorts.OrderBy(x => Guid.NewGuid());
		foreach (var port in RandomNSPort)
		{
			if (IsPortAvailable(port) && !currentGameServerPorts.Values.Any(x => x == port))
			//if (IsPortAvailableTCP(port))
			{
				NSPort = port;
				break;
			}
		}

		if (NSPort == -1)
		{
			return new GameServerGetOrCreateResponse() 
			{ 
				status = JoinStatus.Waiting 
			};
		}
		var random = new Random();
		for (int i = 0; i < 10; i++)
		{
			int RCCRandomPort = random.Next(50000, 60001);
			if (IsPortAvailable(RCCRandomPort) && !mainRCCPortsInUse.Values.Contains(RCCRandomPort))
			//if (IsPortAvailableTCP(RCCRandomPort) && !mainRCCPortsInUse.Values.Contains(RCCRandomPort))
			{
				RCCPort = RCCRandomPort;
				break;
			}
		}
		
		if (NSPort == -1 || RCCPort == -1)
		{
			return new GameServerGetOrCreateResponse() 
			{ 
				status = JoinStatus.Waiting 
			};
		}

		string Start = year switch
		{
			// this fucking sucks. make this one function in the future
			"2018" or "2020" => year == "2020" ? 
				await StartGameServer2020(placeId, RCCPort, NSPort, jobId, 43200, MaxPlayers) : 
				await StartGameServer2018(placeId, RCCPort, NSPort, jobId, 43200, MaxPlayers),
			"2017" => await StartGameServer2017(placeId, RCCPort, NSPort, jobId, 43200, MaxPlayers),
			"2015" => await StartGameServer2015(placeId, RCCPort, NSPort, jobId, 43200, MaxPlayers),
			_ => await StartGameServer(placeId, RCCPort, NSPort, jobId, 43200, MaxPlayers)
		};

		if (Start != "BAD")
		{
			currentGameServerPorts.AddOrUpdate(jobId, NSPort, (key, oldValue) => NSPort);
			return new GameServerGetOrCreateResponse()
			{
				//job = jobId,
				//status = JoinStatus.Joining
				// wait until server starts and pings
				status = JoinStatus.Waiting
			};
		}

		if (Start == "BAD")
		{
			return new GameServerGetOrCreateResponse()
			{
				// status = JoinStatus.Error
				status = JoinStatus.Waiting
			};
		}
		
		return new GameServerGetOrCreateResponse()
		{
			status = JoinStatus.Waiting
		};
	}

	// TODO: MAKE this configurable
	//private static readonly int[] AllowedNetworkPorts = { 50, 51, 52, 54, 55, 56, 57 };
			
	public async Task<string> StartGameServer(long placeId, int RCCPort, int NSPort, string jobId, int JobExpiration, long MaxPlayers)
	{
		//Console.WriteLine($"starting 2016 place {jobId} on id {placeId} with RCC port: {RCCPort}, NS port: {NSPort}");
		// Before we waste our time, check if the place exists
		AssetsService assetsService = new AssetsService();
		GamesService gamesService = new GamesService();
		var AssetCatalogInfo = await assetsService.GetAssetCatalogInfo(placeId);
		var uni = (await gamesService.MultiGetPlaceDetails(new[] { placeId })).First();
		if (AssetCatalogInfo.assetType != Models.Assets.Type.Place)
		{
			return "BAD";
		}
		
		await db.ExecuteAsync(
			"INSERT INTO asset_server (id, asset_id, ip, port, RCCConnection) VALUES (:id::uuid, :asset_id, :ip, :port, :RCCConnection)",
			new
			{
				id = jobId,
				asset_id = placeId,
				ip = "games.zawg.ca",
				port = NSPort,
				RCCConnection = $"127.0.0.1:{RCCPort}",
			});

		Console.WriteLine($"[DEBUG] current GS ports: {string.Join(",", currentGameServerPorts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

		Process rccServer = new Process();
		rccServer.StartInfo.CreateNoWindow = false;
		rccServer.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
		rccServer.StartInfo.FileName = $"{Configuration.RccServicePath}RCCService.exe";
		rccServer.StartInfo.Arguments = string.Format($@"-console -verbose -port {RCCPort}");
		rccServer.StartInfo.RedirectStandardError = false;
		rccServer.StartInfo.RedirectStandardOutput = false;
		rccServer.StartInfo.UseShellExecute = true;
		rccServer.Start();

		string originalScript = File.ReadAllText($"{Configuration.LuaScriptPath}GameServer.lua");
		string finalScript = originalScript.Replace
			("%baseURL%", $"{Configuration.BaseUrl}").Replace
			("%port%", $"{NSPort}").Replace
			("%placeId%", $"{placeId}").Replace
			("%creatorId%", $"{uni.builderId}").Replace
			("%apiKey%", $"{Configuration.RccAuthorization}").Replace
			("_AUTHORIZATION_STRING_", Configuration.GameServerAuthorization);

		string XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<OpenJobEx xmlns=""http://roblox.com/"">
						<job>
							<id>{jobId}</id>
							<category>1</category>
							<cores>1</cores>
							<expirationInSeconds>43600</expirationInSeconds>
						</job>
						<script>
							<name>{Guid.NewGuid().ToString()}</name>
							<script>
								<![CDATA[
								{finalScript}
								]]>
							</script>
						</script>
					</OpenJobEx>
				</soap:Body>
			</soap:Envelope>";

		await SendSoapRequestToRcc($"http://127.0.0.1:{RCCPort}", XML, "OpenJobEx");
		var jobList = currentPlaceIdsInUse.GetOrAdd(placeId, _ => new ConcurrentBag<string>());
		jobList.Add(jobId);
		currentGameServerPorts.TryAdd(jobId, NSPort);
		jobRccs[jobId] = rccServer;
		
		try
		{
			if (!string.IsNullOrEmpty(Configuration.Webhook))
			{
				var webhookcont = new
				{
					content = $"place {placeId} started with port {NSPort} on server {jobId}"
				};
				
				using var httpClient = new HttpClient();
				var content = new StringContent(JsonSerializer.Serialize(webhookcont), Encoding.UTF8, "application/json");
				await httpClient.PostAsync(Configuration.Webhook, content);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"failed to send to webhook (did you configure it?): {ex.Message}");
		}

		return "OK";
	}
			
	public async Task<string> StartGameServer2015(long placeId, int RCCPort, int NSPort, string jobId, int JobExpiration, long MaxPlayers)
	{
		// Before we waste our time, check if the place exists
		AssetsService assetsService = new AssetsService();
		GamesService gamesService = new GamesService();
		var AssetCatalogInfo = await assetsService.GetAssetCatalogInfo(placeId);
		var uni = (await gamesService.MultiGetPlaceDetails(new[] { placeId })).First();
		if (AssetCatalogInfo.assetType != Models.Assets.Type.Place)
		{
			return "BAD";
		}
		
		await db.ExecuteAsync(
			"INSERT INTO asset_server (id, asset_id, ip, port, RCCConnection) VALUES (:id::uuid, :asset_id, :ip, :port, :RCCConnection)",
			new
			{
				id = jobId,
				asset_id = placeId,
				ip = "games.zawg.ca",
				port = NSPort,
				RCCConnection = $"127.0.0.1:{RCCPort}",
			});
			
		Console.WriteLine($"[DEBUG] current GS ports: {string.Join(",", currentGameServerPorts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

		Process rccServer = new Process();
		rccServer.StartInfo.CreateNoWindow = false;
		rccServer.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
		rccServer.StartInfo.FileName = $"{Configuration.RccService2015Path}RCCService.exe";
		rccServer.StartInfo.Arguments = string.Format($@"-console -verbose -port {RCCPort}");
		rccServer.StartInfo.RedirectStandardError = false;
		rccServer.StartInfo.RedirectStandardOutput = false;
		rccServer.StartInfo.UseShellExecute = true;
		rccServer.Start();

		string originalScript = File.ReadAllText($"{Configuration.LuaScriptPath}GameServer.lua");
		string finalScript = originalScript.Replace
			("%port%", $"{NSPort}").Replace
			("%placeId%", $"{placeId}").Replace
			("%creatorId%", $"{uni.builderId}").Replace
			("%apiKey%", $"{Configuration.RccAuthorization}").Replace
			("_AUTHORIZATION_STRING_", Configuration.GameServerAuthorization);

		string XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<OpenJobEx xmlns=""http://roblox.com/"">
						<job>
							<id>{jobId}</id>
							<category>1</category>
							<cores>1</cores>
							<expirationInSeconds>43600</expirationInSeconds>
						</job>
						<script>
							<name>{Guid.NewGuid().ToString()}</name>
							<script>
								<![CDATA[
								{finalScript}
								]]>
							</script>
						</script>
					</OpenJobEx>
				</soap:Body>
			</soap:Envelope>";

		await Task.Delay(5000);
		await SendSoapRequestToRcc($"http://127.0.0.1:{RCCPort}", XML, "OpenJobEx");
		var jobList = currentPlaceIdsInUse.GetOrAdd(placeId, _ => new ConcurrentBag<string>());
		jobList.Add(jobId);
		jobRccs[jobId] = rccServer;
		currentGameServerPorts.TryAdd(jobId, NSPort);
		
		try
		{
			if (!string.IsNullOrEmpty(Configuration.Webhook))
			{
				var webhookcont = new
				{
					content = $"place {placeId} started with port {NSPort} on server {jobId}"
				};
				
				using var httpClient = new HttpClient();
				var content = new StringContent(JsonSerializer.Serialize(webhookcont), Encoding.UTF8, "application/json");
				await httpClient.PostAsync(Configuration.Webhook, content);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"failed to send to webhook (did you configure it?): {ex.Message}");
		}

		return "OK";
	}

	public async Task<string> StartGameServer2017(long placeId, int RCCPort, int NSPort, string jobId, int JobExpiration, long MaxPlayers)
	{
		// Before we waste our time, check if the place exists
		AssetsService assetsService = new AssetsService();
		GamesService gamesService = new GamesService();
		var AssetCatalogInfo = await assetsService.GetAssetCatalogInfo(placeId);
		var uni = (await gamesService.MultiGetPlaceDetails(new[] { placeId })).First();
		if (AssetCatalogInfo.assetType != Models.Assets.Type.Place)
		{
			return "BAD";
		}
		
		await db.ExecuteAsync(
			"INSERT INTO asset_server (id, asset_id, ip, port, RCCConnection) VALUES (:id::uuid, :asset_id, :ip, :port, :RCCConnection)",
			new
			{
				id = jobId,
				asset_id = placeId,
				ip = "games.zawg.ca",
				port = NSPort,
				RCCConnection = $"127.0.0.1:{RCCPort}",
			});

		Console.WriteLine($"[DEBUG] current GS ports: {string.Join(",", currentGameServerPorts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

		Process rccServer = new Process();
		rccServer.StartInfo.CreateNoWindow = false;
		rccServer.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
		rccServer.StartInfo.FileName = $"{Configuration.RccService2017Path}RCCService.exe";
		rccServer.StartInfo.Arguments = string.Format($@"-console -verbose -port {RCCPort}");
		rccServer.StartInfo.RedirectStandardError = false;
		rccServer.StartInfo.RedirectStandardOutput = false;
		rccServer.StartInfo.UseShellExecute = true;
		rccServer.Start();

		string originalScript = File.ReadAllText($"{Configuration.LuaScriptPath}GameServer.lua");
		string finalScript = originalScript.Replace
			("%baseURL%", $"{Configuration.BaseUrl}").Replace
			("%port%", $"{NSPort}").Replace
			("%placeId%", $"{placeId}").Replace
			("%creatorId%", $"{uni.builderId}").Replace
			("%apiKey%", $"{Configuration.RccAuthorization}").Replace
			("_AUTHORIZATION_STRING_", Configuration.GameServerAuthorization);

		string XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<OpenJobEx xmlns=""http://roblox.com/"">
						<job>
							<id>{jobId}</id>
							<category>1</category>
							<cores>1</cores>
							<expirationInSeconds>43600</expirationInSeconds>
						</job>
						<script>
							<name>{Guid.NewGuid().ToString()}</name>
							<script>
								<![CDATA[
								{finalScript}
								]]>
							</script>
						</script>
					</OpenJobEx>
				</soap:Body>
			</soap:Envelope>";

		await SendSoapRequestToRcc($"http://127.0.0.1:{RCCPort}", XML, "OpenJobEx");
		var jobList = currentPlaceIdsInUse.GetOrAdd(placeId, _ => new ConcurrentBag<string>());
		jobList.Add(jobId);
		jobRccs[jobId] = rccServer;
		currentGameServerPorts.TryAdd(jobId, NSPort);
		
		try
		{
			if (!string.IsNullOrEmpty(Configuration.Webhook))
			{
				var webhookcont = new
				{
					content = $"place {placeId} started with port {NSPort} on server {jobId}"
				};
				
				using var httpClient = new HttpClient();
				var content = new StringContent(JsonSerializer.Serialize(webhookcont), Encoding.UTF8, "application/json");
				await httpClient.PostAsync(Configuration.Webhook, content);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"failed to send to webhook (did you configure it?): {ex.Message}");
		}

		return "OK";
	}

	public async Task<string> StartGameServer2018(long placeId, int RCCPort, int NSPort, string jobId, int JobExpiration, long MaxPlayers)
	{
		// Before we waste our time, check if the place exists
		AssetsService assetsService = new AssetsService();
		GamesService gamesService = new GamesService();
		var AssetCatalogInfo = await assetsService.GetAssetCatalogInfo(placeId);
		var uni = (await gamesService.MultiGetPlaceDetails(new[] { placeId })).First();
		if (AssetCatalogInfo.assetType != Models.Assets.Type.Place)
		{
			return "BAD";
		}
		await ModifyServerLua2018(Path.Combine(Configuration.RccService2018Path, "content", "scripts", "CoreScripts", "ServerStarterScript.lua"));
		
		await db.ExecuteAsync(
			"INSERT INTO asset_server (id, asset_id, ip, port, RCCConnection) VALUES (:id::uuid, :asset_id, :ip, :port, :RCCConnection)",
			new
			{
				id = jobId,
				asset_id = placeId,
				ip = "games.zawg.ca",
				port = NSPort,
				RCCConnection = $"127.0.0.1:{RCCPort}",
			});

		Console.WriteLine($"[DEBUG] current GS ports: {string.Join(",", currentGameServerPorts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

		Process rccServer = new Process();
		rccServer.StartInfo.CreateNoWindow = false;
		rccServer.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
		rccServer.StartInfo.FileName = $"{Configuration.RccService2018Path}RCCService.exe";
		// should i get rid of devsetting later
		rccServer.StartInfo.Arguments = $"-console -verbose -settingsfile \"DevSettingsFile.json\" -port {RCCPort}";
		rccServer.StartInfo.RedirectStandardError = false;
		rccServer.StartInfo.RedirectStandardOutput = false;
		rccServer.StartInfo.UseShellExecute = true;
		rccServer.Start();

		await Task.Delay(2000);

		string creatorTypeStr = AssetCatalogInfo.creatorType == CreatorType.User ? "User" : "Group";
		string gameOpenJson = $@"{{
			""Mode"": ""GameServer"",
			""GameId"": ""{jobId}"",
			""Settings"": {{
				""Type"": ""GameOpen"",
				""PlaceId"": {placeId},
				""SessionId"": ""{Guid.NewGuid()}"",
				""CreatorId"": {AssetCatalogInfo.creatorTargetId},
				""GameId"": ""{jobId}"",
				""MachineAddress"": ""games.zawg.ca"",
				""GsmInterval"": 5,
				""MaxPlayers"": {MaxPlayers},
				""MaxGameInstances"": 1,
				""ApiKey"": ""HIGu"",
				""PreferredPlayerCapacity"": 10,
				""DataCenterId"": ""1"",
				""PlaceVisitAccessKey"": """",
				""UniverseId"": {uni.universeId},
				""PlaceFetchUrl"": ""{Configuration.BaseUrl}/asset/?id={placeId}&apiKey={Configuration.RccAuthorization}"",
				""MatchmakingContextId"": 1,
				""CreatorId"": {AssetCatalogInfo.creatorTargetId},
				""CreatorType"": ""{creatorTypeStr}"",
				""PlaceVersion"": 1,
				""BaseUrl"": ""{Configuration.BaseUrl}"",
				""JobId"": ""{jobId}"",
				""script"": ""print('RCC Init')"",
				""PreferredPort"": {NSPort}
			}},
			""Arguments"": {{}}
		}}";

		string XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<OpenJob xmlns=""http://roblox.com/"">
						<job>
							<id>{jobId}</id>
							<expirationInSeconds>{JobExpiration}</expirationInSeconds>
							<category>0</category>
							<cores>1</cores>
						</job>
						<script>
							<name>GameServer</name>
							<script>{EscapeXml(gameOpenJson)}</script>
						</script>
						<arguments>
							<LuaValue>
								<type>LUA_TNIL</type>
							</LuaValue>
						</arguments>
					</OpenJob>
				</soap:Body>
			</soap:Envelope>";

		bool success = await SendSoapRequestToRcc2021($"http://127.0.0.1:{RCCPort}", XML, "OpenJob");
		
		if (!success)
		{
			rccServer.Kill();
		}

		try
		{
			var jobList = currentPlaceIdsInUse.GetOrAdd(placeId, _ => new ConcurrentBag<string>());
			jobList.Add(jobId);
			jobRccs[jobId] = rccServer;
			currentGameServerPorts.TryAdd(jobId, NSPort);
			try
			{
				if (!string.IsNullOrEmpty(Configuration.Webhook))
				{
					var webhookcont = new
					{
						content = $"place {placeId} started with port {NSPort} on server {jobId}"
					};
					
					using var httpClient = new HttpClient();
					var content = new StringContent(JsonSerializer.Serialize(webhookcont), Encoding.UTF8, "application/json");
					await httpClient.PostAsync(Configuration.Webhook, content);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"failed to send to webhook (did you configure it?): {ex.Message}");
			}
		}
		catch (ArgumentException)
		{
			rccServer.Kill();
		}

		return "OK";
	}
	
	public async Task<string> StartGameServer2020(long placeId, int RCCPort, int NSPort, string jobId, int JobExpiration, long MaxPlayers)
	{
		// Before we waste our time, check if the place exists
		AssetsService assetsService = new AssetsService();
		GamesService gamesService = new GamesService();
		var AssetCatalogInfo = await assetsService.GetAssetCatalogInfo(placeId);
		var uni = (await gamesService.MultiGetPlaceDetails(new[] { placeId })).First();
		if (AssetCatalogInfo.assetType != Models.Assets.Type.Place)
		{
			return "BAD";
		}
		
		await ModifyServerLua2018(Path.Combine(Configuration.RccService2020Path, "ExtraContent", "scripts", "CoreScripts", "ServerStarterScript.lua"));
		
		await db.ExecuteAsync(
			"INSERT INTO asset_server (id, asset_id, ip, port, RCCConnection) VALUES (:id::uuid, :asset_id, :ip, :port, :RCCConnection)",
			new
			{
				id = jobId,
				asset_id = placeId,
				ip = "games.zawg.ca",
				port = NSPort,
				RCCConnection = $"127.0.0.1:{RCCPort}",
			});

		Console.WriteLine($"[DEBUG] current GS ports: {string.Join(",", currentGameServerPorts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

		Process rccServer = new Process();
		rccServer.StartInfo.CreateNoWindow = false;
		rccServer.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
		rccServer.StartInfo.FileName = $"{Configuration.RccService2020Path}RCCService.exe";
		rccServer.StartInfo.Arguments = string.Format($@"-console -verbose -port {RCCPort}");
		rccServer.StartInfo.RedirectStandardError = false;
		rccServer.StartInfo.RedirectStandardOutput = false;
		rccServer.StartInfo.UseShellExecute = true;
		rccServer.Start();

		await Task.Delay(2000);

		string creatorTypeStr = AssetCatalogInfo.creatorType == CreatorType.User ? "User" : "Group";
		string gameOpenJson = $@"{{
			""Mode"": ""GameServer"",
			""GameId"": ""{jobId}"",
			""Settings"": {{
				""Type"": ""GameOpen"",
				""PlaceId"": {placeId},
				""SessionId"": ""{Guid.NewGuid()}"",
				""CreatorId"": {AssetCatalogInfo.creatorTargetId},
				""GameId"": ""{jobId}"",
				""MachineAddress"": ""games.zawg.ca"",
				""GsmInterval"": 5,
				""MaxPlayers"": {MaxPlayers},
				""MaxGameInstances"": 1,
				""ApiKey"": ""HIGu"",
				""PreferredPlayerCapacity"": 10,
				""DataCenterId"": ""1"",
				""PlaceVisitAccessKey"": """",
				""UniverseId"": {uni.universeId},
				""PlaceFetchUrl"": ""{Configuration.BaseUrl}/asset/?id={placeId}&apiKey={Configuration.RccAuthorization}"",
				""MatchmakingContextId"": 1,
				""CreatorId"": {AssetCatalogInfo.creatorTargetId},
				""CreatorType"": ""{creatorTypeStr}"",
				""PlaceVersion"": 1,
				""BaseUrl"": ""{Configuration.BaseUrl}"",
				""JobId"": ""{jobId}"",
				""script"": ""print('RCC Init')"",
				""PreferredPort"": {NSPort}
			}},
			""Arguments"": {{}}
		}}";

		string XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<OpenJob xmlns=""http://roblox.com/"">
						<job>
							<id>{jobId}</id>
							<expirationInSeconds>{JobExpiration}</expirationInSeconds>
							<category>0</category>
							<cores>1</cores>
						</job>
						<script>
							<name>GameServer</name>
							<script>{EscapeXml(gameOpenJson)}</script>
						</script>
						<arguments>
							<LuaValue>
								<type>LUA_TNIL</type>
							</LuaValue>
						</arguments>
					</OpenJob>
				</soap:Body>
			</soap:Envelope>";

		bool success = await SendSoapRequestToRcc2021($"http://127.0.0.1:{RCCPort}", XML, "OpenJob");
		
		if (!success)
		{
			rccServer.Kill();
		}

		try
		{
			var jobList = currentPlaceIdsInUse.GetOrAdd(placeId, _ => new ConcurrentBag<string>());
			jobList.Add(jobId);
			jobRccs[jobId] = rccServer;
			currentGameServerPorts.TryAdd(jobId, NSPort);
			try
			{
				if (!string.IsNullOrEmpty(Configuration.Webhook))
				{
					var webhookcont = new
					{
						content = $"place {placeId} started with port {NSPort} on server {jobId}"
					};
					
					using var httpClient = new HttpClient();
					var content = new StringContent(JsonSerializer.Serialize(webhookcont), Encoding.UTF8, "application/json");
					await httpClient.PostAsync(Configuration.Webhook, content);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"failed to send to webhook (did you configure it?): {ex.Message}");
			}
		}
		catch (ArgumentException)
		{
			rccServer.Kill();
		}

		return "OK";
	}

	private string EscapeXml(string input)
	{
		if (string.IsNullOrEmpty(input))
			return input;

		return input
			.Replace("&", "&amp;")
			.Replace("<", "&lt;")
			.Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&apos;");
	}
	
	private async Task ModifyServerLua2018(string Script)
	{
		// too lazy to add this to the readme
		try
		{
			if (File.Exists(Script))
			{
				string ScriptContent = await File.ReadAllTextAsync(Script);

				string Pattern = @"authorization\s*=\s*""[^""]*""";
				string Replacement = $"authorization = \"{Configuration.GameServerAuthorization}\"";
				
				string Modified = Regex.Replace(ScriptContent, Pattern, Replacement, RegexOptions.IgnoreCase);
				
				await File.WriteAllTextAsync(Script, Modified);
			}
			else
			{
				Console.WriteLine($"[INFO] ServerStarterScript not found");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[INFO] failed to edit ServerStarterScript.lua at {Script}: {ex.Message}");
		}
	}
	
	private bool IsPortAvailable(int port)
	{
		try
		{
			// see if our gs dict contains any in use ports
			if (currentGameServerPorts.Values.Any(x => x == port))
			{
				return false;
			}

			// if it says the port is available, do a double check
			// does this even work
			var listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			listener.Stop();
			return true;
		}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
		{
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}
	
/* 	private bool IsPortAvailableTCP(int port)
	{
		if (port < 1 || port > 65535)
			return false;

		try
		{
			var listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			listener.Stop();
			return true;
		}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
		{
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	} */
    
	public static async Task<bool> SendSoapRequestToRcc(string URL, string XML, string SOAPAction, int maxRetries = 5)
	{
		// added attempts for 2015/2017 cause it kinda sucks
		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			using (HttpClient RccHttpClient = new HttpClient())
			{
				RccHttpClient.Timeout = TimeSpan.FromSeconds(30);
				RccHttpClient.DefaultRequestHeaders.Add("SOAPAction", $"http://roblox.com/{SOAPAction}");
				HttpContent XMLContent = new StringContent(XML, Encoding.UTF8, "text/xml");
				
				try
				{
					Console.WriteLine($"[RCC] attempt {attempt}");
					HttpResponseMessage RccHttpClientPost = await RccHttpClient.PostAsync(URL, XMLContent);
					string RccHttpClientResponse = await RccHttpClientPost.Content.ReadAsStringAsync();
					
					if (RccHttpClientPost.IsSuccessStatusCode)
					{
						Console.WriteLine($"[RCC] SOAP res: {RccHttpClientPost.StatusCode} - {RccHttpClientResponse}");
						return true;
					}
					else
					{
						Console.WriteLine($"[RCC] SOAP req failed: {RccHttpClientPost.StatusCode}");

						Console.WriteLine($"[RCC] SOAP req failed (first 500 chars): {XML.Substring(0, Math.Min(500, XML.Length))}");
					}
				}
				catch (Exception e)
				{
					Console.WriteLine($"[RCC] attempt {attempt} failed: {e.ToString()}");

					if (attempt == maxRetries)
					{
						Console.WriteLine($"[RCC] could not send soap request {SOAPAction} to RCC");
						return false;
					}

					await Task.Delay(1000 * attempt);
				}
			}
		}
		return false;
	}
	
	private async Task<bool> SendSoapRequestToRcc2021(string url, string xml, string action, int maxRetries = 8)
	{
		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(30);
					
					var request = new HttpRequestMessage(HttpMethod.Post, url);
					request.Content = new StringContent(xml, Encoding.UTF8, "text/xml");
					request.Headers.Add("SOAPAction", $"http://roblox.com/{action}");
					
					var response = await client.SendAsync(request);

					var resContent = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"[RCC] 2018+ SOAP res: {response.StatusCode} - {resContent}");
					
					return response.IsSuccessStatusCode;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[RCC2021] SOAP request failed: {ex.Message}");
				if (attempt == maxRetries)
				{
					Console.WriteLine($"[RCC] could not send soap request {action} to RCC");
					return false;
				}

				await Task.Delay(1000 * attempt);
			}
		}
		
		return false;
	}
		
    public async Task DeleteOldGameServers()
    {
        // first part, do game servers
        var serversToDelete = (await db.QueryAsync<GameServerEntry>("SELECT id::text, asset_id as assetId FROM asset_server WHERE updated_at <= :t", new
        {
            t = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)),
        })).ToList();
        Console.WriteLine("[info] there are {0} bad servers", serversToDelete.Count);
        foreach (var server in serversToDelete)
        {
			ShutDownServer(server.id);
            var players = await GetGameServerPlayers(server.id);
            foreach (var player in players)
            {
                await OnPlayerLeave(player.userId, server.assetId, server.id);
            }
            Console.WriteLine("[info] deleting server {0}", server.id);
            await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new
            {
                id = server.id,
            });
            //Console.WriteLine("deleted from db line 706 deleteoldgameservers");
            await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new
            {
                id = server.id,
            });
        }
        // second part, do game server players
        // this is so ugly jeez
        /*var orphanedPlayers =
            await db.QueryAsync(
                "SELECT s.id, p.server_id FROM asset_server_player p LEFT JOIN asset_server s ON s.id = p.server_id WHERE s.id IS NULL");
        foreach (var deadbeatDad in orphanedPlayers.Select(c => ((Guid) c.server_id).ToString()).Distinct())
        {
            Console.WriteLine("[info] deleting all orphans for serverId = {0}",deadbeatDad);
            await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new
            {
                id = deadbeatDad,
            });
            Console.WriteLine("deleted from db line 724 DeleteOldGameServers");

        }
        */
		var OldPorts = new List<string>();
		
		foreach (var kvp in currentGameServerPorts)
		{
			// god why does this fucking game server management suck so much
			var serverId = kvp.Key;
			var port = kvp.Value;
			var Exists = await db.QueryFirstOrDefaultAsync<bool>(
				"SELECT EXISTS(SELECT 1 FROM asset_server WHERE id = :id::uuid)",
				new { id = serverId });
				
			if (!Exists)
			{
				Console.WriteLine($"[info] got old server port for {serverId} (port {port}), will be removed");
				OldPorts.Add(serverId);
			}
		}
		
		foreach (var oldServerId in OldPorts)
		{
			// remove. fuck you port
			currentGameServerPorts.TryRemove(oldServerId, out _);
		}
		
		Console.WriteLine($"[info] cleaned up {OldPorts.Count} ports from currentGameServerPorts");
    }

    public async Task<IEnumerable<GameServerPlayer>> GetGameServerPlayers(string serverId)
    {
        return await db.QueryAsync<GameServerPlayer>(
            "SELECT user_id as userId, u.username FROM asset_server_player INNER JOIN \"user\" u ON u.id = asset_server_player.user_id WHERE server_id = :id::uuid", new
            {
               id = serverId,
            });
    }

	public async Task<IEnumerable<GameServerEntryWithPlayers>> GetGameServers(long placeId, int offset, int? limit = null)
	{
		var RealLimit = limit ?? 10;

		var serverIds = (await db.QueryAsync<string>(
			@"SELECT DISTINCT server_id::text 
			  FROM asset_server_player 
			  WHERE asset_id = :placeId
			  LIMIT :limit OFFSET :offset",
			new
			{
				placeId,
				limit = RealLimit,
				offset
			})).ToList();

		var results = new List<GameServerEntryWithPlayers>();
		var tasks = new List<Task>();

		foreach (var serverId in serverIds)
		{
			var task = Task.Run(async () =>
			{
				var players = await GetGameServerPlayers(serverId);
				results.Add(new GameServerEntryWithPlayers
				{
					id = serverId,
					assetId = placeId,
					players = players.ToList()
				});
			});
			tasks.Add(task);
		}

		await Task.WhenAll(tasks);
		return results;
	}
	
	public async Task<string> GetRCCConnection(string serverId)
	{
		return await db.QueryFirstOrDefaultAsync<string>(
			"SELECT RCCConnection FROM asset_server WHERE id = :id::uuid",
			new { id = serverId });
	}
		
	public async Task<GameServerEntry> GetPlayersCurrentServer(long userId)
	{
		return await db.QueryFirstOrDefaultAsync<GameServerEntry>(
			"SELECT server_id::text as id, asset_id as assetId FROM asset_server_player WHERE user_id = :userId",
			new { userId });
	}
	
	public async Task EvictPlayer(long userId, long placeId, string year)
	{
		try
		{
			var CurrentServer = await GetPlayersCurrentServer(userId);
			if (CurrentServer == null || CurrentServer.assetId != placeId)
			{
				Console.WriteLine($"[PlayerEviction] {userId} is not in place {placeId}");
				return;
			}
			var RCCConn = await GetRCCConnection(CurrentServer.id);

			var RCCparts = RCCConn.Split(':');
			if (RCCparts.Length != 2 || !int.TryParse(RCCparts[1], out var RCCPort))
			{
				Console.WriteLine($"[PlayerEviction] bad RCC conn: {RCCConn}");
				return;
			}

			string RCCUrl = $"http://{RCCparts[0]}:{RCCPort}";
			string Script;
			string SoapAction;

			if (year == "2018" || year == "2020")
			{
/* 				Script = $@"{{
					""Mode"": ""EvictPlayer"",
					""MessageVersion"": 1,
					""Settings"": {{
						""PlayerId"": {userId}
					}}
				}}";
				SoapAction = "Execute"; */
				Script = $@"for _, Player in pairs(game:GetService(""Players""):GetPlayers()) do if Player.UserId == {userId} then Player:Kick(""You have been disconnected from the game due to joining on another device."") end end";
				SoapAction = "Execute";
			}
			else if (year == "2016" || year == "2017" || year == "2015")
			{
				Script = $@"for _, Player in pairs(game:GetService(""Players""):GetPlayers()) do if Player.UserId == {userId} then Player:Kick(""You have been disconnected from the game due to joining on another device."") end end";
				//SoapAction = "OpenJobEx";
				SoapAction = "Execute";
			}
			else
			{
				Console.WriteLine($"[PlayerEviction] bad year: {year}");
				return;
			}
				
			string xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<{SoapAction} xmlns=""http://roblox.com/"">
						<job>
							<id>{CurrentServer.id}</id>
							<expirationInSeconds>60</expirationInSeconds>
							<category>0</category>
							<cores>1</cores>
						</job>
						<script>
							<name>GameServer</name>
							<script>
								<![CDATA[
								{Script}
								]]>
							</script>
						</script>
						<arguments>
							<LuaValue>
								<type>LUA_TNIL</type>
							</LuaValue>
						</arguments>
					</{SoapAction}>
				</soap:Body>
			</soap:Envelope>";
			
			string xml2018 = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<{SoapAction} xmlns=""http://roblox.com/"">
						<job>
							<id>{CurrentServer.id}</id>
							<expirationInSeconds>60</expirationInSeconds>
							<category>0</category>
							<cores>1</cores>
						</job>
						<script>
							<name>GameServer</name>
							<script>
								{EscapeXml(Script)}
							</script>
						</script>
						<arguments>
							<LuaValue>
								<type>LUA_TNIL</type>
							</LuaValue>
						</arguments>
					</{SoapAction}>
				</soap:Body>
			</soap:Envelope>";
				
			bool success;
			if (year == "2018" || year == "2020")
			{
				success = await SendSoapRequestToRcc2021(RCCUrl, xml2018, SoapAction);
			}
			else
			{
				success = await SendSoapRequestToRcc(RCCUrl, xml, SoapAction);
			}

			if (success)
			{
				Console.WriteLine($"[PlayerEviction] kicked player {userId} from server {CurrentServer.id}");
				CurrentPlayersInGame.Remove(userId);
				
				await db.ExecuteAsync(
					"DELETE FROM asset_server_player WHERE user_id = :user_id AND server_id = :server_id::uuid", 
					new
					{
						server_id = CurrentServer.id,
						user_id = userId,
					});
			}
			else
			{
				Console.WriteLine($"[PlayerEviction] failed to kick player {userId} from server {CurrentServer.id}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PlayerEviction] error kicking player {userId}: {ex.Message}");
		}
	}
}