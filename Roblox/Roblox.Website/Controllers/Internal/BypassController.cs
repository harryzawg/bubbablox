using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.IO.Compression;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Roblox.Dto.Games;
using Roblox.Dto.Persistence;
using Roblox.Dto.Users;
using Roblox.Dto.Friends;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Libraries.Assets;
using Roblox.Libraries.FastFlag;
using Roblox.Libraries.RobloxApi;
using Roblox.Logging;
using Roblox.Services.Exceptions;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.GameServer;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Controllers.Internal;
using Roblox.Website.Filters;
using Roblox.Website.Middleware;
using Roblox.Website.WebsiteModels.Asset;
using Roblox.Website.WebsiteModels.Games;
using Roblox.Website.WebsiteModels.Promocodes;
using Roblox.Website.WebsiteModels.Discord;
using HttpGet = Roblox.Website.Controllers.HttpGetBypassAttribute;
using JsonSerializer = System.Text.Json.JsonSerializer;
using MultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;
using ServiceProvider = Roblox.Services.ServiceProvider;
using Type = Roblox.Models.Assets.Type;
using System.Data;
using Npgsql;
using Dapper;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class BypassController : ControllerBase
    {
        [HttpGet("internal/release-metadata")]
        public dynamic GetReleaseMetaData([Required] string requester)
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "BadRequest");
        }

        [HttpGet("asset/shader")]
        public async Task<MVC.FileResult> GetShaderAsset(long id)
        {
            var isMaterialOrShader = BypassControllerMetadata.materialAndShaderAssetIds.Contains(id);
            if (!isMaterialOrShader)
            {
                // Would redirect but that could lead to infinite loop.
                // Just throw instead
                throw new RobloxException(400, 0, "Material/Shader");
            }

            var assetId = id;
            try
            {
                var ourId = await services.assets.GetAssetIdFromRobloxAssetId(assetId);
                assetId = ourId;
            }
            catch (RecordNotFoundException)
            {
                // Doesn't exist yet, so create it
                var migrationResult = await MigrateItem.MigrateItemFromRoblox(assetId.ToString(), false, null, default, new ProductDataResponse()
                {
                    Name = "ShaderConversion" + id,
                    AssetTypeId = Type.Special, // Image
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Description = "ShaderConversion1.0",
                });
                assetId = migrationResult.assetId;
            }
            
            var latestVersion = await services.assets.GetLatestAssetVersion(assetId);
            if (latestVersion.contentUrl is null)
            {
                throw new RobloxException(403, 0, "Forbidden"); // ?
            }
            // These files are large, encourage clients to cache them
            HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(360),
            }.ToString();
            var assetContent = await services.assets.GetAssetContent(latestVersion.contentUrl);
            return File(assetContent, "application/binary");
        }

        private bool IsRcc()
        {
            var rccAccessKey = Request.Headers.ContainsKey("accesskey") ? Request.Headers["accesskey"].ToString() : null;
            var isRcc = rccAccessKey == Configuration.RccAuthorization;
            return isRcc;
        }
				
		[HttpGetBypass("game/players/{userId}")]
		public dynamic GetPlayerChatFilter(long userId)
		{
			return new
			{
				ChatFilter = "whitelist"
			};
		}
		
		[HttpGetBypass("/Game/ChatFilter.ashx")]
        public string RCC_GetChatFilter()
        {
            return "True";
        }
		
		private static bool isheaderbad(string headername)
		{
			var badheaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"Transfer-Encoding",
				"Connection",
				"Keep-Alive",
				"Content-Length",
				"Upgrade",
				"Server"
			};
			
			return badheaders.Contains(headername);
		}

        [HttpGetBypass("v2/asset")]
        [HttpGetBypass("v1/asset")]
        [HttpGetBypass("asset")]
        [HttpPostBypass("v1/asset")]
        [HttpPostBypass("asset")]
		public async Task<MVC.ActionResult> GetAssetById(long id, [MVC.FromQuery] string? apiKey = null, [MVC.FromQuery(Name = "assetversionid")] long? assetVersionId = null)
        {
			if (assetVersionId.HasValue)
			{
				id = assetVersionId.Value;
			}
			
			if (apiKey == Configuration.RccAuthorization)
			{
				var latestVersionSecret = await services.assets.GetLatestAssetVersion(id);
				if (latestVersionSecret?.contentUrl == null)
					throw new RobloxException(400, 0, "Content URL is null");

				var assetContentSecret = await services.assets.GetAssetContent(latestVersionSecret.contentUrl);
				return File(assetContentSecret, "application/binary");
			}
			
            // TODO: This endpoint needs to be updated to return a URL to the asset, not the asset itself.
            // The reason for this is so that cloudflare can cache assets without caching the Response of this endpoint, which might be different depending on the client making the request (e.g. under 18 user, over 18 user, rcc, etc).
            var is18OrOver = false;
            if (userSession != null)
            {
                is18OrOver = await services.users.Is18Plus(userSession.userId);
            }

            // TEMPORARY UNTIL AUTH WORKS ON STUDIO! REMEMBER TO REMOVE
            if (HttpContext.Request.Headers.ContainsKey("RbxTempBypassFor18PlusAssets"))
            {
                is18OrOver = true;
            }
            
            var assetId = id;
            var invalidIdKey = "InvalidAssetIdForConversionV1:" + assetId;
            // Opt
            if (Services.Cache.distributed.StringGetMemory(invalidIdKey) != null)
                throw new RobloxException(400, 0, "Asset is invalid or does not exist");
            
            var isBotRequest = Request.Headers["bot-auth"].ToString() == Roblox.Configuration.BotAuthorization;
            var isLoggedIn = userSession != null;
            var encryptionEnabled = !isBotRequest; // bots can't handle encryption :(

            var isMaterialOrShader = BypassControllerMetadata.materialAndShaderAssetIds.Contains(assetId);
            if (isMaterialOrShader)
            {
                return new MVC.RedirectResult("/asset/shader?id=" + assetId);
            }

            var isRcc = IsRcc();
            if (isRcc)
                encryptionEnabled = false;
#if DEBUG
            encryptionEnabled = false;
#endif
            MultiGetEntry details;
			try
				{
					details = await services.assets.GetAssetCatalogInfo(assetId);
				}
				catch (RecordNotFoundException)
				{
					try
					{
						var ourId = await services.assets.GetAssetIdFromRobloxAssetId(assetId);
						assetId = ourId;
					}
					catch (RecordNotFoundException)
					{		
						// i HATE HTTP HEADERS AND PROXIES!!!!!!
						var pxyurl = $"{Roblox.Configuration.GSUrl}/asset/roblox/?id={assetId}";

						using var httpClient = new HttpClient();
						httpClient.Timeout = TimeSpan.FromSeconds(10);
						
						try
						{
							var stopwatch = Stopwatch.StartNew();
							
							var response = await httpClient.GetAsync(pxyurl, HttpCompletionOption.ResponseHeadersRead);
							stopwatch.Stop();
							
							if (response.IsSuccessStatusCode)
							{
								var content = await response.Content.ReadAsByteArrayAsync();
								var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

								Response.Headers.Clear();

								// is it necessary to copy all headers except bad ones?
								foreach (var header in response.Headers)
								{
									if (!isheaderbad(header.Key))
									{
										Response.Headers[header.Key] = header.Value.ToArray();
									}
								}

								Response.Headers["Content-Type"] = contentType;

								return File(content, contentType);
							}
							else
							{
								throw new RobloxException(400, 0, $"{response.StatusCode}");
							}
						}
						catch (Exception ex)
						{				
							if (ex is TaskCanceledException && !ex.Message.Contains("canceled"))
							{
								throw new RobloxException(400, 0, "Timeout");
							}
							
							throw new RobloxException(400, 0, $"{ex.Message}");
						}
					}
					details = await services.assets.GetAssetCatalogInfo(assetId);
				}
			if (details.is18Plus && !isRcc && !isBotRequest && !is18OrOver)
				throw new RobloxException(400, 0, "AssetTemporarilyUnavailable");
			if (details.moderationStatus != ModerationStatus.ReviewApproved && !isRcc && !isBotRequest)
				throw new RobloxException(403, 0, "Asset is not approved");
            
            var latestVersion = await services.assets.GetLatestAssetVersion(assetId);
            Stream? assetContent = null;
			Console.WriteLine($"[debug] assetId={assetId}, assetType={details.assetType}, moderation={details.moderationStatus}, isRcc={isRcc}, isBot={isBotRequest}, is18={is18OrOver}");
            switch (details.assetType)
            {
				// Special types
				case Roblox.Models.Assets.Type.TeeShirt:
					var teeShirtData = ContentFormatters.GetTeeShirt(latestVersion.contentId);
					var teeShirtBytes = Encoding.UTF8.GetBytes(teeShirtData);
					return new MVC.FileContentResult(teeShirtBytes, "application/binary");

				case Models.Assets.Type.Shirt:
					var shirtData = ContentFormatters.GetShirt(latestVersion.contentId);
					var shirtBytes = Encoding.UTF8.GetBytes(shirtData);
					return new MVC.FileContentResult(shirtBytes, "application/binary");

				case Models.Assets.Type.Pants:
					var pantsData = ContentFormatters.GetPants(latestVersion.contentId);
					var pantsBytes = Encoding.UTF8.GetBytes(pantsData);
					return new MVC.FileContentResult(pantsBytes, "application/binary");
                // Types that require no authentication and aren't encrypted
                case Models.Assets.Type.Image:
                case Models.Assets.Type.Special:
                    if (latestVersion.contentUrl != null)
                        assetContent = await services.assets.GetAssetContent(latestVersion.contentUrl);
                    // encryptionEnabled = false;
                    break;
                // Types that require no authentication
                case Models.Assets.Type.Audio:
                case Models.Assets.Type.Mesh:
                case Models.Assets.Type.Hat:
                case Models.Assets.Type.Model:
                case Models.Assets.Type.Decal:
                case Models.Assets.Type.Head:
                case Models.Assets.Type.Face:
                case Models.Assets.Type.Gear:
                case Models.Assets.Type.Badge:
                case Models.Assets.Type.Animation:
                case Models.Assets.Type.Torso:
                case Models.Assets.Type.RightArm:
                case Models.Assets.Type.LeftArm:
                case Models.Assets.Type.RightLeg:
                case Models.Assets.Type.LeftLeg:
                case Models.Assets.Type.Package:
                case Models.Assets.Type.GamePass:
                case Models.Assets.Type.Plugin: // TODO: do plugins need auth?
                case Models.Assets.Type.MeshPart:
                case Models.Assets.Type.HairAccessory:
                case Models.Assets.Type.FaceAccessory:
                case Models.Assets.Type.NeckAccessory:
                case Models.Assets.Type.ShoulderAccessory:
                case Models.Assets.Type.FrontAccessory:
                case Models.Assets.Type.BackAccessory:
                case Models.Assets.Type.WaistAccessory:
                case Models.Assets.Type.ClimbAnimation:
                case Models.Assets.Type.DeathAnimation:
                case Models.Assets.Type.FallAnimation:
                case Models.Assets.Type.IdleAnimation:
                case Models.Assets.Type.JumpAnimation:
                case Models.Assets.Type.RunAnimation:
                case Models.Assets.Type.SwimAnimation:
                case Models.Assets.Type.WalkAnimation:
                case Models.Assets.Type.PoseAnimation:
				case Models.Assets.Type.EmoteAnimation:
                case Models.Assets.Type.SolidModel:
                    if (latestVersion.contentUrl is null)
                        throw new RobloxException(400, 0, "Content URL is null"); // todo: should we log this?
						Console.WriteLine($"[debug] no content URL for assetId: {assetId}, assetType: {details.assetType}, moderationStatus: {details.moderationStatus}");
                    if (details.assetType == Models.Assets.Type.Audio)
                    {
                        // Convert to WAV file (todo: do we keep this?)
                        assetContent = await services.assets.GetAudioContentAsWav(assetId, latestVersion.contentUrl);
                    }
                    else
                    {
                        assetContent = await services.assets.GetAssetContent(latestVersion.contentUrl);
                    }
                    break;
                default:
                    // anything else requires auth
                    var ok = false;
                    if (isRcc)
                    {
                        encryptionEnabled = false;
						ok = true;
                        var placeIdHeader = Request.Headers["roblox-place-id"].ToString();
                        long placeId = 0;
                        if (!string.IsNullOrEmpty(placeIdHeader))
                        {
                            try
                            {
                                placeId = long.Parse(Request.Headers["roblox-place-id"].ToString());
                            }
                            catch (FormatException)
                            {
                                // Ignore
                            }
                        }
                        // if rcc is trying to access current place, allow through
                        ok = (placeId == assetId);
                        // If game server is trying to load a new place (current placeId is empty), then allow it
                        if (!ok && details.assetType == Models.Assets.Type.Place && placeId == 0)
                        {
                            // Game server is trying to load, so allow it
                            ok = true;
                        }
                        // If rcc is making the request, but it's not for a place, validate the request:
                        if (!ok)
                        {
                            // Check permissions
                            var placeDetails = await services.assets.GetAssetCatalogInfo(placeId);
                            if (placeDetails.creatorType == details.creatorType &&
                                placeDetails.creatorTargetId == details.creatorTargetId)
                            {
                                // We are authorized
                                ok = true;
                            }
                        }
						Console.WriteLine($"[debug] default branch, ok={ok}, creatorType={details.creatorType}, creatorTargetId={details.creatorTargetId}");
                    }
                    else
                    {
                        // It's not RCC making the request. are we authorized?
                        if (userSession != null)
                        {
                            // Use current user as access check
                            ok = await services.assets.CanUserModifyItem(assetId, userSession.userId);
                            if (!ok)
                            {
                                // Note that all users have access to "Roblox"'s content for legacy reasons
                                ok = (details.creatorType == CreatorType.User && details.creatorTargetId == 1);
                            }
#if DEBUG
                            // If staff, allow access in debug builds
                            if (await services.users.IsUserStaff(userSession.userId))
                            {
                                ok = true;
                            }
#endif
                            // Don't encrypt assets being sent to authorized users - they could be trying to download their own place to give to a friend or something
                            if (ok)
                            {
                                encryptionEnabled = false;
                            }
                        }
                    }

                    if (ok && latestVersion.contentUrl != null)
                    {
                        assetContent = await services.assets.GetAssetContent(latestVersion.contentUrl);
                    }

                    break;
            }

            if (assetContent != null)
            {
                return File(assetContent, "application/binary");
            }

            Console.WriteLine("[info] got BadRequest on /asset/ endpoint");
            throw new BadRequestException();
        }
		
		[HttpGet("Game/GamePass/GamePassHandler.ashx")]
		public async Task<MVC.ActionResult> GamePassHandler(string Action, long UserID, long PassID)
		{
			if (Action == "HasPass")
			{
				var has = await services.users.GetUserAssets(UserID, PassID);
				var Result = has.Any() ? "True" : "False";
				var xmlResponse = $"<Value Type=\"boolean\">{Result}</Value>";
				
				Response.ContentType = "text/xml; charset=utf-8";
				return Content(xmlResponse);
			}

			throw new NotImplementedException();
		}

        [HttpGet("Game/LuaWebService/HandleSocialRequest.ashx")]
        public async Task<string> LuaSocialRequest([Required, MVC.FromQuery] string method, long? playerid = null, long? groupid = null, long? userid = null)
        {
            // TODO: Implement these
			method = method.ToLower();
			if (method == "isingroup" && playerid != null && groupid != null)
			{
				bool isInGroup = false;

				// remove later
				if (playerid == 261 && groupid == 2868472)
				{
					return "<Value Type=\"boolean\">true</Value>";
				}

				try
				{
					if (groupid == 1200769 && await StaffFilter.IsStaff(playerid ?? 0))
					{
						isInGroup = true;
					}

					var group = await services.groups.GetUserRoleInGroup((long)groupid, (long)playerid);
					if (group.rank != 0)
						isInGroup = true;
				}
				catch (Exception)
				{
				}

				return "<Value Type=\"boolean\">" + (isInGroup ? "true" : "false") + "</Value>";
			}

			if (method == "getgrouprank" && playerid != null && groupid != null)
			{
				int rank = 0;
				
				// also remove later
				if (playerid == 261 && groupid == 2868472)
				{
					return "<Value Type=\"integer\">254</Value>";
				}

				try
				{
					var group = await services.groups.GetUserRoleInGroup((long)groupid, (long)playerid);
					rank = group.rank;
				}
				catch (Exception)
				{
				}

				return "<Value Type=\"integer\">" + rank + "</Value>";
			}

            if (method == "getgrouprole" && playerid != null && groupid != null)
            {
                var groups = await services.groups.GetAllRolesForUser((long) playerid);
                foreach (var group in groups)
                {
                    if (group.groupId == groupid)
                    {
                        return group.name;
                    }
                }

                return "Guest";
            }

            if (method == "isfriendswith" && playerid != null && userid != null)
            {
                var status = (await services.friends.MultiGetFriendshipStatus((long) playerid, new[] {(long) userid})).FirstOrDefault();
                if (status != null && status.status == "Friends")
                {
                    return "<Value Type=\"boolean\">True</Value>";
                }
                return "<Value Type=\"boolean\">False</Value>";

            }

            if (method == "isbestfriendswith")
            {
                return "<Value Type\"boolean\">False</value>";
            }

            throw new NotImplementedException();
        }

		[HttpGet("login/negotiate.ashx"), HttpGet("login/negotiateasync.ashx")]
		public object Negotiate(string suggest)
		{
			HttpContext.Response.Cookies.Append(".ROBLOSECURITY", suggest, new CookieOptions
			{
				Domain = null,
				HttpOnly = true,
				Secure = true,
				Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(364)),
				IsEssential = true,
				Path = "/",
				SameSite = SameSiteMode.Lax,
			});

			return suggest;
		}
		
        [HttpGet("/auth/submit")]
        public MVC.RedirectResult SubmitAuth(string auth)
        {
            return new MVC.RedirectResult("/");
        }
		
		[HttpGetBypass("Game/LoadPlaceInfo.ashx")]
		public MVC.IActionResult LoadPlaceInfo([Required] long placeId)
		{
			return Ok();
		}
		
		[HttpGetBypass("asset/isowned")]
		public async Task<dynamic> CheckAssetOwnership([Required] long assetId)
		{
			if (userSession == null)
			{
				throw new RobloxException(401, 0, "Not authenticated");
			}

			var ownsAsset = (await services.users.GetUserAssets(userSession.userId, assetId)).Any();
			return new
			{
				isOwned = ownsAsset
			};
		}
		
	private const string ExpiredApplicationMessage = "For security reasons, this application has been expired. Please create a new application and try again.";
    private const string BadApplicationMessage =
	"This application is either not approved or has already been used. Please confirm the URL is correct, and try again.";
    private const string BadUsernameOrPasswordMessage = "Incorrect username or password. Please try again";
    private const string BadCaptchaMessage = "Your captcha could not be verified. Please try again.";
    private const string EmptyUsernameMessage = "Empty username";
    private const string EmptyPasswordMessage = "Empty password/password too short";
    private const string LoginDisabledMessage = "Login is disabled at this time. Try again later.";
    private const string RateLimitSecondMessage = "Too many attempts. Try again in a few seconds.";
    private const string RateLimit15MinutesMessage = "Too many attempts. Try again in 15 minutes.";
    private const string LockedAccountMessage = "This account is locked. Please contact us through Discord.";
			
	private static string HashString(string input)
	{
		using var sha256 = SHA256.Create();
		var bytes = Encoding.UTF8.GetBytes(input);
		var hashBytes = sha256.ComputeHash(bytes);
		return Convert.ToBase64String(hashBytes);
	}
		
	[HttpPostBypass("login")]
	public async Task<MVC.IActionResult> Login(
		[MVC.FromForm] string? username,
		[MVC.FromForm] string? password)
	{
		var ip = GetIP(GetRequesterIpRaw(HttpContext));
		var hashedIp = HashString(ip);

		try
		{
			FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
		}
		catch (RobloxException)
		{
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(LoginDisabledMessage));
		}

		if (string.IsNullOrWhiteSpace(username))
		{
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(EmptyUsernameMessage));
		}

		if (string.IsNullOrEmpty(password) || password.Length < 3)
		{
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(EmptyPasswordMessage));
		}

		long userId = 0;
		try
		{
			userId = await services.users.GetUserIdFromUsername(username);
		}
		catch (RecordNotFoundException)
		{
			// Do nothing here.
		}

		if (!await services.cooldown.TryCooldownCheck("LoginAttemptV1:" + hashedIp, TimeSpan.FromSeconds(5)))
		{
			Roblox.Metrics.UserMetrics.ReportLoginConcurrentLockHit();
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(RateLimitSecondMessage));
		}

		var loginKey = "LoginAttemptCountV1:" + hashedIp;
		var attemptCount = (await services.cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();

		if (!await services.cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
		{
			Roblox.Metrics.UserMetrics.ReportLoginFloodCheckReached(attemptCount.Length);
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(RateLimit15MinutesMessage));
		}

		var timer = new Stopwatch();
		timer.Start();
		if (userId == 0)
		{
			await PreventTimingExploits(timer);
			Roblox.Metrics.UserMetrics.ReportUserLoginAttempt(false);
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(BadUsernameOrPasswordMessage));
		}

		var passwordOk = await services.users.VerifyPassword(userId, password);
		await PreventTimingExploits(timer);
		if (!passwordOk)
		{
			Roblox.Metrics.UserMetrics.ReportUserLoginAttempt(false);
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(BadUsernameOrPasswordMessage));
		}

		var userinfo = await services.users.GetUserById(userId);
		if (userinfo.accountStatus == AccountStatus.MustValidateEmail)
		{
			return Redirect("/?loginmsg=" + Uri.EscapeDataString(LockedAccountMessage));
		}

		var sess = await services.users.CreateSession(userId);
		var sesscookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
		{
			sessionId = sess,
			createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
		});
		
		// delete these here if the user already has an account
		Response.Cookies.Delete("discord_id");
		Response.Cookies.Delete("discord_profile");
		Response.Cookies.Delete("signupkey");
		Response.Cookies.Delete("resetpasstoken");
		Response.Cookies.Delete("resetpasswordverified");

		HttpContext.Response.Cookies.Append(
			".ROBLOSECURITY", 
			sesscookie,
			new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Expires = DateTimeOffset.Now.AddYears(1)
			});

		return Redirect("/home");
	}

	private async Task PreventTimingExploits(Stopwatch watch)
	{
		watch.Stop();
		Writer.Info(LogGroup.AbuseDetection, "PreventTimingExploits elapsed={0}ms", watch.ElapsedMilliseconds);
		const long sleepTimeMs = 200;
		var sleepTime = sleepTimeMs - watch.ElapsedMilliseconds;
		if (sleepTime is < 0 or > sleepTimeMs)
		{
			sleepTime = 0;
		}
		if (sleepTime != 0)
			await Task.Delay(TimeSpan.FromMilliseconds(sleepTime));
	}
	
	[HttpGet("UserCheck/checkifinvalidusernameforsignup")]
	public async Task<MVC.IActionResult> CheckUsernameAvailability([MVC.FromQuery] string username)
	{
		// 0 = available
		// 1 = taken
		// 2 = invalid
		
		// debug username to test forms
		if (username?.Equals("jongus", StringComparison.OrdinalIgnoreCase) == true)
			return Content("{\"data\":0}", "application/json");

		if (string.IsNullOrWhiteSpace(username))
			return Content("{\"data\":2}", "application/json");

		if (!await services.users.IsUsernameValid(username))
			return Content("{\"data\":2}", "application/json");

		if (!await services.users.IsNameAvailableForSignup(username))
			return Content("{\"data\":1}", "application/json");

		return Content("{\"data\":0}", "application/json");
	}
	
	private string EncryptWithKey(string input, string key)
	{
		using var aes = Aes.Create();
		aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32, '\0')[..32]);
		aes.IV = new byte[16];
		
		var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
		using var ms = new MemoryStream();
		using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
		using (var sw = new StreamWriter(cs))
		{
			sw.Write(input);
		}
		
		return Convert.ToBase64String(ms.ToArray());
	}

	private string DecryptWithKey(string input, string key)
	{
		using var aes = Aes.Create();
		aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32, '\0')[..32]);
		aes.IV = new byte[16];
		
		var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
		using var ms = new MemoryStream(Convert.FromBase64String(input));
		using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
		using var sr = new StreamReader(cs);
		
		return sr.ReadToEnd();
	}
	
	// this is so stupid (this should match discordcb)
	private bool ValidateSignupCookie()
	{
		const string key = "Thisisthezachydramaandthebubbabloxsignupkeyidksomerandomshithere";
		var cookie = Request.Cookies["signupkey"];
		
		if (string.IsNullOrEmpty(cookie))
			return false;
		
		try
		{
			var decrypted = DecryptWithKey(cookie, key);
			return decrypted.StartsWith("THISISABUBBABLOXSIGNUPKEYANDTHISISTHESTARTINGPOINTFORITPLZCHANGEME|") && 
				   decrypted.EndsWith("|BUBBABLOX");
		}
		catch
		{
			return false;
		}
	}
	
	private class TokenRes
	{
		public string access_token { get; set; }
		public string token_type { get; set; }
		public int Expires_in { get; set; }
		public string refresh_token { get; set; }
		public string scope { get; set; }
	}

	private class DiscordUser
	{
		public string id { get; set; }
		public string username { get; set; }
		public string discriminator { get; set; }
		public string avatar { get; set; }
	}
	
	[HttpGetBypass("forgot-password")]
	public async Task<MVC.IActionResult> FrorgotPassword()
	{
		var clientId = Roblox.Configuration.DiscordClientID;
		var redirecturl = Uri.EscapeDataString(Roblox.Configuration.DiscordForgotPasswordRedirect);
		var scope = Uri.EscapeDataString("identify");
		
		return new MVC.RedirectResult(
			$"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={redirecturl}&response_type=code&scope={scope}"
		);
	}

	[HttpGetBypass("forgotcb")]
	public async Task<MVC.IActionResult> ForgotPasswordCallback([MVC.FromQuery] string code)
	{
		try
		{
			var httpClient = new HttpClient();
			var parameters = new Dictionary<string, string>
			{
				{"client_id", Roblox.Configuration.DiscordClientID},
				{"client_secret", Roblox.Configuration.DiscordClientSecret},
				{"grant_type", "authorization_code"},
				{"code", code},
				{"redirect_uri", Roblox.Configuration.DiscordForgotPasswordRedirect}
			};

			var response = await httpClient.PostAsync("https://discord.com/api/oauth2/token", 
				new FormUrlEncodedContent(parameters));
			
			if (!response.IsSuccessStatusCode)
			{
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Discord verification failed. Please try again.");
			}

			var tokenResponse = await response.Content.ReadFromJsonAsync<TokenRes>();
			
			httpClient.DefaultRequestHeaders.Authorization = 
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.access_token);
			
			var userRes = await httpClient.GetAsync("https://discord.com/api/users/@me");
			var userInfo = await userRes.Content.ReadFromJsonAsync<DiscordUser>();

			var userId = await services.users.GetUserIdFromDiscordId(userInfo.id);
			if (userId == 0)
			{
				return Redirect("/forgotpasswordOrUsername?forgotmsg=There is no account linked to this Discord");
			}

			var resetToken = Guid.NewGuid().ToString();
			var expiry = DateTime.UtcNow.AddHours(1);
			
			await services.users.CreatePasswordResetToken(userId, resetToken, expiry);

			var encryptedToken = EncryptWithKey($"{userId}|{resetToken}|{expiry:o}", Configuration.DiscordKey);
			
			Response.Cookies.Append("resetpasstoken", encryptedToken, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Lax,
				Expires = expiry
			});
			
			Response.Cookies.Append("resetpasswordverified", "true", new CookieOptions
			{
				HttpOnly = false,
				Secure = true,
				SameSite = SameSiteMode.Lax,
				Expires = DateTimeOffset.Now.AddMinutes(60)
			});

			return Redirect("/forgotpasswordOrUsername");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"password reset error: {ex}");
			return Redirect("/forgotpasswordOrUsername?forgotmsg=Password reset failed. Please try again.");
		}
	}

	[HttpPostBypass("reset-password")]
	public async Task<MVC.IActionResult> ResetPassword([MVC.FromForm] string newPassword)
	{
		try
		{
			var cookie = Request.Cookies["resetpasstoken"];
			
			if (string.IsNullOrEmpty(cookie))
			{
				Response.Cookies.Delete("resetpasstoken");
				Response.Cookies.Delete("resetpasswordverified");
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Invalid/expired token! Please try again.");
			}

			var decrypted = DecryptWithKey(cookie, Configuration.DiscordKey);
			var parts = decrypted.Split('|');
			if (parts.Length != 3 || !long.TryParse(parts[0], out var userId) || 
				!DateTime.TryParse(parts[2], out var expiry))
			{
				Response.Cookies.Delete("resetpasstoken");
				Response.Cookies.Delete("resetpasswordverified");
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Invalid token, please try again!");
			}

			// make expiry UTC if it's not already cause it likes to expire itself
			if (expiry.Kind != DateTimeKind.Utc)
			{
				expiry = expiry.ToUniversalTime();
			}

			if (expiry < DateTime.UtcNow)
			{
				Response.Cookies.Delete("resetpasstoken");
				Response.Cookies.Delete("resetpasswordverified");
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Token has expired, please try again!");
			}

			var resetToken = parts[1];
			var isValid = await services.users.ValidatePasswordResetToken(userId, resetToken);
			if (!isValid)
			{
				Response.Cookies.Delete("resetpasstoken");
				Response.Cookies.Delete("resetpasswordverified");
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Invalid token, please try again!");
			}
			
			if (string.IsNullOrEmpty(newPassword) || !services.users.IsPasswordValid(newPassword))
			{
				return Redirect("/forgotpasswordOrUsername?forgotmsg=Invalid password.");
			}

			await services.users.ChangePassword(userId, newPassword);

			await services.users.DeleteResetPassword(userId, resetToken);
			Response.Cookies.Delete("resetpasstoken");
			Response.Cookies.Delete("resetpasswordverified");

			return Redirect("/forgotpasswordOrUsername?redirect=true");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"password reset error: {ex}");
			return Redirect("/forgotpasswordOrUsername?forgotmsg=Password reset failed. Please try again.");
		}
	}
		
	[HttpGetBypass("discordverify")]
	public async Task<MVC.IActionResult> DiscordURL()
	{
		var clientId = Roblox.Configuration.DiscordClientID;
		var redirecturl = Uri.EscapeDataString(Roblox.Configuration.DiscordRedirect);
		var scope = Uri.EscapeDataString("identify");
		
		return new MVC.RedirectResult(
			$"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={redirecturl}&response_type=code&scope={scope}"
		);
	}
		
	[HttpGetBypass("discordcb")]
	public async Task<MVC.IActionResult> DiscordVerify([MVC.FromQuery] string code)
	{
		try
		{
			if (Request.Query.ContainsKey("error"))
			{
				return Redirect("/");
			}

			if (Request.Cookies["discord_id"] != null)
			{
				return Redirect("/");
			}

			var clientid = Roblox.Configuration.DiscordClientID;
			var clientsec = Roblox.Configuration.DiscordClientSecret;
			var redirurl = Roblox.Configuration.DiscordRedirect;

			var httpClient = new HttpClient();
			var parameters = new Dictionary<string, string>
			{
				{"client_id", clientid},
				{"client_secret", clientsec},
				{"grant_type", "authorization_code"},
				{"code", code},
				{"redirect_uri", redirurl}
			};

			var Response = await httpClient.PostAsync("https://discord.com/api/oauth2/token", 
				new FormUrlEncodedContent(parameters));
			
			if (!Response.IsSuccessStatusCode)
			{
				return Redirect("/?signupmsg=Discord verification failed. Please try again.");
			}

			var discordtoken = await Response.Content.ReadFromJsonAsync<TokenRes>();
			
			httpClient.DefaultRequestHeaders.Authorization = 
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordtoken.access_token);
			
			var userres = await httpClient.GetAsync("https://discord.com/api/users/@me");
			var userinfo = await userres.Content.ReadFromJsonAsync<DiscordUser>();

			if (await services.users.IsDiscordIdUsed(userinfo.id))
			{
				return Redirect("/?signupmsg=This Discord account is already linked to another BubbaBlox account.");
			}
			
			// this is so retarded please change this later (this should match validatesignupcookie)
			const string key = "Thisisthezachydramaandthebubbabloxsignupkeyidksomerandomshithere";
			var token = $"THISISABUBBABLOXSIGNUPKEYANDTHISISTHESTARTINGPOINTFORITPLZCHANGEME|{DateTime.UtcNow:yyyyMMddHHmmss}|BUBBABLOX";
			var encryptedtoken = EncryptWithKey(token, key);
			
			HttpContext.Response.Cookies.Append("signupkey", encryptedtoken, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Lax,
				Expires = DateTimeOffset.Now.AddDays(1)
			});

			HttpContext.Response.Cookies.Append("discord_id", userinfo.id, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Lax,
				Expires = DateTimeOffset.Now.AddMinutes(10)
			});

			HttpContext.Response.Cookies.Append("discord_profile", JsonSerializer.Serialize(new 
			{
				id = userinfo.id,
				username = userinfo.username,
				discriminator = userinfo.discriminator,
				avatar = userinfo.avatar
			}), new CookieOptions
			{
				HttpOnly = false,
				Secure = true,
				SameSite = SameSiteMode.Lax,
				Expires = DateTimeOffset.Now.AddMinutes(10),
				Path = "/"
			});

			return Redirect("/");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Discord verification error: {ex.Message}");
			return Redirect("/?signupmsg=Discord verification failed. Please try again.");
		}
	}
	
	[HttpGetBypass("login-with-discord")]
	public async Task<MVC.IActionResult> DiscordLoginRedir()
	{
		var clientId = Roblox.Configuration.DiscordClientID;
		var redirecturl = Uri.EscapeDataString(Roblox.Configuration.DiscordLoginRedirect);
		var scope = Uri.EscapeDataString("identify");
		
		return new MVC.RedirectResult(
			$"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={redirecturl}&response_type=code&scope={scope}"
		);
	}
	
	[HttpGetBypass("logincb")]
	public async Task<MVC.IActionResult> DiscordLogin([MVC.FromQuery] string code)
	{
		try
		{
			if (Request.Query.ContainsKey("error"))
			{
				return Redirect("/?loginmsg=Discord login failed, please try again!");
			}

			var httpClient = new HttpClient();
			var parameters = new Dictionary<string, string>
			{
				{"client_id", Roblox.Configuration.DiscordClientID},
				{"client_secret", Roblox.Configuration.DiscordClientSecret},
				{"grant_type", "authorization_code"},
				{"code", code},
				{"redirect_uri", Roblox.Configuration.DiscordLoginRedirect}
			};

			var token = await httpClient.PostAsync("https://discord.com/api/oauth2/token", 
				new FormUrlEncodedContent(parameters));
			
			if (!token.IsSuccessStatusCode)
			{
				return Redirect("/?loginmsg=Discord verification failed, please try again!");
			}

			var tokendata = await token.Content.ReadFromJsonAsync<TokenRes>();
			
			httpClient.DefaultRequestHeaders.Authorization = 
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokendata.access_token);
			
			var discordinfo = await httpClient.GetAsync("https://discord.com/api/users/@me");
			var discordID = await discordinfo.Content.ReadFromJsonAsync<DiscordUser>();

			var ID = await services.users.GetUserIdFromDiscordId(discordID.id);
			if (ID == 0)
			{
				return Redirect("/?loginmsg=There is no account linked to this Discord");
			}


			var info = await services.users.GetUserById(ID);
			if (info.accountStatus != AccountStatus.Ok)
			{
				return Redirect("/?loginmsg=Account locked, please contact a staff member");
			}

			var sessionId = await services.users.CreateSession(ID);
			var cookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
			{
				sessionId = sessionId,
				createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
			});

			HttpContext.Response.Cookies.Append(
				".ROBLOSECURITY", 
				cookie,
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.None,
					Expires = DateTimeOffset.Now.AddYears(1)
				});

			return Redirect("/home");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Discord login error: {ex}");
			return Redirect("/?loginmsg=Discord login failed, please try again!");
		}
	}

	[HttpPostBypass("login/signup")]
	[MVC.Consumes("application/x-www-form-urlencoded")]
	// the form errors probably don't work, but doesn't hurt to try (TODO: replace this iwth something else cause it didn't lmao)
	public async Task<MVC.IActionResult> Signup(
		[MVC.FromForm] string username,
		[MVC.FromForm] string password,
		[MVC.FromForm] string birthday = null,
		[MVC.FromForm] int? gender = null,
		[MVC.FromForm] string context = null,
		[MVC.FromForm] bool isEligibleForHideAdsAbTest = false)
	{
		if (!ValidateSignupCookie())
		{
			return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
				new List<string> { "AbuseDetection-ERR" });
		}

		var discordUserId = Request.Cookies["discord_id"];
		if (string.IsNullOrEmpty(discordUserId))
		{
			return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
				new List<string> { "AbuseDetection-IDErr" });
		}

		if (await services.users.IsDiscordIdUsed(discordUserId))
		{
			return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
				new List<string> { "AbuseDetection-AlreadyUsed" });
		}
		
		MVC.IActionResult ReturnFormError(string msg, List<string> reasons)
		{
			return StatusCode(403, new 
			{
				message = msg,
				reasons = reasons,
				fieldErrors = new object[0] 
			});
		}

		try
		{
			FeatureFlags.FeatureCheck(FeatureFlag.SignupEnabled);
		}
		catch (RobloxException)
		{
			return Redirect("/?signupmsg=Registration is temporarily unavailable. Please try again later.");
		}

		var ip = GetIP(GetRequesterIpRaw(HttpContext));

		try
		{
			await services.cooldown.CooldownCheck($"signup:step1:" + ip, TimeSpan.FromSeconds(5));
		}
		catch (CooldownException)
		{
			Writer.Info(LogGroup.SignUp, "Sign up failed, cooldown step 1");
			return ReturnFormError("Too many attempts. Try again in about 5 seconds.", 
				new List<string> { "Cooldown" });
		}

		if (string.IsNullOrWhiteSpace(username))
			return ReturnFormError("Username is required", 
				new List<string> { "UsernameRequired" });

		if (!await services.users.IsUsernameValid(username))
			return ReturnFormError(
				"Invalid Username", 
				new List<string> { "InvalidUsername" });

		if (!await services.users.IsNameAvailableForSignup(username))
			return ReturnFormError("Username is already taken", 
				new List<string> { "UsernameTaken" });

		if (string.IsNullOrWhiteSpace(password))
			return ReturnFormError("Password is required", 
				new List<string> { "PasswordRequired" });

		if (!services.users.IsPasswordValid(password))
			return ReturnFormError(
				"Invalid password", 
				new List<string> { "InvalidPassword" });

		if (string.IsNullOrWhiteSpace(birthday))
			return ReturnFormError("Birthday is required", 
				new List<string> { "BirthdayRequired" });

		if (!gender.HasValue)
			return ReturnFormError("Gender is required", 
				new List<string> { "GenderRequired" });

		if (gender.Value != 2 && gender.Value != 3)
			return ReturnFormError("Invalid gender selection", 
				new List<string> { "InvalidGender" });

		if (!await Roblox.AbuseDetection.Report.UsersAbuse.ShouldAllowCreation(new(ip)))
			return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
				new List<string> { "AbuseDetection" });

		var finalcool = "signup:step2:" + ip;
		try
		{
			await services.cooldown.CooldownCheck(finalcool, TimeSpan.FromMinutes(5));
		}
		catch (CooldownException)
		{
			return ReturnFormError("Too many attempts. Try again in about 5 minutes.", 
				new List<string> { "Cooldown" });
		}

		try
		{
			var genderEnum = gender.Value switch
			{
				3 => Gender.Female,
				2 => Gender.Male,
				_ => Gender.Unknown
			};

			var newuser = await services.users.CreateUser(username, password, genderEnum);
			
			// add discord to DB incase they alt
			// ik you can just make a discord alt but they are usually hard to make in my experience and need phone number and shit
			if (!string.IsNullOrEmpty(discordUserId))
			{
				await services.users.LinkDiscordAccount(newuser.userId, discordUserId);
			}

			var sess = await services.users.CreateSession(newuser.userId);

			var sesscookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
			{
				sessionId = sess,
				createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
			});

			HttpContext.Response.Cookies.Append(
				".ROBLOSECURITY",
				sesscookie,
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.None,
					Expires = DateTimeOffset.Now.AddYears(1)
				});

			var discprofilejson = Request.Cookies["discord_profile"];
			DiscordProfile discordProfile = null;
			string discordUsername = "Unknown";
			string discordAvatarUrl = null;

			if (!string.IsNullOrEmpty(discprofilejson))
			{
				try
				{
					discordProfile = JsonConvert.DeserializeObject<DiscordProfile>(discprofilejson);
					discordUsername = $"{discordProfile.username}#{discordProfile.discriminator}";
					if (!string.IsNullOrEmpty(discordProfile.avatar))
					{
						discordAvatarUrl = $"https://cdn.discordapp.com/avatars/{discordUserId}/{discordProfile.avatar}.png";
					}
				}
				catch (Exception ex)
				{
					Writer.Info(LogGroup.SignUp, "failed to parse discord profile", ex);
				}
			}

			try
			{
				var webhook = Roblox.Configuration.SignupWebhook;
				string genderText = gender.Value == 3 ? "Female" : "Male";
				
				var embed = new
				{
					title = "Signup",
					description = $"A new user has signed up",
					color = 0x00ff00,
					thumbnail = new
					{
						url = discordAvatarUrl
					},
					fields = new[]
					{
						new
						{
							name = "Username",
							value = username,
							inline = true
						},
						new
						{
							name = "Gender",
							value = genderText,
							inline = true
						},
						new
						{
							name = "Discord ID",
							value = discordUserId,
							inline = true
						},
						new
						{
							name = "Discord Name",
							value = discordUsername,
							inline = true
						}
					},
					timestamp = DateTime.UtcNow.ToString("o")
				};

				var payload = new
				{
					embeds = new[] { embed }
				};

				using (var httpClient = new HttpClient())
				{
					var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
					await httpClient.PostAsync(webhook, content);
				}
			}
			catch (Exception ex)
			{
				Writer.Info(LogGroup.SignUp, "Failed to send signup notif to webhook (is it configured?)", ex);
			}

			Response.Cookies.Delete("discord_id");
			Response.Cookies.Delete("discord_profile");
			Response.Cookies.Delete("signupkey");
			Response.Cookies.Delete("resetpasstoken");
			Response.Cookies.Delete("resetpasswordverified");
			
			return Redirect("/home");
		}
		catch (Exception ex)
		{
			await services.cooldown.ResetCooldown(finalcool);
			Writer.Info(LogGroup.SignUp, "Signup failed", ex);
			return ReturnFormError("An unexpected error occurred. Please try again.", 
				new List<string> { "UnexpectedError" });
		}
	}
		// this is for the PHP place launcher so it gets the correct data
		[HttpGetBypass("game/get-data")]
		public async Task<dynamic> GetGameData([Required] long placeId, string? ticket = null)
		{
			try
			{
				long userId;
				
				if (!string.IsNullOrEmpty(ticket))
				{
					try 
					{
						var ticketData = services.gameServer.DecodeTicket(ticket, null);
						userId = ticketData.userId;
						
						if (userId <= 0)
						{
							throw new RobloxException(401, 0, "Invalid user ID");
						}

						var ticketUserInfo = await services.users.GetUserById(userId);
						if (ticketUserInfo == null)
						{
							throw new RobloxException(404, 0, "User not found");
						}
					}
					catch (Exception decodeEx)
					{
						Console.WriteLine($"bad ticket: {decodeEx.Message}");
						throw new RobloxException(401, 0, "Invalid ticket");
					}
				}
				else if (userSession != null)
				{
					userId = userSession.userId;
				}
				else
				{
					throw new RobloxException(401, 0, "Not authenticated");
				}

				var userInfo = await services.users.GetUserById(userId);
				if (userInfo == null)
				{
					throw new RobloxException(404, 0, "User not found");
				}

				int accountAgeDays = (int)(DateTime.UtcNow - userInfo.created).TotalDays;

				var membership = await services.users.GetUserMembership(userId);
				string membershipType = membership?.membershipType switch
				{
					MembershipType.OutrageousBuildersClub => "OutrageousBuildersClub",
					MembershipType.TurboBuildersClub => "TurboBuildersClub",
					MembershipType.BuildersClub => "BuildersClub",
					_ => "None"
				};

				var placeDetails = await services.assets.GetAssetCatalogInfo(placeId);
				if (placeDetails == null || placeDetails.assetType != Type.Place)
				{
					throw new RobloxException(404, 0, "Place not found");
				}
				
				long universeId = await services.games.GetUniverseId(placeId);

				string creatorName;
				if (placeDetails.creatorType == CreatorType.User)
				{
					var creatorUser = await services.users.GetUserById(placeDetails.creatorTargetId);
					creatorName = creatorUser?.username ?? "Unknown";
				}
				else
				{
					creatorName = placeDetails.creatorName ?? "Unknown";
				}

				return new
				{
					success = true,
					user = new
					{
						userId = userId,
						username = userInfo.username,
						accountAgeDays = accountAgeDays,
						membershipType = membershipType
					},
					place = new
					{
						placeId = placeId,
						creatorId = placeDetails.creatorTargetId,
						creatorType = placeDetails.creatorType.ToString(),
						creatorName = creatorName,
						universeId = universeId
					}
				};
			}
			catch (Exception ex)
			{
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}
		
		public static long startUserId {get;set;} = 30;
#if DEBUG
        [HttpGetBypass("/game/get-join-script-debug")]
        public async Task<dynamic> GetJoinScriptDebug(long placeId, long userId = 12)
        {
            //startUserId = 12;
            var Result = services.gameServer.CreateTicket(startUserId, placeId, GetIP());
            startUserId++;
            return new
            {
                placeLauncher = $"{Configuration.BaseUrl}/placelauncher.ashx?ticket={HttpUtility.UrlEncode(Result)}",
                authenticationTicket = Result,
            };
        }
#endif
		// ik this sucks but  i was too lazy to implement the actual join script stuff
		// only reason for this is cause i'm too stupid to fix the ticket with authentication
		[HttpGetBypass("/game/PlaceLauncher.ashx")]
		[HttpPostBypass("/game/PlaceLauncher.ashx")]
		public async Task<MVC.IActionResult> PlaceLaunch()
		{
			try 
			{
				var plgs = $"{Configuration.GSUrl}/Game/PlaceLauncher.ashx{Request.QueryString}";
				
				using var httpClient = new HttpClient(new HttpClientHandler() {
					AllowAutoRedirect = false,
					UseCookies = false
				});

				httpClient.Timeout = TimeSpan.FromSeconds(30);
				
				foreach (var header in Request.Headers)
				{
					if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) || 
						header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
						header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) ||
						header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					try
					{
						if (!httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
						{
							//Console.WriteLine($"could not add header: {header.Key}");
						}
					}
					catch (Exception ex)
					{
						//Console.WriteLine($"error adding header {header.Key: {ex.Message}");
					}
				}

				var response = await httpClient.GetAsync(plgs, HttpCompletionOption.ResponseHeadersRead);

				var context = ControllerContext.HttpContext;
				foreach (var header in response.Headers)
				{
					if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
						continue;
						
					context.Response.Headers[header.Key] = header.Value.ToArray();
				}
				foreach (var header in response.Content.Headers)
				{
					if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
						continue;
						
					context.Response.Headers[header.Key] = header.Value.ToArray();
				}

				var stream = await response.Content.ReadAsStreamAsync();
				return new MVC.FileStreamResult(stream, 
					response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"placelauncher error: {ex}");
				return StatusCode(500, "Could not connect to game server");
			}
		}

		[HttpGetBypass("/game/PlaceLauncherBT.ashx")]
		[HttpPostBypass("/game/PlaceLauncherBT.ashx")]
		public async Task<dynamic> PlaceLaunchBT(long placeId)
		{	
			bool is2020Client = Request.Query.ContainsKey("2020");
			bool is2018Client = Request.Query.ContainsKey("2018");
			bool is2017Client = Request.Query.ContainsKey("2017");
			bool is2015Client = Request.Query.ContainsKey("2015");

			if (!await services.games.IsPlayable(placeId))
			{
				return BadRequest(new 
				{
					jobId = (string?)null,
					status = (int)JoinStatus.Error,
					message = "You can not access this place at this time."
				});
			}
			
			bool bypasscheck = Request.Headers.TryGetValue("btzawgPHPgameserverstart", out var headerValue) 
										  && headerValue == "startgamesessionforthisplace";

			if (userSession == null && !bypasscheck)
			{
				return BadRequest();
			}

			FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled, FeatureFlag.GameJoinEnabled);

			GameServerJwt details = new GameServerJwt
			{
				userId = userSession?.userId ?? 1,
				placeId = placeId,
				t = "GameJoinTicketV1.1",
				iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
				ip = GetIP()
			};

			var Result = is2020Client ?
				await services.gameServer.GetServerForPlace2020(placeId) :
				is2018Client ? 
				await services.gameServer.GetServerForPlace2018(placeId) :
				is2017Client ?
				await services.gameServer.GetServerForPlace2017(placeId) :
				is2015Client ?
				await services.gameServer.GetServerForPlace2015(placeId) :
				await services.gameServer.GetServerForPlace(details.placeId);
				
			if (Result.status == JoinStatus.Joining)
			{
				await Roblox.Metrics.GameMetrics.ReportGameJoinPlaceLauncherReturned(details.placeId);

				int serverPort = 0;
				if (GameServerService.currentGameServerPorts.TryGetValue(Result.job, out int port))
				{
					serverPort = port;
				}
				else
				{
					serverPort = -1;
				}

				var roblosecurity = Request.Cookies[".ROBLOSECURITY"];
				var joinScriptUrl = $"{Configuration.BaseUrl}/game/join.ashx?placeid={placeId}&ticket={Uri.EscapeDataString(roblosecurity)}";
				
				if (is2020Client)
					joinScriptUrl += "&2020=true";
				else if (is2018Client)
					joinScriptUrl += "&2018=true";
				else if (is2017Client)
					joinScriptUrl += "&2017=true";
				else if (is2015Client)
					joinScriptUrl += "&2015=true";
				
				return new
				{
					jobId = Result.job,
					status = (int)Result.status,
					serverPort = serverPort,
					joinScriptUrl = joinScriptUrl,
					authenticationUrl = Configuration.BaseUrl + "/Login/Negotiate.ashx",
					authenticationTicket = roblosecurity,
					message = (string?)null,
				};
			}

			return new
			{
				jobId = (string?)null,
				status = (int)Result.status,
				message = "Waiting for server",
			};
		}
		
		[HttpGetBypass("/game/Join.ashx")]
		[HttpPostBypass("/game/Join.ashx")]
		public async Task<MVC.IActionResult> JoinGame()
		{
			try 
			{
				var plgs = $"{Configuration.GSUrl}/Game/Join.ashx{Request.QueryString}";
				
				using var httpClient = new HttpClient(new HttpClientHandler() {
					AllowAutoRedirect = false,
					UseCookies = false
				});

				httpClient.Timeout = TimeSpan.FromSeconds(30);
				
				foreach (var header in Request.Headers)
				{
					if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) || 
						header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
						header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) ||
						header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					try
					{
						if (!httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
						{
							//Console.WriteLine($"could not add header: {header.Key}");
						}
					}
					catch (Exception ex)
					{
						//Console.WriteLine($"error adding header {header.Key}: {ex.Message}");
					}
				}

				var response = await httpClient.GetAsync(plgs, HttpCompletionOption.ResponseHeadersRead);

				var context = ControllerContext.HttpContext;
				foreach (var header in response.Headers)
				{
					if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
						continue;
						
					context.Response.Headers[header.Key] = header.Value.ToArray();
				}
				foreach (var header in response.Content.Headers)
				{
					if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
						continue;
						
					context.Response.Headers[header.Key] = header.Value.ToArray();
				}

				var stream = await response.Content.ReadAsStreamAsync();
				return new MVC.FileStreamResult(stream, 
					response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"join.ashx error: {ex}");
				return StatusCode(500, "Could not connect to game server");
			}
		}
		
		[HttpGetBypass("GetAllowedSecurityKeys")]
        public MVC.ActionResult<dynamic> AllowedSecurity()
        {
            return true;
        }
		
		[HttpGetBypass("GetAllowedMD5Hashes")]
        public MVC.ActionResult<dynamic> AllowedMD5Hashes()
        {
            List<string> allowedList = new List<string>()
            {
				"97e93df61c3357531585cebb22d2edff"
            };

            return new { data = allowedList };
        }

        [HttpGetBypass("GetAllowedSecurityVersions")]
        public MVC.ActionResult<dynamic> AllowedSecurityVersions()
        {
            List<string> allowedList = new List<string>()
            {  
				"0.285.0pcplayer",
				"0.283.0pcplayer",
				"0.275.0pcplayer"
            };
			
            return new { data = allowedList };
        }
		
		[HttpGetBypass("/v1/settings/application")]
		public async Task<MVC.IActionResult> RCCNewApplication(string applicationName)
		{
			string json = "PCDesktopClient";
			if (!string.IsNullOrEmpty(applicationName))
			{
				switch (applicationName)
				{
					case "PCDesktopClient":
						json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "PCDesktopClient.json");
						break;

					case "RCCServiceBubbleRev2021RCCIsSoTuff":
						json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "RCCService.json");
						break;
						
					case "GD5Z5gO1n0gYX1P":
						json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "PCDesktopClient.json");
						break;
				}
			}

			if (!System.IO.File.Exists(json))
			{
				return NotFound("{}");
			}

			string content = await System.IO.File.ReadAllTextAsync(json);
			return Content(content, "text/plain");
		}
		
		public enum ThumbnailType
        {
            UserHeadshot = 1,
            UserAvatar,
            Asset,
            PlaceIcon
        }
		
		public class ThumbnailEntry
		{
			public long targetId { get; set; }
			public ThumbnailState state { get; set; }
			public string? imageUrl { get; set; }
			public string version { get; set; } = "TN3";
		}
		
		public class BatchRequestEntry
		{
			public string requestId { get; set; }
			public string type { get; set; }
			public long targetId { get; set; }
		}
		
		public enum ThumbnailState
		{
			Error = 1,
			Completed,
			InReview,
			Pending,
			Blocked,
			TemporarilyUnavailable
			// 'Error', 'Completed', 'InReview', 'Pending', 'Blocked', 'TemporarilyUnavailable'],
		}
		
		private async Task<IEnumerable<dynamic>> MultiGetThumbnailsGeneric(
			List<BatchRequestEntry> entries, 
			string typeName, 
			Func<IEnumerable<long>, Task<IEnumerable<Roblox.Dto.Thumbnails.ThumbnailEntry>>> getThumbnailsFunc)
		{
			var result = new List<dynamic>();
			var targetIds = entries.Where(x => x.type == typeName).Select(x => x.targetId).ToList();
			
			if (targetIds.Any())
			{
				var thumbnails = await getThumbnailsFunc(targetIds);
				foreach (var thumbnail in thumbnails)
				{
					var imageUrl = thumbnail.imageUrl;
					if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					{
						imageUrl = Configuration.BaseUrl + imageUrl;
					}
					
					result.Add(new
					{
						requestId = entries.First(x => x.targetId == thumbnail.targetId && x.type == typeName).requestId,
						errorCode = 0,
						errorMessage = "",
						targetId = thumbnail.targetId,
						state = (ThumbnailState)thumbnail.state,
						imageUrl = imageUrl
					});
				}
			}
			
			return result;
		}
						
		private async Task<MVC.RedirectResult> GetThumbnailUrl(long id, ThumbnailType type)
		{
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				var avatar18Plus = await services.avatar.IsUserAvatar18Plus(id);
				if (avatar18Plus)
					return new MVC.RedirectResult("/img/blocked.png", false);
			}

			List<ThumbnailEntry> result = null;

			switch (type)
			{
				case ThumbnailType.UserHeadshot:
					var headshots = await services.thumbnails.GetUserHeadshots(new[] { id });
					result = headshots.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.UserAvatar:
					var avatars = await services.thumbnails.GetUserThumbnails(new[] { id });
					result = avatars.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.Asset:
					var assets = await services.thumbnails.GetAssetThumbnails(new[] { id });
					result = assets.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.PlaceIcon:
					long universeId = await services.games.GetUniverseId(id);
					var gameIcons = await services.thumbnails.GetGameIcons(new[] { universeId });
					result = gameIcons.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
			}

			var imageUrl = result?.FirstOrDefault()?.imageUrl ?? "/img/placeholder.png";
			return new MVC.RedirectResult(imageUrl, false);
		}

        //avatar stuff
        [HttpGetBypass("avatar-thumbnail/image")]
        public async Task<MVC.RedirectResult> GetAvatarThumbnail(long userId, string? username)
        {
            if (username != null)
            {
                try
                {
                    userId = await services.users.GetUserIdFromUsername(username);
                }
                catch (Exception)
                {
                    return new MVC.RedirectResult("/img/blocked.png", false);
                }
            }
            return await GetThumbnailUrl(userId, ThumbnailType.UserAvatar);
        }

        //headshot stuff
        [HttpGetBypass("headshot-thumbnail/image")]
        [HttpGetBypass("thumbs/avatar-headshot.ashx")]
        public async Task<MVC.RedirectResult> GetAvatarHeadShot(long userId)
        {
            return await GetThumbnailUrl(userId, ThumbnailType.UserHeadshot);
        }

        //place icon
        [HttpGetBypass("Thumbs/PlaceIcon.ashx")]
        [HttpGetBypass("Thumbs/GameIcon.ashx")]
        public async Task<MVC.RedirectResult> GetGameIcon(long assetId)
        {
            return await GetThumbnailUrl(assetId, ThumbnailType.PlaceIcon);
        }

        //asset icon stuff
        [HttpGet("asset-thumbnail/image")]
        [HttpGetBypass("Game/Tools/ThumbnailAsset.ashx")]
        public async Task<MVC.RedirectResult> GetAssetThumbnail(long assetId, long? aid)
        {        
            if(aid != null)
                assetId = (long)aid;
            return await GetThumbnailUrl(assetId, ThumbnailType.Asset);
        }

        //all json thumbnail apis
        [HttpGetBypass("avatar-thumbnail/json")]
        public async Task<dynamic> GetAvatarThumbnailJson([Required] long userId)
        {
            var result = (await services.thumbnails.GetUserThumbnails(new[] {userId})).ToList();
            return new
            {
                Url = $"{Configuration.BaseUrl}{result[0].imageUrl}",
                Final = true,
                SubstitutionType = 0
            };
        }

        [HttpGetBypass("asset-thumbnail/json")]
        public async Task<dynamic> GetAssetThumbnailJson([Required] long assetId)
        {
            var result = (await services.thumbnails.GetAssetThumbnails(new[] {assetId})).ToList();
            return new
            {
                Url = $"{Configuration.BaseUrl}{result[0].imageUrl}",
                Final = true,
                SubstitutionType = 0
            };
        }     

		[HttpGetBypass("asset-gameicon/multiget")]
		public async Task<dynamic> GetGameIconMultiGet([MVC.FromQuery] List<long> universeId)
		{
			var gameIcons = await services.thumbnails.GetGameIcons(universeId);
			return gameIcons.Select(x => new
			{
				x.targetId,
				state = (ThumbnailState)x.state,
				imageUrl = x.imageUrl,
				version = x.version
			}).ToList();
		}

		[HttpGetBypass("v1/games/icons")]
		public async Task<RobloxCollection<ThumbnailEntry>> GetGameIcons(string universeIds)
		{
			var parsed = universeIds.Split(",").Select(long.Parse).Distinct().ToList();
			if (parsed.Count is > 200 or < 0) throw new BadRequestException();
			
			var result = await services.thumbnails.GetGameIcons(parsed);
			var result2 = result.Select(thumbnail => new ThumbnailEntry
			{
				targetId = thumbnail.targetId,
				imageUrl = Configuration.BaseUrl + thumbnail.imageUrl,
				state = (ThumbnailState)thumbnail.state,
			}).ToList();
			
			return new()
			{
				data = result2,
			};
		}

		[HttpGet("v1/users/avatar-headshot")]
		public async Task<RobloxCollection<ThumbnailEntry>> GetMultiHeadshot(string userIds)
		{
			var parsed = userIds.Split(",").Select(long.Parse).Distinct().ToList();
			if (parsed.Count is > 200 or < 0) throw new BadRequestException();
			
			var result = (await services.thumbnails.GetUserHeadshots(parsed)).ToList();
			var result2 = result.Select(x => new ThumbnailEntry
			{
				targetId = x.targetId,
				state = (ThumbnailState)x.state,
				imageUrl = x.imageUrl,
				version = x.version
			}).ToList();
			
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				foreach (var item in result2)
				{
					if (item.imageUrl is null) continue;

					var avatar18Plus = await services.avatar.IsUserAvatar18Plus(item.targetId);
					if (avatar18Plus)
					{
						item.state = ThumbnailState.Blocked;
						item.imageUrl = "/img/blocked.png";
					}
					else
					{
						item.imageUrl = Configuration.BaseUrl + item.imageUrl;
					}
				}
			}
			else
			{
				foreach (var item in result2)
				{
					if (item.imageUrl is null) continue;
					item.imageUrl = Configuration.BaseUrl + item.imageUrl;
				}
			}
			return new()
			{
				data = result2,
			};
		}
		
		[HttpPostBypass("v1/batch")]
		public async Task<dynamic> BatchThumbnailsRequest()
		{
			try
			{
				bool isGzip = Request.Headers["Content-Encoding"].ToString() == "gzip";
				
				IEnumerable<BatchRequestEntry> requestEntries;
				var tasks = new List<Task<IEnumerable<dynamic>>>();
				
				if (isGzip)
				{
					using (var decompressedStream = new MemoryStream())
					{
						using (var requestStream = Request.Body)
						{
							using (var gzipStream = new GZipStream(requestStream, CompressionMode.Decompress))
							{
								Console.WriteLine($"[BatchThumbnails] decompressing gzip stream");
								await gzipStream.CopyToAsync(decompressedStream);
							}
						}
						decompressedStream.Seek(0, SeekOrigin.Begin);

						using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
						{
							var json = await reader.ReadToEndAsync();
							
							try
							{
								requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
								Console.WriteLine($"[BatchThumbnails] deserialized {requestEntries?.Count() ?? 0} entrie(s) from gzip");
							}
							catch (Exception deserializeEx)
							{
								Console.WriteLine($"[BatchThumbnails] JSON deserialization failed: {deserializeEx.Message}");
								Console.WriteLine($"[BatchThumbnails] StackTrace: {deserializeEx.StackTrace}");
								throw;
							}
						}
					}
				}
				else
				{
					using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
					{
						var json = await reader.ReadToEndAsync();
						
						try
						{
							requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
							Console.WriteLine($"[BatchThumbnails] deserialized {requestEntries?.Count() ?? 0} JSON entrie(s)");
						}
						catch (Exception deserializeEx)
						{
							Console.WriteLine($"[BatchThumbnails] JSON deserialization failed: {deserializeEx.Message}");
							Console.WriteLine($"[BatchThumbnails] StackTrace: {deserializeEx.StackTrace}");
							throw;
						}
					}
				}

				var thumbs = requestEntries.ToList();
				Console.WriteLine($"[BatchThumbnails] processing {thumbs.Count} thumbnail request(s)");
				
				var taskDefinitions = new List<(string name, Func<IEnumerable<long>, Task<IEnumerable<Roblox.Dto.Thumbnails.ThumbnailEntry>>> func)>
				{
					("Avatar", services.thumbnails.GetUserThumbnails),
					("AvatarThumbnail", services.thumbnails.GetUserThumbnails),
					("AvatarHeadShot", services.thumbnails.GetUserHeadshots),
					("GameIcon", services.thumbnails.GetGameIcons),
					("GameThumbnail", services.thumbnails.GetAssetThumbnails),
					("Asset", services.thumbnails.GetAssetThumbnails),
					("AssetThumbnail", services.thumbnails.GetAssetThumbnails),
					("GroupIcon", services.thumbnails.GetGroupIcons),
				};

				var taskList = new List<Task<IEnumerable<dynamic>>>();
				
				foreach (var (name, func) in taskDefinitions)
				{
					var targetIds = thumbs.Where(x => x.type == name).Select(x => x.targetId).ToList();
					if (targetIds.Any())
					{
						Console.WriteLine($"[BatchThumbnails] queueing {name} task with target IDs: {string.Join(", ", targetIds)}");
						taskList.Add(MultiGetThumbnailsGeneric(thumbs, name, func));
					}
					else
					{
						//Console.WriteLine($"[BatchThumbnails] no entries for {name}, skipping task");
					}
				}

				Console.WriteLine($"[BatchThumbnails] starting {taskList.Count} tasks");
				var stopwatch = Stopwatch.StartNew();
				
				try
				{
					var allResults = await Task.WhenAll(taskList);
					stopwatch.Stop();

					foreach (var result in allResults)
					{
						if (result != null)
						{
							foreach (var item in result)
							{
								Console.WriteLine($"[BatchThumbnails] Result: {JsonConvert.SerializeObject(item)}");
							}
						}
					}

					return new RobloxCollection<dynamic>()
					{
						data = allResults.SelectMany(x => x),
					};
				}
				catch (Exception taskEx)
				{
					stopwatch.Stop();
					Console.WriteLine($"[BatchThumbnails] Task execution failed after {stopwatch.ElapsedMilliseconds}ms: {taskEx.Message}");
					Console.WriteLine($"[BatchThumbnails] StackTrace: {taskEx.StackTrace}");
					throw;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BatchThumbnails] Overall processing failed: {ex.Message}");
				Console.WriteLine($"[BatchThumbnails] StackTrace: {ex.StackTrace}");
				throw;
			}
		}

/*         [HttpPostBypass("v1/batch")]
        public async Task<dynamic> BatchThumbnailsRequest()
        {
            bool isGzip = Request.Headers["Content-Encoding"].ToString() == "gzip";
            IEnumerable<BatchRequestEntry> requestEntries;
            var tasks = new List<Task<IEnumerable<dynamic>>>();
            Console.WriteLine(isGzip);
            if (isGzip)
            {
                using (var decompressedStream = new MemoryStream())
                {
                    using (var requestStream = Request.Body)
                    {
                        using (var gzipStream = new GZipStream(requestStream, CompressionMode.Decompress))
                        {
                            await gzipStream.CopyToAsync(decompressedStream);
                        }
                    }
                    decompressedStream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();
                        Console.WriteLine(json);
                        requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
                    }
                }
            }
            else
            {
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    var json = await reader.ReadToEndAsync();
                    Console.WriteLine(json);
                    requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
                }
            }

            var thumbs = requestEntries.ToList();
            var allResults = await Task.WhenAll(new List<Task<IEnumerable<dynamic>>>()
            {
                MultiGetThumbnailsGeneric(thumbs, "Avatar", services.thumbnails.GetUserThumbnails),
                MultiGetThumbnailsGeneric(thumbs, "AvatarThumbnail", services.thumbnails.GetUserThumbnails),
                MultiGetThumbnailsGeneric(thumbs, "AvatarHeadShot", services.thumbnails.GetUserHeadshots),
                MultiGetThumbnailsGeneric(thumbs, "GameIcon", services.thumbnails.GetGameIcons),
                MultiGetThumbnailsGeneric(thumbs, "GameThumbnail", services.thumbnails.GetAssetThumbnails),
                MultiGetThumbnailsGeneric(thumbs, "Asset", services.thumbnails.GetAssetThumbnails),
                MultiGetThumbnailsGeneric(thumbs, "AssetThumbnail", services.thumbnails.GetAssetThumbnails),
                MultiGetThumbnailsGeneric(thumbs, "GroupIcon", services.thumbnails.GetGroupIcons),
            });
            return new RobloxCollection<dynamic>()
            {
                data = allResults.SelectMany(x => x),
            };
        } */
		
		[HttpPostBypass("/api/thumbnail")]
		public async Task ThumbnailEndpoint()
		{
			try
			{
				string jsonBody;
				using (var reader = new StreamReader(Request.Body))
				{
					jsonBody = await reader.ReadToEndAsync();
				}

				var headers = new Dictionary<string, string>();
				foreach (var key in Request.Headers.Keys)
				{
					headers[key] = Request.Headers[key];
				}

				var queryParams = new Dictionary<string, string>();
				foreach (var key in Request.Query.Keys)
				{
					queryParams[key] = Request.Query[key];
				}

				Console.WriteLine($"Received thumbnail data:");
				Console.WriteLine($"Headers: {System.Text.Json.JsonSerializer.Serialize(headers)}");
				Console.WriteLine($"Query Params: {System.Text.Json.JsonSerializer.Serialize(queryParams)}");
				Console.WriteLine($"Body: {jsonBody}");

				Response.StatusCode = 200;
				await Response.WriteAsync("Thumbnail received successfully");
			}
			catch (Exception ex)
			{
				Response.StatusCode = 500;
				await Response.WriteAsync($"Error: {ex.Message}");
			}
		}
		
		[HttpPostBypass("moderation/filtertext")]
        public dynamic GetModerationText()
        {
            var text = services.filter.FilterText(HttpContext.Request.Form["text"].ToString());
            return new
            {
                success = true,
                data = new 
                {
                    white = text,
                    black = text
                }
            };
        }
		
        [HttpPostBypass("moderation/v2/filtertext")]
        public dynamic GetModerationTextV2()
        {
            var text = services.filter.FilterText(HttpContext.Request.Form["text"].ToString());
            var json = new
            {
                success = true,
                data = new
                {
                    AgeUnder13 = text,
                    Age13OrOver = text,
                }
            };
            string jsonString = JsonConvert.SerializeObject(json);
            return Content(jsonString, "application/json");
        }
		
		[HttpGetBypass("/Game/ClientPresence.ashx")]
        public async Task ClientPresenceAshx(string action, long placeId, long userId, bool IsTeleport)
        {
            if(action == "disconnect"){
                string JobId = await services.gameServer.GetJobIdByUserId(userId);
                if(JobId == null)
                {
                    return;
                }
                await services.gameServer.OnPlayerLeave(userId, placeId, JobId);
            }
        }
		
		[HttpGetBypass("v1.1/game-start-info")]
        public dynamic GameStartInfo(long universeId)
        {
            return new
            {
                gameAvatarType = "PlayerChoice",
                allowCustomAnimations = "True",
                universeAvatarCollisionType = "OuterBox",
                universeAvatarBodyType = "Standard",
                jointPositioningType = "ArtistIntent",
                message = "",
                universeAvatarMinScales = new
                {
                    height = 0.9,
                    width = 0.7,
                    head = 0.95,
                    depth = 0.0,
                    proportion = 0.0,
                    bodyType = 0.0
                },
                universeAvatarMaxScales = new
                {
                    height = 1.05,
                    width = 1.0,
                    head = 1.0,
                    depth = 0.0,
                    proportion = 1.0,
                    bodyType = 1.0
                },
                universeAvatarAssetOverrides = new List<object>(),
                moderationStatus = ""
            };
        }
		
		[HttpGetBypass("v1/avatar-fetch")]
        [HttpGetBypass("/v1.1/avatar-fetch")]
        public async Task<MVC.IActionResult> CharacterFetch(long userId, long? placeId = null)
        {
            List<long> accessoryVersionIds = new List<long>();
            List<long> equippedGearVersionIds = new List<long>();
            string userAgent = Request.Headers["User-Agent"].ToString();
            var wornAssets = await services.avatar.GetWornAssets(userId);
            var avatar = await services.avatar.GetAvatar(userId);
			var avatarTypeEntry = await services.avatar.GetAvatarType(userId);
			var scalesEntry = await services.avatar.GetAvatarScales(userId);
			bool gearsEnabled = false;
			List<dynamic> emotes = new List<dynamic>();

            var assetInfo = await services.assets.MultiGetInfoById(wornAssets);
			if (FeatureFlags.IsEnabled(FeatureFlag.GearsEnabled))
			{
				if (placeId.HasValue)
				{
					gearsEnabled = await services.games.AreGearsEnabled(placeId.Value);
				}
			}
            dynamic bodyColors = new
            {
				// 2020+
                headColorId = avatar.headColorId,
                leftArmColorId = avatar.leftArmColorId,
                leftLegColorId = avatar.leftLegColorId,
                rightArmColorId = avatar.rightArmColorId,
                rightLegColorId = avatar.rightLegColorId,
                torsoColorId = avatar.torsoColorId,
				// 2018
                HeadColor = avatar.headColorId,
                LeftArmColor = avatar.leftArmColorId,
                LeftLegColor = avatar.leftLegColorId,
                RightArmColor = avatar.rightArmColorId,
                RightLegColor = avatar.rightLegColorId,
                TorsoColor = avatar.torsoColorId
            };
			dynamic scales = new { 
				height = scalesEntry.height / 100.0,
				Height = scalesEntry.height / 100.0,
				width = scalesEntry.width / 100.0,
				Width = scalesEntry.width / 100.0,
				head = scalesEntry.head / 100.0,
				Head = scalesEntry.head / 100.0,
				depth = 1,
				Depth = 1,
				proportion = scalesEntry.proportion / 100.0,
				Proportion = scalesEntry.proportion / 100.0,
				bodyType = scalesEntry.bodyType / 100.0,
				BodyType = scalesEntry.bodyType / 100.0
			};
			string AvatarType = avatarTypeEntry.isR15 ? "R15" : "R6";
			Dictionary<string, long> animationAssetIds = new Dictionary<string, long>();
			Dictionary<string, long> animations = new Dictionary<string, long>();
			int emotePos = 1;

			foreach (long assetId in wornAssets)
			{
				var catinfo = await services.assets.GetAssetCatalogInfo(assetId);

				if (catinfo.assetType == Type.Gear)
				{
					if (gearsEnabled)
					{
						equippedGearVersionIds.Add(catinfo.id);
					}
				}
				else
				{
					accessoryVersionIds.Add(catinfo.id);
				}

				switch (catinfo.assetType)
				{
					case Type.ClimbAnimation:
						animationAssetIds["climb"] = assetId;
						animations["climb"] = assetId;
						break;
					case Type.FallAnimation:
						animationAssetIds["fall"] = assetId;
						animations["fall"] = assetId;
						break;
					case Type.IdleAnimation:
						animationAssetIds["idle"] = assetId;
						// Only default R15 Idle works cause all the other ones just make the player have parkinsons
						//animations["idle"] = assetId;
						break;
					case Type.JumpAnimation:
						animationAssetIds["jump"] = assetId;
						animations["jump"] = assetId;
						break;
					case Type.RunAnimation:
						animationAssetIds["run"] = assetId;
						animations["run"] = assetId;
						break;
					case Type.SwimAnimation:
						animationAssetIds["swim"] = assetId;
						animations["swim"] = assetId;
						break;
					case Type.WalkAnimation:
						animationAssetIds["walk"] = assetId;
						animations["walk"] = assetId;
						break;
					case Type.EmoteAnimation:
						emotes.Add(new
						{
							assetId = assetId,
							assetName = catinfo.name,
							position = emotePos++
						});
                break;
				}
			}
/* 			if (userAgent != "Roblox/Win2020")
			{
				equippedGearVersionIds = new List<long>();
			} */
			var result = new
			{
				resolvedAvatarType = AvatarType,
				playerAvatarType = AvatarType,
				accessoryVersionIds,
				equippedGearVersionIds,
				assetAndAssetTypeIds = assetInfo.Select(c => new
				{
					assetId = c.id,
					assetTypeId = (int)c.assetType,
				}),
				backpackGearVersionIds = equippedGearVersionIds,
				animationAssetIds = animationAssetIds,
				animations = animations,
				scales,
				bodyColorsUrl = $"{Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId}",
				bodyColors,
				emotes
			};

			string jsonString = JsonConvert.SerializeObject(result);
			return Content(jsonString, "application/json");
		}
		
		[HttpGetBypass("v2/users/{userId:long}/groups/roles")]
        public async Task<RobloxCollection<dynamic>> GetUserGroupRoles(long userId)
        {
            var roles = await services.groups.GetAllRolesForUser(userId);
            var result = new List<dynamic>();
            foreach (var role in roles)
            {
                var groupDetails = await services.groups.GetGroupById(role.groupId);
                result.Add(new
                {
                    group = new
                    {
                        id = groupDetails.id,
                        name = groupDetails.name,
                        memberCount = groupDetails.memberCount,
                    },
                    role = role,
                });
            }
            if (await StaffFilter.IsStaff(userId))
            {
                result.Add(new
                {
                    group = new
                    {
                        id = 1200769,
                        name = "Administrator",
                        memberCount = 100,
                    },
                    role = new
                    {
                        id = 1,
                        name = "Admin",
                        rank = 100
                    }
                });
            }
            return new()
            {
                data = result,
            };
        }
		
	    [HttpGetBypass("universal-app-configuration/v1/behaviors/app-patch/content")]
        public dynamic AppPatch()
        {
            List<long> CanaryUserIds = new List<long>();
            return new 
            {
                SchemeVersion = "1",
                CanaryUserIds,
                CanaryPercentage = 0,
            };
        }
		
		[HttpGetBypass("v1/users/{userId}/friends/statuses")]
        public async Task<dynamic> MultiGetFriendshipStatus(string userIds)
        {
            dynamic ids = null; 
            try
            {
                ids = userIds.Split(",").Select(long.Parse).Distinct().ToList();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }

            if (ids.Count == 0 || ids.Count > 100)
                throw new BadRequestException();

            var data = await services.friends.MultiGetFriendshipStatus(safeUserSession.userId, ids);
            return new
            {
                data = data,
            };
        }

        [HttpGetBypass("v1/users/{userId:long}/friends")]
        public async Task<RobloxCollection<FriendEntry>> GetUserFriends(long userId)
        {
            var result = await services.friends.GetFriends(userId);
            return new RobloxCollection<FriendEntry>()
            {
                data = result,
            };
        }

        [HttpGetBypass("v1/user/friend-requests/count")]
        public async Task<dynamic> GetFriendRequestCount()
        {
            var result = await services.friends.GetFriendRequestCount(safeUserSession.userId);
            return new
            {
                count = result,
            };
        }

        [HttpGetBypass("v1/users/{userId}/friends/count")]
        public async Task<dynamic> GetFriendCount(long userId)
        {
            var result = await services.friends.CountFriends((long)userId);
            return new
            {
                count = result,
            };
        }
        [HttpGetBypass("v1/metadata")]
        public dynamic GetMetadata()
        {
            return new
            {
                isNearbyUpsellEnabled = false,
                isFriendsUserDataStoreCacheEnabled = false,
                userName = safeUserSession.username,
                displayName = safeUserSession.username,
            };
        }

        [HttpGetBypass("v1/my/friends/requests")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetMyFriendRequests(string? cursor, int limit)
        {
            if (limit is <= 0 or > 100) limit = 10;

            return await services.friends.GetFriendRequests(safeUserSession.userId, cursor, limit);
        }

        [HttpPostBypass("v1/users/{userIdToRequest}/request-friendship")]
        public async Task<dynamic> RequestFriendshipv1(long userIdToRequest)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == userIdToRequest)
                throw new BadRequestException(7, "The user cannot be friends with itself");
            await services.friends.RequestFriendship(safeUserSession.userId, userIdToRequest);
            
            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }

        [HttpPostBypass("v1/users/{userIdToAccept:long}/accept-friend-request")]
        public async Task AcceptFriendRequest(long userIdToAccept)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == userIdToAccept)
                throw new BadRequestException(7, "The user cannot be friends with itself");

            await services.friends.AcceptFriendRequest(safeUserSession.userId, userIdToAccept);
        }

        [HttpPostBypass("v1/users/{userIdToDecline:long}/decline-friend-request")]
        public async Task DeclineFriendRequestV1(long userIdToDecline)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeclineFriendRequest(safeUserSession.userId, userIdToDecline);
        }

        [HttpPostBypass("v1/users/{userIdToRemove:long}/unfriend")]
        public async Task UnfriendUser(long userIdToRemove)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeleteFriend(safeUserSession.userId, userIdToRemove);
        }

        [HttpPostBypass("v1/users/{userIdToFollow:long}/follow")]
        public async Task<dynamic> FollowUser(long userIdToFollow)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            if (userIdToFollow == safeUserSession.userId)
                throw new BadRequestException();
            await services.friends.FollowerUser(safeUserSession.userId, userIdToFollow);

            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }

        [HttpPostBypass("v1/users/{userIdToUnfollow:long}/unfollow")]
        public async Task DeleteFollowingV1(long userIdToUnfollow)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            await services.friends.DeleteFollowing(safeUserSession.userId, userIdToUnfollow);
        }

        [HttpGetBypass("v1/users/{userId:long}/followers/count")]
        public async Task<dynamic> CountFollowers(long userId)
        {
            var result = await services.friends.CountFollowers(userId);
            return new
            {
                count = result,
            };
        }
        
        [HttpGetBypass("v1/users/{userId:long}/followings/count")]
        public async Task<dynamic> CountFollowings(long userId)
        {
            var result = await services.friends.CountFollowings(userId);
            return new
            {
                count = result,
            };
        }

        [HttpGetBypass("v1/users/{userId:long}/followers")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetFollowers(long userId, int limit, string? cursor)
        {
            if (limit is > 100 or < 1) limit = 10;
            return await services.friends.GetFollowers(userId, cursor, limit);
        }

        [HttpGetBypass("v1/users/{userId:long}/followings")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetFollowings(long userId, int limit, string? cursor)
        {
            if (limit is > 100 or < 1) limit = 10;
            return await services.friends.GetFollowings(userId, cursor, limit);
        }

        [HttpPostBypass("v1/user/following-exists")]
        public async Task<dynamic> FollowingExists([Required,MVC.FromBody] FollowingExistsRequest request)
        {
            var result = new List<dynamic>();

            foreach (var userId in request.targetUserIds)
            {
                if (userSession is null)
                {
                    result.Add(new
                    {
                        isFollowing = false,
                        userId,
                    });
                    continue;
                }
                
                var isFollowing = await services.friends.IsOneFollowingTwo(userSession.userId, userId);
                result.Add(new
                {
                    isFollowing,
                    userId,
                });
            }
            
            return new
            {
                followings = result,
            };
        }

        [HttpGetBypass("universal-app-configuration/v1/behaviors/app-policy/content")]
        public dynamic AppPolicy()
        {
            string policyContent = System.IO.File.ReadAllText(Configuration.JsonDataDirectory + "AppPolicy.json");
            dynamic? policyJson = JsonConvert.DeserializeObject<ExpandoObject>(policyContent);
            return policyJson ?? "";
        }
		
	   [HttpGetBypass("v1/user/{userId:long}/is-admin-developer-console-enabled")]
        public async Task<dynamic> NewCanManage(long userId)
        {
            long placeId = long.Parse(Request.Headers["roblox-place-id"].ToString());
            bool canManagePlace = await services.assets.CanUserModifyItem(placeId, userId);
            bool isOwner =  StaffFilter.IsOwner(userId);
            return new 
            {
                isAdminDeveloperConsoleEnabled = (canManagePlace || isOwner)
            };
        }
        [HttpGetBypass("universes/get-universe-places")]
        public async Task<dynamic> GetPlaces(long universeId)
        {
            var place = await services.games.GetRootPlaceId(universeId);
            var placeInfo = await services.assets.GetAssetCatalogInfo(place);
            return new
            {
                FinalPage = true,
                RootPlace = place,
                Places = new
                {
                    PlaceId = place,
                    Name = placeInfo.name,
                },
                PageSize = 50
            };
        }
		
		[HttpGetBypass("/currency/balance")]
        public async Task<dynamic> GetBalance()
        {
            return await services.economy.GetBalance(CreatorType.User, safeUserSession.userId);
        }

        [HttpGetBypass("/ownership/hasasset")]
        public async Task<string> DoesOwnAsset(long userId, long assetId)
        {
            return (await services.users.GetUserAssets(userId, assetId)).Any() ? "true" : "false";
        }
		

        [HttpGetBypass("user/follow")]
        [HttpPostBypass("user/follow")]
        public async Task<dynamic> FollowUserV1(long followedUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            if (followedUserId == safeUserSession.userId)
                throw new BadRequestException();
            await services.friends.FollowerUser(safeUserSession.userId, followedUserId);

            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }
		
        [HttpGetBypass("users/get-by-username")]
        public async Task<dynamic> GetByUsername(string username)
        {
            var userInfo = await services.users.GetUserByName(username);
            var onlineStatus = (await services.users.MultiGetPresence(new[] {userInfo.userId})).First();
            return new 
            {
                Id = userInfo.userId,
                Username = username,
                AvatarUri = "null",
                AvatarFinal = false,
                IsOnline = onlineStatus.userPresenceType,
            };
        }
		
        [HttpGetBypass("users/account-info")]
        [HttpPostBypass("users/account-info")]
        public async Task<dynamic> accountInfo()
        {

            var roles = new string[] { };
            if (userSession == null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new
                {
                    success = false,
                    message = "Unauthorized"
                };
            }
            var userBalance = await services.economy.GetUserBalance(userSession.userId);
            var jsonData = new
            {
                UserId =  userSession.userId,
                Username = userSession.username,
                DisplayName = userSession.username,
                HasPasswordSet = true,
                Email = "Higu@higu.ca",
                MembershipType = 3,
                RobuxBalance = userBalance.robux,
                AgeBracket = 0,
                Roles = roles,
                EmailNotificationEnabled = false,
                PasswordNotifcationEnabled = false,
            };
            string jsonString = JsonConvert.SerializeObject(jsonData);
            return Content(jsonString, "application/json");
        }
		
        [HttpPostBypass("user/following-exists")]
        [HttpGetBypass("user/following-exists")]
        public async Task<dynamic> FollowingExists(long userId, long followerUserId)
        {
            var result = new List<dynamic>();
                if (userSession is null)
                {
                    result.Add(new
                    {
                        isFollowing = false,
                        userId,
                    });
                }
                
                var isFollowing = await services.friends.IsOneFollowingTwo(safeUserSession.userId, followerUserId);
                result.Add(new
                {
                    isFollowing,
                    userId,
                });
            
            return new
            {
                followings = result,
            };
        }
		
        [HttpPostBypass("user/unfollow")]
        public async Task DeleteFollowing(long followedUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            await services.friends.DeleteFollowing(safeUserSession.userId, followedUserId);
        }
		
        [HttpPostBypass("user/decline-friend-request")]
        public async Task DeclineFriendRequest(long requesterUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeclineFriendRequest(safeUserSession.userId, requesterUserId);
        }
		
        [HttpGetBypass("user/request-friendship")]
        [HttpPostBypass("user/request-friendship")]
        public async Task<dynamic> RequestFriendship(long recipientUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == recipientUserId)
                throw new BadRequestException(7, "The user cannot be friends with itself");
            await services.friends.RequestFriendship(safeUserSession.userId, recipientUserId);
            
            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }
		
        [HttpGetBypass("user/get-friendship-count")]
        public async Task<dynamic> GetFriendsAmount(long? userId)
        {
            if(userId == null)
            {
                userId = safeUserSession.userId;
            }
            int amountFriends = await services.friends.CountFriends((long)userId);
            return new 
            {
                success = true,
                message = "Success",
                count = amountFriends
            };
        }
		
        [HttpPostBypass("game/load-place-info")]
        public async Task<dynamic> LoadPlaceInfo()
        {
            var placeId = Request.Headers["roblox-place-id"];
            long.TryParse(placeId, out long assetId);
            var details = await services.assets.GetAssetCatalogInfo(assetId);
            var jsonData = new
            {
                CreatorId =  details.creatorTargetId,
                CreatorType = "User",
                PlaceVersion = details.id,
                GameId = assetId,
                IsRobloxPlace = details.creatorTargetId == 1
            };
            string jsonString = JsonConvert.SerializeObject(jsonData);
            return Content(jsonString, "application/json");
        }
		
		[HttpPostBypass("/v1.0/SequenceStatistics/AddToSequence")]
        [HttpPostBypass("/v1.1/Counters/Increment")]
        [HttpPostBypass("/v1.0/SequenceStatistics/BatchAddToSequencesV2")]
        [HttpPostBypass("v1.0/MultiIncrement")]
        [HttpPostBypass("/game/report-stats")]
        [HttpGetBypass("usercheck/show-tos")]
        [HttpGetBypass("/v1.1/Counters/Increment")]
        [HttpGetBypass("notifications/signalr/negotiate")]
        [HttpGetBypass("notifications/negotiate")]
        [HttpPostBypass("v1.1/Counters/BatchIncrement")]
        [HttpGetBypass("v1.1/Counters/BatchIncrement")]
        public MVC.OkResult TelemetryFunctions()
        {
            return Ok();
        }
		
        [HttpPostBypass("userblock/getblockedusers")]
        [HttpGetBypass("userblock/getblockedusers")]
        public MVC.OkResult GetBlocked()
        {
            return Ok();
        }
		
		public class BatchAssetRequest
		{
			public long assetId { get; set; }
			public string assetType { get; set; }
			public string requestId { get; set; }
		}
		
		[HttpPostBypass("asset/batch")]
        [HttpPostBypass("v1/assets/batch")]
        public async Task<MVC.IActionResult> AssetBatch()
        {
            List<BatchAssetRequest> requestData;
            bool isGzip = Request.Headers["Content-Encoding"].ToString() == "gzip";
            
            if (isGzip)
            {
                using (var decompressedStream = new MemoryStream())
                {
                    using (var requestStream = Request.Body)
                    {
                        using (var gzipStream = new GZipStream(requestStream, CompressionMode.Decompress))
                        {
                            await gzipStream.CopyToAsync(decompressedStream);
                        }
                    }
                    decompressedStream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();
                        Console.WriteLine(json);
                        requestData = JsonSerializer.Deserialize<List<BatchAssetRequest>>(json);
                    }
                }
            }
            else
            {
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    var json = await reader.ReadToEndAsync();
                    Console.WriteLine(json);
                    requestData = JsonSerializer.Deserialize<List<BatchAssetRequest>>(json);
                }
            }
            if (requestData == null)
            {
                throw new BadRequestException();
            }
            var assetReturnInfo = new List<object>();
            foreach (var request in requestData)
            {
                Console.WriteLine(request.assetId);
                assetReturnInfo.Add(new
                {
                    Location = $"{Configuration.BaseUrl}/v1/asset?id={request.assetId}",
                    RequestId = request.requestId,
                    IsHashDynamic = true,
                    IsCopyrightProtected = true, 
                    IsArchived = false,
                });
            }

            return Content(JsonSerializer.Serialize(assetReturnInfo), "application/json");
        }
		
		[HttpGetBypass("/universes/validate-place-join")]
		public async Task<string> ValidatePlaceJoin()
		{
			return "true";
		}
		
		// server log endpoint
		[HttpGetBypass("users/{userId}/canmanage/{assetId}")]
		public async Task<dynamic> CanManagePlace(long userId, long assetId)
		{
			try
			{
				var userInfo = await services.users.GetUserById(userId);
				if (userInfo == null)
				{
					return new
					{
						Success = false,
						CanManage = false
					};
				}

				var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);

				var canManage = await services.assets.CanUserModifyItem(assetId, userId);
				
				return new 
				{
					Success = true,
					CanManage = canManage
				};
			}
			catch (RecordNotFoundException)
			{
				return new
				{
					Success = false,
					CanManage = false
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine($"error in CanManage: {ex}");
				return new
				{
					Success = false,
					CanManage = false
				};
			}
		}

		[HttpGetBypass("Asset/CharacterFetch.ashx")]
		public async Task<string> CharacterFetch(long userId, long placeId)
		{
			var assets = (await services.avatar.GetWornAssets(userId)).ToList();
			
			// filter out gears if the FFlag is disabled
			if (!FeatureFlags.IsEnabled(FeatureFlag.GearsEnabled))
			{
				return await FilterOutGears(assets, userId);
			}
			
			// if game has gears enabled, then include them and if not then it filters them out
			var gearsEnabled = await services.games.AreGearsEnabled(placeId);
			if (!gearsEnabled)
			{
				return await FilterOutGears(assets, userId);
			}
			
			return $"{Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId};{string.Join(";", assets.Select(c => Configuration.BaseUrl + "/Asset/?id=" + c))}";
		}

		private async Task<string> FilterOutGears(List<long> assets, long userId)
		{
			var filtered = new List<long>();
			foreach (var assetId in assets)
			{
				try 
				{
					var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);
					if (assetInfo.assetType != Type.Gear)
					{
						filtered.Add(assetId);
					}
				}
				catch
				{
					// if we can't get asset info for some reason, just include it anyway
					filtered.Add(assetId);
				}
			}
			return $"{Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId};{string.Join(";", filtered.Select(c => Configuration.BaseUrl + "/Asset/?id=" + c))}";
		}

		private void CheckServerAuth(string auth)
		{
			// TODO: make this configurable!!!!!!!
		    string expected = Roblox.Configuration.GameServerAuthorization;
			
			/* Console.WriteLine($"[DEBUG] got auth: {auth}");

			Console.WriteLine($"[DEBUG] expected auth: {expected}");
			*/

			if (auth != expected)
			{
				string url = HttpContext.Request.GetEncodedUrl();
				string ip = GetRequesterIpRaw(HttpContext);

				Console.WriteLine($"[INFO] auth failed: {url}");

				Roblox.Metrics.GameMetrics.ReportRccAuthorizationFailure(url, auth, ip);
				
				Console.WriteLine($"[ERROR] auth failed");

				throw new BadRequestException();
			}

			//Console.WriteLine($"[INFO] auth success");
		}

        [HttpPostBypass("/gs/activity")]
        public async Task<dynamic> GetGsActivity([Required, MVC.FromBody] ReportActivity request)
        {
            Console.WriteLine(request.authorization);

            CheckServerAuth(request.authorization);
            var Result = await services.gameServer.GetLastServerPing(request.serverId);
            return new
            {
                isAlive = Result >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1)),
                updatedAt = Result,
            };
        }

        [HttpPostBypass("/gs/ping")]
        public async Task ReportServerActivity([Required, MVC.FromBody] ReportActivity request)
        {
            CheckServerAuth(request.authorization);
            await services.gameServer.SetServerPing(request.serverId);
        }

        [HttpPostBypass("/gs/delete")]
        public async Task DeleteServer([Required, MVC.FromBody] ReportActivity request)
        {
            CheckServerAuth(request.authorization);
            await services.gameServer.DeleteGameServer(request.serverId);
        }

		[HttpPostBypass("/gs/shutdown")]
		public async Task ShutDownServer([Required, MVC.FromBody] ReportActivity request)
		{
			CheckServerAuth(request.authorization);
			
			if (services.gameServer.IsServerShutDown(request.serverId))
			{
				return;
			}
			
			services.gameServer.ShutDownServer(request.serverId);
		}

		[HttpPostBypass("/gs/players/report")]
		public async Task ReportPlayerActivity([Required, MVC.FromBody] ReportPlayerActivity request)
		{
			CheckServerAuth(request.authorization);
			
			if (request.eventType == "Leave")
			{
				await services.gameServer.OnPlayerLeave(request.userId, request.placeId, request.serverId);
				
				try 
				{
					var wh = Roblox.Configuration.Webhook;
					if (!string.IsNullOrEmpty(wh))
					{
						var userInfo = await services.users.GetUserById(request.userId);
						var message = $"player {userInfo.username} (ID: {request.userId}) left place {request.placeId} on server {request.serverId}";
						
						using (var httpClient = new HttpClient())
						{
							var payload = new { content = message };
							var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
							await httpClient.PostAsync(wh, content);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error sending leave to Discord: {ex.Message}");
				}
			}
			else if (request.eventType == "Join")
			{
				await Roblox.Metrics.GameMetrics.ReportGameJoinSuccess(request.placeId);
				await services.gameServer.OnPlayerJoin(request.userId, request.placeId, request.serverId);

				try 
				{
					var wh = Roblox.Configuration.Webhook;
					if (!string.IsNullOrEmpty(wh))
					{
						var userInfo = await services.users.GetUserById(request.userId);
						var message = $"player {userInfo.username} (ID: {request.userId}) joined place {request.placeId} on server {request.serverId}";
						
						using (var httpClient = new HttpClient())
						{
							var payload = new { content = message };
							var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
							await httpClient.PostAsync(wh, content);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error sending join to Discord: {ex.Message}");
				}
			}
			else
			{
				throw new Exception("unexpected type " + request.eventType);
			}
		}

        [HttpPostBypass("/gs/a")]
        public void ReportGS()
        {
            // Doesn't do anything yet. See: services/api/src/controllers/bypass.ts:1473
            return;
        }

        [HttpPostBypass("/Game/ValidateTicket.ashx")]
        public async Task<string> ValidateClientTicketRcc([Required, MVC.FromBody] ValidateTicketRequest request)
        {
#if DEBUG
            return "true";
#endif
            
            try
            {
                // Below is intentionally caught by local try/catch. RCC could crash if we give a 500 error.
                FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled, FeatureFlag.GameJoinEnabled);
                var ticketData = services.gameServer.DecodeTicket(request.ticket, null);
                if (ticketData.userId != request.expectedUserId)
                {
                    // Either bug or someone broke into RCC
                    Roblox.Metrics.GameMetrics.ReportTicketErrorUserIdNotMatchingTicket(request.ticket,
                        ticketData.userId, request.expectedUserId);
                    throw new Exception("Ticket userId does not match expected userId");
                }
                // From TS: it is possible for a client to spoof username or appearance to be empty string, 
                // so make sure you don't do much validation on those params (aside from assertion that it's a string)
                if (request.expectedUsername != null)
                {
                    var userInfo = await services.users.GetUserById(ticketData.userId);
                    if (userInfo.username != request.expectedUsername)
                    {
                        throw new Exception("Ticket username does not match expected username");
                    }
                }

                if (request.expectedAppearanceUrl != null)
                {
                    // will always be format of "http://localhost/Asset/CharacterFetch.ashx?userId=12", NO EXCEPTIONS!
                    var expectedUrl =
                        $"{Roblox.Configuration.BaseUrl}/Asset/CharacterFetch.ashx?userId={ticketData.userId}";
                    if (request.expectedAppearanceUrl != expectedUrl)
                    {
                        throw new Exception("Character URL is bad");
                    }
                }
                
                // Confirm user isn't already in a game
                var gameStatus = (await services.users.MultiGetPresence(new [] {ticketData.userId})).First();
                if (gameStatus.placeId != null && gameStatus.userPresenceType == PresenceType.InGame)
                {
                    // Make sure that the only game they are playing is the one they are trying to join.
                    var playingGames = await services.gameServer.GetGamesUserIsPlaying(ticketData.userId);
                    foreach (var game in playingGames)
                    {
                        if (game.id != request.gameJobId)
                            throw new Exception("User is already playing another game");
                    }
                }

                return "true";
            }
            catch (Exception e)
            {
                Console.WriteLine("[error] Verify ticket failed. Error = {0}\n{1}", e.Message, e.StackTrace);
                return "false";
            }
        }

        [HttpPostBypass("/game/validate-machine")]
        public dynamic ValidateMachine()
        {
            return new
            {
                success = true,
                message = "",
            };
        }

        [HttpGetBypass("Users/ListStaff.ashx")]
        public async Task<IEnumerable<long>> GetStaffList()
        {
            return (await StaffFilter.GetStaff()).Where(c => c != 12);
        }

        [HttpGetBypass("Users/GetBanStatus.ashx")]
        public async Task<IEnumerable<dynamic>> MultiGetBanStatus(string userIds)
        {

            var ids = userIds.Split(",").Select(long.Parse).Distinct();
            var Result = new List<dynamic>();
#if DEBUG
            return ids.Select(c => new
            {
                userId = c,
                isBanned = false,
            });
#else
            var multiGetResult = await services.users.MultiGetAccountStatus(ids);
            foreach (var user in multiGetResult)
            {
                Result.Add(new
                {
                    userId = user.userId,
                    isBanned = user.accountStatus != AccountStatus.Ok,
                });
            }

            return Result;
#endif
        }

        [HttpGetBypass("Asset/BodyColors.ashx")]
        public async Task<string> GetBodyColors(long userId)
        {
            var colors = await services.avatar.GetAvatar(userId);

            var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

            var robloxRoot = new XElement("roblox",
                new XAttribute(XNamespace.Xmlns + "xmime", "http://www.w3.org/2005/05/xmlmime"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(xsi + "noNamespaceSchemaLocation", "http://www.roblox.com/roblox.xsd"),
                new XAttribute("version", 4)
            );
            robloxRoot.Add(new XElement("External", "null"));
            robloxRoot.Add(new XElement("External", "nil"));
            var items = new XElement("Item", new XAttribute("class", "BodyColors"));
            var properties = new XElement("Properties");
            // set colors
            properties.Add(new XElement("int", new XAttribute("name", "HeadColor"), colors.headColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "LeftArmColor"), colors.leftArmColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "LeftLegColor"), colors.leftLegColorId.ToString()));
            properties.Add(new XElement("string", new XAttribute("name", "Name"), "Body Colors"));
            properties.Add(new XElement("int", new XAttribute("name", "RightArmColor"), colors.rightArmColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "RightLegColor"), colors.rightLegColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "TorsoColor"), colors.torsoColorId.ToString()));
            properties.Add(new XElement("bool", new XAttribute("name", "archivable"), "true"));
            // add
            items.Add(properties);
            robloxRoot.Add(items);
            // return as string
            return new XDocument(robloxRoot).ToString();
        }

        private void ValidateBotAuthorization()
        {
#if DEBUG == false
	        if (Request.Headers["bot-auth"].ToString() != Roblox.Configuration.BotAuthorization)
	        {
		        throw new Exception("Internal");
	        }
#endif
        }

        [HttpGetBypass("botapi/migrate-alltypes")]
        public async Task<dynamic> MigrateAllItemsBot([Required, MVC.FromQuery] string url)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(url, false, null, new List<Type>()
            {
                Type.Image,
                Type.Audio,
                Type.Mesh,
                Type.Lua,
                Type.Model,
                Type.Decal,
                Type.Animation,
                Type.SolidModel,
                Type.MeshPart,
                Type.ClimbAnimation,
                Type.DeathAnimation,
                Type.FallAnimation,
                Type.IdleAnimation,
                Type.JumpAnimation,
                Type.RunAnimation,
                Type.SwimAnimation,
                Type.WalkAnimation,
                Type.PoseAnimation,
            }, default, false);
        }

        [HttpGetBypass("botapi/migrate-clothing")]
        public async Task<dynamic> MigrateClothingBot([Required] string assetId)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(assetId, true, 5, new List<Models.Assets.Type>() { Models.Assets.Type.TeeShirt, Models.Assets.Type.Shirt, Models.Assets.Type.Pants });
        }
        
        [HttpGetBypass("BuildersClub/Upgrade.ashx")]
        public MVC.IActionResult UpgradeNow()
        {
            return new MVC.RedirectResult("/buildersclub");
        }
		
		[HttpPostBypass("buildersclub/membership")]
		public async Task<dynamic> UpdateMembership([Required, MVC.FromForm] string membershipType)
		{
			if (userSession == null)
				throw new RobloxException(401, 0, "Not authenticated");

			if (!Enum.TryParse<MembershipType>(membershipType, out var membershipTypeParsed) || 
				!Enum.IsDefined(membershipTypeParsed))
			{
				throw new RobloxException(400, 0, "Invalid membership type");
			}

			await services.users.InsertOrUpdateMembership(userSession.userId, membershipTypeParsed);
			var metadata = MembershipMetadata.GetMetadata(membershipTypeParsed);
			
			return new
			{
				success = true,
				message = $"Membership successfully changed to {metadata.displayName}. You will now receive {metadata.dailyRobux} Robux each day.",
				newMembership = membershipTypeParsed.ToString()
			};
		}
        
		[HttpGetBypass("Setting/QuietGet/{type}")]
		public MVC.ActionResult<dynamic> GetAppSettings(string type, [MVC.FromQuery] string? apiKey = "")
		{
			try
			{
				if (!Configuration.AllowedQuietGetJson.Any(x => x.Equals(type, StringComparison.OrdinalIgnoreCase)))
				{
					Console.WriteLine($"[RetrieveClientFFlags] disallowed JSON trying to be requested!");
					return "Go away";
				}

				bool use2015 = apiKey.Equals("2015MRCC-2015-2015-2015-2015MidRCC15", StringComparison.OrdinalIgnoreCase);
				
				string fileName = type;
				if (use2015)
				{
					fileName = type + "2015";
					Console.WriteLine($"[RetrieveClientFFlags] Using 2015 flags for: {type}");
				}

				string jsonFilePath = Path.Combine(Configuration.JsonDataDirectory, fileName + ".json");
				
				if (use2015 && !System.IO.File.Exists(jsonFilePath))
				{
					Console.WriteLine($"[RetrieveClientFFlags] 2015 flasg not found, falling back");
					jsonFilePath = Path.Combine(Configuration.JsonDataDirectory, type + ".json");
				}

				string jsonContent = System.IO.File.ReadAllText(jsonFilePath);
				dynamic? clientAppSettingsData = JsonConvert.DeserializeObject<ExpandoObject>(jsonContent);

				return clientAppSettingsData ?? "";
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[RetrieveClientFFlags] could not get FFlags: {ex.Message}");
				return new {};
			}
		}
		
		[HttpGet("logout")]
        public MVC.IActionResult Logout()
        {
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }
            return Redirect("/");
        }

        [HttpGetBypass("abusereport/UserProfile"), HttpGetBypass("abusereport/asset"), HttpGetBypass("abusereport/user"), HttpGetBypass("abusereport/users")]
        public MVC.IActionResult ReportAbuseRedirect()
        {
            return new MVC.RedirectResult("/internal/report-abuse");
        }

        [HttpGetBypass("/my/economy-status")]
        public dynamic GetEconomyStatus()
        {
            return new
            {
                isMarketplaceEnabled = true,
                isMarketplaceEnabledForAuthenticatedUser = true,
                isMarketplaceEnabledForUser = true,
                isMarketplaceEnabledForGroup = true,
            };
        }

        [HttpPostBypass("persistence/increment")]
        public async Task<dynamic> IncrementPersistence(long placeId, string key, string type, string scope, string target, int value)
        {
            // increment?placeId=%i&key=%s&type=%s&scope=%s&target=&value=%i
            
            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            
            return new
            {
                data = (object?) null,
            };
        }

        [HttpPostBypass("persistence/getSortedValues")]
        public async Task<dynamic> GetSortedPersistenceValues(long placeId, string type, string scope, string key, int pageSize, bool ascending, int inclusiveMinValue = 0, int inclusiveMaxValue = 0)
        {
            // persistence/getSortedValues?placeId=0&type=sorted&scope=global&key=Level%5FHighscoResponse20&pageSize=10&ascending=False"
            // persistence/set?placeId=124921244&key=BF2%5Fds%5Ftest&&type=standard&scope=global&target=BF2%5Fds%5Fkey%5Ftmp&valueLength=31
            
            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            
            return new
            {
                data = new
                {
                    Entries = ArraySegment<int>.Empty,
                    ExclusiveStartKey = (string?)null,
                },
            };
        }

        [HttpPostBypass("persistence/getv2")]
        public async Task<dynamic> GetPersistenceV2(long placeId, string type, string scope)
        {
            var rawBody = await new StreamReader(Request.Body).ReadToEndAsync();
            if (rawBody.StartsWith("&"))
            {
                rawBody = rawBody.Substring(1);
            }
            // getV2?placeId=%i&type=%s&scope=%s
            // Expected format is:
            //	{ "data" : 
            //		[
            //			{	"Value" : value,
            //				"Scope" : scope,							
            //				"Key" : key,
            //				"Target" : target
            //			}
            //		]
            //	}
            // or for non-existing key:
            // { "data": [] }
            
            // for no sub key:
            // Expected format is:
            //	{ "data" : value }
            Console.WriteLine("Request = {0}", rawBody);
            using var ds = ServiceProvider.GetOrCreate<DataStoreService>();
            var requests = rawBody.Split("\n").Where(c => !string.IsNullOrWhiteSpace(c)).Distinct();
            
            var Result = new List<GetKeyEntry>();
            foreach (var request in requests)
            {
                var des = JsonSerializer.Deserialize<GetKeyScope>(request);
                
                var Response = await ds.Get(placeId, type, des.scope ?? scope, des.key, des.target);
                if (!string.IsNullOrWhiteSpace(Response))
                    Result.Add(new GetKeyEntry()
                    {
                        Key = des.key,
                        Scope = des.scope ?? scope,
                        Target =des.target,
                        Value = Response,
                    });
            }

            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            
            return new
            {
                data = Result,
            };
        }

        [HttpPostBypass("persistence/set")]
        public async Task<dynamic> Set(long placeId, string key, string type, string scope, string target, int valueLength, [Required, MVC.FromBody] SetRequest request)
        {
            // { "data" : value }
            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            await ServiceProvider.GetOrCreate<DataStoreService>()
                .Set(placeId, target, type, scope, key, valueLength, request.data);
            
            return new
            {
                data = request.data,
            };
        }
		
		private static string FormatTimeSpan(TimeSpan span)
		{
			if (span.TotalDays >= 1)
				return $"{(int)span.TotalDays} day{(span.TotalDays >= 2 ? "s" : "")}";
			if (span.TotalHours >= 1)
				return $"{(int)span.TotalHours} hour{(span.TotalHours >= 2 ? "s" : "")}";
			if (span.TotalMinutes >= 1)
				return $"{(int)span.TotalMinutes} minute{(span.TotalMinutes >= 2 ? "s" : "")}";
			return $"{(int)span.TotalSeconds} second{(span.TotalSeconds >= 2 ? "s" : "")}";
		}
		
		[HttpGetBypass("promocodes/redeem")]
		public async Task<dynamic> RedeemPromoCode(
			[Required] string code,
			[MVC.FromServices] NpgsqlConnection db)
		{
			if (userSession == null)
			{
				throw new RobloxException(401, 0, "Not authenticated");
			}

			code = code.Trim().ToUpper();
			var userId = userSession.userId;

			PromoCodeEntry promocode;
			try
			{
				promocode = await db.QuerySingleOrDefaultAsync<PromoCodeEntry>(
					"SELECT * FROM promocodes WHERE code = @code",
					new { code });

				if (promocode == null)
				{
					throw new RobloxException(400, 0, "This promocode does not exist");
				}

				var currentUtcTime = DateTime.UtcNow;
				var ExpiresAtUtc = promocode.expires_at.HasValue 
					? DateTime.SpecifyKind(promocode.expires_at.Value, DateTimeKind.Utc)
					: (DateTime?)null;

				if (!promocode.active)
				{
					throw new RobloxException(400, 0, "This promocode is no longer active");
				}

				if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < currentUtcTime)
				{
					var timeSinceExpired = currentUtcTime - ExpiresAtUtc.Value;
					throw new RobloxException(400, 0, 
						/* $"This promocode expired {FormatTimeSpan(timeSinceExpired)} ago"); */
						"This promocode has expired");
				}

				if (promocode.expires_at.HasValue && promocode.expires_at.Value < DateTime.UtcNow)
				{
					throw new RobloxException(400, 0, "This promocode has expired");
				}

				if (promocode.uses >= promocode.maxuses)
				{
					throw new RobloxException(400, 0, "This promocode has reached it's maximum redemptions");
				}

				var hasRedeemed = await db.ExecuteScalarAsync<int>(
					"SELECT COUNT(*) FROM promocode_redemptions WHERE user_id = @userId AND promocode = @promocodeID",
					new { userId, promocodeID = promocode.id }) > 0;

				if (hasRedeemed)
				{
					throw new RobloxException(400, 0, "You have already redeemed this promocode");
				}

				using var transaction = await db.BeginTransactionAsync();
				try
				{
					await db.ExecuteAsync(
						"UPDATE promocodes SET uses = uses + 1 WHERE id = @id",
						new { id = promocode.id },
						transaction);

					await db.ExecuteAsync(
						"INSERT INTO promocode_redemptions (promocode, user_id, asset_id, robux) " +
						"VALUES (@promocodeID, @userId, @assetId, @robux)",
						new
						{
							promocodeID = promocode.id,
							userId,
							assetId = promocode.asset_id,
							robux = promocode.robux
						},
						transaction);

					if (promocode.asset_id.HasValue)
					{
						await db.ExecuteAsync(
							"INSERT INTO user_asset (user_id, asset_id) VALUES (@userId, @assetId)",
							new { userId, assetId = promocode.asset_id.Value },
							transaction);
					}

					if (promocode.robux.HasValue && promocode.robux.Value > 0)
					{
						await db.ExecuteAsync(
							"UPDATE user_economy SET balance_robux = balance_robux + @amount WHERE user_id = @userId",
							new { userId, amount = promocode.robux.Value },
							transaction);
					}

					await transaction.CommitAsync();
				}
				catch
				{
					await transaction.RollbackAsync();
					throw;
				}

				string name = null;
				if (promocode.asset_id.HasValue)
				{
					try
					{
						var assetInfo = await services.assets.GetAssetCatalogInfo(promocode.asset_id.Value);
						name = assetInfo.name;
					}
					catch { /* ignore */ }
				}

				return new
				{
					success = true,
					message = "Promocode redeemed successfully!",
					assetId = promocode.asset_id,
					name,
					robux = promocode.robux
				};
			}
			finally
			{
				// idk man
			}
		}
    }
}
