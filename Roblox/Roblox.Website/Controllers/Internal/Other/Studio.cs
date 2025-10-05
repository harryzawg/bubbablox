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

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Studio : ControllerBase 
    {		
		private async Task RateLimitCheck()
		{
			var loginKey = "LoginAttemptCountV1:" + GetIP();
			var attemptCount = (await services.cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();

			if (!await services.cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
			{
				throw new ForbiddenException(15, "Too many attempts, please wait about 10 minutes before retrying!");
			}
		}
		
		public class LoginRequest
		{
			public string? username { get; set; } = null;
			public string ctype { get; set; } = "";
			public string cvalue { get; set; } = "";
			public string password { get; set; } = "";
		}
		
		// GOD i hate this shit
		private async Task<string> GetRequestBody()
		{
			HttpContext.Request.EnableBuffering();

			using var reader = new StreamReader(
				HttpContext.Request.Body,
				Encoding.UTF8,
				detectEncodingFromByteOrderMarks: false,
				bufferSize: 1024,
				leaveOpen: true
			);

			string body = await reader.ReadToEndAsync();

			HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
			
			//Console.WriteLine(body);

			return body;
		}

		// studio
		[HttpPostBypass("v2/login")]
		public async Task<dynamic> LoginV2()
		{
			try
			{
				FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
			}
			catch (RobloxException)
			{
				throw new RobloxException(503, 0, "Login is currently disabled. Please try again later.");
			}
			await RateLimitCheck();
			string requestBody = await GetRequestBody();
			string? username = "";
			string? password = "";

			if (string.IsNullOrEmpty(requestBody))
				throw new BadRequestException(8, "Empty request.");
			
			var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
			if (userAgent == "RobloxStudio/WinInet")
			{
				try
				{
					var loginRequest = JsonConvert.DeserializeObject<LoginRequest>(requestBody);
					username = loginRequest?.username ?? loginRequest?.cvalue;
					password = loginRequest?.password;
				}
				catch (Exception)
				{
					Console.WriteLine("Failed to login");
				}
			}
			else
			{
				try
				{
					var loginRequest = JsonConvert.DeserializeObject<LoginRequest>(requestBody);
					username = loginRequest?.username ?? loginRequest?.cvalue;
					password = loginRequest?.password;
				}
				catch (Exception)
				{
					Console.WriteLine("Failed to login");
				}
			}

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
				throw new BadRequestException(3, "Username and Password are required. Please try again.");

			UserInfo userInfo;
			try
			{
				userInfo = await services.users.GetUserByName(username);
			}
			catch (RecordNotFoundException)
			{
				throw new ForbiddenException(1, "Incorrect username or password. Please try again.");
			}
			
			await Login(username, password, userInfo.userId);

			await CreateSessionAndSetCookie(userInfo.userId);
			return new
			{
				membershipType = 4,
				userInfo.username,
				name = userInfo.username,
				isUnder13 = false,
				countryCode = "US",
				userId = userInfo.userId,
				id = userInfo.userId,
				displayName = userInfo.username,
				user = new
				{
					id = userInfo.userId,
					name = userInfo.username,
					displayName = userInfo.username
				},
				isBanned = false
			};
		}
		
		private async Task<bool> Login(string username, string password, long userId)
		{
			try
			{
				FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
			}
			catch (RobloxException)
			{
				throw new RobloxException(503, 0, "Login is currently disabled. Please try again later.");
			}
			await RateLimitCheck();
			try
			{
				if (!await services.users.VerifyPassword(userId, password))
					throw new ForbiddenException(1, "Incorrect username or password. Please try again");
			}
			catch (RecordNotFoundException)
			{
				throw new ForbiddenException(4, "Your account has been locked. Please reset your password to unlock your account.");
			}

			return true;
		}
		
		private async Task<string> CreateSessionAndSetCookie(long userId)
		{
			var sessionCookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
			{
				sessionId = await services.users.CreateSession(userId),
				createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
			});

			HttpContext.Response.Cookies.Append(Middleware.SessionMiddleware.CookieName, sessionCookie, new CookieOptions()
			{
				Domain = ".{Configuration.BaseUrl}",
				Secure = false,
				Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(364)),
				IsEssential = true,
				Path = "/",
				SameSite = SameSiteMode.Lax,
			});
			return sessionCookie;
		}
	}
}