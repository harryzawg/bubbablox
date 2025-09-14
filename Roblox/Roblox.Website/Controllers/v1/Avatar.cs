using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Avatar;
using Roblox.Exceptions;
using Roblox.Models.Avatar;
using Roblox.Rendering;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.WebsiteModels;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/avatar/v1")]
public class AvatarControllerV1 : ControllerBase
{
    private void FeatureCheck()
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AvatarsEnabled);
    }
	
	private void AttemptScheduleRender(bool forceRedraw = false)
    {
        var userId = safeUserSession.userId;
        using (var cache = ServiceProvider.GetOrCreate<AvatarCache>())
        {
            if (!cache.AttemptScheduleRender(userId)) return;
        }
        
        
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            
            using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
            try
            {
                var assetIds = await cache.GetPendingAssets(userId);
                var newColors = await cache.GetColors(userId);
                await services.avatar.RedrawAvatar(userId, assetIds, newColors, AvatarType.R6, forceRedraw);
            }
            catch (Exception e)
            {
                Console.WriteLine("Background render failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                cache.UnscheduleRender(userId);
            }
        });
    }
    
	private void AttemptScheduleRenderR15(IEnumerable<long>? assetIds = null, string? currentThumbnail = null, string? currentHeadshot = null, bool forceRedraw = false)
	{
		var userId = safeUserSession.userId;

		using (var cache = ServiceProvider.GetOrCreate<AvatarCache>())
		{
			var scheduled = cache.AttemptScheduleRender(userId);
			if (!scheduled)
			{
				Console.WriteLine($"render not scheduled for {userId}");
				return;
			}
		}

		Task.Run(async () =>
		{
			using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
			try
			{
				var assetsToUse = assetIds ?? await cache.GetPendingAssets(userId);
				var colors = await cache.GetColors(userId);
				// Idk why it breaks when i don't pass assets but i'm too lazy to fix it
				await services.avatar.RedrawAvatarR15(userId, assetsToUse, colors, currentThumbnail, currentHeadshot, forceRedraw);
				Console.WriteLine($"R15 render completed for {userId}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"R15 background render failed: {0}\n{1}", e.Message, e.StackTrace);
			}
			finally
			{
				cache.UnscheduleRender(userId);
			}
		});
	}

/* 	[HttpGet("avatar/r15test")]
	public async Task<dynamic> r15Test()
	{
		try
		{
			var userId = userSession.userId;

			using var avatarService = ServiceProvider.GetOrCreate<AvatarService>();
			await avatarService.UpdateUserAvatarImages(userId, null, null);

			var assets = (await services.avatar.GetWornAssets(userId)).ToList();
			AttemptScheduleRenderR15(assets, true);

			return new { success = "Hi gu" };
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[ERROR] r15 render sucks: {ex.Message}\n{ex.StackTrace}");
			return new { error = ex.Message };
		}
	} */
    
    [HttpPost("avatar/redraw-thumbnail")]
    public async Task RequestRedrawAvatar()
    {
        FeatureCheck();
		var userId = userSession.userId;

		var avatarTypeEntry = await services.avatar.GetAvatarType(safeUserSession.userId);
		var avatarImages = await services.avatar.GetAvatarImages(userId);
		// My/Avatar instantly tries to load the avatar, so set it to pending then when it's done it properly loads
		await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null);
		var wearingAssets = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();

		if (avatarTypeEntry.isR15)
		{
			AttemptScheduleRenderR15(wearingAssets, avatarImages.thumbnailUrl, avatarImages.headshotUrl, true);
		}
		else
		{
			AttemptScheduleRender(true);
		}
    }
	
	[HttpPost("avatar/set-wearing-assets")]
	public async Task SetWornAssets([Required, FromBody] SetWearingAssetsRequest request)
	{
		FeatureCheck();
		var userId = userSession.userId;

		var wornAssets = (await services.avatar.GetWornAssets(userId)).ToList();
		var avatarImages = await services.avatar.GetAvatarImages(userId);

		using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
		await cache.SetPendingAssets(userId, request.assetIds);

		var avatarTypeEntry = await services.avatar.GetAvatarType(userId);
		
		// Really stupid hack but rendering animations are very unnecessary
		var Added = request.assetIds.Except(wornAssets).ToList();
		var Removed = wornAssets.Except(request.assetIds).ToList();

		var ChangedAssets = Added.Concat(Removed).ToList();
		if (!ChangedAssets.Any())
		{
			return;
		}

		var ChangedAssetsInfo = await services.assets.MultiGetInfoById(ChangedAssets);

		var Animations = new HashSet<Models.Assets.Type>
		{
			Models.Assets.Type.ClimbAnimation,
			Models.Assets.Type.FallAnimation,
			// it renders your idle animation now
			//Models.Assets.Type.IdleAnimation,
			Models.Assets.Type.JumpAnimation,
			Models.Assets.Type.RunAnimation,
			Models.Assets.Type.SwimAnimation,
			Models.Assets.Type.WalkAnimation,
			Models.Assets.Type.EmoteAnimation
		};
		
		var avatar = await services.avatar.GetAvatar(userId);
		var colors = (ColorEntry)avatar;
		
		await services.avatar.UpdateUserAvatar(userId, colors, request.assetIds);

		var HasVisualChange = ChangedAssetsInfo.Any(a => !Animations.Contains(a.assetType));
		if (!HasVisualChange)
		{
			return;
		}

		await services.avatar.UpdateUserAvatarImages(userId, null, null);

		if (avatarTypeEntry.isR15)
		{
			AttemptScheduleRenderR15(request.assetIds, avatarImages.thumbnailUrl, avatarImages.headshotUrl);
		}
		else
		{
			AttemptScheduleRender();
		}
	}

    [HttpPost("avatar/assets/{assetId:long}/wear")]
    public async Task WearAsset([Required] long assetId)
    {
        FeatureCheck();
        var currentlyWorn = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();
        if (!currentlyWorn.Contains(assetId))
        {
            currentlyWorn.Add(assetId);
        }

        using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        await cache.SetPendingAssets(safeUserSession.userId, currentlyWorn);
        
        AttemptScheduleRender();
    }

    [HttpPost("avatar/set-body-colors")]
    public async Task SetBodyColors([Required, FromBody] SetColorsRequest colors)
    {
        FeatureCheck();
		var userId = userSession.userId;
        
        using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        await cache.SetColors(safeUserSession.userId, colors);
		var avatarTypeEntry = await services.avatar.GetAvatarType(safeUserSession.userId);
		// My/Avatar instantly tries to load the avatar, so set it to pending then when it's done it properly loads
		await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null);
		
		var wearingAssets = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();
		
		if (avatarTypeEntry.isR15)
		{
			AttemptScheduleRenderR15(wearingAssets);
		}
		else
		{
			AttemptScheduleRender();
		}
    }

	[HttpGet("recent-items/{item}/list")]
	public async Task<dynamic> GetRecentItems()
	{
		FeatureCheck();
		var recent = await services.avatar.GetRecentItems(safeUserSession.userId);
		var multiGet = await services.assets.MultiGetInfoById(recent);

		// Filter out animations/other items because it doesn't have a limit in recent and it breaks or just shows it for some reason
		var Filter = new[]
		{
			Models.Assets.Type.ClimbAnimation,
			Models.Assets.Type.FallAnimation,
			Models.Assets.Type.IdleAnimation,
			Models.Assets.Type.JumpAnimation,
			Models.Assets.Type.RunAnimation,
			Models.Assets.Type.SwimAnimation,
			Models.Assets.Type.WalkAnimation,
			Models.Assets.Type.EmoteAnimation,
			Models.Assets.Type.Package,
			Models.Assets.Type.Place,
			Models.Assets.Type.Audio,
			Models.Assets.Type.Badge,
			Models.Assets.Type.GamePass,
			Models.Assets.Type.Mesh,
			Models.Assets.Type.Image,
		};

		var filtered = multiGet
			.Where(c => !Filter.Contains(c.assetType))
			.Select(c => new
			{
				id = c.id,
				name = c.name,
				type = "Asset",
				assetType = new
				{
					id = (int)c.assetType,
					name = c.assetType,
				}
			});

		return new
		{
			data = filtered
		};
	}

    [HttpGet("users/{userId:long}/outfits")]
    public async Task<dynamic> GetUserOutfits(long userId, int itemsPerPage, int page)
    {
        FeatureCheck();
        var offset = itemsPerPage * page - itemsPerPage;
        var result = (await services.avatar.GetUserOutfits(userId, itemsPerPage, offset)).ToList();
        return new
        {
            filteredCount = 0,
            data = result,
            total = result.Count,
        };
    }

    [HttpPost("outfits/{outfitId:long}/wear")]
    public async Task WearOutfit(long outfitId)
    {
        FeatureCheck();
        var outfitDetails = await services.avatar.GetOutfitById(outfitId);
        await services.avatar.RedrawAvatar(safeUserSession.userId, outfitDetails.assetIds, outfitDetails.details, AvatarType.R6);
    }

    /// <summary>
    /// Create an outfit
    /// </summary>
    /// <remarks>
    /// Unlike Roblox, this method ignores the body parameters - it just uses the outfit of the authenticated user.
    /// </remarks>
    [HttpPost("outfits/create")]
    public async Task CreateOutfit([Required,FromBody] CreateOutfitRequest request)
    {
        FeatureCheck();
        var assets = await services.avatar.GetWornAssets(safeUserSession.userId);
        var existingAvatar = await services.avatar.GetAvatar(safeUserSession.userId);
        await services.avatar.CreateOutfit(safeUserSession.userId, request.name, existingAvatar.thumbnailUrl,
            existingAvatar.headshotUrl, new OutfitExtendedDetails()
            {
                details = new OutfitAvatar()
                {
                    headColorId = existingAvatar.headColorId,
                    torsoColorId = existingAvatar.torsoColorId,
                    leftArmColorId = existingAvatar.leftArmColorId,
                    rightArmColorId = existingAvatar.rightArmColorId,
                    leftLegColorId = existingAvatar.leftLegColorId,
                    rightLegColorId = existingAvatar.rightLegColorId,
                    userId = safeUserSession.userId,
                },
                assetIds = assets,
            });
    }

    [HttpPost("outfits/{outfitId:long}/delete")]
    public async Task DeleteOutfit(long outfitId)
    {
        FeatureCheck();
        var info = await services.avatar.GetOutfitById(outfitId);
        if (info.details.userId != userSession.userId)
            throw new ForbiddenException(0, "Forbidden");
        
        await services.avatar.DeleteOutfit(outfitId);
    }
    
    /// <summary>
    /// Update an outfit
    /// </summary>
    /// <remarks>
    /// Unlike Roblox, this method ignores the body parameters - it just uses the outfit of the authenticated user.
    /// </remarks>
    [HttpPatch("outfits/{outfitId:long}")]
    public async Task UpdateOutfit(long outfitId, [Required,FromBody] UpdateOutfitRequest request)
    {
        FeatureCheck();
        var outfitDetails = await services.avatar.GetOutfitById(outfitId);
        if (outfitDetails.details.userId != safeUserSession.userId)
            throw new ForbiddenException();
        var assets = await services.avatar.GetWornAssets(safeUserSession.userId);
        var existingAvatar = await services.avatar.GetAvatar(safeUserSession.userId);
        await services.avatar.UpdateOutfit(outfitId, request.name, existingAvatar.thumbnailUrl,
            existingAvatar.headshotUrl, new OutfitExtendedDetails()
            {
                details = new OutfitAvatar()
                {
                    headColorId = existingAvatar.headColorId,
                    torsoColorId = existingAvatar.torsoColorId,
                    leftArmColorId = existingAvatar.leftArmColorId,
                    rightArmColorId = existingAvatar.rightArmColorId,
                    leftLegColorId = existingAvatar.leftLegColorId,
                    rightLegColorId = existingAvatar.rightLegColorId,
                    userId = safeUserSession.userId,
                },
                assetIds = assets,
            });
    }

    [HttpGet("users/{userId:long}/avatar")]
    public async Task<dynamic> GetAvatar(long userId)
    {
        var assets = await services.avatar.GetWornAssets(userId);
        var existingAvatar = await services.avatar.GetAvatar(userId);
        var multiGetResults = await services.assets.MultiGetInfoById(assets);
		var avatarTypeEntry = await services.avatar.GetAvatarType(userId);
		var scalesEntry = await services.avatar.GetAvatarScales(userId);

        return new
        {
			scales = new
			{
				height = scalesEntry.height / 100.0,
				width = scalesEntry.width / 100.0,
				head = scalesEntry.head / 100.0,
				depth = 1,
				proportion = scalesEntry.proportion / 100.0,
				bodyType = scalesEntry.bodyType / 100.0,
			},
			playerAvatarType = avatarTypeEntry.isR15 ? "R15" : "R6",
            bodyColors = (ColorEntry)existingAvatar,
            assets = multiGetResults.Select(c =>
            {
                return new
                {
                    id = c.id,
                    name = c.name,
                    assetType = new
                    {
                        id = (int) c.assetType,
                        name = c.assetType,
                    },
                };
            }),
        };
    }

    [HttpGet("avatar")]
    public async Task<dynamic> GetMyAvatar()
    {
        return await GetAvatar(userSession.userId);
    }

    [HttpGet("avatar/metadata")]
    public dynamic GetAvatarMetadata()
    {
        return new
        {
            enableDefaultClothingMessage = false,
            isAvatarScaleEmbeddedInTab = true,
            isBodyTypeScaleOutOfTab = true,
            scaleHeightIncrement = 0.05,
            scaleWidthIncrement = 0.05,
            scaleHeadIncrement = 0.05,
            scaleProportionIncrement = 0.05,
            scaleBodyTypeIncrement = 0.05,
            supportProportionAndBodyType = true,
            showDefaultClothingMessageOnPageLoad = false,
            areThreeDeeThumbsEnabled = true,
        };
    }

    [HttpGet("avatar-rules")]
    public dynamic GetAvatarRules()
    {
        return new
        {
            playerAvatarTypes = Enum.GetNames<AvatarType>(),
            scales = new
            {
                height = new
                {
                    min = 0.9,
                    max = 1.05,
                    increment = 0.01,
                },
                width = new
                {
                    min = 0.7,
                    max = 1.0,
                    increment = 0.01,
                },
                head = new
                {
                    min = 0.95,
                    max = 1.0,
                    increment = 0.01,
                },
                proportion = new
                {
                    min = 0.0,
                    max = 1.0,
                    increment = 0.01,
                },
                bodyType = new
                {
                    min = 0.0,
                    max = 1.0,
                    increment = 0.01,
                },
            },
            wearableAssetTypes = new List<dynamic>()
            {
                new { maxNumber = 3, id = 8, name = "Hat" },
                new { maxNumber = 1, id = 41, name = "Hair Accessory" },
                new { maxNumber = 1, id = 42, name = "Face Accessory" },
                new { maxNumber = 1, id = 43, name = "Neck Accessory" },
                new { maxNumber = 1, id = 44, name = "Shoulder Accessory" },
                new { maxNumber = 1, id = 45, name = "Front Accessory" },
                new { maxNumber = 1, id = 46, name = "Back Accessory" },
                new { maxNumber = 1, id = 47, name = "Waist Accessory" },
                new { maxNumber = 1, id = 18, name = "Face" },
                new { maxNumber = 1, id = 19, name = "Gear" },
                new { maxNumber = 1, id = 17, name = "Head" },
                new { maxNumber = 1, id = 29, name = "Left Arm" },
                new { maxNumber = 1, id = 30, name = "Left Leg" },
                new { maxNumber = 1, id = 12, name = "Pants" },
                new { maxNumber = 1, id = 28, name = "Right Arm" },
                new { maxNumber = 1, id = 31, name = "Right Leg" },
                new { maxNumber = 1, id = 11, name = "Shirt" },
                new { maxNumber = 1, id = 2, name = "T-Shirt" },
                new { maxNumber = 1, id = 27, name = "Torso" },
                new { maxNumber = 1, id = 48, name = "Climb Animation" },
                new { maxNumber = 1, id = 49, name = "Death Animation" },
                new { maxNumber = 1, id = 50, name = "Fall Animation" },
                new { maxNumber = 1, id = 51, name = "Idle Animation" },
                new { maxNumber = 1, id = 52, name = "Jump Animation" },
                new { maxNumber = 1, id = 53, name = "Run Animation" },
                new { maxNumber = 1, id = 54, name = "Swim Animation" },
                new { maxNumber = 1, id = 55, name = "Walk Animation" },
                new { maxNumber = 1, id = 56, name = "Pose Animation" },
                new { maxNumber = 0, id = 61, name = "Emote Animation" },
            },
            bodyColorsPalette = Roblox.Models.Avatar.AvatarMetadata.GetColors(),
            basicBodyColorsPalette = new List<dynamic>()
            {
              new { brickColorId = 364, hexColor = "#5A4C42", name = "Dark taupe" },
				new { brickColorId = 217, hexColor = "#7C5C46", name = "Brown" },
				new { brickColorId = 359, hexColor = "#AF9483", name = "Linen" },
				new { brickColorId = 18, hexColor = "#CC8E69", name = "Nougat" },
				new {
					brickColorId = 125,
					hexColor = "#EAB892",
					name = "Light orange",
				},
				new { brickColorId = 361, hexColor = "#564236", name = "Dirt brown" },
				new {
					brickColorId = 192,
					hexColor = "#694028",
					name = "Reddish brown",
				},
				new { brickColorId = 351, hexColor = "#BC9B5D", name = "Cork" },
				new { brickColorId = 352, hexColor = "#C7AC78", name = "Burlap" },
				new { brickColorId = 5, hexColor = "#D7C59A", name = "Brick yellow" },
				new { brickColorId = 153, hexColor = "#957977", name = "Sand red" },
				new { brickColorId = 1007, hexColor = "#A34B4B", name = "Dusty Rose" },
				new { brickColorId = 101, hexColor = "#DA867A", name = "Medium red" },
				new {
					brickColorId = 1025,
					hexColor = "#FFC9C9",
					name = "Pastel orange",
				},
				new {
					brickColorId = 330,
					hexColor = "#FF98DC",
					name = "Carnation pink",
				},
				new { brickColorId = 135, hexColor = "#74869D", name = "Sand blue" },
				new { brickColorId = 305, hexColor = "#527CAE", name = "Steel blue" },
				new { brickColorId = 11, hexColor = "#80BBDC", name = "Pastel Blue" },
				new {
					brickColorId = 1026,
					hexColor = "#B1A7FF",
					name = "Pastel violet",
				},
				new { brickColorId = 321, hexColor = "#A75E9B", name = "Lilac" },
				new {
					brickColorId = 107,
					hexColor = "#008F9C",
					name = "Bright bluish green",
				},
				new { brickColorId = 310, hexColor = "#5B9A4C", name = "Shamrock" },
				new { brickColorId = 317, hexColor = "#7C9C6B", name = "Moss" },
				new { brickColorId = 29, hexColor = "#A1C48C", name = "Medium green" },
				new {
					brickColorId = 105,
					hexColor = "#E29B40",
					name = "Br. yellowish orange",
				},
				new {
					brickColorId = 24,
					hexColor = "#F5CD30",
					name = "Bright yellow",
				},
				new {
					brickColorId = 334,
					hexColor = "#F8D96D",
					name = "Daisy orange",
				},
				new {
					brickColorId = 199,
					hexColor = "#635F62",
					name = "Dark stone grey",
				},
				new { brickColorId = 1002, hexColor = "#CDCDCD", name = "Mid gray" },
				new {
					brickColorId = 1001,
					hexColor = "#F8F8F8",
					name = "Institutional white",
				},  
            },
            minimumDeltaEBodyColorDifference = 11.4,
            defaultClothingAssetLists = new
            {
                defaultShirtAssetIds = new List<long>() {1,2},
                defaultPantAssetIds = new List<long>() {1,2},
            },
            bundlesEnabledForUser = false,
            emotesEnabledForUser = false,
        };
    }

	[HttpPost("avatar/set-scales"), HttpPost("avatar/set-player-avatar-type")]
	public async Task<dynamic> AvatarSetScalesAndType()
	{
		try
		{
			// what did pekora actually return here
			using var reader = new StreamReader(Request.Body);
			var body = await reader.ReadToEndAsync();
			
			var json = JObject.Parse(body);
			
			var userId = userSession.userId;
			
			if (Request.Path.Value.Contains("set-player-avatar-type"))
			{
				var playerAvatarType = json.Value<int>("playerAvatarType");
				await services.avatar.UpdateAvatarType(userId, playerAvatarType);
				await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null);
        
				AttemptScheduleRenderR15();
				
				return new { success = true };
			}
			else if (Request.Path.Value.Contains("set-scales"))
			{
				await services.avatar.UpdateScales(
					userId,
					json.Value<decimal>("height"),
					json.Value<decimal>("width"),
					json.Value<decimal>("head"),
					json.Value<decimal>("proportion"),
					json.Value<decimal>("bodyType")
				);
				await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null);
        
				AttemptScheduleRenderR15();
				
				return new { success = true };
			}
			
			return new { success = "No" };
		}
		catch (Exception ex)
		{
			return new  { success = false, error = ex.Message };
		}
	}
}