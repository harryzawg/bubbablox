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
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Models.Users;
using Roblox.Models.Economy;
using Roblox.Dto.Assets;
using Roblox.Models.Assets;
using Roblox.Website.WebsiteModels.Asset;
using Roblox.Dto.Tickets;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class TicketBot : ControllerBase
    {
		private void ValidateBotAuth()
        {
	        if (Request.Headers["BB-botAPIkey"].ToString() != Roblox.Configuration.BotAuthorization)
	        {
		        throw new Exception("Internal");
	        }
        }
		
		[HttpGetBypass("botapi/tickets/user/{discordId}")]
		public async Task<ActionResult<UserDiscord>> GetUserByDiscordId(string discordId)
		{
			try
			{
				ValidateBotAuth();
				var User = await services.users.GetUserDataByDiscordId(discordId);
				
				return Ok(User);
			}
			catch (RecordNotFoundException)
			{
				return NotFound();
			}
			catch (Exception ex)
			{
				return StatusCode(500);
			}
		}

		[HttpPostBypass("botapi/tickets/transcripts")]
		public async Task<ActionResult> StoreTranscriptMessages([MVC.FromBody] TicketTranscriptRequest request)
		{
			try
			{
				ValidateBotAuth();
				
				if (request?.data == null || request.data.Count == 0)
				{
					return BadRequest();
				}
				var LatestTicket = await services.users.GetLatestTicket();
				foreach (var kvp in request.data)
				{
					var TRequest = kvp.Value;
					
					if (string.IsNullOrEmpty(TRequest.user) || 
						string.IsNullOrEmpty(TRequest.message) || 
						string.IsNullOrEmpty(TRequest.discordId))
					{
						continue;
					}
					
					try
					{
						long userId;
						if (!long.TryParse(TRequest.user, out userId))
						{
							userId = await services.users.GetUserIdFromUsername(TRequest.user);
						}
						
						await services.users.StoreTranscriptMessage(
							LatestTicket,
							userId, 
							TRequest.discordId, 
							TRequest.message,
							request.name
						);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"failed to store transcript for {TRequest.user}: {ex.Message}");
					}
				}
				
				return Ok();
			}
			catch (Exception ex)
			{
				return StatusCode(500);
			}
		}
    }
}