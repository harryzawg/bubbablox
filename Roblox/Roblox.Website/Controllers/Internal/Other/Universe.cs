using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using MVC = Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Universe : ControllerBase 
    {		
		[HttpGetBypass("Game/LoadPlaceInfo.ashx")]
		public MVC.IActionResult LoadPlaceInfo([Required] long placeId)
		{
			return Ok();
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
		
		[HttpGetBypass("v1.1/game-start-info")]
        public async Task<dynamic> GameStartInfo(long universeId)
        {
			var placeId = await services.games.GetRootPlaceId(universeId);
			var RigType = await services.games.GetRigType(placeId);
            return new
            {
				gameAvatarType = RigType switch
				{
					"playerChoice" => "PlayerChoice",
					"MorphToR6" => "MorphToR6",
					"MorphToR15" => "MorphToR15",
					_ => "PlayerChoice"
				},
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
		
		[HttpPostBypass("/game/validate-machine")]
        public dynamic ValidateMachine()
        {
            return new
            {
                success = true,
                message = "",
            };
        }
		
		[HttpGetBypass("/universes/validate-place-join")]
		public async Task<string> ValidatePlaceJoin()
		{
			return "true";
		}
		
		[HttpGetBypass("universal-app-configuration/v1/behaviors/app-policy/content")]
        public dynamic AppPolicy()
        {
            string policyContent = System.IO.File.ReadAllText(Configuration.JsonDataDirectory + "AppPolicy.json");
            dynamic? policyJson = JsonConvert.DeserializeObject<ExpandoObject>(policyContent);
            return policyJson ?? "";
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
	}
}	