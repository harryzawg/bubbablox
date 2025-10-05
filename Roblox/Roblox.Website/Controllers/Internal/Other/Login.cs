using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Middleware;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Services.Exceptions;
using Roblox.Website.WebsiteModels.Discord;
using Npgsql;
using Dapper;
using Roblox.Dto.Users;
using HttpGetBypassUserCheck = Roblox.Website.Controllers.HttpGetBypassAttribute;

// Everything login/signup here
namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class LoginSignup : ControllerBase 
    {		
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
				// what do we do here
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
				
			bool Is2FAEnabled = await services.twoFactor.IsEnabled(userId);
			if (Is2FAEnabled)
			{
				var TempAuth = GenerateTemp2FAToken(userId);
				var TempAuthExp = DateTimeOffset.UtcNow.AddMinutes(15);
					
				Response.Cookies.Append("2FACode", TempAuth, new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Lax,
					Expires = TempAuthExp
				});

				return Redirect("/login/2fa");
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
			Response.Cookies.Delete("2FACode");

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
		
		private static readonly Dictionary<string, long> Temp2FATokens = new Dictionary<string, long>();
		private static readonly object Temp2FALock = new object();
		
		private string GenerateTemp2FAToken(long userId)
		{
			var token = Guid.NewGuid().ToString();

			lock (Temp2FALock)
			{
				Temp2FATokens[token] = userId;
			}

			_ = Task.Run(async () =>
			{
				await Task.Delay(TimeSpan.FromMinutes(15));
				lock (Temp2FALock)
				{
					Temp2FATokens.Remove(token);
				}
			});

			return token;
		}
		
		// stupid? yes
		[HttpGet("login/2fa")]
		public IActionResult TwoFAPage()
		{
			var HTML = Path.Combine(Configuration.PublicDirectory, "Data", "2FA.html");
			if (!System.IO.File.Exists(HTML))
				return NotFound();

			var content = System.IO.File.ReadAllText(HTML);
			// FUCK csp.
			var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
			content = content.Replace("{{NONCE}}", nonce);

			Response.Headers["Content-Security-Policy"] =
				$"script-src 'self' 'nonce-{nonce}'";

			return Content(content, "text/html");
		}

		[HttpPost("login/2fa")]
		public async Task<IActionResult> TwoFactorLogin([FromForm] string code)
		{
			var token = Request.Cookies["2FACode"];
			
			if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(code))
				return Redirect("/login/2fa?err=Bad+token+or+code");

			long userId;
			lock (Temp2FALock)
			{
				if (!Temp2FATokens.TryGetValue(token, out userId))
				{
					return Redirect("/");
				}
			}

			bool Valid = await services.twoFactor.VerifyCode(userId, code);
			if (!Valid)
			{
				return Redirect("/login/2fa?err=Bad/expired+2FA+code,+please+try+again!");
			}

			lock (Temp2FALock)
			{
				Temp2FATokens.Remove(token);
			}

			HttpContext.Response.Cookies.Append(
				"2FACode",
				"",
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.None,
					// expire, cause the login was successful
					Expires = DateTimeOffset.UnixEpoch
				});

			var sess = await services.users.CreateSession(userId);
			var sessCookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry
			{
				sessionId = sess,
				createdAt = DateTimeOffset.Now.ToUnixTimeSeconds()
			});

			HttpContext.Response.Cookies.Append(
				".ROBLOSECURITY",
				sessCookie,
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

				var Token = Guid.NewGuid().ToString();
				var expiry = DateTime.UtcNow.AddHours(1);
				
				await services.users.CreatePasswordResetToken(userId, Token, expiry);

				var encryptedToken = EncryptWithKey($"{userId}|{Token}|{expiry:o}", Roblox.Configuration.DiscordKey);
				
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

				var decrypted = DecryptWithKey(cookie, Roblox.Configuration.DiscordKey);
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

				var Torken = parts[1];
				var isValid = await services.users.ValidatePasswordResetToken(userId, Torken);
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

				await services.users.DeleteResetPassword(userId, Torken);
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
		
		[HttpGetBypass("login-with-discord")]
		public async Task<MVC.IActionResult> DiscordLoginRedir()
		{
			var ID = Roblox.Configuration.DiscordClientID;
			var Redirect = Uri.EscapeDataString(Roblox.Configuration.DiscordLoginRedirect);
			var scope = Uri.EscapeDataString("identify");
			
			return new MVC.RedirectResult(
				$"https://discord.com/api/oauth2/authorize?client_id={ID}&redirect_uri={Redirect}&response_type=code&scope={scope}"
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
				var discordUser = await discordinfo.Content.ReadFromJsonAsync<DiscordUser>();

				long ID;
				// THIS IS TESTING. Remove in the future plz
				if (discordUser.id == "713175126358884483")
				{
					ID = 1;
				}
				else
				{
					ID = await services.users.GetUserIdFromDiscordId(discordUser.id);
					if (ID == 0)
					{
						return Redirect("/?loginmsg=There is no account linked to this Discord");
					}
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
		
		[HttpGetBypass("discordverify")]
		public async Task<MVC.IActionResult> DiscordURL()
		{
			var ID = Roblox.Configuration.DiscordClientID;
			var Redirect = Uri.EscapeDataString(Roblox.Configuration.DiscordRedirect);
			var scope = Uri.EscapeDataString("identify");
			
			return new MVC.RedirectResult(
				$"https://discord.com/api/oauth2/authorize?client_id={ID}&redirect_uri={Redirect}&response_type=code&scope={scope}"
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
				var key = Roblox.Configuration.DiscordKey;
				var token = $"BBSignUp|{DateTime.UtcNow:yyyyMMddHHmmss}|KeyValidation";
				var IDtoken = $"BBSignUp|{userinfo.id}|DiscordId";
				var encryptedtoken = EncryptWithKey(token, key);
				var encryptedId = EncryptWithKey(IDtoken, key);
				
				HttpContext.Response.Cookies.Append("signupkey", encryptedtoken, new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Lax,
					Expires = DateTimeOffset.Now.AddDays(1)
				});

				HttpContext.Response.Cookies.Append("discord_id", encryptedId, new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Lax,
					Expires = DateTimeOffset.Now.AddMinutes(10)
				});

				HttpContext.Response.Cookies.Append("discord_profile", Newtonsoft.Json.JsonConvert.SerializeObject(new
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
		
		[HttpGetBypassUserCheck("UserCheck/checkifinvalidusernameforsignup")]
		public async Task<MVC.IActionResult> CheckUsernameAvailability([MVC.FromQuery] string username)
		{
			// 0 = available
			// 1 = taken
			// 2 = invalid
			
			// debug username to test forms
			if (username?.Equals("TheJongu", StringComparison.OrdinalIgnoreCase) == true)
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
		
		// this is so stupid (this should match discordcb) (IT is better now.)
		private async Task<bool> ValidateSignupCookie(NpgsqlConnection db)
		{
			var key = Roblox.Configuration.DiscordKey;
			var cookie = Request.Cookies["signupkey"];
			
			if (string.IsNullOrEmpty(cookie))
				return false;
			
			try
			{
				var decrypted = DecryptWithKey(cookie, key);
				if (!decrypted.StartsWith("BBSignUp|") || 
					!decrypted.EndsWith("|KeyValidation"))
				return false;
				
				// check if this has been used before, if so return false as user is trying tos ign up again
				var Used = await db.ExecuteScalarAsync<bool>(
					"SELECT EXISTS(SELECT 1 FROM user_signup_tokens WHERE token = @token)",
					new { token = cookie });
			
				if (Used)
					return false;
				
				var parts = decrypted.Split('|');
				if (parts.Length != 3)
					return false;
				
				if (!DateTime.TryParseExact(parts[1], "yyyyMMddHHmmss", 
					System.Globalization.CultureInfo.InvariantCulture, 
					System.Globalization.DateTimeStyles.None, out var TokenDateTime))
					return false;
					
				// 3 hours to sign up
				if (DateTime.UtcNow - TokenDateTime > TimeSpan.FromHours(3))
					return false;
				
				return true;
			}
			catch
			{
				return false;
			}
		} 
		
		private string ValidateDiscordKey()
		{
			var key = Roblox.Configuration.DiscordKey;
			var cookie = Request.Cookies["discord_id"];
			
			if (string.IsNullOrEmpty(cookie))
				return null;
			
			try
			{
				var decrypted = DecryptWithKey(cookie, key);
				if (!decrypted.StartsWith("BBSignUp|") || 
					!decrypted.EndsWith("|DiscordId"))
					return null;
				
				var parts = decrypted.Split('|');
				if (parts.Length != 3)
					return null;
				
				return parts[1];
			}
			catch
			{
				return null;
			}
		}

		[HttpPostBypass("login/signup")]
		[MVC.Consumes("application/x-www-form-urlencoded")]
		// the form errors probably don't work, but doesn't hurt to try (TODO: replace this iwth something else cause it didn't lmao)
		public async Task<MVC.IActionResult> Signup(
			[MVC.FromServices] NpgsqlConnection db,
			[MVC.FromForm] string username,
			[MVC.FromForm] string password,
			[MVC.FromForm] string birthday = null,
			[MVC.FromForm] int? gender = null,
			[MVC.FromForm] string context = null,
			[MVC.FromForm] bool isEligibleForHideAdsAbTest = false)
		{
			// error 1 = already signed up with discord id
			// error 2 = no discord ID in cookies
			// error 3 = failed to validate signup cookie (bad encryption, expired, already used)
			if (!await ValidateSignupCookie(db))
			{
				return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
					new List<string> { "AbuseDetection-3" });
			}

			var DiscordID = ValidateDiscordKey();
			if (string.IsNullOrEmpty(DiscordID))
			{
				return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
					new List<string> { "AbuseDetection-2" });
			}

			if (await services.users.IsDiscordIdUsed(DiscordID))
			{
				return ReturnFormError("Registration is temporarily unavailable. Please try again later.", 
					new List<string> { "AbuseDetection-1" });
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
				
				var signupkey = Request.Cookies["signupkey"];
				await db.ExecuteAsync(
					"INSERT INTO user_signup_tokens (token, user_id, discord_id, used_at) " +
					"VALUES (@token, @userId, @discordId, CURRENT_TIMESTAMP)",
					new
					{
						token = signupkey,
						userId = newuser.userId,
						discordId = DiscordID
					});
				
				// add discord to DB incase they alt
				// ik you can just make a discord alt but they are usually hard to make in my experience and need phone number and shit
				if (!string.IsNullOrEmpty(DiscordID))
				{
					await services.users.LinkDiscordAccount(newuser.userId, DiscordID);
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

				// should we also encrypt this? not really anything harmful anyone can do with this except like change their username
				// discord ID is already encrypted so
				var DiscordProfileJSON = Request.Cookies["discord_profile"];
				DiscordProfile DihcordProfile = null;
				string DiscordUsername = "Unknown";
				string DiscordAvatarURL = null;

				if (!string.IsNullOrEmpty(DiscordProfileJSON))
				{
					try
					{
						DihcordProfile = JsonConvert.DeserializeObject<DiscordProfile>(DiscordProfileJSON);
						DiscordUsername = $"{DihcordProfile.username}#{DihcordProfile.discriminator}";
						if (!string.IsNullOrEmpty(DihcordProfile.avatar))
						{
							DiscordAvatarURL = $"https://cdn.discordapp.com/avatars/{DiscordID}/{DihcordProfile.avatar}.png";
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
							url = DiscordAvatarURL
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
								value = DiscordID,
								inline = true
							},
							new
							{
								name = "Discord Name",
								value = DiscordUsername,
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
				Response.Cookies.Delete("2FACode");
				
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
	}
}	