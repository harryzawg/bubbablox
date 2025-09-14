using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using MVC = Microsoft.AspNetCore.Mvc;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Character : ControllerBase 
    {		
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
						// Only default R15 Idle works in 2018 cause all the other ones just make the player have parkinsons
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
	}
}	