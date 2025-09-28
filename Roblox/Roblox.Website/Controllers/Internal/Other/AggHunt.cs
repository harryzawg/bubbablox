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
using HttpGetEgg = Roblox.Website.Controllers.HttpGetBypassAttribute;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class EggHunt : ControllerBase
    {
        private static bool IsTheAggHuntEnably = true;
		// Top secret fbi bobux key
        private const string ApiKey = "TopSecretAggHuntKeyDoNOTLEAKPlz";
        
        [HttpGetEgg("game/EggHunt.ashx")]
        public async Task<IActionResult> AggHuntReqrust([FromQuery] long? placeId, [FromQuery] long? playerId, [FromQuery] long? eggId, [FromQuery] bool? toggle, [FromQuery] bool? getStatus, [FromQuery] string apiKey)
        {
            if (apiKey != ApiKey)
            {
                return Unauthorized("Bad man.... No leaky.");
            }
			
            if (toggle.HasValue)
            {
                IsTheAggHuntEnably = toggle.Value;
                return Ok(new { enabled = IsTheAggHuntEnably });
            }

            if (getStatus.HasValue && getStatus.Value)
            {
                return Ok(new { enabled = IsTheAggHuntEnably });
            }

            if (placeId.HasValue && playerId.HasValue && eggId.HasValue)
            {
                if (!IsTheAggHuntEnably)
                {
                    return Ok(new { disabled = true });
                }

                try
                {
                    await GiveEggToUser(playerId.Value, eggId.Value, placeId.Value);
                    
                    return Ok(new { 
                        success = true
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { 
                        success = false,
                        message = $"Error giving player agg: {ex.Message}" 
                    });
                }
            }

            return BadRequest("Something happened idk man");
        }

        private async Task GiveEggToUser(long userId, long assetId, long placeId)
        {
			using var Assets = Roblox.Services.ServiceProvider.GetOrCreate<AssetsService>();
			using var Users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
			
			bool DoesThisStupidIdiotOwnTheAsset = await Assets.DoesUserOwnAsset(userId, assetId);
			if (!DoesThisStupidIdiotOwnTheAsset)
			{
				await Users.GiveUserEgg(userId, assetId);
			}
		}
    }
}