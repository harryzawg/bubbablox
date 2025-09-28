using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Web;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Assets;
using Roblox.Exceptions;
using Roblox.Libraries.Assets;
using Roblox.Models.Assets;
using Roblox.Models.Groups;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Catalog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using Roblox.Libraries;
using Type = System.Type;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/")]
public class WebController : ControllerBase
{
    private static ControllerServices staticServices { get; } = new();
    
    static WebController()
    {
        // Init server close tasks
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await staticServices.gameServer.DeleteOldGameServers();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[info] KillOldservers task failed: {0}\n{1}",e.Message,e.StackTrace);
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        });
    }
	
	public class RobloxThumbnailBatchResponse
	{
		public RobloxThumbnailData[] data { get; set; }
	}

	public class RobloxThumbnailData
	{
		public long targetId { get; set; }
		public string state { get; set; }
		public string imageUrl { get; set; }
		public string errorMessage { get; set; }
	}
	
	private async Task<string?> GetRobloxAssetThumbnail(long robloxAssetId)
	{
		try
		{
			var ThumbReq = new[]
			{
				new
				{
					requestId = $"{robloxAssetId}::Asset:420x420:Png:regular:",
					type = "Asset",
					targetId = robloxAssetId,
					token = "",
					format = "Png",
					size = "420x420",
					version = ""
				}
			};

			using var client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.ParseAdd(
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
				"(KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
			);
			client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
			client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

			var response = await client.PostAsync(
				"https://thumbnails.roblox.com/v1/batch",
				new StringContent(JsonConvert.SerializeObject(ThumbReq),
					System.Text.Encoding.UTF8, "application/json")
			);

			if (!response.IsSuccessStatusCode)
			{
				var err = await response.Content.ReadAsStringAsync();
				Console.WriteLine($"thumb API error for {robloxAssetId}: {response.StatusCode} {err}");
				return null;
			}

			var content = await response.Content.ReadAsStringAsync();
			var ThumbnailRes = JsonConvert.DeserializeObject<RobloxThumbnailBatchResponse>(content);

			if (ThumbnailRes?.data == null || ThumbnailRes.data.Length == 0)
				return null;

			var ThumbData = ThumbnailRes.data[0];
			if (ThumbData.state != "Completed" || string.IsNullOrEmpty(ThumbData.imageUrl))
				return null;

			return ThumbData.imageUrl;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to get Roblox thumbnail for {robloxAssetId}: {ex.Message}");
			return null;
		}
	}
	
	[HttpGet("thumbs/asset.ashx")]
	public async Task<RedirectResult> GetAssetThumbnail([Required] long assetId)
	{
		var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);

		if (!authUser18Plus)
		{
			var exists = await services.assets.DoesAssetExist(assetId);

			if (exists)
			{
				var asset18Plus = await services.assets.Is18Plus(assetId);
				if (asset18Plus)
					return new RedirectResult("/img/blocked.png", false);
			}
		}

		var result = (await services.thumbnails.GetAssetThumbnails(new[] { assetId })).ToList();

		if (result.Count == 0 || string.IsNullOrEmpty(result[0].imageUrl))
		{
			var roblox = await GetRobloxAssetThumbnail(assetId);
			if (!string.IsNullOrEmpty(roblox))
				return new RedirectResult(roblox, false);

			return new RedirectResult("/img/placeholder.png", false);
		}

		return new RedirectResult(result[0].imageUrl, false);
	}

    [HttpGet("userads/redirect")]
    public async Task<IActionResult> AdRedirect(string data)
    {
        // please ignore the "url" half of data string, it is legacy and should not be trusted
        var decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data));
        var arr = decoded.Split("|");
        var adId = long.Parse(arr[0]);
        var ad = await services.assets.GetAdvertisementById(adId);
        // if the ad isn't running, don't report it as a click.
        // maybe someone clicked after leaving their computer online overnight or something?
        if (ad.isRunning)
        {
            await services.assets.IncrementAdvertisementClick(ad.id);
        }
        switch (ad.targetType)
        {
            case UserAdvertisementTargetType.Asset:
                var itemData = await services.assets.GetAssetCatalogInfo(ad.targetId);
                var redirectUrl = "/catalog/" + itemData.id + "/" + UrlUtilities.ConvertToSeoName(itemData.name);
                return Redirect(redirectUrl);
            case UserAdvertisementTargetType.Group:
                return Redirect("/My/Groups.aspx?gid=" + ad.targetId);
            default:
                throw new NotImplementedException();
        }
    }

    [HttpGet("/users/favorites/list-json")]
    public async Task<dynamic> GetFavoritesLegacy(long userId, Models.Assets.Type assetTypeId, int pageNumber = 1,
        int itemsPerPage = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (itemsPerPage < 1 || itemsPerPage > 100) itemsPerPage = 10;
        
        // /users/favorites/list-json?assetTypeId=9&itemsPerPage=100&pageNumber=1&userId=3081467602
        var favs = await services.assets.GetFavoritesOfType(userId, assetTypeId, itemsPerPage,
            (itemsPerPage * pageNumber) - itemsPerPage);
        var details = (await services.assets.MultiGetInfoById(favs.Select(c => c.assetId))).ToList();
        var universeStuff =
            await services.games.MultiGetPlaceDetails(details.Where(c => c.assetType == Models.Assets.Type.Place)
                .Select(c => c.id));
        
        return new
        {
            IsValid = true,
            Data = new
            {
                Page = pageNumber,
                ItemsPerPage = itemsPerPage,
                PageType = "favorites",
                Items = details.Select(c =>
                {
                    var details = universeStuff.FirstOrDefault(x => x.placeId == c.id);
                    
                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" :
                                c.itemRestrictions.Contains("Limited") ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.id,
                            UniverseId = details?.universeId,
                            Name = c.name,
                            AbsoluteUrl = "/catalog/" + c.id + "/--",
                            AssetType = (int) c.assetType,
                            AssetCategory = 0,
                            CurrentVersionId = 0,
                            LastUpdated = (string?) null,
                        },
                        Creator = new
                        {
                            Id = c.creatorTargetId,
                            Name = c.creatorName,
                            Type = (int) c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.Group
                                ? "/My/Groups.aspx?gid=" + c.creatorTargetId
                                : "/users/" + c.creatorTargetId + "/profile",
                        },
                        Product = new
                        {
                            PriceInRobux = c.price,
                            PriceInTickets = c.priceTickets,
                            IsForSale = c.isForSale,
                            Is18Plus = c.is18Plus,
                            IsLimited = c.itemRestrictions.Contains("Limited"),
                            IsLimitedUnique = c.itemRestrictions.Contains("LimitedUnique"),
                            IsFree = c.price == 0,
                        },
                    };
                }),
            },
        };
    }

    [HttpGet("users/inventory/list-json")]
    public async Task<dynamic> GetUserInventoryLegacy(long userId, Models.Assets.Type assetTypeId, string? cursor = "",
        int itemsPerPage = 10)
    {
        var count = await services.inventory.CountInventory(userId, assetTypeId);
        if (count == 0)
            return new
            {
                IsValid = true,
                Data = new
                {
                    TotalItems = 0,
                    nextPageCursor = (string?)null,
                    previousPageCursor = (string?)null,
                    PageType = "inventory",
                    Items = Array.Empty<int>(),
                }
            };
        int offset = !string.IsNullOrWhiteSpace(cursor) ? int.Parse(cursor) : 0;
        int limit = itemsPerPage;
        if (limit is > 100 or < 1) limit = 10;

        var canView = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);
        if (!canView)
            return new
            {
                IsValid = false,
                Data = "User does not exist",
            };

        var result = (await services.inventory.GetInventory(userId, assetTypeId, "desc", limit, offset)).ToList();
        var moreAvailable = count > (offset + limit);

        return new
        {
            IsValid = true,
            Data = new
            {
                TotalItems = count,
                Start = 0,
                End = -1,
                Page = ((int) (offset / limit))+1,
                nextPageCursor = moreAvailable ? (offset + limit).ToString() : null,
                previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
                ItemsPerPage = limit,
                PageType = "inventory",
                Items = result.Select(c =>
                {
                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.isLimitedUnique ? "limited-unique" : c.isLimited ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.assetId,
                            UniverseId = (long?) null,
                            Name = c.name,
                            AbsoluteUrl = "/item-item?id=" + c.assetId,
                            AssetType = (int) c.assetTypeId,
                        },
                        Creator = new
                        {
                            Id = c.creatorId,
                            Name = c.creatorName,
                            Type = (int) c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.User
                                ? $"/users/{c.creatorId}/profile"
                                : $"/My/Groups.aspx?gid={c.creatorId}",
                        },
                        Product = new
                        {
                            PriceInRobux = c.originalPrice ?? 0,
                            SerialNumber = c.serialNumber,
                        },
                        PrivateSeller = (object?) null,
                        Thumbnail = new { },
                        UserItem = new { },
                    };
                }),
            },
        };
    }

    [HttpPost("asset/toggle-profile")]
    public async Task<dynamic> AddAssetToProfile([Required, FromBody] AddToProfileCollectionsRequest request)
    {
        var currentCollection = (await services.inventory.GetCollections(safeUserSession.userId)).ToList();
        if (request.addToProfile)
        {
            var ownsItem = await services.users.GetUserAssets(safeUserSession.userId, request.assetId);
            if (!ownsItem.Any())
                return new
                {
                    isValid = false,
                    data = new { },
                    error = "You do not own this item",
                };
            
            if (!currentCollection.Contains(request.assetId))
            {
                await services.inventory.SetCollections(safeUserSession.userId, currentCollection.Prepend(request.assetId).Distinct());
            }   
        }
        else
        {
            currentCollection.RemoveAll(c => c == request.assetId);
            await services.inventory.SetCollections(safeUserSession.userId, currentCollection);
        }

        return new
        {
            isValid = true,
            data = new { },
            error = "",
        };
    }

	[HttpGet("users/profile/robloxcollections-json")]
	public async Task<dynamic> GetUserCollections(long userId)
	{
		if (userId == 1)
		{
			return new { };
		}

		var result = (await services.inventory.GetCollections(userId)).ToList();
		if (result.Count < 1)
		{
			var inventory = await services.inventory.GetInventory(userId, Models.Assets.Type.Hat, "desc", 6, 0);
			result = inventory.Take(6).Select(c => c.assetId).ToList();
		}
		var items = (await services.assets.MultiGetInfoById(result)).ToArray();
		return new
		{
			CollectionsItems = result.Select(id =>
			{
				var c = items.First(i => i.id == id);
				return new
				{
					Id = c.id,
					AssetSeoUrl = $"/item-item?id=" + c.id,
					Name = c.name,
					FormatName = (string?) null,
					Thumbnail = new
					{
						Final = true,
						Url = $"/thumbs/asset.ashx?assetId={c.id}&width=420&height=420&format=png",
						Id = c.id,
					},
					AssetRestrictionIcon = new
					{
						TooltipText = (string?) null,
						CssTag = c.itemRestrictions.Contains("Limited") ? "limited" :
							c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" : null,
						LoadAssetRestrictionIconCss = false,
						HasTooltip = false,
					},
				};
			}),
		};
	}

    [HttpGet("comments/get-json")]
    public async Task<dynamic> GetAssetComments(long assetId, int startIndex)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        var details = (await services.assets.MultiGetAssetDeveloperDetails(new []{assetId})).First();
        if (!details.enableComments)
        {
            return new
            {
                IsUserModerator = false,
                Comments = new List<dynamic>(),
                MaxRows = 10,
                AreCommentsDisabled = true,
            };
        }

        var com = await services.assets.GetComments(assetId, startIndex, 10);
        var isModerator = userSession != null && (await services.users.GetStaffPermissions(userSession.userId))
            .Any(a => a.permission == Access.DeleteComment);
        
        return new
        {
            IsUserModerator = isModerator,
            MaxRows = 10,
            AreCommentsDisabled = false,
            Comments = com.Select(c => new
            {
                Id = c.id,
                PostedDate = c.createdAt.ToString("MMM").Replace(".", "") + c.createdAt.ToString(" dd, yyyy | h:mm ") + c.createdAt.ToString("tt").ToUpper().Replace(".", ""),
                AuthorName = c.username,
                AuthorId = c.userId,
                Text = c.comment,
                ShowAuthorOwnsAsset = false,
                AuthorThumbnail = new
                {
                    AssetId = 0,
                    AssetHash = (string?) null,
                    AssetTypeId = 0,
                    Url = "/Thumbs/avatar.ashx?userId=" + c.userId,
                    IsFinal = true,
                },
            })
        };
    }

    [HttpPost("comments/post")]
    public async Task<dynamic> AddComment([Required, FromBody] AddCommentRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        try
        {
            await services.assets.AddComment(request.assetId, userSession.userId, request.text);
            return new
            {
                ErrorCode = (string?)null,
            };
        }
        catch (ArgumentException e)
        {
            return new
            {
                ErrorCode = e.Message,
            };
        }
    }
	
	[HttpGet("game/get-join-script")]
	public async Task<string> GetJoinScript(long placeId)
	{
		FeatureFlags.FeatureCheck(FeatureFlag.GameJoinEnabled);
		
		if (!await services.games.IsPlayable(placeId))
		{
			throw new BadRequestException(400, "You can not access this place at this time.");
		}

		var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
		if (placeInfo.assetType != Models.Assets.Type.Place)
			throw new BadRequestException();

		var modInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] { placeId })).First();
		if (modInfo.moderationStatus != ModerationStatus.ReviewApproved)
			throw new BadRequestException();

		string baselink = Roblox.Configuration.BaseUrl;
		string auth = $"{baselink}/Login/Negotiate.ashx";
		string ticket = Request.Cookies[".ROBLOSECURITY"];

		int? year = await services.games.GetPlaceYear(placeId);
		
		string PL = $"{baselink}/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}";
		if (year.HasValue)
		{
			PL += $"&{year.Value}=true";
		}

		string args = $"-a \"{auth}\" -j \"{PL}\" -t \"{ticket}\"";

		return args;
	}

    [HttpGet("usercheck/show-tos")]
    public dynamic GetIsTosCheckRequired()
    {
        return new
        {
            success = true,
        };
    }

	[HttpGet("games/getgameinstancesjson")]
	public async Task<dynamic> GetGameServers(long placeId, int startIndex)
	{
		var limit = 10;
		var offset = startIndex;
		var servers = (await services.gameServer.GetGameServers(placeId, offset, limit)).ToList();
		var details = (await services.games.MultiGetPlaceDetails(new []{placeId})).First();
		var random = new Random();

		return new
		{
			PlaceId = placeId,
			ShowShutdownAllButton = false, // todo: enable if user has perms
			Collection = servers.Select(c =>
			{
				var players = c.players.ToList();
				return new
				{
					Capacity = details.maxPlayerCount,
					Ping = random.Next(50, 106),
					Fps = 60, // todo
					ShowSlowGameMessage = false, // todo
					UserCanJoin = true, // todo: false if vip server
					ShowShutdownButton = false, // todo: true if vip server player owns or user has perms
					JoinScript = (string?) null, // todo
					FriendsMouseover = "",
					FriendsDescription = "",
					PlayersCapacity = $"{players.Count} of {details.maxPlayerCount}",
					RobloxAppJoinScript = "", // todo
					CurrentPlayers = players.Select(c => new
					{
						Id = c.userId,
						Username = c.username,
						Thumbnail = new
						{
							IsFinal = true,
							Url = "/Thumbs/Avatar-Headshot.ashx?userid=" + c.userId,
						},
					}),
				};
			}),
			TotalCollectionSize = servers.Count,
		};
	}

    [HttpGet("search/users/results")]
    public async Task<dynamic> SearchUsersJson(string? keyword = null, int offset = 0, int limit = 10)
    {
        if (limit is > 100 or < 1)
            limit = 10;
        if ((offset / limit) > 1000)
            offset = 0;

        var result = (await services.users.SearchUsers(keyword, limit, offset)).ToArray();
        if (result.Length == 0)
            return new
            {
                Keyword = keyword,
                StartIndex = offset,
                MaxRows = limit,
                TotalResults = 0,
                UserSearchResults = Array.Empty<int>(),
            };
        // No DB pagination yet, it's just too expensive to be worth it right now
        var userInfo = await services.users.MultiGetUsersById(result.Skip(offset).Take(limit).Select(c => c.userId));
        return new
        {
            Keyword = keyword,
            StartIndex = offset,
            MaxRows = limit,
            TotalResults = result.Length,
            UserSearchResults = userInfo.Select(c => new
            {
                UserId = c.id,
                Name = c.name,
                DisplayName = c.displayName,
                Blurb = "",
                PreviousUserNamesCsv = "",
                IsOnline = false,
                LastLocation = (string?) null,
                UserProfilePageUrl = "/users/" + c.id + "/profile",
                LastSeenDate = (string?) null,
                PrimaryGroup = "",
                PrimaryGroupUrl = "",
            }),
        };
    }

    private static readonly List<Models.Assets.Type> AllowedAssetTypes = new()
    {
        Models.Assets.Type.Audio,
        Models.Assets.Type.TeeShirt,
        Models.Assets.Type.Shirt,
        Models.Assets.Type.Pants,
        Models.Assets.Type.Image,
		Models.Assets.Type.Mesh,
		Models.Assets.Type.Badge,
		Models.Assets.Type.GamePass,
    };

    private static int pendingAssetUploads { get; set; } = 0;
    private static readonly Mutex pendingAssetUploadsMux = new();

	[HttpPost("develop/upload-version")]
	public async Task UploadVersion([Required, FromForm] UploadAssetVersionRequest request)
	{
		var info = await services.assets.GetAssetCatalogInfo(request.assetId);
		var canUpload = await services.assets.CanUserModifyItem(info.id, safeUserSession.userId);

		// you can only upload place files right now
		if (info.assetType != Models.Assets.Type.Place)
		{
			canUpload = false;
		}

		if (canUpload == false)
			throw new RobloxException(403, 0, "Unauthorized");
/* 
		lock (pendingAssetUploadsMux)
		{
			if (pendingAssetUploads >= 2)
				throw new RobloxException(429, 0, "TooManyRequests");
			pendingAssetUploads++;
		} */

		try
		{
			var fs = request.file.OpenReadStream();
			if (!await services.assets.ValidateAssetFile(fs, Models.Assets.Type.Place))
				throw new RobloxException(400, 0, "The asset file doesn't look correct. Please try again.");
			fs.Position = 0;
			
			// This was for 2016 but the api key is a lot more reliable
/* 			var rbxlpath = Path.Combine(Configuration.RccServicePath, "content", $"{request.assetId}.rbxl");
			await using (var fileStream = new FileStream(rbxlpath, FileMode.Create, FileAccess.Write))
			{
				await fs.CopyToAsync(fileStream);
			}
			fs.Position = 0; */

			await services.assets.CreateAssetVersion(request.assetId, safeUserSession.userId, fs);
			// wait before re-rendering just in case it hasn't updated the RBXL yet
			_ = Task.Run(async () =>
			{
				await Task.Delay(5000);
				services.assets.RenderAsset(request.assetId, info.assetType);
			});
		}
		finally
		{
			lock (pendingAssetUploadsMux)
			{
				pendingAssetUploads--;
			}
		}
	}
	
	[HttpPost("develop/upload-icon")]
	public async Task<IActionResult> UploadGameIcon([Required] long placeId, [Required] IFormFile file)
	{
		var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
		if (placeInfo.assetType != Models.Assets.Type.Place)
			throw new BadRequestException(0, "Only places can have icons");

		await services.assets.ValidatePermissions(placeId, safeUserSession.userId);

		var stream = file.OpenReadStream();
		var pictureData = await services.assets.ValidateImage(stream);
		if (pictureData == null)
			throw new BadRequestException(0, "Invalid image file");

		stream.Position = 0;
		await services.assets.CreateGameIcon(placeId, stream);
		
		return Ok(new
		{
			success = true
		});
	}
    
    [HttpPost("develop/upload")]
    public async Task<CreateResponse> UploadItem([Required, FromForm] UploadAssetRequestBadges request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
        if (!AllowedAssetTypes.Contains(request.assetType) || userSession == null) throw new BadRequestException();
        // flood check Start
        // 1 attempt every 5 seconds per user
        await services.cooldown.CooldownCheck("Develop:Upload:StartUserId:" + userSession.userId, TimeSpan.FromSeconds(5));
        // IP flood check too! same limit as userId for now
        await services.cooldown.CooldownCheck("Develop:Upload:StartIp:" + GetIP(), TimeSpan.FromSeconds(5));
        
        var isClothing =
            request.assetType is Models.Assets.Type.Shirt or Models.Assets.Type.Pants or Models.Assets.Type.TeeShirt;
		var isAudio = request.assetType is Models.Assets.Type.Audio;
		var isImage = request.assetType is Models.Assets.Type.Image;
		var isMesh = request.assetType is Models.Assets.Type.Mesh;
		var isBadge = request.assetType is Models.Assets.Type.Badge;
		var isGamePass = request.assetType is Models.Assets.Type.GamePass;

		if (!isClothing && !isAudio && !isImage && !isMesh && !isBadge && !isGamePass)
			throw new RobloxException(400, 0, "Endpoint does not support this assetType: " + request.assetType);
        
        // Limit of 50 assets globally pending approval before failure
        var pendingAssets = await services.assets.CountAssetsPendingApproval();
        if (pendingAssets >= 50)
        {
            Metrics.UserMetrics.ReportGlobalPendingAssetsFloodCheckReached(userSession.userId);
            throw new RobloxException(400, 0, "There are too many pending items. Try again in a few minutes.");
        }
        
        var groupId = request.groupId == null ? 0 : request.groupId.Value;
        var creatorType = groupId == 0 ? CreatorType.User : CreatorType.Group;
        var creatorId = creatorType == CreatorType.User ? userSession.userId : groupId;
        // check perms
        if (creatorType == CreatorType.Group)
        {
            var hasPermission = await services.groups.DoesUserHavePermission(userSession.userId, groupId,
                GroupPermission.CreateItems);
            if (!hasPermission)
                throw new RobloxException(401, 0, "Unauthorized");
        }
        
        // Limit of 10 pending assets per user/group
        if (groupId == 0)
        {
            var myPendingItems =
                await services.assets.CountAssetsByCreatorPendingApproval(userSession.userId, CreatorType.User);
            if (myPendingItems >= 20)
            {
                Metrics.UserMetrics.ReportPendingAssetsFloodCheckReached(userSession.userId);
                throw new RobloxException(409, 0,
                    "You have uploaded too many items in a short period of time. Wait a few minutes and try again.");
            }
        }
        else
        {
            var myPendingItems =
                await services.assets.CountAssetsByCreatorPendingApproval(groupId, CreatorType.Group);
            if (myPendingItems >= 20)
            {
                Metrics.UserMetrics.ReportPendingAssetsFloodCheckReached(userSession.userId);
                throw new RobloxException(409, 0, "You have uploaded too many items in a short period of time. Wait a few minutes and try again.");
            }
        }
        // Global max of 5 pending asset uploads. To prevent people spamming stuff from a million IPs and accounts.
        // Note that this is not distributed right now, it's just local per server.
        lock (pendingAssetUploadsMux)
        {
            if (pendingAssetUploads >= 8)
            {
                Metrics.UserMetrics.ReportGlobalUploadsFloodcheckReached(userSession.userId);
                throw new RobloxException(409, 0, "There are too many pending assets at this time. Try again in a few minutes.");
            }
            pendingAssetUploads++;
        }

		try
		{
			if (isClothing)
			{
				var stream = request.file.OpenReadStream();
				var imager = await Imager.ReadAsync(stream);
				stream.Position = 0;
				
				if (imager == null)
					throw new BadRequestException(0, "Could not find image service, please contact me on Discord!");

				// convert to PNG cause RCC is stupid and hates other file formats
				if (imager.imageFormat != ImagerFormat.PNG)
				{
					using var image = await Image.LoadAsync(stream);
					var pngstr = new MemoryStream();
					await image.SaveAsPngAsync(pngstr);
					pngstr.Position = 0;
					stream = pngstr;
					
					imager = await Imager.ReadAsync(stream);
					stream.Position = 0;
				}
				
				await CheckForAudios(stream, request.file.FileName, userSession.userId);

				var clothingValidation = await services.assets.ValidateClothing(stream, request.assetType);
				if (clothingValidation == null)
					throw new BadRequestException(0, "Invalid image file");

				// create the texture
				var imageAsset = await services.assets.CreateAsset(request.file.FileName, request.assetType + " Image",
					userSession.userId, creatorType, creatorId, stream, Models.Assets.Type.Image,
					Genre.All,
					ModerationStatus.AwaitingApproval);

				stream.Position = 0;
				
				await services.assets.InsertOrUpdateAssetVersionMetadataImage(
					imageAsset.assetVersionId, 
					(int)stream.Length,
					imager.width,
					imager.height,
					imager.imageFormat,
					await services.assets.GenerateImageHash(stream));
				
				// create the asset
				var asset = await services.assets.CreateAsset(request.name, null, userSession.userId, creatorType, creatorId, null, 
					request.assetType, Genre.All, imageAsset.moderationStatus, default,
					default, default, default, imageAsset.assetId);
				
				// give asset to user
				await services.users.CreateUserAsset(userSession.userId, asset.assetId);
				return asset;
			}
            else if (isImage)
            {
                var stream = request.file.OpenReadStream();
				await CheckForAudios(stream, request.file.FileName, userSession.userId);
                var pictureData = await services.assets.ValidateImage(stream);
                if (pictureData == null)
                    throw new BadRequestException(0, "Invalid image file");
                stream.Position = 0;
                // create the texture
                var imageAsset = await services.assets.CreateAsset(request.name, "Image",
                    userSession.userId, creatorType, creatorId, stream, Models.Assets.Type.Image,
                    Genre.All,
                    ModerationStatus.AwaitingApproval);
                stream.Position = 0;
                await services.assets.InsertOrUpdateAssetVersionMetadataImage(imageAsset.assetVersionId, (int)stream.Length,
                    pictureData.width, pictureData.height, pictureData.imageFormat,
                    await services.assets.GenerateImageHash(stream));
               
                return imageAsset;
            }
            else if (isAudio)
            {
                // check if has enough
                var balance = await services.economy.GetBalance(creatorType, creatorId);
                if (balance.robux < 25)
                    throw new BadRequestException(0, "Not enough Robux for purchase");
                // validate auto
                var stream = request.file.OpenReadStream();
                var ok = await services.assets.IsAudioValid(stream);
                if (ok != AudioValidation.Ok)
                {
                    throw new BadRequestException(0, "Bad audio file. Error = " + ok.ToString());
                }
                // charge
                await services.economy.ChargeForAudioUpload(creatorType, creatorId);
                stream.Position = 0;
                // create item
                var asset = await services.assets.CreateAsset(request.name, null, userSession.userId, CreatorType.User,
                    userSession.userId, stream, Models.Assets.Type.Audio, Genre.All, ModerationStatus.AwaitingApproval);
                return asset;
            }
			else if (isMesh)
			{
				var file = Path.GetExtension(request.file.FileName).ToLower();
				if (file != ".mesh")
				{
					throw new BadRequestException(0, "You can only upload .mesh files");
				}
							
				var stream = request.file.OpenReadStream();
				
				// validate mesh
				var isValid = await services.assets.ValidateMeshFile(stream);
				if (!isValid)
					throw new BadRequestException(0, "Invalid mesh file");
				
				stream.Position = 0;
				// create
				var asset = await services.assets.CreateAsset(
					request.name, 
					null, 
					userSession.userId, 
					creatorType, 
					creatorId, 
					stream, 
					Models.Assets.Type.Mesh, 
					Genre.All, 
					ModerationStatus.AwaitingApproval);
				
				// give
				await services.users.CreateUserAsset(userSession.userId, asset.assetId);

				// generate OBJ so admins can see a preview
				try 
				{
					Console.WriteLine("generating OBJ...");
					var version = await services.assets.GetLatestAssetVersion(asset.assetId);
					var content = Path.Combine(Configuration.AssetDirectory, version.contentUrl);
					var obj = Path.ChangeExtension(content, ".obj");
					
					var OBJProcess = new ProcessStartInfo
					{
						FileName = Path.Combine(Configuration.PublicDirectory, "OBJToRBXMeshOBJ.exe"),
						Arguments = $"\"{content}\" -obj",
						WorkingDirectory = Configuration.AssetDirectory,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					};
					
					using (var process = Process.Start(OBJProcess))
					{
						process.StartInfo.RedirectStandardOutput = true;
						process.StartInfo.RedirectStandardError = true;
						
						await process.WaitForExitAsync();
						
						var output = await process.StandardOutput.ReadToEndAsync();
						var error = await process.StandardError.ReadToEndAsync();
						
						if (process.ExitCode != 0 || !System.IO.File.Exists(obj))
						{
							Console.WriteLine($"could not generate OBJ for mesh {asset.assetId}, exit code: {process.ExitCode}");
							Console.WriteLine($"out: {output}");
							Console.WriteLine($"error: {error}");
						}
						else
						{
							Console.WriteLine($"generated OBJ for mesh {asset.assetId}");
							//Console.WriteLine($"out: {output}");
						}
					}
				}
				catch (Exception e)
				{
					Console.WriteLine($"error generating OBJ for mesh {asset.assetId}: {e}");
				}

				return asset;
			}
			else if (isBadge || isGamePass)
			{
				var stream = request.file.OpenReadStream();
				var pictureData = await services.assets.ValidateImage(stream);
				if (pictureData == null)
					throw new BadRequestException(0, "Invalid image file");
				stream.Position = 0;

				var Image = await services.assets.CreateAsset(
					request.name + " Image", 
					"Thumbnail",
					userSession.userId, 
					creatorType, 
					creatorId, 
					stream, 
					Models.Assets.Type.Image,
					Genre.All,
					ModerationStatus.AwaitingApproval,
					skipHashCheck: true);
				
				stream.Position = 0;
				await services.assets.InsertOrUpdateAssetVersionMetadataImage(
					Image.assetVersionId, 
					(int)stream.Length,
					pictureData.width, 
					pictureData.height, 
					pictureData.imageFormat,
					await services.assets.GenerateImageHash(stream));
				
				var asset = await services.assets.CreateAsset(
					request.name, 
					request.description,
					userSession.userId, 
					creatorType, 
					creatorId, 
					null, 
					request.assetType, 
					Genre.All, 
					ModerationStatus.AwaitingApproval,
					default,
					default,
					default,
					default,
					Image.assetId);

				await services.users.CreateUserAsset(userSession.userId, asset.assetId);
				
				if (isBadge)
				{
					if (request.placeId == null)
						throw new BadRequestException(0, "Badges must have a placeId");
					
					await services.assets.InsertBadge(asset.assetId, request.placeId.Value, userSession.userId);
				}
				else if (isGamePass)
				{
					if (request.placeId == null)
						throw new BadRequestException(0, "Passes must have a placeId");
					
					await services.assets.InsertPass(asset.assetId, request.placeId.Value, userSession.userId);
				}
				
				return asset;
			}

            throw new BadRequestException(0, "Invalid asset type");
        }
        finally
        {
            lock (pendingAssetUploadsMux)
            {
                pendingAssetUploads--;
            }
        }
    }
	async Task CheckForAudios(Stream stream, string File, long userId)
	{
		stream.Position = 0;

		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms);
		ms.Position = 0;

		using var httpClient = new HttpClient();
		using var content = new MultipartFormDataContent();
		content.Add(new StreamContent(ms), "file", File);

		var response = await httpClient.PostAsync("http://localhost:3030/validateImage", content);

		stream.Position = 0;

		if (!response.IsSuccessStatusCode)
		{
			// if we're here, the py script returned BadRequest so that indicates the image has some audio embedded
			if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
			{
				// start a task to ban the yser
				_ = Task.Run(async () =>
				{
					try
					{
						var Users = new Roblox.Services.UsersService();
						await Users.BanForBypass(userId);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to ban user for audio bypass: {0}", ex.Message);
					}
				});
				// if response bad, throw vague error as well
				throw new BadRequestException(0, "Image service unavailable");
			}
			// if we're here, that means the image format or extension is just bad
			else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
			{
				throw new BadRequestException(0, "Invalid image format");
			}
			else
			{
				throw new BadRequestException(0, "Image validator unavailable, please try again later");
			}
		}
	}
}