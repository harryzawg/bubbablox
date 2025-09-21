using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class ClientData : ControllerBase 
    {	
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
		
		// TODO: Before source release, make these actually fucking work
		// It's so incredibly insecure having these patched in RCC, but i cannot find a fix for some reason cause RCC SUCKS. 
		// (i know why now and i'm just too lazy to fix it, but please do this in the future)
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
		
		[HttpGetBypass("Setting/24")]
		public async Task<MVC.ActionResult> GetAppSettings2014()
		{
			string json = "2014LFFlags";
			json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "2014LFFlags.json");
			string content = await System.IO.File.ReadAllTextAsync(json);
			return Content(content, "text/plain");
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
						
					case "StudioApp":
						json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "StudioApp.json");
						break;

					// Make this configurable in appsettyings
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
		
		[HttpPostBypass("Game/ChatFilter.ashx")]
		public dynamic ChatFilter()
		{
			try
			{
				var text = HttpContext.Request.Form["text"].ToString();
				var userId = HttpContext.Request.Form["userId"].ToString();
				var placeId = HttpContext.Request.Headers["placeId"].ToString();
				var gameInstanceId = HttpContext.Request.Headers["gameInstanceID"].ToString();

				// add real filter eventually
				// var text = Filter(text, userId, placeId);
				return new
				{
					data = new 
					{
						white = "Hi gu",
						black = "Hi gu"
					}
				};
			}
			catch (Exception ex)
			{
				return new
				{
					data = new 
					{
						white = "#",
						black = "#"
					}
				};
			}
		}
		
		// Make an actual filter function later
		[HttpPostBypass("moderation/filtertext")]
        public dynamic GetModerationText()
        {
            //var text = FilterFunction(HttpContext.Request.Form["text"].ToString());
			var text = HttpContext.Request.Form["text"].ToString();
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
            //var text = FilterFunction(HttpContext.Request.Form["text"].ToString());
			var text = HttpContext.Request.Form["text"].ToString();
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
	}
}	