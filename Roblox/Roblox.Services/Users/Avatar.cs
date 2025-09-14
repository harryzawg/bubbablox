using System.Security.Cryptography;
using System.Text;
using Dapper;
using Roblox.Dto;
using Roblox.Dto.Avatar;
using Roblox.Models.Assets;
using Roblox.Models.Avatar;
using Roblox.Services.DbModels;
using Roblox.Exceptions.Services.Users;
using Roblox.Rendering;
using Roblox.Services.Exceptions;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services;

public class AvatarService : ServiceBase, IService
{
    public async Task<IEnumerable<long>> GetWornAssets(long userId)
    {
        // useless inner join is intentional:
        // it's so that we filter out items the user no longer owns.
        return (await db.QueryAsync<AssetId>(
            "SELECT distinct(ua.asset_id) as assetId FROM user_avatar_asset av INNER JOIN user_asset ua ON ua.user_id = av.user_id AND ua.asset_id = av.asset_id WHERE av.user_id = :user_id", new
            {
                user_id = userId,
            })).Select(c => c.assetId);
    }

    public async Task<bool> IsUserAvatar18Plus(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>
        ("SELECT count(*) AS total FROM user_avatar_asset INNER JOIN asset ON asset.id = user_avatar_asset.asset_id WHERE asset.is_18_plus AND user_avatar_asset.user_id = :user_id",
            new
            {
                user_id = userId,
            });
        return result.total != 0;
    }

    public async Task<IEnumerable<long>> GetRecentItems(long userId)
    {
        var result =
            await db.QueryAsync(
                "SELECT distinct asset_id, max(id) FROM user_asset WHERE user_id = :user_id GROUP BY asset_id ORDER BY max(id) DESC, asset_id LIMIT 50",
                new
                {
                    user_id = userId,
                });
        return result.Select(c => (long) c.asset_id);
    }

	public async Task<AvatarWithColors> GetAvatar(long userId)
	{
		var existingAvatar = await db.QuerySingleOrDefaultAsync<DatabaseAvatarWithImages>(
			"SELECT head_color_id, torso_color_id, left_arm_color_id, right_arm_color_id, left_leg_color_id, right_leg_color_id, thumbnail_url, headshot_thumbnail_url FROM user_avatar WHERE user_id = :user_id",
			new
			{
				user_id = userId,
			});
		return new AvatarWithColors()
		{
			headColorId = existingAvatar.head_color_id,
			torsoColorId = existingAvatar.torso_color_id,
			rightArmColorId = existingAvatar.right_arm_color_id,
			leftArmColorId = existingAvatar.left_arm_color_id,
			rightLegColorId = existingAvatar.right_leg_color_id,
			leftLegColorId = existingAvatar.left_leg_color_id,
			thumbnailUrl = existingAvatar.thumbnail_url,
			headshotUrl = existingAvatar.headshot_thumbnail_url,
		};
	}

    public async Task<ColorEntry> GetAvatarColors(long userId)
    {
        var existingAvatar = await db.QuerySingleOrDefaultAsync<DatabaseAvatar>(
            "SELECT head_color_id, torso_color_id, left_arm_color_id,right_arm_color_id,left_leg_color_id,right_leg_color_id FROM user_avatar WHERE user_id = :user_id",
            new
            {
                user_id = userId,
            });
        return new ColorEntry()
        {
            headColorId = existingAvatar.head_color_id,
            torsoColorId = existingAvatar.torso_color_id,
            rightArmColorId = existingAvatar.right_arm_color_id,
            leftArmColorId = existingAvatar.left_arm_color_id,
            rightLegColorId = existingAvatar.right_leg_color_id,
            leftLegColorId = existingAvatar.left_leg_color_id,
        };
    }
	
	// Get previous avatar images in case render fails
	public async Task<AvatarImages> GetAvatarImages(long userId)
	{
		var result = await db.QuerySingleOrDefaultAsync<AvatarImages>(
			"SELECT thumbnail_url as thumbnailUrl, headshot_thumbnail_url as headshotUrl FROM user_avatar WHERE user_id = :user_id",
			new { user_id = userId });
		
		return result ?? new AvatarImages();
	}

	public class AvatarImages
	{
		public string? thumbnailUrl { get; set; }
		public string? headshotUrl { get; set; }
	}

	private readonly Models.Assets.Type[] _wearableAssetTypes = new[]
	{
		Type.Shirt,
		Type.Pants,
		Type.TeeShirt,
		
		Type.Face,
		Type.Hat,
		Type.FrontAccessory,
		Type.BackAccessory,
		Type.WaistAccessory,
		Type.HairAccessory,
		Type.NeckAccessory,
		Type.ShoulderAccessory,
		Type.FaceAccessory,
		
		Type.LeftArm,
		Type.RightArm,
		Type.LeftLeg,
		Type.RightLeg,
		Type.Torso,
		Type.Head,
		
		Type.Gear,

		Type.ClimbAnimation,
		Type.FallAnimation,
		Type.IdleAnimation,
		Type.JumpAnimation,
		Type.RunAnimation,
		Type.SwimAnimation,
		Type.WalkAnimation,
		Type.EmoteAnimation,
	};

    
    /// <summary>
    /// Filter the dirtyAssetIds. This will remove moderated/pending items, items the user doesn't own, invalid items, etc.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dirtyAssetIds"></param>
    /// <returns></returns>
    public async Task<IEnumerable<long>> FilterAssetsForRender(long userId, IEnumerable<long> dirtyAssetIds)
    {
        var assetIds = dirtyAssetIds.ToList();
        if (assetIds.Count != 0)
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>();
            // Get the moderation status for each item
            var moderationStatus = (await db.QueryAsync<AssetModerationEntry>(
                "SELECT moderation_status as moderationStatus, id as assetId, asset_type as assetType FROM asset WHERE id = ANY(:ids)", new
                {
                    ids = assetIds,
                })).ToList();
            // Duplicate the list so we can mutate it
            var safeModList = moderationStatus.ToList();
            // Add package contents, if required
            foreach (var item in moderationStatus)
            {
                if (item.assetType == Type.Package)
                {
                    var packageAssetIds = await assets.GetPackageAssets(item.assetId);
                    var otherModStatus = (await db.QueryAsync<AssetModerationEntry>(
                        "SELECT moderation_status as moderationStatus, id as assetId, asset_type as assetType FROM asset WHERE id = ANY(:ids)", new
                        {
                            ids = packageAssetIds.ToList(),
                        })).ToList();
                    foreach (var nestedAsset in otherModStatus)
                    {
                        safeModList.Add(nestedAsset);
                        assetIds.Add(nestedAsset.assetId);
                    }
                }
            }
            // Filter items by moderation status - we only want to render ReviewApproved
            assetIds = assetIds.Where(c =>
            {
                var hasEntry = safeModList.Find(v => v.assetId == c);
                if (hasEntry == null) return false;
                if (!_wearableAssetTypes.Contains(hasEntry.assetType)) return false;
                if (hasEntry.moderationStatus != ModerationStatus.ReviewApproved) return false;
                return true;
            }).ToList();
            // Finally, confirm user actually owns each assetId
            // goodAssetIds is a list of assets the user owns
            var goodAssetIds = new List<long>();
            foreach (var id in assetIds)
            {
                var ownResult = await db.QuerySingleOrDefaultAsync<UserAssetEntry>(
                    "SELECT asset_id as assetId, user_id as userId from user_asset WHERE user_id = :user_id AND asset_id = :asset_id LIMIT 1",
                    new
                    {
                        user_id = userId,
                        asset_id = id,
                    });
                if (ownResult != null)
                {
                    goodAssetIds.Add(id);
                }
            }

            assetIds = goodAssetIds;
        }

        return assetIds;
    }
	// add these to dto lateer
	public class AvatarTypeEntry
	{
		public bool isR15 { get; set; }
	}

	public class ScaleEntry
	{
		public int height { get; set; }
		public int width { get; set; }
		public int head { get; set; }
		public int proportion { get; set; }
		public int bodyType { get; set; }
	}
	
	public async Task<AvatarTypeEntry> GetAvatarType(long userId)
	{
		var result = await db.QuerySingleOrDefaultAsync<AvatarTypeEntry>(
			"SELECT r15 as isR15 FROM user_avatar_type WHERE user_id = :user_id",
			new { user_id = userId });
		
		return result ?? new AvatarTypeEntry { isR15 = false }; // default to r6 if bugged
	}

	public async Task<ScaleEntry> GetAvatarScales(long userId)
	{
		var result = await db.QuerySingleOrDefaultAsync<ScaleEntry>(
			"SELECT height, width, head, proportion, body_type as bodyType FROM user_avatar_type WHERE user_id = :user_id",
			new { user_id = userId });
		
		// default values if bugged
		return result ?? new ScaleEntry 
		{ 
			height = 100, 
			width = 100, 
			head = 100, 
			proportion = 100, 
			bodyType = 100 
		};
	}

	public async Task UpdateScales(long userId, decimal height, decimal width, decimal head, decimal proportion, decimal bodyType)
	{
		var scales = new ScaleEntry
		{
			height = (int)(height * 100),
			width = (int)(width * 100),
			head = (int)(head * 100),
			proportion = (int)(proportion * 100),
			bodyType = (int)(bodyType * 100)
		};
		
		await db.ExecuteAsync(@"
			INSERT INTO user_avatar_type (user_id, height, width, head, proportion, body_type) 
			VALUES (:user_id, :height, :width, :head, :proportion, :body_type)
			ON CONFLICT (user_id) 
			DO UPDATE SET 
				height = :height, 
				width = :width, 
				head = :head, 
				proportion = :proportion, 
				body_type = :body_type",
		new 
		{
			user_id = userId,
			height = scales.height,
			width = scales.width,
			head = scales.head,
			proportion = scales.proportion,
			body_type = scales.bodyType
		});
	}
	
	public async Task UpdateAvatarType(long userId, int playerAvatarType)
	{
		var isR15 = playerAvatarType == 2; // 2 = R15, 1 = R6
		
		await db.ExecuteAsync(@"
			INSERT INTO user_avatar_type (user_id, r15) 
			VALUES (:user_id, :r15)
			ON CONFLICT (user_id) 
			DO UPDATE SET 
				r15 = :r15",
		new 
		{
			user_id = userId,
			r15 = isR15,
		});
	}

    public string GetAvatarHash(ColorEntry colors, IEnumerable<long> assetVersionIds, ScaleEntry scales, AvatarTypeEntry avatarType)
    {
        var assets = assetVersionIds.Distinct().ToList();
        assets.Sort((a, b) => a > b ? 1 : a == b ? 0 : -1);
        var str =
            $"avatar-hash-1.4:{string.Join(",", assets)}:{colors.headColorId},{colors.torsoColorId},{colors.leftArmColorId},{colors.rightArmColorId},{colors.leftLegColorId},{colors.rightLegColorId},{(avatarType.isR15 ? "R15" : "R6")},{scales.height},{scales.width},{scales.head},{scales.proportion},{scales.bodyType}";
        var hasher = SHA256.Create();
        var bits = hasher.ComputeHash(Encoding.UTF8.GetBytes(str));
        return Convert.ToHexString(bits).ToLower();
    }

    private async Task<IEnumerable<long>> MultiGetAssetVersionsFromAssetIds(IEnumerable<long> assetIds)
    {
        // todo: make this more efficient :(
        var ids = new List<long>();
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        foreach (var id in assetIds.Distinct())
        {
            var latest = await assets.GetLatestAssetVersion(id);
            ids.Add(latest.assetVersionId);
        }
        return ids.Distinct();
    }

    /// <summary>
    /// Update the userId's avatar. Returns a hash. This does not render or validate anything.
    /// </summary>
    public async Task<string> UpdateUserAvatar(long userId, ColorEntry colors, IEnumerable<long> assetIds)
    {
        var idsList = assetIds.ToList();
        return await InTransaction(async (trx) =>
        {
            await UpdateAsync("user_avatar", "user_id", userId, new
            {
                head_color_id = colors.headColorId,
                torso_color_id = colors.torsoColorId,
                right_arm_color_id = colors.rightArmColorId,
                left_arm_color_id = colors.leftArmColorId,
                right_leg_color_id = colors.rightLegColorId,
                left_leg_color_id = colors.leftLegColorId,
            });
            await db.ExecuteAsync("DELETE FROM user_avatar_asset WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            foreach (var item in idsList)
            {
                await db.ExecuteAsync("INSERT INTO user_avatar_asset (user_id, asset_id) VALUES (:user_id, :asset_id)",
                    new
                    {
                        user_id = userId,
                        asset_id = item,
                    });
            }

            var assetVersions = await MultiGetAssetVersionsFromAssetIds(idsList);
			var avatarType = await GetAvatarType(userId);
			var scales = await GetAvatarScales(userId);
            return GetAvatarHash(colors, assetVersions, scales, avatarType);
        });
    }

    public async Task UpdateUserAvatarImages(long userId, string? headshotImage, string? thumbnailImage)
    {
        await db.ExecuteAsync(
            "UPDATE user_avatar SET thumbnail_url = :thumbnail_url, headshot_thumbnail_url = :headshot_url WHERE user_id = :user_id",
            new
            {
                user_id = userId,
                thumbnail_url = thumbnailImage,
                headshot_url = headshotImage,
            });
    }
	
	public async Task<string?> GetUserHeadshotUrl(long userId)
	{
		return await db.QuerySingleOrDefaultAsync<string?>(
			"SELECT headshot_thumbnail_url FROM user_avatar WHERE user_id = :user_id",
			new { user_id = userId }
		);
	}

    public async Task<IEnumerable<OutfitEntry>> GetUserOutfits(long userId, int limit, int offset)
    {
        return await db.QueryAsync<OutfitEntry>(
            "SELECT id, name, created_at as created FROM user_outfit WHERE user_id = :user_id ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                user_id = userId,
                limit = limit,
                offset = offset,
            });
    }

    public async Task<OutfitExtendedDetails> GetOutfitById(long outfitId)
    {
        var result = await db.QuerySingleOrDefaultAsync<OutfitAvatar>(
            "SELECT head_color_id as headColorId, torso_color_id as torsoColorId, left_arm_color_id as leftArmColorId, right_arm_color_id as rightArmColorId, left_leg_color_id as leftLegColorId, right_leg_color_id as rightLegColorId, user_id as userId FROM user_outfit WHERE id = :id",
            new
            {
                id = outfitId,
            });
        var assets =
            (await db.QueryAsync<AssetId>(
                "SELECT asset_id as assetId FROM user_outfit_asset WHERE outfit_id = :outfit_id",
                new {outfit_id = outfitId})).Select(c => c.assetId);

        return new OutfitExtendedDetails()
        {
            assetIds = assets,
            details = result,
        };
    }

    public async Task CreateOutfit(long userId, string name, string? thumbnailUrl, string? headshotUrl,
        OutfitExtendedDetails outfitDetails)
    {
        var existingOutfitCount = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) as total FROM user_outfit WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
        if (existingOutfitCount.total >= 100)
            throw new TooManyOutfitsException();
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            throw new OutfitNameTooShortException();
        if (name.Length > 25)
            throw new OutfitNameTooLongException();
        // image check
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(headshotUrl))
            throw new NoImageUrlException();

        await InTransaction(async (trx) =>
        {
            var id = await InsertAsync("user_outfit", new
            {
                name = name,
                user_id = userId,
                // colors
                head_color_id = outfitDetails.details.headColorId,
                torso_color_id = outfitDetails.details.torsoColorId,
                left_arm_color_id = outfitDetails.details.leftArmColorId,
                right_arm_color_id = outfitDetails.details.rightArmColorId,
                left_leg_color_id = outfitDetails.details.leftLegColorId,
                right_leg_color_id = outfitDetails.details.rightLegColorId,
                // type
                avatar_type = AvatarType.R6,
                // images
                headshot_thumbnail_url = headshotUrl,
                thumbnail_url = thumbnailUrl,
            });
            foreach (var assetId in outfitDetails.assetIds)
            {
                await InsertAsync("user_outfit_asset", "outfit_id", new
                {
                    outfit_id = id,
                    asset_id = assetId,
                });
            }

            return 0;
        });
    }

    public async Task UpdateOutfit(long outfitId, string name, string? thumbnailUrl, string? headshotUrl,
        OutfitExtendedDetails outfitDetails)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            throw new OutfitNameTooShortException();
        if (name.Length > 25)
            throw new OutfitNameTooLongException();
        // image check
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(headshotUrl))
            throw new NoImageUrlException();
        await InTransaction(async (trx) =>
        {
            await UpdateAsync("user_outfit", outfitId, new
            {
                name = name,
                // colors
                head_color_id = outfitDetails.details.headColorId,
                torso_color_id = outfitDetails.details.torsoColorId,
                left_arm_color_id = outfitDetails.details.leftArmColorId,
                right_arm_color_id = outfitDetails.details.rightArmColorId,
                left_leg_color_id = outfitDetails.details.leftLegColorId,
                right_leg_color_id = outfitDetails.details.rightLegColorId,
                // type
                avatar_type = AvatarType.R6,
                // images
                headshot_thumbnail_url = headshotUrl,
                thumbnail_url = thumbnailUrl,
            });
            await db.ExecuteAsync("DELETE FROM user_outfit_asset WHERE outfit_id = :id", new {id = outfitId});
            foreach (var assetId in outfitDetails.assetIds)
            {
                await InsertAsync("user_outfit_asset", "outfit_id", new
                {
                    outfit_id = outfitId,
                    asset_id = assetId,
                });
            }

            return 0;
        });
    }

    public async Task DeleteOutfit(long outfitId)
    {
        await InTransaction(async (t) =>
        {
            await db.ExecuteAsync("DELETE FROM user_outfit WHERE id = :id", new
            {
                id = outfitId,
            });
            await db.ExecuteAsync("DELETE FROM user_outfit_asset WHERE outfit_id = :outfit_id", new
            {
                outfit_id = outfitId,
            });
            return 0;
        });
    }
	
	private async Task<bool> ConfirmAssetSelectionIsOkForRender(long userId, IEnumerable<long> unknownAssetIds)
	{
		var assets = new AssetsService();
		var assetIds = unknownAssetIds.ToList();
		if (assetIds.Count == 0) return true;
		var details = await assets.MultiGetInfoById(assetIds);

		// vars
		var gear = 0;
		var face = 0;
		var shirt = 0;
		var pants = 0;
		var tShirt = 0;
		var accessories = 0;
		var leftArm = 0;
		var rightArm = 0;
		var leftLeg = 0;
		var rightLeg = 0;
		var torso = 0;
		var head = 0;
		var animations = 0;
		var emoteAnimations = 0;	

		// track bad assets
		var invalid = new List<string>();
		var assetstype = new Dictionary<Type, List<long>>();

		foreach (var item in details)
		{
			if (!assetstype.ContainsKey(item.assetType))
			{
				assetstype[item.assetType] = new List<long>();
			}
			assetstype[item.assetType].Add(item.id);

			switch (item.assetType)
			{
				case Models.Assets.Type.TeeShirt:
					tShirt++;
					if (tShirt > 1) invalid.Add($"too many T-Shirts (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.Shirt:
					shirt++;
					if (shirt > 1) invalid.Add($"too many Shirts (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.Pants:
					pants++;
					if (pants > 1) invalid.Add($"too many Pants (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.ClimbAnimation:
				case Models.Assets.Type.FallAnimation:
				case Models.Assets.Type.IdleAnimation:
				case Models.Assets.Type.JumpAnimation:
				case Models.Assets.Type.RunAnimation:
				case Models.Assets.Type.SwimAnimation:
				case Models.Assets.Type.WalkAnimation:
					animations++;
					if (animations > 7) invalid.Add($"too many AvatarAnimations (limit 7) - Asset {item.id}");
					break;
				case Models.Assets.Type.EmoteAnimation:
					emoteAnimations++;
					if (emoteAnimations > 8) invalid.Add($"too many Emotes (limit 8) - Asset {item.id}");
					break;
				case Models.Assets.Type.Gear:
					gear++;
					if (gear > 1) invalid.Add($"too many Gears (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.Face:
					face++;
					if (face > 1) invalid.Add($"too many Faces (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.Hat:
				case Models.Assets.Type.FrontAccessory:
				case Models.Assets.Type.BackAccessory:
				case Models.Assets.Type.HairAccessory:
				case Models.Assets.Type.NeckAccessory:
				case Models.Assets.Type.ShoulderAccessory:
				case Models.Assets.Type.WaistAccessory:
				case Models.Assets.Type.FaceAccessory:
					accessories++;
					if (accessories > 6) invalid.Add($"too many Accessories (limit 6) - Asset {item.id}");
					break;
				case Models.Assets.Type.Head:
					head++;
					if (head > 1) invalid.Add($"too many Heads (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.Torso:
					torso++;
					if (torso > 1) invalid.Add($"too many Torso accessories (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.LeftArm:
					leftArm++;
					if (leftArm > 1) invalid.Add($"too many left arms (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.RightArm:
					rightArm++;
					if (rightArm > 1) invalid.Add($"too many right arms (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.LeftLeg:
					leftLeg++;
					if (leftLeg > 1) invalid.Add($"too many left legs (limit 1) - Asset {item.id}");
					break;
				case Models.Assets.Type.RightLeg:
					rightLeg++;
					if (rightLeg > 1) invalid.Add($"too many right legs (limit 1) - Asset {item.id}");
					break;
				default:
					invalid.Add($"bad asset type: {item.assetType} - Asset {item.id}");
					break;
			}
		}

		// send to webhook if any invalid cause people keep complaining and idek what it is so
		// remove this later plz
		if (invalid.Count > 0)
		{
			try
			{
				using var http = new HttpClient();
				var message = $"Bad assets for user {userId}:\n{string.Join("\n", invalid.Take(10))}";
				if (invalid.Count > 10)
				{
					message += $"\n...and {invalid.Count - 10} more errors";
				}

				var payload = new
				{
					content = message
				};
				// try catch cause it wouldn't fucking send the message and i couldn't figure out WHY...
				var json = System.Text.Json.JsonSerializer.Serialize(payload);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				
				var response = await http.PostAsync(Roblox.Configuration.Webhook, content);
				
				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"wh failed, bad status");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"wh exception: {ex}");
			}
			
			return false;
		}

		return true;
	}

    private bool IsColorValid(int color)
    {
        var allColors = Roblox.Models.Avatar.AvatarMetadata.GetColors();
        foreach (var item in allColors)
        {
            if (item.brickColorId == color)
            {
                return true;
            }
        }

        return false;
    }

    public bool AreColorsOk(ColorEntry colors)
    {
        if (!IsColorValid(colors.headColorId)) return false;
        if (!IsColorValid(colors.torsoColorId)) return false;
        if (!IsColorValid(colors.leftArmColorId)) return false;
        if (!IsColorValid(colors.rightArmColorId)) return false;
        if (!IsColorValid(colors.leftLegColorId)) return false;
        if (!IsColorValid(colors.rightLegColorId)) return false;
        return true;
    }

    public string GetAvatarRedLockKey(long userId)
    {
        return $"update avatar web v1 {userId}";
    }

    public async Task RedrawAvatar(long userId, IEnumerable<long>? newAssetIds = null, ColorEntry? colors = null,
        AvatarType? avatarType = null, bool forceRedraw = false, bool ignoreLock = true)
    {
        // required services
        using var assets = ServiceProvider.GetOrCreate<AssetsService>();

        // params
        avatarType ??= AvatarType.R6;

        await using var redLock =
            await Cache.redLock.CreateLockAsync(GetAvatarRedLockKey(userId), TimeSpan.FromSeconds(5));
        if (!redLock.IsAcquired && !ignoreLock) throw new LockNotAcquiredException();

        var assetIds = newAssetIds?.ToList();

        // If list provided is null, then the caller wants us to grab the items ourselves
        assetIds ??= (await GetWornAssets(userId)).ToList();
        colors ??= await GetAvatarColors(userId);

        if (!AreColorsOk(colors))
            throw new RobloxException(400, 0, "Colors are invalid");


        if (assetIds.Count != 0)
        {
            assetIds = (await FilterAssetsForRender(userId, assetIds)).ToList();
        }

        var assetsOk = await ConfirmAssetSelectionIsOkForRender(userId, assetIds);
        if (!assetsOk)
            throw new RobloxException(400, 0, "One or more assets are invalid");
        // Now, update the avatar. This returns a hash
        var avatarHash = await UpdateUserAvatar(userId, colors, assetIds);
        // Get our image urls
        var thumbnailUrl = $"/images/thumbnails/{avatarHash}_thumbnail.png";
        var headshotUrl = $"/images/thumbnails/{avatarHash}_headshot.png";
        if (!forceRedraw)
        {
            // Check if the hash exists already - If they do, we can skip rendering!
            if (File.Exists(Configuration.PublicDirectory + thumbnailUrl) &&
                File.Exists(Configuration.PublicDirectory + headshotUrl))
            {
                // Since both files exist, we can just update the URL and exit
                await UpdateUserAvatarImages(userId, headshotUrl, thumbnailUrl);
                return;
            }
        }

        // We have to call render library now.
        // Set image urls to null:
        await UpdateUserAvatarImages(userId, null, null);
        // Create request
        var extendedAssetDetails = await assets.MultiGetInfoById(assetIds);
        var request = new Roblox.Rendering.AvatarData()
        {
            userId = userId,
            assets = extendedAssetDetails.Select(c => new AvatarAssetEntry()
            {
                id = c.id,
                assetType = new AvatarAssetTypeEntry()
                {
                    id = (int) c.assetType,
                },
            }),
            bodyColors = new AvatarBodyColors()
            {
                headColorId = colors.headColorId,
                torsoColorId = colors.torsoColorId,
                leftArmColorId = colors.leftArmColorId,
                rightArmColorId = colors.rightArmColorId,
                leftLegColorId = colors.leftLegColorId,
                rightLegColorId = colors.rightLegColorId,
            },
            playerAvatarType = "R6",
        };
        // Sane timeout of 30 seconds. If a render takes longer than this, something's probably broken
		 using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter(TimeSpan.FromSeconds(30));
		
		// Run thumbnail first so it shows up in the editor faster
		var thumbnailStream = await CommandHandler.RequestPlayerThumbnail(request, cancellation.Token);
		
		// Write thumb
		await using (var fileStream = File.Create(Configuration.PublicDirectory + thumbnailUrl))
		{
			thumbnailStream.Seek(0, SeekOrigin.Begin);
			await thumbnailStream.CopyToAsync(fileStream);
		}
		
		// Update with only thumbnail, then it will process headshot
		await UpdateUserAvatarImages(userId, null, thumbnailUrl);
		
		var headshotStream = await CommandHandler.RequestPlayerHeadshot(request, cancellation.Token);

		await using (var fileStream = File.Create(Configuration.PublicDirectory + headshotUrl))
		{
			headshotStream.Seek(0, SeekOrigin.Begin);
			await headshotStream.CopyToAsync(fileStream);
		}
		
		// Finally, update the avatar thumbnail
		await UpdateUserAvatarImages(userId, headshotUrl, thumbnailUrl);
	}
	
	public async Task RedrawAvatarR15(long userId, IEnumerable<long>? newAssetIds = null, ColorEntry? colors = null, 
		string? currentThumbnail = null, string? currentHeadshot = null, bool forceRedraw = false, bool ignoreLock = true)
    {
        // required services
        using var assets = ServiceProvider.GetOrCreate<AssetsService>();

        await using var redLock =
            await Cache.redLock.CreateLockAsync(GetAvatarRedLockKey(userId), TimeSpan.FromSeconds(5));
        if (!redLock.IsAcquired && !ignoreLock) throw new LockNotAcquiredException();

        var assetIds = newAssetIds?.ToList();

        // If list provided is null, then the caller wants us to grab the items ourselves
        assetIds ??= (await GetWornAssets(userId)).ToList();
        colors ??= await GetAvatarColors(userId);

        if (!AreColorsOk(colors))
            throw new RobloxException(400, 0, "Colors are invalid");


        if (assetIds.Count != 0)
        {
            assetIds = (await FilterAssetsForRender(userId, assetIds)).ToList();
        }

		var assetsOk = await ConfirmAssetSelectionIsOkForRender(userId, assetIds);
		if (!assetsOk)
		{
			// if fails, set to the old avatar thumbnail
			await UpdateUserAvatarImages(userId, currentHeadshot, currentThumbnail);
			throw new RobloxException(400, 0, "One or more assets are invalid");
		}
        // Now, update the avatar. This returns a hash
        var avatarHash = await UpdateUserAvatar(userId, colors, assetIds);
        // Get our image urls
        var thumbnailUrl = $"/images/thumbnails/{avatarHash}_thumbnail.png";
        var headshotUrl = $"/images/thumbnails/{avatarHash}_headshot.png";
        if (!forceRedraw)
        {
            // Check if the hash exists already - If they do, we can skip rendering!
            if (File.Exists(Configuration.PublicDirectory + thumbnailUrl) &&
                File.Exists(Configuration.PublicDirectory + headshotUrl))
            {
                // Since both files exist, we can just update the URL and exit
                await UpdateUserAvatarImages(userId, headshotUrl, thumbnailUrl);
                return;
            }
        }

        // We have to call render library now.
        // Set image urls to null:
        await UpdateUserAvatarImages(userId, null, null);
        // Create request
        var extendedAssetDetails = await assets.MultiGetInfoById(assetIds);
		// Sane timeout of 30 seconds. If a render takes longer than this, something's probably broken
		using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		var Thumb = CommandHandler.RequestPlayerThumbnailR15(userId, cancellation.Token);
		var Headshot = CommandHandler.RequestPlayerHeadshot(new Roblox.Rendering.AvatarData()
		{
			userId = userId,
			assets = extendedAssetDetails.Select(c => new AvatarAssetEntry()
			{
				id = c.id,
				assetType = new AvatarAssetTypeEntry() { id = (int)c.assetType }
			}),
			bodyColors = new AvatarBodyColors()
			{
				headColorId = colors.headColorId,
				torsoColorId = colors.torsoColorId,
				leftArmColorId = colors.leftArmColorId,
				rightArmColorId = colors.rightArmColorId,
				leftLegColorId = colors.leftLegColorId,
				rightLegColorId = colors.rightLegColorId,
			},
			playerAvatarType = "R6"
		}, cancellation.Token);

		await Task.WhenAll(Thumb, Headshot);

		await using (var fileStream = File.Create(Configuration.PublicDirectory + thumbnailUrl))
		{
			var thumbStream = await Thumb;
			thumbStream.Seek(0, SeekOrigin.Begin);
			await thumbStream.CopyToAsync(fileStream, cancellation.Token);
			await UpdateUserAvatarImages(userId, null, thumbnailUrl);
		}

		await using (var fileStream = File.Create(Configuration.PublicDirectory + headshotUrl))
		{
			var headStream = await Headshot;
			headStream.Seek(0, SeekOrigin.Begin);
			await headStream.CopyToAsync(fileStream, cancellation.Token);
		}

		await UpdateUserAvatarImages(userId, headshotUrl, thumbnailUrl);
	}

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}