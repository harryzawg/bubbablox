// This controller is special: It directly accesses DB/Redis for ease of use.
// Admin features do not have to handle load like other controllers do - it's unlikely even two people will be using
// this controller at any given time.
// Features should be easy to add and easy to remove. All that really matters is ease of writing and security - speed,
// best practices, etc, do not matter.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Data;
using Newtonsoft.Json;
using Dapper;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Roblox.Cache;
using Roblox.Dto;
using Roblox.Dto.Admin;
using Roblox.Dto.Assets;
using Roblox.Dto.Avatar;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Staff;
using Roblox.Dto.Users;
using Roblox.Dto.Tickets;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Avatar;
using Roblox.Models.Db;
using Roblox.Models.Economy;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Trades;
using Roblox.Models.Users;
using Roblox.Models.Promocodes;
using Roblox.Models.Thumbnails;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Asset;
using Roblox.Website.WebsiteModels.Admin.MigrateUGC;
using Roblox.Website.WebsiteModels.Admin.Thumbnails;
using Roblox.Website.WebsiteModels.Admin.Verified;
using Type = Roblox.Models.Assets.Type;

// ReSharper disable InconsistentNaming

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/admin-api/api/")]
#if RELEASE
[ApiExplorerSettings(IgnoreApi = true)]
#endif
public class AdminApiController : ControllerBase
{
    private NpgsqlConnection db => services.assets.db;
    private DistributedCache redis => Roblox.Services.Cache.distributed;
    private static readonly long startTime = DateTimeOffset.Now.ToUnixTimeSeconds();

    private static string? adminBundleJs { get; set; }
    private static string? adminBundleCss { get; set; }
    private static string? adminBundleHtml { get; set; }
    private static readonly Mutex adminStaticMux = new();
    private static readonly string adminRandomUrlPart = Guid.NewGuid().ToString();

    private bool IsLoggedIn()
    {
        return base.userSession != null;
    }

    private new UserSession userSession
    {
        get
        {
            if (base.userSession == null)
                throw new StaffException("Not logged in");
            return base.userSession!;
        }
    }

    [HttpGet("/admin/build-redirect/bundle.js")]
    public IActionResult GetAdminBuildJs()
    {
        return new RedirectResult("/admin/build/" + adminRandomUrlPart + "/bundle.js");
    }

    [HttpGet("/admin/build/{part}/bundle.js")]
    public async Task<IActionResult> GetAdminBundleJsReal()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
		adminBundleJs = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/build/bundle.js");
        return Content(adminBundleJs, "application/javascript");
    }

    [HttpGet("/admin/build-redirect/bundle.css")]
    public IActionResult GetAdminBundleCss()
    {
        return new RedirectResult("/admin/build/" + adminRandomUrlPart + "/bundle.css");
    }

    [HttpGet("/admin/build/{part}/bundle.css")]
    public async Task<IActionResult> GetAdminBundleCssReal()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
		adminBundleCss = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/build/bundle.css");
        return Content(adminBundleCss, "text/css");
    }

    // Wildcards are not easily supported... https://stackoverflow.com/questions/51973631/wildcard-in-route-attribute-for-webapi?rq=1
    [HttpGet("/admin/"), HttpGet("/admin/{one}"), HttpGet("/admin/{one}/{two}"), HttpGet("/admin/{one}/{two}/{three}"), HttpGet("/admin/{one}/{two}/{three}/{four}"), HttpGet("/admin/{one}/{two}/{three}/{four}/{five}"), HttpGet("/admin/{one}/{two}/{three}/{four}/{five}/{six}")]
    public async Task<IActionResult> GetAdminView()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
		adminBundleHtml = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/index.html");
        return Content(adminBundleHtml, "text/html");
    }

    [HttpGet("permissions")]
    public async Task<dynamic> GetPermissions()
    {
        var isOwner = StaffFilter.IsOwner(userSession.userId);
        var permissions = await services.users.GetStaffPermissions(userSession.userId);
        var isAdmin = isOwner;
        var isMod = isAdmin;
        return new
        {
            rank = new
            {
                name = isOwner ? "Owner" : isAdmin ? "admin" : isMod ? "Mod" : null,
                details = new
                {
                    isAdmin,
                    isModerator = isMod,
                    isOwner,
                },
                permissions = isOwner
                    ? Enum.GetValues<Access>()
                    : permissions.Select(c => c.permission),
            }
        };
    }

    [HttpGet("staff/list"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<UserId>> GetAllStaff()
    {
        return await services.users.GetAllStaff();
    }
    
    [HttpGet("staff/permissions/list"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<Access>> GetAllPermissions()
    {
        return Enum.GetValues<Access>();
    }

    [HttpGet("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<StaffUserPermissionEntry>> GetUserPermissions(long userId)
    {
        return await services.users.GetStaffPermissions(userId);
    }

    [HttpPost("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task SetUserPermissions(long userId, Access permission)
    {
/*         if (!StaffFilter.IsOwner(userSession.userId))
            throw new Exception("InternalServerError"); */

        await services.users.AddStaffPermission(userId, permission);
    }

    [HttpDelete("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task DeletePermission(long userId, Access permission)
    {
        await services.users.RemoveStaffPermission(userId, permission);
    }

    [HttpGet("stats"), StaffFilter(Access.GetStats)]
    public dynamic GetStatus()
    {
        using var proc = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();
        var allocatedMem = proc.WorkingSet64;
        var memoryInUse = gcInfo.HeapSizeBytes;
        return new
        {
            memory = new
            {
                allocated = (allocatedMem / 1024 / 1024) + " KB",
                used = (memoryInUse / 1024 / 1024) + " KB",
            },
            serverStartTime = startTime,
        };
    }

    [HttpGet("alert"), StaffFilter(Access.GetAlert)]
    public async Task<dynamic> GetSystemMessage()
    {
        var msg = await services.users.GetGlobalAlert();
        return new
        {
            LinkText = "",
            LinkUrl = msg?.url ?? "",
            Text = msg?.message ?? "",
            IsVisible = msg != null,
        };
    }

    [HttpPost("alert"), StaffFilter(Access.SetAlert)]
    public async Task SetAlert([Required, FromBody] SetAlertRequest request)
    {
        if (request.text == "") request.text = null;
        if (request.text is { Length: > 255 })
            throw new StaffException("Text is over the limit of 255 characters");
        if (request.url is {Length: > 255})
            throw new StaffException("URL is over 255 characters");
        if (string.IsNullOrWhiteSpace(request.url))
            request.url = null;
        
        // why the FUCK did floatzel add this shit?

        //if (request.url != null)
        //{
            //var url = new Uri(request.url);
            //if (!url.Host.EndsWith(".example.com") && url.Host != "example.com")
            //    throw new StaffException("All URLs must link to example.com. Base was " + url.Host);
        //}
        Writer.Info(LogGroup.AbuseDetection, "User {0} is setting alert to '{1}'", userSession.userId, request.text);
        await services.users.SetGlobalAlert(request.text, request.url);
        await db.ExecuteAsync("INSERT INTO moderation_set_alert (actor_id, alert, alert_url) VALUES (:user_id, :text, :url)", new
        {
            user_id = userSession.userId,
            text = request.text,
            url = request.url,
        });
    }

    [HttpPost("create-user"), StaffFilter(Access.CreateUser)]
    public async Task<dynamic> CreateUser([Required, FromBody] CreateUserRequest req)
    {
        if (req.username == null)
            throw new StaffException("Bad username");
        if (req.password == null)
            throw new StaffException("Bad password");
        
        return await services.users.CreateUser(req.username, req.password, Gender.Male, req.userId);
    }
	
	[HttpPost("promocodes/create"), StaffFilter(Access.GiveUserItem)]
	public async Task<dynamic> CreatePromoCode([Required, FromBody] CreatePCReq request)
	{
		if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length < 4 || request.Code.Length > 50)
			throw new StaffException("Code must be between 4-50 characters");
		
		if (request.AssetId == null && request.Robux == null)
			throw new StaffException("Must specify either AssetId or Robux");
		
		if (request.AssetId != null)
		{
			try
			{
				await services.assets.GetAssetCatalogInfo(request.AssetId.Value);
			}
			catch (RecordNotFoundException)
			{
				throw new StaffException("Asset does not exist");
			}
		}
		
		if (request.Robux is < 0 or > 1000000)
			throw new StaffException("Robux amount must be between 0-1,000,000");

		if (request.MaxUses is < 1 or > 1000000)
			throw new StaffException("Max uses must be between 1-1,000,000");

		var existing = await db.QuerySingleOrDefaultAsync<Total>(
			"SELECT COUNT(*) as total FROM promocodes WHERE code = :code",
			new { code = request.Code.ToUpper() });
		
		if (existing.total > 0)
			throw new StaffException("Promocode already exists");

		DateTime? expiresAt = null;
		if (request.ExpiresInSeconds != null)
		{
			expiresAt = DateTime.UtcNow.AddSeconds(request.ExpiresInSeconds.Value);
		}
		else if (request.ExpiresInMinutes != null)
		{
			expiresAt = DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes.Value);
		}
		else if (request.ExpiresInHours != null)
		{
			expiresAt = DateTime.UtcNow.AddHours(request.ExpiresInHours.Value);
		}
		else if (request.ExpiresInDays != null)
		{
			expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays.Value);
		}

		var id = await db.ExecuteScalarAsync<int>(
			@"INSERT INTO promocodes 
			  (code, asset_id, robux, expires_at, maxuses, active) 
			  VALUES 
			  (@code, @asset_id, @robux, @expires_at, @maxuses, @active) 
			  RETURNING id",
			new
			{
				code = request.Code.ToUpper(),
				asset_id = request.AssetId,
				robux = request.Robux,
				expires_at = expiresAt,
				maxuses = request.MaxUses,
				active = request.IsActive
			});

		return new
		{
			id,
			message = "Promocode created successfully!"
		};
	}

	[HttpGet("promocodes/list"), StaffFilter(Access.GiveUserItem)]
	public async Task<IEnumerable<PCEntry>> ListPromoCodes(int limit = 100, int offset = 0)
	{
		return await db.QueryAsync<PCEntry>(
			"SELECT * FROM promocodes ORDER BY id LIMIT :limit OFFSET :offset",
			new { limit, offset });
	}

	[HttpGet("promocodes/redemptions"), StaffFilter(Access.GiveUserItem)]
	public async Task<IEnumerable<PCREntry>> GetRedemptions(int promocodeID, int limit = 1000000, int offset = 0)
	{
		return await db.QueryAsync<PCREntry>(
			@"SELECT pr.*, u.username 
			  FROM promocode_redemptions pr
			  LEFT JOIN ""user"" u ON u.id = pr.user_id
			  WHERE pr.promocode = :promocodeID
			  ORDER BY pr.redeemed_at DESC
			  LIMIT :limit OFFSET :offset",
			new { promocodeID, limit, offset });
	}

	[HttpPost("promocodes/toggle-active"), StaffFilter(Access.GiveUserItem)]
	public async Task<dynamic> TogglePromoCodeActive([FromBody] TogPC request)
	{
		var affected = await db.ExecuteAsync(
			"UPDATE promocodes SET active = @active WHERE id = @promocodeID",
			new { 
				promocodeID = request.promocodeID, 
				active = request.isActive 
			});

		if (affected == 0)
		{
			throw new StaffException("Promocode not found");
		}

		return new
		{
			success = true,
			message = $"Promocode {request.promocodeID} updated to {(request.isActive ? "active" : "inactive")}"
		};
	}
	
	// do npgsql shit here cause it couldn't delete it (even when in a transaction??)
	[HttpDelete("promocodes/delete"), StaffFilter(Access.GiveUserItem)]
	public async Task<dynamic> DeletePromoCode(
		[FromBody] DeletePC request,
		[FromServices] NpgsqlConnection db)
	{
		if (request.promocodeID <= 0)
		{
			throw new StaffException("Invalid promocode");
		}

		try
		{
			var existing = await db.QuerySingleOrDefaultAsync<PCEntry>(
				"SELECT * FROM promocodes WHERE id = @id",
				new { id = request.promocodeID });

			if (existing == null)
			{
				throw new StaffException("Promocode not found");
			}

			using var transaction = await db.BeginTransactionAsync();
			try
			{
				await db.ExecuteAsync(
					"DELETE FROM promocode_redemptions WHERE promocode = @id",
					new { id = request.promocodeID },
					transaction);

				await db.ExecuteAsync(
					"DELETE FROM promocodes WHERE id = @id",
					new { id = request.promocodeID },
					transaction);

				await transaction.CommitAsync();

				return new
				{
					success = true,
					message = "Promocode deleted successfully!"
				};
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				throw new StaffException("Failed to delete promocode: " + ex.Message);
			}
		}
		catch (Exception ex)
		{
			throw new StaffException("An error occurred while deleting the promocode: " + ex.Message);
		}
		finally
		{
		}
	}

    [HttpPost("force-application"), StaffFilter(Access.ForceApplication)]
    public async Task<dynamic> ForceApplication([Required, FromBody] ForceApplicationReq req)
    {
        if (req.socialURL == null)
            throw new StaffException("Bad Social URL");

        // Check for invite/current application and delete them if they are found

        var inviteId = services.users.GetUserInvite(req.userId);

        // Delete Invite if the user has one        
        
        if (inviteId != null)
            await services.users.DeleteUserInvite(req.userId);

        // Create the application

        var id = await services.users.CreateApplication(new CreateUserApplicationRequest()
            {
                about = "Forced Application",
                socialPresence = req.socialURL,
                isVerified = true,
                verifiedUrl = req.socialURL,
                verificationPhrase = "Forced Application",
                verifiedId = "1",
            });

        // get the join id

        var joinId = await services.users.ProcessApplication(id, 1, UserApplicationStatus.Approved);

        // Finally, apply the application to the account.
        await services.users.SetApplicationUserIdByJoinId(joinId, req.userId);

        return "Join application added to user";
    }

    [HttpGet("groups/pending-icons"), StaffFilter(Access.GetPendingGroupIcons)]
    public async Task<dynamic> GetPendingIcons()
    {
        var result = await db.QueryAsync("SELECT group_icon.*, u.username as creatorName FROM group_icon INNER JOIN \"user\" u ON u.id = group_icon.user_id WHERE is_approved = 0 ORDER BY group_id");
        foreach (var item in result)
        {
            item.name = Configuration.CdnBaseUrl + "/images/groups/" + item.name;
        }

        return result;
    }

    [HttpGet("asset/moderation-details"), StaffFilter(Access.GetAssetModerationDetails)]
    public async Task<dynamic> GetModerationDetails(long assetId)
    {
        var item = await db.QuerySingleOrDefaultAsync(
            "SELECT asset.id, asset.name, asset_thumbnail.content_url FROM asset LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id WHERE asset.id = :id",
            new
            {
                id = assetId,
            });
        var avDetails = await services.assets.GetLatestAssetVersion(assetId);
        var names = await services.users.GetUserById(avDetails.creatorId);
        item.creatorName = names.username;
        if (item.content_url != null)
            item.content_url = Configuration.CdnBaseUrl + "/images/thumbnails/" + item.content_url + ".png";
        return item;
    }

    [HttpGet("assets/get-asset-stream"), StaffFilter(Access.GetPendingModerationItems)]
    public async Task<Stream> GetPendingAssetStream(long assetId)
    {
        var isPending = await services.assets.GetAssetCatalogInfo(assetId);
        if (isPending.moderationStatus != ModerationStatus.AwaitingApproval && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Item is not pending: " + isPending.moderationStatus);
        var version = await services.assets.GetLatestAssetVersion(assetId);
        if (version.contentUrl != null)
            return await services.assets.GetAudioContentAsWav(assetId, version.contentUrl);
        
        throw new StaffException("Unsupported action");
    }
	
	[HttpGet("assets/pending-assets/image/{contentUrl}")]
	public async Task<IActionResult> GetPendingImage(string contentUrl)
	{
		// for security as anyone could do path traversal
		if (contentUrl.Contains("..") || Path.IsPathRooted(contentUrl))
		{
			return BadRequest("Could not find image");
		}
		
		if (string.IsNullOrWhiteSpace(contentUrl))
		{
			Console.WriteLine("Content URL is null/empty");
			return NotFound();
		}

		var file = Path.Combine(Configuration.AssetDirectory, contentUrl);

		if (!System.IO.File.Exists(file))
		{
			return NotFound();
		}

		try
		{
			var str = new FileStream(file, FileMode.Open, FileAccess.Read);
			var ct = "image/png"; // are the files always png?
			return File(str, ct);
		}
		catch (Exception ex)
		{
			return StatusCode(500, "InternalServerError");
		}
	}	
	
	[HttpGet("assets/pending-assets/mesh/{contentUrl}.obj")]
	public async Task<IActionResult> GetMeshOBJ(string contentUrl)
	{
		// for security as anyone could do path traversal
		if (contentUrl.Contains("..") || Path.IsPathRooted(contentUrl))
		{
			return BadRequest("Could not find OBJ");
		}
		
		if (string.IsNullOrWhiteSpace(contentUrl))
		{
			Console.WriteLine("Content URL is null/empty");
			return NotFound();
		}

		var obj = Path.Combine(Configuration.AssetDirectory, $"{contentUrl}.obj");

		if (!System.IO.File.Exists(obj))
		{
			return NotFound("OBJ file not found");
		}

		try
		{
			var str = new FileStream(obj, FileMode.Open, FileAccess.Read);
			var file = Path.GetFileName(obj);
			return File(str, "text/plain", file);
		}
		catch (Exception ex)
		{
			Writer.Info(LogGroup.AdminApi, $"Error serving OBJ file {contentUrl}: {ex}");
			return StatusCode(500, "Error serving file");
		}
	}
				
	[HttpGet("assets/pending-assets"), StaffFilter(Access.GetPendingModerationItems)]
	public async Task<IEnumerable<dynamic>> GetPendingAssets()
	{
		var offset = 0;
		var result = new List<PendingAssetEntry>();

		while (result.Count < 10)
		{
			var query = new SqlBuilder();
			var t = query.AddTemplate(
				@"SELECT asset.id, asset.name, asset_thumbnail.content_url, asset.asset_type as assetType
				  FROM asset
				  LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id
				  /**where**/
				  ORDER BY asset.id LIMIT 10 OFFSET :offset");

			query.OrWhereMulti("(asset.moderation_status = :status AND asset.asset_type = $1)", new []
			{
				Type.Image,
				Type.Decal,
				Type.Audio,
				Type.Face,
				Type.Mesh,
				Type.Lua,
				Type.Model,
				Type.Package,
				Type.Place,
				Type.Plugin,
				Type.MeshPart,
				Type.Mesh,
				Type.SolidModel,
			});

			query.AddParameters(new
			{
				status = ModerationStatus.AwaitingApproval,
				offset = offset,
			});

			var firstPass = (await db.QueryAsync<PendingAssetEntry>(
				t.RawSql,
				t.Parameters)).ToList();

			if (firstPass.Count == 0) return result; // all done!
			offset += firstPass.Count;

			foreach (var item in firstPass)
			{
				var latest = await services.assets.GetLatestAssetVersion(item.id);
				item.creatorId = latest.creatorId;
				var userInfo = await services.users.GetUserById(latest.creatorId);
				item.creatorName = userInfo.username;

				// prevent 43 assets rendering at once
				// if (item.content_url == null && item.assetType != Type.Audio)
				// {
				//     services.assets.RenderAsset(item.id, item.assetType);
				//     continue;
				// }

				if (item.content_url == null && latest.contentUrl != null)
				{
					item.content_url = latest.contentUrl;
				}

				if (item.content_url != null)
				{
					if (item.assetType == Type.Mesh)
					{
						item.content_url = $"/admin-api/api/assets/pending-assets/mesh/{latest.contentUrl}.obj";
					}
					else
					{
						item.content_url = $"/admin-api/api/assets/pending-assets/image/{item.content_url}";
					}
				}

				result.Add(item);
			}
		}

		return result;
	}

	[HttpPost("asset/moderate"), StaffFilter(Access.SetAssetModerationStatus)]
	public async Task ModerateAsset([Required, FromBody] ModerateAssetRequest request)
	{
		var details = await db.QuerySingleOrDefaultAsync<AssetModerationStatus>(
			"SELECT moderation_status as moderationStatus, roblox_asset_id as robloxAssetId FROM asset WHERE asset.id = :id", new { id = request.assetId });
		var currentStatus = details.moderationStatus;
		if (currentStatus == ModerationStatus.ReviewApproved && !request.isApproved)
		{
			// Rate limit for staff to moderate already approved items
			if (!await services.cooldown.TryIncrementBucketCooldown("ModerateApprovedItem_Hour", 60, TimeSpan.FromHours(1)))
				throw new StaffException("Moderation of already approved item rate limit exceeded (hour). Contact an administrator.");
			if (!await services.cooldown.TryIncrementBucketCooldown("ModerateApprovedItem_Day", 100, TimeSpan.FromDays(1)))
				throw new StaffException("Moderation of already approved item rate limit exceeded (day). Contact an administrator.");
		}
		if (details.canEarnRobuxFromApproval)
			await AwardCommissionForModeration();

		var newStatus = request.isApproved ? ModerationStatus.ReviewApproved : ModerationStatus.Declined;

		await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new
		{
			is_18_plus = request.is18Plus,
			status = newStatus,
			id = request.assetId,
		});
		await services.assets.InsertAssetModerationLog(request.assetId, userSession.userId, newStatus);
		
		// send message to asset creator if declined
		if (!request.isApproved)
		{
			try 
			{
				var assetInfo = await services.assets.GetAssetCatalogInfo(request.assetId);
				var latestVersion = await services.assets.GetLatestAssetVersion(request.assetId);
				
				await services.privateMessages.CreateMessage(latestVersion.creatorId, 1, "Asset Declined",
					$"Hello,\n" +
					$"Your asset, {assetInfo.name} (ID: {request.assetId}) was declined due to it being inappropriate or violating our policies. Please do not upload assets that violate our rules.\n\n" +
					$"Thank you, The Roblox Team");
			}
			catch (Exception ex)
			{
				Writer.Info(LogGroup.AdminApi, "Failed to send decline message for asset {0}: {1}", request.assetId, ex);
			}
		}

		var children = (await db.QueryAsync<AssetVersionWithIdEntry>("SELECT DISTINCT asset_id as assetId FROM asset_version WHERE content_id = :id", new
		{
			id = request.assetId,
		})).ToArray();
		// update children
		foreach (var item in children)
		{
			await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new
			{
				is_18_plus = request.is18Plus,
				status = newStatus,
				id = item.assetId,
			});
			await services.assets.InsertAssetModerationLog(item.assetId, userSession.userId, newStatus);
		}

		if (details.robloxAssetId != null && details.robloxAssetId != 0)
		{
			var duplicates = await db.QueryAsync<AssetVersionWithIdEntry>(
				"SELECT id as assetId FROM asset WHERE roblox_asset_id = :id", new
				{
					id = details.robloxAssetId.Value,
				});
			foreach (var dupe in duplicates)
			{
				await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new
				{
					is_18_plus = request.is18Plus,
					status = newStatus,
					id = dupe.assetId,
				});
				await services.assets.InsertAssetModerationLog(dupe.assetId, userSession.userId, newStatus);
			}
		}

		// re-render the next asset if the approved asset is an image and the next asset is a teeshirt, pants, or shirt
		// update badge/gamepass thumbnail to the image's thumb if the next asset is either badge or gamepass
		if (request.isApproved && newStatus == ModerationStatus.ReviewApproved)
		{
			var assetdetails = await services.assets.GetAssetCatalogInfo(request.assetId);
			if (assetdetails.assetType == Type.Image)
			{
				Console.WriteLine($"image {request.assetId} ({assetdetails.name}) was approved, but skipping render");

				_ = Task.Run(async () => 
				{
					await Task.Delay(TimeSpan.FromSeconds(2));
					
					var nextid = request.assetId + 1;
					var nextdetails = await services.assets.GetAssetCatalogInfo(nextid);
					if (nextdetails != null)
					{
						if (nextdetails.assetType == Type.TeeShirt || nextdetails.assetType == Type.Pants || nextdetails.assetType == Type.Shirt)
						{
							services.assets.RenderAsset(nextid, nextdetails.assetType);
						}
						else if (nextdetails.assetType == Type.Badge || nextdetails.assetType == Type.GamePass)
						{
							var ContentURL = await db.QuerySingleOrDefaultAsync<string>(
								"SELECT content_url FROM asset_version WHERE asset_id = :id ORDER BY id DESC LIMIT 1",
								new { id = request.assetId }
							);

							if (!string.IsNullOrEmpty(ContentURL))
							{
								await db.ExecuteAsync(
									@"INSERT INTO asset_thumbnail (asset_id, asset_version_id, content_url, moderation_status)
									  VALUES (:assetId, :assetVersionId, :contentUrl, :moderationStatus)",
									new
									{
										assetId = nextid,
										assetVersionId = nextid,
										contentUrl = ContentURL,
										moderationStatus = newStatus
									}
								);

								Console.WriteLine($"Set badge/gamepass {nextid}'s cont url to image {request.assetId}'s content URL");
							}
							else
							{
								Console.WriteLine($"no content URL found for image {request.assetId}");
							}
						}
					}
					else
					{
						Console.WriteLine($"asset {nextid} not found");
					}
				});
			}
		}
	}
	
	[HttpPost("asset/thumbnail"), StaffFilter(Access.SetAssetModerationStatus)]
	public async Task<IActionResult> ChangeThumbnail(
		[Required] long assetId, 
		[Required] IFormFile thumbnail)
	{
		var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);
		
		if (thumbnail == null || thumbnail.Length == 0)
			throw new StaffException("No file provided");
		
		if (!thumbnail.ContentType.StartsWith("image/"))
			throw new StaffException("File must be an image");
		
		if (thumbnail.Length > 10 * 1024 * 1024)
			throw new StaffException("File size must be less than 10MB");

		var existingthumbnail = await db.QuerySingleOrDefaultAsync(
			"SELECT content_url FROM asset_thumbnail WHERE asset_id = :assetId",
			new { assetId });
		
		if (existingthumbnail == null || existingthumbnail.content_url == null)
			throw new StaffException("Asset does not have a thumbnail to change");

		using var sha256 = SHA256.Create();
		using var stream = thumbnail.OpenReadStream();
		var hashbyte = await sha256.ComputeHashAsync(stream);
		var hash = BitConverter.ToString(hashbyte).Replace("-", "").ToLowerInvariant();
		var newcu = hash; // new content url
		var newthumbpath = Path.Combine(Configuration.ThumbnailsDirectory, newcu + ".png"); // new thumbnail path

		try
		{
			stream.Position = 0;
			
			using (var str = new FileStream(newthumbpath, FileMode.Create))
			{
				await thumbnail.CopyToAsync(str);
			}

			await db.ExecuteAsync(
				"UPDATE asset_thumbnail SET content_url = :newcu WHERE asset_id = :assetId",
				new 
				{
					assetId,
					newcu
				});

			await db.ExecuteAsync(
				"INSERT INTO moderation_overwrite_thumbnail (asset_id, actor_id, content_url) " +
				"VALUES (:assetId, :actorId, :contentUrl)",
				new 
				{
					assetId,
					actorId = userSession.userId,
					contentUrl = newcu
				});
				
			return Ok(new 
			{
				success = true,
				message = "Thumbnail changed successfully",
				newcu
			});
		}
		catch (Exception ex)
		{
			// if something fucked up clean up the uploaded thumb
			if (System.IO.File.Exists(newthumbpath))
			{
				System.IO.File.Delete(newthumbpath);
			}
			// todo: should we delete the old thumb?
			Writer.Info(LogGroup.AdminApi, "Could not update thumbnail for asset {0}: {1}", assetId, ex);
			throw new StaffException("Failed to overwrite thumbnail: " + ex.Message);
		}
	}
	
	[HttpPost("asset/set-rap"), StaffFilter(Access.SetAssetProduct)]
	public async Task SetAssetRap([Required, FromBody] SetRapReq request)
	{
		var info = await services.assets.GetAssetCatalogInfo(request.assetId);
		
		if (request.rap < 0 || request.rap > 100000000) // 100 million max RAP
			throw new StaffException("RAP must be between 0 and 100 million");
		
		await db.ExecuteAsync(
			"UPDATE asset SET recent_average_price = :rap WHERE id = :assetId",
			new 
			{
				rap = request.rap,
				assetId = request.assetId
			});
		// Ack gu
		await db.ExecuteAsync(
			"INSERT INTO moderation_set_rap (asset_id, actor_id, new_rap) " +
			"VALUES (:assetId, :actorId, :newRap)",
			new 
			{
				assetId = request.assetId,
				actorId = userSession.userId,
				newRap = request.rap
			});
	}

    [HttpPost("asset/moderate-and-delete"), StaffFilter(Access.SetAssetModerationStatus)]
    public async Task ModerateAndDeleteItem([Required, FromBody] ModerateAssetRequest request)
    {
		// UNCOMMENT AFTER BUNDLES FIXED
        // 30 deletions/hour
        //if (!await services.cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Hour", 30, TimeSpan.FromHours(1)))
            //throw new StaffException("Asset deletion rate limit exceeded (hour). Contact an administrator.");
        
        // 100/day
        //if (!await services.cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Day", 100, TimeSpan.FromDays(1)))
           // throw new StaffException("Asset deletion rate limit exceeded (day). Contact an administrator.");

        await ModerateAsset(request);
        
        if (!request.isApproved)
        {
            var details = await services.assets.GetAssetCatalogInfo(request.assetId);
            var minCreationTime = DateTime.UtcNow.Subtract(TimeSpan.FromDays(7));
            if (details.createdAt < minCreationTime)
            {
                throw new StaffException("This asset cannot be deleted since it was created too long ago");
            }
            // Delete the asset
            await services.assets.DeleteAsset(request.assetId);
        }
    }

    [HttpGet("icons/pending-assets"), StaffFilter(Access.GetPendingModerationGameIcons)]
    public async Task<dynamic> GetPendingAssetIcons()
    {
        var firstPass = (await db.QueryAsync(
            "SELECT asset_icon.id, asset.name, asset_icon.content_url, asset_icon.asset_id as asset_id FROM asset_icon INNER JOIN asset ON asset.id = asset_icon.asset_id WHERE asset_icon.moderation_status = :status ORDER BY asset.id LIMIT 10",
            new { status = ModerationStatus.AwaitingApproval })).ToList();
        if (firstPass.Count == 0) return new List<dynamic>();

        foreach (var item in firstPass)
        {
            try
            {
                var latest = await services.assets.GetLatestAssetVersion((long) item.asset_id);
                item.creatorId = (object) latest.creatorId;
                var userInfo = await services.users.GetUserById(latest.creatorId);
                item.creatorName = (object) userInfo.username;
                item.content_url = Configuration.CdnBaseUrl + "/images/thumbnails/" + item.content_url + ".png";
            }
            catch (Exception)
            {
                item.creatorId = (object) 1;
                item.creatorName = (object) "ROBLOX";
            }
        }

        return firstPass;
    }

	[HttpPost("gift/open/{assetId:long}/{assetToGive:long}"), StaffFilter(Access.GiveUserItem)]
	public async Task<dynamic> OpenGift(long assetId, long assetToGive)
	{
		try
		{
			Console.WriteLine($"getting all owners of gift {assetId} to give asset {assetToGive}");

			var productDetails = await services.assets.GetProductForAsset(assetToGive);
			if (productDetails == null)
			{
				throw new StaffException($"Asset {assetToGive} does not exist");
			}

			var giftOwners = await db.QueryAsync(
				@"SELECT 
					ua.id as user_asset_id,
					ua.user_id,
					u.username,
					a.name as asset_name
				  FROM user_asset ua
				  INNER JOIN ""user"" u ON ua.user_id = u.id
				  INNER JOIN asset a ON ua.asset_id = a.id
				  WHERE ua.asset_id = :assetId
				  AND ua.user_id IS NOT NULL",
				new { assetId });

			var owners = giftOwners.ToList();
			
			if (!owners.Any())
			{
				throw new StaffException($"No owners found for gift {assetId}");
			}

			Console.WriteLine($"got {owners.Count} owners of gift {assetId}");

			var isLU = productDetails.isLimitedUnique;
			long serialBase = isLU ? await services.assets.GetSaleCount(assetToGive) : 0;

			foreach (var owner in owners)
			{
				try
				{
					var userId = (long)owner.user_id;
					var userAssetId = (long)owner.user_asset_id;
					var username = (string)owner.username;

					Console.WriteLine($"giving {assetToGive} from gift {assetId} for user {userId} ({username})");

					long? serial = null;
					if (isLU)
					{
						serial = ++serialBase;
					}

					var newID = await db.ExecuteScalarAsync<long>(
						"INSERT INTO user_asset (asset_id, user_id, serial, created_at, updated_at, price) " +
						"VALUES (:asset_id, :user_id, :serial, NOW(), NOW(), 0) RETURNING id",
						new
						{
							asset_id = assetToGive,
							user_id = userId,
							serial = serial
						});

					await db.ExecuteAsync(
						"INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) " +
						"VALUES (:user_id, :author_user_id, :user_asset_id, null)",
						new
						{
							user_id = userId,
							author_user_id = userSession.userId,
							user_asset_id = newID,
						});
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error processing gift: {ex}");
				}
			}

			if (isLU)
			{
				await db.ExecuteAsync(
					"UPDATE asset SET sale_count = :count WHERE id = :assetId",
					new { count = serialBase, assetId = assetToGive });
				
				Console.WriteLine($"updated sale count for asset {assetToGive} to {serialBase}");
			}

			return new
			{
				success = true
			};
			
			Console.WriteLine($"successfully opened gift {assetToGive}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"error in gift open: {ex}");
			throw new StaffException($"failed to open gift (invalid ID or invalid gift ID?): {ex.Message}");
		}
	}

    [HttpPost("icon/moderate"), StaffFilter(Access.SetGameIconModerationStatus)]
    public async Task ModerateIcon([Required, FromBody] ModerateIconRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync(
            "SELECT moderation_status, content_url, asset_id FROM asset_icon WHERE asset_icon.id = :id", new { id = request.iconId });
        if (details == null) throw new StaffException("Asset ID is invalid");
        if ((ModerationStatus)details.moderation_status != ModerationStatus.AwaitingApproval && !StaffFilter.IsOwner(userSession.userId))
        {
            throw new StaffException(
                "You can only moderate items in a pending state. This item was already approved or declined.");
        }
        
        await AwardCommissionForModeration();

        if (request.isApproved)
        {
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id", new
            {
                id = request.iconId,
                status = ModerationStatus.ReviewApproved,
            });
            
            if (request.is18Plus)
            {
                // update asset
                await db.ExecuteAsync("UPDATE asset SET is_18_plus = true WHERE id = :id", new
                {
                    id = (long) details.asset_id,
                });
            }
        }
        else
        {
            // delete it
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id", new
            {
                status = ModerationStatus.Declined,
                id = request.iconId,
            });
            await services.assets.DeleteAssetContent((string)details.content_url, Configuration.ThumbnailsDirectory);
        }
    }

    private async Task AwardCommissionForModeration()
    {
        // give commission
        await services.economy.IncrementCurrency(userSession.userId, CurrencyType.Tickets, 5);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Tickets,
			// Stupid
            amount = 5,
            // details
            sub_type = TransactionSubType.StaffAssetModeration,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    private async Task AwardCommissionForApplicationReview()
    {
        // give commission
        await services.economy.IncrementCurrency(userSession.userId, CurrencyType.Tickets, 5);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
			// fuck you. NOTHING. we don't use apps anymore
            amount = 5,
            // details
            sub_type = TransactionSubType.StaffApplicationReview,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    [HttpPost("groups/icon-toggle"), StaffFilter(Access.SetGroupIconModerationStatus)]
    public async Task ToggleIcon([Required, FromBody] IconToggleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.name))
            throw new StaffException("Invalid icon");
        
        if (request.name.IndexOf("/", StringComparison.Ordinal) != -1)
        {
            var loc = request.name.LastIndexOf("/", StringComparison.Ordinal) + 1;
            request.name = request.name[loc..];
        }
        
        if (request.name.IndexOf("/", StringComparison.Ordinal) != -1 || request.name.IndexOf("\\", StringComparison.Ordinal) != -1)
        {
            throw new StaffException("Invalid filename: " + request.name);
        }

        var affected = await db.ExecuteAsync(
            "UPDATE group_icon SET is_approved = :approved WHERE group_id = :gid AND name = :name", new
            {
                request.approved,
                gid = request.groupId,
                request.name,
            });
        if (affected == 0)
            throw new StaffException(
                "The icon URL is no longer valid. Maybe the group owner created a new icon before the previous one was approved?");

        await AwardCommissionForModeration();
        
        if (request.approved == 2)
        {
            await services.assets.DeleteAssetContent(request.name, Configuration.GroupIconsDirectory);
        }
    }

    [HttpGet("groups/get-by-id"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupModerationInfo(long groupId)
    {
        var icon = await db.QuerySingleOrDefaultAsync("SELECT * FROM group_icon WHERE group_id = :gid", new
        {
            gid = groupId,
        });
        var info = await db.QuerySingleOrDefaultAsync("SELECT * FROM \"group\" WHERE id = :gid", new
        {
            gid = groupId,
        });
        icon.name = Configuration.CdnBaseUrl + "/images/groups/" + icon.name;

        return new
        {
            icon,
            info,
        };
    }

	[HttpGet("user-joins"), StaffFilter(Access.GetUserJoinCount)]
	public async Task<dynamic> GetUserJoinCount(string period)
	{
		if (period == "totalactive")
		{
			var activeCount = await db.QuerySingleOrDefaultAsync<Total>(
				"SELECT COUNT(*) AS total FROM \"user\" WHERE status = 1");
			return new
			{
				total = activeCount.total,
			};
		}
		else
		{
			var t = DateTime.UtcNow.Subtract(period is "past-day" ? TimeSpan.FromDays(1) :
					period is "past-hour" ? TimeSpan.FromHours(1) :
					period is "past-week" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(30));
			var all = await db.QuerySingleOrDefaultAsync<Total>(
				"SELECT COUNT(*) AS total FROM \"user\" WHERE created_at >= :t", new
				{
					t,
				});
			return new
			{
				all.total,
			};
		}
	}

    private static readonly List<string> whitelistedUserSorts = new()
    {
        "user_economy.balance_robux",
        "user_economy.balance_tickets",
        "user.id",
        "user.online_at",
    };

	[HttpGet("users"), StaffFilter(Access.GetUsersList)]
	public async Task<dynamic> GetUsers(string orderByColumn = "user.id", string? orderByMode = "asc", int limit = 10,
		int offset = 0, string? query = null)
	{
		if (!whitelistedUserSorts.Contains(orderByColumn))
			throw new StaffException("Invalid sort column");
		if (orderByMode != "asc" && orderByMode != "desc")
			throw new StaffException("Invalid sort mode");
		if (limit is > 10000 or < 1) limit = 10;
		orderByColumn = orderByColumn.Replace("user_economy", "ue").Replace("user", "u");

		var sql = new SqlBuilder();
		var t = sql.AddTemplate(
			"SELECT u.id, u.username, u.description, u.created_at, u.online_at, u.status, u.is_18_plus, ja.id as join_application_id, ja.status as join_application_status, ui.id as invite_id, ui.author_id as invite_author_id, us.*, ue.* FROM \"user\" u LEFT JOIN user_settings us ON us.user_id = u.id LEFT JOIN user_economy ue on u.id = ue.user_id LEFT JOIN join_application ja on u.id = ja.user_id LEFT JOIN user_invite ui on u.id = ui.user_id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new {limit, offset });
		sql.OrderBy(orderByColumn + " " + orderByMode + " NULLS LAST");
		if (!string.IsNullOrEmpty(query))
		{
			sql.Where("u.username ilike :query", new
			{
				query = query,
			});
		}

		var all = await db.QueryAsync(t.RawSql, t.Parameters);
		return new
		{
			data = all.Select(c =>
			{
				c.status = ((AccountStatus)c.status).ToString();
				
				// Handle null trade_filter - get first enum value as default
				var tradeFilterValues = Enum.GetValues(typeof(TradeQualityFilter));
				var defaultTradeFilter = tradeFilterValues.Length > 0 
					? tradeFilterValues.GetValue(0).ToString()
					: "None"; // Fallback if enum is empty
				
				c.trade_filter = c.trade_filter != null 
					? ((TradeQualityFilter)c.trade_filter).ToString() 
					: defaultTradeFilter;
					
				c.inventory_privacy = ((GeneralPrivacy)c.inventory_privacy).ToString();
				c.trade_privacy = ((GeneralPrivacy)c.trade_privacy).ToString();
				c.private_message_privacy = ((GeneralPrivacy)c.private_message_privacy).ToString();
				c.gender = ((Gender)c.gender).ToString();
				if (c.join_application_status != null)
					c.join_application_status = ((UserApplicationStatus) c.join_application_status).ToString();
				c.is_admin = (object)false;
				c.is_moderator = (object)false;
				c.password = "";

				return c;
			}),
		};
	}
	
	// stupid helper function cause GetUserById has a cache or something and doesn't update
	public async Task<bool> GetUserVerified(long userId)
	{
		return await db.ExecuteScalarAsync<bool>(
			"SELECT verified FROM \"user\" WHERE id = :userId",
			new { userId });
	}
	
    [HttpGet("user"), StaffFilter(Access.GetUserDetailed)]
    public async Task<dynamic> GetUserInfoDetailed(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT u.id, u.username, u.description, u.created_at, u.online_at, u.status, us.*, ue.*, avatar.thumbnail_url, ub.author_user_id as ban_author_user_id, ban_author.username as ban_author_username, ub.reason as ban_reason, ub.internal_reason as ban_reason_internal, ub.created_at as ban_created_at, ub.updated_at as ban_updated_at FROM \"user\" u LEFT JOIN user_settings us on u.id = us.user_id LEFT JOIN user_economy ue on u.id = ue.user_id LEFT JOIN user_avatar avatar ON avatar.user_id = u.id LEFT JOIN user_ban ub ON ub.user_id = u.id LEFT JOIN \"user\" as ban_author ON ban_author.id = ub.author_user_id WHERE u.id = :user_id LIMIT 1", new
        {
            user_id = userId,
        });
        if (result == null) throw new StaffException("Invalid user ID");
        var joinInvite = await services.users.GetUserInvite(userId);
        var joinApp = await services.users.GetApplicationByUserId(userId);
        var membership = await services.users.GetUserMembership(userId);
        var year = await services.users.GetYear(userId);
        
        if (result.thumbnail_url != null)
            result.thumbnail_url = Configuration.CdnBaseUrl + result.thumbnail_url;
        result.theme = ((ThemeTypes) result.theme).ToString();
        result.status = ((AccountStatus)result.status).ToString();
        result.trade_filter = ((TradeQualityFilter)result.trade_filter).ToString();
        result.inventory_privacy = ((GeneralPrivacy)result.inventory_privacy).ToString();
        result.trade_privacy = ((GeneralPrivacy)result.trade_privacy).ToString();
        result.private_message_privacy = ((GeneralPrivacy)result.private_message_privacy).ToString();
        result.gender = ((Gender)result.gender).ToString();
        result.is_admin = (object)await StaffFilter.IsStaff(userId);
        result.is_moderator = (object)false;
        result.membership = (object?)membership;
        result.invite = (object?) joinInvite;
        result.joinApp = (object?) joinApp;
        result.year = year.ToString();
		result.verified = (object)await GetUserVerified(userId);
        return result;
    }
	
	[HttpPost("users/verify"), StaffFilter(Access.GiveUserBadge)]
	public async Task<dynamic> ToggleUserVerifiedStatus([Required, FromBody] VerifiedReq request)
	{	
		await db.ExecuteAsync(
			"UPDATE \"user\" SET verified = NOT verified WHERE id = :userId",
			new { userId = request.userId });
			
		var Status = await db.ExecuteScalarAsync<bool>(
			"SELECT verified FROM \"user\" WHERE id = :userId",
			new { userId = request.userId });
			
		// add this
/* 		await db.ExecuteAsync(
			"INSERT INTO moderation_toggle_verified (user_id, actor_id, new_status) VALUES (:userId, :actorId, :Status)",
			new 
			{
				userId = request.userId,
				actorId = userSession.userId,
				Status = !userInfo.isVerified
			}); */
		
		return new
		{
			success = true,
			isVerified = Status
		};
	}
	
	// HASHED. not real ips.
	[HttpGet("users/alts"), StaffFilter(Access.GetUsersList)]
	public async Task<dynamic> GetPossibleAlts()
	{
		var duplicate = await db.QueryAsync<dynamic>(
			@"SELECT hashed_ip, COUNT(*) as count 
			  FROM user_hashed_ips 
			  GROUP BY hashed_ip 
			  HAVING COUNT(*) > 1");
		
		if (!duplicate.Any())
		{
			return new {};
		}

		var containers = new Dictionary<int, dynamic>();
		int containernum = 1;

		foreach (var ip in duplicate)
		{
			var possiblealts = await db.QueryAsync<dynamic>(
				@"SELECT u.id as user_id, u.username, u.status, uhi.block_status, uhi.hashed_ip, uhi.last_seen 
				  FROM user_hashed_ips uhi
				  JOIN ""user"" u ON uhi.user_id = u.id
				  WHERE uhi.hashed_ip = @hashed_ip
				  ORDER BY uhi.last_seen DESC",
				new { hashed_ip = ip.hashed_ip });
			
			var users = possiblealts.ToList();
			for (int i = 0; i < users.Count; i++)
			{
				for (int j = i + 1; j < users.Count; j++)
				{
					if (users[i].user_id == 1 || users[j].user_id == 1)
						continue;
					
					if (users[i].status == 1 || users[j].status == 1)
					{
						containers.Add(containernum, new
						{
							users = new List<dynamic> { users[i], users[j] }
						});
						containernum++;
					}
				}
			}
		}
		
		if (containers.Count == 0)
		{
			return new {};
		}

		return new
		{
			data = containers
		};
	}

    private bool IsAdmin()
    {
        return StaffFilter.IsOwner(userSession.userId);
    }

    private async Task<bool> IsStaff(long userId)
    {
        return StaffFilter.IsOwner(userId) || (await StaffFilter.GetPermissions(userId)).Any();
    }

    [HttpPost("unban"), StaffFilter(Access.UnbanUser)]
    public async Task UnbanUser([Required, FromBody] UserIdRequest request)
    {
        var status = await services.users.GetUserById(request.userId);
        if (status.accountStatus == AccountStatus.Forgotten)
            throw new StaffException("Forgotten accounts cannot be un-banned");

        await db.ExecuteAsync("UPDATE \"user\" SET status = :st WHERE id = :id", new
        {
            st = AccountStatus.Ok,
            id = request.userId,
        });
        // log
        await db.ExecuteAsync("INSERT INTO moderation_unban (user_id, actor_id) VALUES (:user_id, :actor_id)", new
        {
            user_id = request.userId,
            actor_id = userSession.userId,
        });
        // actually unban
        await db.ExecuteAsync("DELETE FROM user_ban WHERE user_id = :id", new { id = request.userId });
		// remove poison ban
		await db.ExecuteAsync("UPDATE user_hashed_ips SET poisoned = false WHERE user_id = :user_id", new
		{
			user_id = request.userId,
		});
    }
    
	[HttpPost("ban"), StaffFilter(Access.BanUser)]
	public async Task BanUser([Required, FromBody] BanUserRequest request)
	{
		DateTime? expirationDate = string.IsNullOrWhiteSpace(request.expires) || request.expires == "Poisoned(IP)" || request.expires == "permanent" ? (DateTime?)null : DateTime.Parse(request.expires);
		var doesExpire = expirationDate != null;
		if (string.IsNullOrWhiteSpace(request.expires) || request.expires == "permanent")
		{
			expirationDate = null;
			doesExpire = false;
		}
		else if (request.expires == "Poisoned(IP)")
		{
			expirationDate = null;
			doesExpire = false;
		}
		else
		{
			expirationDate = DateTime.Parse(request.expires);
			doesExpire = true;
		}
		
		var info = await services.users.GetUserById(request.userId);
		if (info.accountStatus != AccountStatus.Ok && info.accountStatus != AccountStatus.Suppressed && info.accountStatus != AccountStatus.MustValidateEmail)
			throw new StaffException("You cannot ban this user. Current status is " + info.accountStatus);
		if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
			throw new StaffException("You cannot ban this user.");
		// insert ban
		await db.ExecuteAsync(
			"INSERT INTO user_ban (user_id, reason, author_user_id, expired_at, internal_reason) VALUES (:user_id, :reason, :author, :expires, :internal_reason)", new
			{
				internal_reason = request.internalReason,
				user_id = request.userId,
				request.reason,
				author = userSession.userId,
				expires = expirationDate,
			});
		// insert into user ban history
		await db.ExecuteAsync(
			"INSERT INTO moderation_user_ban (user_id, reason, author_user_id, expired_at, internal_reason) VALUES (:user_id, :reason, :author, :expires, :internal_reason)", new
			{
				internal_reason = request.internalReason,
				user_id = request.userId,
				request.reason,
				author = userSession.userId,
				expires = expirationDate,
			});
		// log
		await db.ExecuteAsync("INSERT INTO moderation_ban (user_id, actor_id, reason, internal_reason, expired_at) VALUES (:user_id, :author, :reason, :internal_reason, :expires)", new
		{
			user_id = request.userId,
			author = userSession.userId,
			reason = request.reason,
			internal_reason = request.internalReason,
			expires = expirationDate,
		});
		// mark as banned
		await db.ExecuteAsync("UPDATE \"user\" SET status = :st WHERE id = :id", new
		{
			st = request.expires == "Poisoned(IP)" ? AccountStatus.Poisoned : (doesExpire ? AccountStatus.Suppressed : AccountStatus.Deleted),
			id = request.userId,
		});

		// take all limited items off sale
		await db.ExecuteAsync("UPDATE user_asset SET price = 0 WHERE price != 0 AND user_id = :user_id", new
		{
			user_id = request.userId,
		});
		// if poison ban, mark users ip as poisoned
		if (request.expires == "Poisoned(IP)")
		{
			await db.ExecuteAsync(
				"INSERT INTO user_hashed_ips (user_id, hashed_ip, last_seen, block_status, poisoned) " +
				"VALUES (:user_id, '', NOW(), 0, true) " +
				"ON CONFLICT (user_id) DO UPDATE SET poisoned = true, last_seen = NOW()",
				new
				{
					user_id = request.userId,
				});
		}
	}

    [HttpPost("user/create-message"), StaffFilter(Access.CreateMessage)]
    public async Task CreateMessage([Required, FromBody] CreateMessageRequest request)
    {
        // validate user
        await services.users.GetUserById(request.userId);
        if (request.body.Length is > 1024 or < 1)
            throw new StaffException("Body is not valid");
        if (request.subject.Length is > 64 or < 1)
            throw new StaffException("Subject is not valid");
        await db.ExecuteAsync(
            "INSERT INTO user_message (user_id_to, user_id_from, subject, body) VALUES (:user_id_to, 1, :subject, :body)",
            new
            {
                user_id_to = request.userId,
                request.subject,
                request.body,
            });
        await db.ExecuteAsync("INSERT INTO moderation_admin_message(user_id, actor_id, body, subject) VALUES (:user_id, :actor_id, :body, :subject)", new
        {
            user_id = request.userId,
            actor_id = userSession.userId,
            request.body,
            request.subject,
        });
    }

    [HttpGet("user/messages-from-admins"), StaffFilter(Access.GetAdminMessages)]
    public async Task<dynamic> GetMessagesFromStaff(long userId, int limit = 10, int offset = 0)
    {
        if (limit is > 100 or < 1) limit = 10;
        return await db.QueryAsync(
            "SELECT user_message.* FROM user_message WHERE user_id_to = :id AND user_id_from = 1 ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                id = userId,
                limit,
                offset,
            });
    }

    [HttpPost("user/nullify-password"), StaffFilter(Access.NullifyPassword)]
    public async Task NullifyUserPassword([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Bad user id");
        await db.ExecuteAsync("UPDATE \"user\" SET password = '' WHERE id = :id", new
        {
            id = request.userId,
        });
    }

    [HttpPost("user/logout"), StaffFilter(Access.DestroyAllSessionsForUser)]
    public async Task DeleteAllSessions([Required, FromBody] UserIdRequest request)
    {
        await services.users.ExpireAllSessions(request.userId);
    }

    [HttpPost("user/lock"), StaffFilter(Access.LockAccount)]
    public async Task LockUser([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot lock this user");
        await db.ExecuteAsync("UPDATE \"user\" SET status = :status, session_expired_at = now() WHERE id = :id", new
        {
            id = request.userId,
            status = AccountStatus.MustValidateEmail,
        });
    }

    [HttpPost("user/regenerate-avatar"), StaffFilter(Access.RegenerateAvatar)]
    public async Task RegenAvatarRequest([Required, FromBody] UserIdRequest request)
    {
        await services.avatar.RedrawAvatar(request.userId, default, default, default, true, true);
    }

    [HttpPost("user/reset-avatar"), StaffFilter(Access.ResetAvatar)]
    public async Task ResetAvatar([Required, FromBody] UserIdRequest request)
    {
        await services.avatar.RedrawAvatar(request.userId, new List<long>(), new ColorEntry
        {
            headColorId = 194,
            torsoColorId = 23,
            rightArmColorId = 194,
            leftArmColorId = 194,
            rightLegColorId = 102,
            leftLegColorId = 102,
        }, AvatarType.R6);
    }

    [HttpGet("user/status-history"), StaffFilter(Access.GetUserStatusHistory)]
    public async Task<dynamic> GetUserStatusHistory([Required, FromQuery] long userId)
    {
        return await db.QueryAsync("SELECT * FROM user_status WHERE user_id = :user_id AND status IS NOT NULL ORDER BY id DESC", new
        {
            user_id = userId,
        });
    }

    [HttpGet("user/comment-history"), StaffFilter(Access.DeleteComment)]
    public async Task<dynamic> GetUserCommentHistory([Required, FromQuery] long userId)
    {
        return await db.QueryAsync("SELECT * FROM asset_comment WHERE user_id = :user_id ORDER BY id DESC LIMIT 1000",
            new
            {
                user_id = userId,
            });
    }
    
    [HttpDelete("user/status"), StaffFilter(Access.DeleteUserStatus)]
    public async Task DeleteUserStatus([Required, FromQuery] long userId, [Required, FromQuery] long statusId)
    {
        await db.ExecuteAsync("UPDATE user_status SET status = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = statusId,
            user_id = userId,
        });
    }

    [HttpPost("asset/refund-transaction"), StaffFilter(Access.RefundAndDeleteFirstPartyAssetSale)]
    public async Task RefundTransaction(long transactionId, long assetId, long expectedAmount, long userId)
    {
        // First, lookup to make sure params are valid.
        var transaction = await db.QuerySingleOrDefaultAsync<RefundTransactionEntry>(
            "SELECT id, asset_id as assetId, amount, user_id_one as userId, user_asset_id as userAssetId, currency_type as currencyType FROM user_transaction WHERE id = :id", new
            {
                id = transactionId,
            });
        if (transaction == null)
            throw new StaffException("Transaction does not exist");
        if (transaction.userId != userId || transaction.assetId != assetId || transaction.amount != expectedAmount)
            throw new StaffException("Transaction state is not valid. Reload the page and try again");
        // Make sure UAID exists
        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            try
            {
                var us = await services.users.GetUserAssetById(transaction.userAssetId.Value);
                if (us == null || us.userId != userId)
                    throw new StaffException("User asset does not exist or is no longer owned by this user");
            }
            catch (RecordNotFoundException)
            {
                throw new StaffException("User asset no longer exists");
            }
        }
        // check avatar
        var av = await services.avatar.GetWornAssets(userId);
        var shouldUpdateAvatar = av.Any(a => a == assetId);

        // log
        await db.ExecuteAsync("INSERT INTO moderation_refund_transaction(actor_id, user_id_one, user_id_two, asset_id, user_asset_id, amount, currency_type, transaction_id) VALUES(:actor_id, :user_id_one, :user_id_two, :asset_id, :user_asset_id, :amount, :currency_type, :transaction_id)", new
        {
            actor_id = userSession.userId,
            user_id_one = userId,
            user_id_two = transaction.otherUserId,
            asset_id = assetId,
            user_asset_id = transaction.userAssetId,
            amount = expectedAmount,
            currency_type = transaction.currencyType,
            transaction_id = transactionId,
        });
        // First, refund the user
        await services.economy.IncrementCurrency(CreatorType.User, userId, transaction.currencyType, transaction.amount);
        // transfer the uaid
        var bd = await services.users.GetUserIdFromUsername("BadDecisions");
        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE id = :id", new
            {
                id = transaction.userAssetId.Value,
                bd,
            });
        }
        else
        {
            // we can't delete one uaid. just move any owned copies the user has.
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE asset_id = :asset_id AND user_id = :user_id", new
            {
                asset_id = transaction.assetId,
                user_id = transaction.userId,
                bd,
            });
        }
        
        // delete transaction
        await db.ExecuteAsync("DELETE FROM user_transaction WHERE id = :id", new
        {
            id = transactionId,
        });

        if (shouldUpdateAvatar)
        {
            Writer.Info(LogGroup.AdminApi, "refunded transaction {0}. userId {1} requires a redraw", transaction.id, userId);
            // avatar requires an update.
            await services.avatar.RedrawAvatar(userId, default, default, default, default, true);
        }
    }

    [HttpGet("asset/product-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<dynamic> GetAssetProductHistory(long assetId)
    {
        return await db.QueryAsync(
                "SELECT p.id, p.asset_id, a.name, p.actor_id, u.username, p.is_for_sale, price_in_tickets, price_in_robux, p.is_limited, p.is_limited_unique, p.max_copies, p.offsale_at, p.created_at FROM moderation_update_product p LEFT JOIN asset a ON a.id = asset_id LEFT JOIN \"user\" u ON u.id = p.actor_id WHERE p.asset_id = :asset_id ORDER BY id DESC", new
                {
                    asset_id = assetId,
                });
    }

    [HttpGet("asset/sale-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<dynamic> GetSaleHistory(long assetId, int limit, int offset, DateTime? start = null, DateTime? end = null)
    {
        var qb = new SqlBuilder();
        var t = qb.AddTemplate("SELECT t.id, t.user_id_one, u.username, t.amount, t.currency_type, t.user_asset_id, t.created_at FROM user_transaction t INNER JOIN \"user\" u ON u.id = t.user_id_one /**where**/ ORDER BY id DESC LIMIT :limit OFFSET :offset", new
        {
            limit = limit,
            offset = offset,
        });
        qb.Where("t.user_id_two = 1 AND t.type = :type AND t.sub_type = :sub_type AND t.asset_id = :asset_id", new
        {
            type = PurchaseType.Purchase,
            sub_type = TransactionSubType.ItemPurchase,
            asset_id = assetId,
        });
        if (start != null)
        {
            qb.Where("t.created_at >= :start", new
            {
                start = start.Value,
            });
        }
        if (end != null)
        {
            qb.Where("t.created_at <= :end", new
            {
                end = end.Value,
            });
        }
        return await db.QueryAsync(t.RawSql, t.Parameters);
    }

    [HttpGet("logs"), StaffFilter(Access.GetAdminLogs)]
    public async Task<dynamic> GetModerationLogs(string logType, int limit = 10, int offset = 0)
    {
        if (limit is > 100 or < 1) limit = 10;
        
        switch (logType)
        {
            case "ban":
            {
                var result = await db.QueryAsync(
                    "SELECT ub.id, ub.created_at, ub.expired_at, ub.reason, ub.internal_reason, ub.user_id, u1.username, ub.actor_id, u2.username as author_username FROM moderation_ban ub INNER JOIN \"user\" u1 ON u1.id = ub.user_id INNER JOIN \"user\" u2 ON u2.id = ub.actor_id ORDER BY ub.id DESC LIMIT :limit OFFSET :offset",
                    new
                    {
                        limit, offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "Expires",
                        "Reason",
                        "Internal Reason",
                        "UserID",
                        "Username",
                        "AuthorID",
                        "Author Username",
                    },
                };
            }
            case "unban":
            {
                var result = await db.QueryAsync(
                    "SELECT ub.id, ub.created_at, ub.user_id, u1.username, ub.actor_id, u2.username as author_username FROM moderation_unban ub INNER JOIN \"user\" u1 ON u1.id = ub.user_id INNER JOIN \"user\" u2 ON u2.id = ub.actor_id ORDER BY ub.id DESC LIMIT :limit OFFSET :offset",
                    new
                    {
                        limit, 
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "UserID",
                        "Username",
                        "AuthorID",
                        "Author Username",
                    },
                };
            }
			case "rap":
			{
				var result = await db.QueryAsync(
					"SELECT msr.id, msr.created_at, msr.asset_id, a.name as asset_name, " +
					"msr.new_rap as rap, msr.actor_id, u.username as actor_username " +
					"FROM moderation_set_rap msr " +
					"LEFT JOIN asset a ON a.id = msr.asset_id " +
					"LEFT JOIN \"user\" u ON u.id = msr.actor_id " +
					"ORDER BY msr.id DESC LIMIT :limit OFFSET :offset",
					new
					{
						limit, 
						offset,
					});
				return new
				{
					data = result,
					columns = new[]
					{
						"#",
						"Date",
						"Asset ID",
						"Asset Name",
						"New RAP",
						"Author ID",
						"Author Username",
					},
				};
			}
			case "item-resale":
			{
			var result = await db.QueryAsync(
				@"SELECT 
					msa.id,
					msa.user_asset_id,
					msa.user_id,
					u.username as user_name,
					msa.asset_id,
					a.name as asset_name,
					msa.old_price,
					msa.new_price,
					msa.created_at,
					msa.updated_at
				  FROM moderation_sell_asset msa
				  LEFT JOIN ""user"" u ON u.id = msa.user_id
				  LEFT JOIN asset a ON a.id = msa.asset_id
				  ORDER BY msa.id DESC
				  LIMIT :limit OFFSET :offset",
				new
				{
					limit,
					offset,
				});
				
				return new
				{
					data = result,
					columns = new[]
					{
						"#",
						"UAID",
						"User ID",
						"Username",
						"Asset ID",
						"Asset Name",
						"Old Price",
						"New Price",
						"Created At",
						"Updated At",
					},
				};
			}
			case "item-resale-purchase":
			{
			var result = await db.QueryAsync(
				@"SELECT 
					mpra.id,
					mpra.user_asset_id,
					mpra.buyer_user_id,
					ub.username as buyer_username,
					mpra.seller_user_id,
					us.username as seller_username,
					mpra.asset_id,
					mpra.purchase_price,
					mpra.created_at,
					mpra.updated_at
				  FROM moderation_purchase_resale_asset mpra
				  LEFT JOIN ""user"" ub ON ub.id = mpra.buyer_user_id
				  LEFT JOIN ""user"" us ON us.id = mpra.seller_user_id
				  LEFT JOIN asset a ON a.id = mpra.asset_id
				  ORDER BY mpra.id DESC
				  LIMIT :limit OFFSET :offset",
				new
				{
					limit,
					offset,
				});
				
				return new
				{
					data = result,
					columns = new[]
					{
						"#",
						"UAID",
						"Buyer User ID",
						"Buyer Username",
						"Seller User ID",
						"Seller Username",
						"Asset ID",
						"Purchase Price",
						"Created At",
						"Updated At",
					},
				};
			}
			case "asset-modification":
			{
				var result = await db.QueryAsync(
					@"SELECT 
						mma.id,
						mma.created_at,
						mma.asset_id,
						a.name as asset_name,
						mma.old_name,
						mma.new_name,
						mma.old_description,
						mma.new_description,
						mma.actor_id,
						u.username as actor_username
					  FROM moderation_modify_asset mma
					  LEFT JOIN asset a ON a.id = mma.asset_id
					  LEFT JOIN ""user"" u ON u.id = mma.actor_id
					  ORDER BY mma.id DESC
					  LIMIT :limit OFFSET :offset",
					new
					{
						limit,
						offset,
					});
				
				return new
				{
					data = result,
					columns = new[]
					{
						"#",
						"Date",
						"Asset ID",
						"Asset Name",
						"Old Name",
						"New Name",
						"Old Description",
						"New Description",
						"Author ID",
						"Author Username",
					},
				};
			}
            case "item":
            {
                var result = await db.QueryAsync(
                    "SELECT mgi.id, mgi.created_at, mgi.user_asset_id, ua.asset_id, mgi.user_id, u1.username, mgi.author_user_id, u2.username as author_username FROM moderation_give_item mgi INNER JOIN \"user\" u1 ON u1.id = mgi.user_id INNER JOIN \"user\" u2 ON u2.id = mgi.author_user_id INNER JOIN \"user_asset\" ua ON ua.id = mgi.user_asset_id ORDER BY mgi.id DESC LIMIT :limit OFFSET :offset", new
                    {
                        limit,
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "UserAssetID",
                        "AssetID",
                        "UserID",
                        "Username",
                        "Author ID",
                        "Author Username",
                    },
                };
            }
            case "asset":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, asset_id, actor_id, action, created_at FROM moderation_manage_asset ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result.Select(c =>
                    {
                        var oldStatus = (int) c.action;
                        c.action = (object)(ModerationStatus) oldStatus;
                        return c;
                    }),
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Author ID",
                        "Status",
                        "Date",
                    },
                };
            }
            case "alert":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, alert, alert_url, actor_id, created_at FROM moderation_set_alert ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Text",
                        "URL",
                        "Author ID",
                        "Date",
                    },
                };
            }
            case "message":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, subject, body, actor_id, user_id, created_at FROM moderation_admin_message ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Subject",
                        "Body",
                        "Author ID",
                        "User ID",
                        "Date",
                    },
                };
            }
			case "thumbnail":
			{
                var result =
                    await db.QueryAsync(
                        "SELECT id, asset_id, actor_id, content_url, created_at FROM moderation_overwrite_thumbnail ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Author ID",
                        "Content URL",
                        "Date",
                    },
                };
            }
            case "applications":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, application_id, author_user_id, new_status, created_at FROM moderation_change_join_app ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result.Select(c =>
                    {
                        var oldStatus = (int) c.new_status;
                        c.new_status = (object)(UserApplicationStatus) oldStatus;
                        return c;
                    }),
                    columns = new List<string>
                    {
                        "#",
                        "Application ID",
                        "Author ID",
                        "New Status",
                        "Date",
                    },
                };
            }
            case "refund":
            {
                // moderation_refund_transaction
                var result =
                    await db.QueryAsync(
                        "SELECT id, asset_id, actor_id, user_id_one, amount, currency_type, user_asset_id, created_at FROM moderation_refund_transaction ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Author ID",
                        "UserID",
                        "Amount",
                        "Currency",
                        "UAID",
                        "Date",
                    },
                };
            }
			case "password":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, user_id, actor_id, created_at FROM moderation_reset_password ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "User ID",
                        "Author ID",
                        "Date",
                    },
                };
            }
            case "product":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT p.id, p.asset_id, a.name, p.actor_id, p.is_for_sale, price_in_tickets, price_in_robux, p.is_limited, p.is_limited_unique, p.max_copies, p.offsale_at, p.created_at FROM moderation_update_product p LEFT JOIN asset a ON a.id = asset_id ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Name",
                        "Author ID",
                        "IsForSale",
                        "Price (R$)",
                        "Price (T$)",
                        "Limited",
                        "LimitedU",
                        "MaxCopies",
                        "Offsale",
                        "Date",
                    },
                };
            }
            case "robux":
            case "tickets":
            {
                var table = logType == "robux" ? "moderation_give_robux" : "moderation_give_tickets";
                var result = await db.QueryAsync(
                    "SELECT mgr.id, mgr.created_at, mgr.amount, mgr.user_id, u1.username, mgr.author_user_id, u2.username as author_username FROM "+table+" mgr INNER JOIN \"user\" u1 ON u1.id = mgr.user_id INNER JOIN \"user\" u2 ON u2.id = mgr.author_user_id ORDER BY mgr.id DESC LIMIT :limit OFFSET :offset ",
                    new
                    {
                        limit,
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        logType == "robux" ? "Robux Amount" : "Tix Amount",
                        "UserID",
                        "Username",
                        "Author ID",
                        "Author Username",
                    },
                };
            }
            default:
                throw new StaffException("Bad log type " + logType);
        }

    }
	
	[HttpGet("transcripts/tickets"), StaffFilter(Access.GetAdminLogs)]
	public async Task<dynamic> GetTranscriptTickets(int limit = 50, int offset = 0)
	{
		if (limit is > 500 or < 1) limit = 50;

		var tickets = await db.QueryAsync<TranscriptTicket>(
			@"SELECT 
				ticket_id,
				name,
				MAX(created_at) as last_message,
				COUNT(*) as message_count
			FROM moderation_transcripts
			GROUP BY ticket_id, name
			ORDER BY last_message DESC
			LIMIT :limit OFFSET :offset",
			new { limit, offset });

		return new
		{
			data = tickets,
			columns = new[]
			{
				"Ticket #",
				"Name",
				"Last Message Time",
				"Message Count"
			}
		};
	}

	[HttpGet("transcripts/messages"), StaffFilter(Access.GetAdminLogs)]
	public async Task<dynamic> GetTranscriptMessages(long ticketId, int limit = 100, int offset = 0)
	{
		if (limit is > 500 or < 1) limit = 100;

		var messages = await db.QueryAsync<Transcript>(
			@"SELECT mt.*, u.username 
			FROM moderation_transcripts mt
			LEFT JOIN ""user"" u ON u.id = mt.user_id
			WHERE mt.ticket_id = :ticketId
			ORDER BY mt.created_at DESC
			LIMIT :limit OFFSET :offset",
			new { ticketId, limit, offset });

		return new
		{
			data = messages,
			columns = new[]
			{
				"#",
				"Ticket ID",
				"User ID",
				"Username",
				"Discord ID",
				"Message",
				"Created At"
			}
		};
	}

    [HttpGet("getbadges"), StaffFilter(Access.GetUserBadges)]
    public async Task<dynamic> GetUserBadges(long userId)
    {
        return await services.accountInformation.GetUserBadges(userId);
    }

    [HttpPost("givebadge"), StaffFilter(Access.GiveUserBadge)]
    public async Task GiveUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        var ent = BadgesMetadata.Badges.Find(v => v.id == request.badgeId);
        if (ent == null)
            throw new StaffException("BadgeId does not exist");
        await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }

    [HttpPost("deletebadge"), StaffFilter(Access.DeleteUserBadge)]
    public async Task DeleteUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        await db.ExecuteAsync("DELETE FROM user_badge WHERE user_id = :user_id AND badge_id = :badge_id", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }
	
	    [HttpPost("givetickets"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserTickets([Required, FromBody] GiveUserTicketsRequest request)
    {
        // temporary
        if (request.tickets is <= 0 or > 10000000)
            throw new StaffException("Invalid ticket amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_tickets = balance_tickets + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.tickets,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_tickets (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = request.tickets,
            });
    }

    [HttpPost("removetickets"), StaffFilter(Access.GiveUserRobux)]
    public async Task RemoveUserTickets([Required, FromBody] GiveUserTicketsRequest request)
    {
        // temporary

        if (request.tickets is <= 0 or > 10000000)
            throw new StaffException("Invalid tickets amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_tickets = balance_tickets - :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.tickets,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_tickets (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = -request.tickets,
            });
    }

    [HttpPost("giverobux"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserRobux([Required, FromBody] GiveUserRobuxRequest request)
    {
        // temporary

        if (request.robux is <= 0 or > 10000000)
            throw new StaffException("Invalid robux amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_robux = balance_robux + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.robux,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_robux (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = request.robux,
            });
    }

    [HttpPost("removerobux"), StaffFilter(Access.GiveUserRobux)]
    public async Task RemoveUserRobux([Required, FromBody] GiveUserRobuxRequest request)
    {
        // temporary
        if (request.robux is <= 0 or > 10000000)
            throw new StaffException("Invalid robux amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_robux = balance_robux - :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.robux,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_robux (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = -request.robux,
            });
    }

    [HttpGet("user-collectibles"), StaffFilter(Access.GetUserCollectibles)]
    public async Task<dynamic> GetUserCollectibles(long userId)
    {
        var result = (await db.QueryAsync("SELECT asset_id, user_asset.id as user_asset_id, asset.name FROM user_asset INNER JOIN asset ON asset.id = user_asset.asset_id WHERE user_asset.user_id = :user_id AND (asset.is_limited = true OR asset.is_limited_unique = true)",
            new { user_id = userId })).ToList();
        return result;
    }
	
	[HttpGet("user-items"), StaffFilter(Access.GetUserCollectibles)]
	public async Task<dynamic> GetUsetItems(long userId) =>
    (await db.QueryAsync("SELECT asset_id, user_asset.id as user_asset_id, asset.name FROM user_asset INNER JOIN asset ON asset.id = user_asset.asset_id WHERE user_asset.user_id = :user_id AND asset.is_limited = false AND asset.is_limited_unique = false", 
		new { user_id = userId })).ToList();


    [HttpPost("removeitem"), StaffFilter(Access.RemoveUserItem)]
    public async Task RemoveItem([Required, FromBody] RemoveItemRequest request)
    {
        var transferTo = await services.users.GetUserIdFromUsername("BadDecisions");
        var affected = await db.ExecuteAsync(
            "UPDATE user_asset SET price = 0, user_id = :new_user_id, updated_at = now() WHERE user_id = :old_user_id AND user_asset.id = :user_asset_id",
            new
            {
                new_user_id = transferTo,
                old_user_id = request.userId,
                user_asset_id = request.userAssetId,
            });
        if (affected != 1)
            throw new StaffException("User asset is no longer owned by this user");
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
            new
            {
                user_id = transferTo,
                author_user_id = userSession.userId,
                user_asset_id = request.userAssetId,
                user_id_from = request.userId,
            });
    }

    [HttpGet("assets/giveitem-circ"), StaffFilter(Access.GiveUserItem)]
    public async Task<IEnumerable<StaffUserAssetTrackEntry>> GetGiveItemCirc(long assetId, int limit)
    {
        long transferTo;
		
		try
		{
			transferTo = await services.users.GetUserIdFromUsername("BadDecisions");
		}
		catch (RecordNotFoundException)
		{
			throw new StaffException("BadDecisions doesn't exist, please create an account with ID 12 and BadDecisions as the username!");
		}
		
        return (await db.QueryAsync<StaffUserAssetTrackEntry>(
            "SELECT user_asset.id, user_asset.asset_id as assetId, user_asset.user_id as userId, u.username, user_asset.serial FROM user_asset INNER JOIN \"user\" u ON u.id = user_asset.user_id INNER JOIN \"user_ban\" ub ON ub.user_id = user_asset.user_id WHERE user_asset.asset_id = :asset_id AND ((u.status = :status AND ub.created_at <= :time_sub) OR u.id = :bad) AND user_asset.user_id != 1 ORDER BY user_asset.serial DESC NULLS LAST LIMIT :limit",
            new
            {
                bad = transferTo,
                status = AccountStatus.Deleted,
                // ban must be 30 or more days old (bans cannot be appealed after 30 days)
                time_sub = DateTime.UtcNow.Subtract(TimeSpan.FromDays(31)),
                asset_id = assetId,
                limit = limit,
            })).ToList();
    }

    [HttpPost("giveitem"), StaffFilter(Access.GiveUserItem)]
    public async Task GiveItem([Required, FromBody] GiveItemRequest request)
    {
        // temporary
        var details = await services.assets.GetAssetCatalogInfo(request.assetId);
        if (!details.itemRestrictions.Contains("LimitedUnique") && request.giveSerial)
            throw new StaffException("This asset is not limited unique, cannot give serial");
        
        // try to get term copies
        var terminatedCopies = (await GetGiveItemCirc(request.assetId, request.copies)).ToList();
        if (terminatedCopies.Count < request.copies)
        {
            // We'll have to create items
            for (var i = 0; i < (request.copies - terminatedCopies.Count); i++)
            {
                var saleCount = await services.assets.GetSaleCount(request.assetId);
                long? serial = request.giveSerial ? saleCount + 1 : null;

                var id = await db.QuerySingleOrDefaultAsync(
                    "INSERT INTO user_asset (asset_id, user_id, serial) VALUES (:asset_id, :user_id, :serial) RETURNING user_asset.id", new
                    {
                        asset_id = request.assetId,
                        user_id = request.userId,
                        serial = serial,
                    });
                await db.ExecuteAsync(
                    "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, null)",
                    new
                    {
                        user_id = request.userId,
                        user_asset_id = (long)id.id,
                        author_user_id = userSession.userId,
                    });
                
                if (serial != null)
                {
                    // create fake transaction as well
                    await services.economy.InsertTransaction(new AssetPurchaseTransaction(request.userId,
                        details.creatorType, details.creatorTargetId, CurrencyType.Robux, 0, request.assetId, (long)id.id));
                    await services.assets.IncrementSaleCount(request.assetId);
                }
            }
        }
        // Transfer terminated copies and log
        foreach (var item in terminatedCopies)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :uid, updated_at = now(), price = 0 WHERE id = :id", new
            {
                id = item.id,
                uid = request.userId,
            });
            await db.ExecuteAsync(
                "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
                new
                {
                    user_id = request.userId,
                    author_user_id = userSession.userId,
                    user_asset_id = item.id,
                    user_id_from = item.userId,
                });
        }
    }

    [HttpGet("trackitem"), StaffFilter(Access.TrackItem)]
    public dynamic TrackItem(long userAssetId)
    {
        throw new StaffException("Endpoint is not implemented yet. Complain to the owner ;-;");
    }

    [HttpPost("user/delete"), StaffFilter(Access.DeleteUser)]
    public async Task DeleteUser([Required, FromBody] UserIdRequest request)
    {
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Only the owner can GDPR delete accounts");
        if (await IsStaff(request.userId))
            throw new StaffException("Cannot delete this user");
        var k = "staff:userdeletion:v1";
        if ((await redis.StringGetAsync(k)) != null)
            throw new StaffException(
                "An account deletion was already requested recently. Try again in about 10 seconds.");
        await redis.StringSetAsync(k, "{}", TimeSpan.FromSeconds(10));
        
        await services.users.DeleteUser(request.userId, true);
        // reset av
        await ResetAvatar(request);
    }

    [HttpGet("user/usernames"), StaffFilter(Access.GetPreviousUsernames)]
    public async Task<IEnumerable<string>> GetPreviousUsernames(long userId)
    {
        return (await services.users.GetPreviousUsernames(userId)).Select(c => c.username);
    }

    [HttpPost("user/usernames/delete"), StaffFilter(Access.DeleteUsername)]
    public async Task DeleteUsername([Required, FromBody] DeleteUsernameRequest request)
    {
        // Temporary
        var previousNames = (await services.users.GetPreviousUsernames(request.userId)).ToList();
        var totalChanges = previousNames.Where(c => c.username.ToLower() == request.username.ToLower()).ToList();
        if (totalChanges.Count == 0)
            throw new StaffException("The username provided has not been used by this user.");

        var amountToRefund = totalChanges.Count * 1000;
        await services.users.InTransaction(async _ =>
        {
            var usersDb = services.users.db;
            await usersDb.ExecuteAsync("DELETE FROM user_previous_username WHERE user_id = :id AND username ILIKE :name", new
            {
                id = request.userId,
                name = request.username,
            });
            await usersDb.ExecuteAsync(
                "INSERT INTO user_message (subject, body, user_id_from, user_id_to) VALUES (:subject, :body, 1, :user_id)",
                new
                {
                    user_id = request.userId,
                    subject = "Username Refund",
                    body = @$"Hello,

We have deleted one of your previous usernames, ""{request.username}"". You will no longer have access to this username. You have been refunded {amountToRefund} Robux for a total of {totalChanges.Count} times this name was in use.

Thank you for your understanding,


-The Roblox Team"
                });
            await usersDb.ExecuteAsync(
                "UPDATE user_economy SET balance_robux = balance_robux + :amt WHERE user_id = :user_id", new
                {
                    user_id = request.userId,
                    amt = amountToRefund,
                });
            return 0;
        });
    }

    [HttpDelete("user/comment"), StaffFilter(Access.DeleteComment)]
    public async Task DeleteComment([Required, FromQuery] long userId, [Required, FromQuery] long commentId)
    {
        await db.ExecuteAsync("UPDATE asset_comment SET comment = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = commentId,
            user_id = userId,
        });
    }
    
    [HttpPost("delete-forum-post"), StaffFilter(Access.DeleteForumPost)]
    public async Task DeleteForumPost([Required, FromBody] DeleteForumPostRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync("SELECT id, thread_id FROM forum_post WHERE id = :id", new
        {
            id = request.postId,
        });
        if (details == null)
            throw new StaffException("Post does not exist");
        if (details.thread_id == null)
        {
            await db.ExecuteAsync("DELETE FROM forum_post WHERE id = :id OR thread_id = :id",
                new { id = request.postId });
        }
        else
        {
            await db.ExecuteAsync("UPDATE forum_post SET post = '[ Content Deleted ]' WHERE id = :id",
                new { id = request.postId });
        }
    }

    [HttpPost("lock-forum-thread"), StaffFilter(Access.LockForumThread)]
    public async Task LockForumThread(long threadId)
    {
        await db.ExecuteAsync("UPDATE forum_post SET is_locked = true WHERE id = :id AND thread_id IS NULL", new
        {
            id = threadId,
        });
    }

    [HttpPost("lottery/run"), StaffFilter(Access.RunLottery)]
    public async Task<dynamic> RunLottery()
    {
        var log = Writer.CreateWithId(LogGroup.Lottery);
        log.Info("Lottery start. Initiated by {0}", userSession.userId);
        var allItems = (await GetLotteryItems()).ToList();
        log.Info("There are {0} items available",allItems.Count);
        if (allItems.Count == 0)
            throw new StaffException("There are no items available for lottery");
        var allUsers = (await GetEligibleLotteryUsers()).ToList();
        log.Info("There are {0} users available",allUsers.Count);
        var randomItem = allItems[new Random().Next(0, allItems.Count)];
        log.Info("Picked item. UAID = {0} Old Owner = {1}", randomItem.userAssetId, randomItem.userId);
        var randomUser = allUsers[new Random().Next(0, allUsers.Count)];
        log.Info("Picked user. ID = {0}", randomUser.userId);
        // transfer item...
        await db.ExecuteAsync("UPDATE user_asset SET user_id = :user_id, updated_at = now(), price = 0 WHERE id = :id", new
        {
            user_id = randomUser.userId,
            id = randomItem.userAssetId,
        });
        log.Info("item {0} transferred from {1} to {2}", randomItem.userAssetId, randomItem.userId, randomUser.userId);
        // send messages
        await services.privateMessages.CreateMessage(randomUser.userId, 1, "You Won The Lottery!",
            "Congrats! Your account was chosen as the winner for today's lottery, where a Limited or Limited Unique item is given away after the owner has been offline for 6 months or more.\n\nThe item you won is: " +
            randomItem.name + ", which has a Recent Average Price of " + randomItem.recentAveragePrice + ". The item has already been added to your account - no action is required to claim it.\nIf you do not want this item, you can sell it on the market or trade it with another user for an item you do want.\n\n-The Roblox team");
        log.Info("sent message to user picked {0}", randomUser.userId);
        await services.privateMessages.CreateMessage(randomItem.userId, 1, "Inactive Account Penalty",
            "Hello\n\nAs part of our efforts to encourage activity and discourage account compromises, we have removed the item " +
            randomItem.name +
            " from your inventory, and awarded it to a random player who was active at the time of our lottery draw. We understand that you may not have been expecting this to happen, however, it is outlined in our policy that we reserve the right to remove items from accounts once they've been inactive for 6 months or longer. At the time of sending this message, your account has been inactive since " + randomUser.onlineAt.ToString("MMMM dd, yyyy") + "\n\nItems taken from your account for lottery purposes cannot be restored. We hope you understand,\n\n-The Roblox Team");
        log.Info("sent message to old asset owner {0}", randomItem.userId);
        await redis.StringSetAsync("lottery_run_v1", "{}", TimeSpan.FromMinutes(30));
        return new
        {
            randomItem.name, randomUser.username,
        };
    }

	[HttpGet("lottery/get-users-eligible")]
	public async Task<IEnumerable<UserLotteryEntry>> GetEligibleLotteryUsers()
	{
		var eligibleUsers = await db.QueryAsync<UserLotteryEntry>(
			"SELECT u.username, u.id as userId, u.online_at as onlineAt " +
			"FROM \"user\" u " +
			"WHERE (u.online_at >= :online_time AND u.status = :status) " +
			"AND u.id NOT IN (1, 2500, 12)", new
			{
				status = AccountStatus.Ok,
				online_time = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)),
			});
		return eligibleUsers;
	}

	[HttpGet("lottery/get-items")]
	public async Task<IEnumerable<LotteryItemEntry>> GetLotteryItems()
	{
		var log = Writer.CreateWithId(LogGroup.Lottery);
		
		var baddecisionsassets = await db.QuerySingleOrDefaultAsync<int>(
			"SELECT COUNT(*) FROM user_asset ua " +
			"INNER JOIN \"asset\" a ON a.id = ua.asset_id " +
			"WHERE ua.user_id = 12 AND (a.is_limited OR a.is_limited_unique) AND NOT a.is_for_sale");
		
		log.Info($"BadDecisions has {baddecisionsassets} eligible limited items");

		var inactiveitems = await db.QueryAsync<LotteryItemEntry>(
			"SELECT a.name, a.id as assetId, a.recent_average_price as recentAveragePrice, " +
			"u.id as userId, u.online_at as onlineAt, u.username, ua.id as userAssetId " +
			"FROM user_asset ua " +
			"INNER JOIN \"user\" u on u.id = ua.user_id " +
			"INNER JOIN \"asset\" a ON a.id = ua.asset_id " +
			"WHERE (u.id != 1 AND u.online_at <= :time AND (a.is_limited OR a.is_limited_unique) " +
			"AND NOT a.is_for_sale AND u.status = :status) " +
			"ORDER BY u.online_at LIMIT 1000", new
			{
				status = AccountStatus.Ok,
				time = DateTime.UtcNow.Subtract(TimeSpan.FromDays(31)),
			});

		var baddecisionsitems = await db.QueryAsync<LotteryItemEntry>(
			"SELECT a.name, a.id as assetId, a.recent_average_price as recentAveragePrice, " +
			"u.id as userId, u.online_at as onlineAt, u.username, ua.id as userAssetId " +
			"FROM user_asset ua " +
			"INNER JOIN \"user\" u on u.id = ua.user_id " +
			"INNER JOIN \"asset\" a ON a.id = ua.asset_id " +
			"WHERE ua.user_id = 12 AND (a.is_limited OR a.is_limited_unique) AND NOT a.is_for_sale " +
			"AND u.status = :status " +
			"ORDER BY a.id LIMIT 1000",
			new
			{
				status = AccountStatus.Ok
			});

		if (baddecisionsitems.Any())
		{
			foreach (var item in baddecisionsitems)
			{
				log.Info($"BadDecisions item: {item.name} (AssetID: {item.assetId}, UAID: {item.userAssetId})");
			}
		}
		else
		{
			log.Info("no BadDecisions items found despite count showing some?");
			
			var dbgitems = await db.QueryAsync(
				"SELECT ua.id as user_asset_id, a.id as asset_id, a.name, a.is_limited, a.is_limited_unique, a.is_for_sale " +
				"FROM user_asset ua INNER JOIN asset a ON a.id = ua.asset_id " +
				"WHERE ua.user_id = 12");
				
			foreach (var item in dbgitems)
			{
				//log.Info($"dabh {item.name} (AssetID: {item.asset_id}, UAID: {item.user_asset_id}, Limited: {item.is_limited}, LimitedUnique: {item.is_limited_unique}, ForSale: {item.is_for_sale})");
			}
		}

		var combined = inactiveitems.ToList();
		combined.AddRange(baddecisionsitems);

		log.Info($"found {inactiveitems.Count()} items from inactive users");
		log.Info($"found {baddecisionsitems.Count()} items from BadDecisions");
		log.Info($"total items in pool: {combined.Count}");

		return combined;
	}

    [HttpGet("asset/types")]
    public Dictionary<int,string> GetAssetTypes()
    {
        var all = new Dictionary<int, string>();
        foreach (var value in Enum.GetValues<Type>())
        {
            all[(int) value] = value.ToString();
        }

        return all;
    }

    [HttpGet("asset/genres")]
    public Dictionary<int,string> GetAssetGenres()
    {
        var all = new Dictionary<int, string>();
        foreach (var value in Enum.GetValues<Genre>())
        {
            all[(int) value] = value.ToString();
        }

        return all;
    }

	[HttpPost("asset/re-render"), StaffFilter(Access.RequestAssetReRender)]
	public async Task RequestAssetReRender([Required, FromBody] ReRenderRequest request)
	{
		var details = await services.assets.GetAssetCatalogInfo(request.assetId);
		
		if (details.assetType == Type.Image)
		{
			throw new StaffException("You cannot re-render images, they already show up on-site if accepted");
		}
		
		services.assets.RenderAsset(request.assetId, details.assetType);
	}
    
    [HttpGet("asset/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<dynamic> GetAssetDetails(long assetId)
    {
        var devInfo = await services.assets.MultiGetAssetDeveloperDetails(new[] {assetId});
        var info = await services.assets.MultiGetInfoById(new[] {assetId});
        return new
        {
            developerInfo = devInfo, info,
        };
    }

    [HttpGet("product/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<dynamic> GetProductDetails(long assetId)
    {
        return await services.assets.GetProductForAsset(assetId);
    }

    private async Task InsertProductLog(long assetId, long userId, bool isLimited, bool isLimitedUnique, DateTime? offsaleAt, int? maxCopies, long? priceRobux, long?priceTickets, bool isForSale)
    {
        await db.ExecuteAsync("INSERT INTO moderation_update_product (asset_id, actor_id, is_limited, is_limited_unique, offsale_at, max_copies, price_in_robux, price_in_tickets, is_for_sale) VALUES (@asset_id, @actor_id, @is_limited, @is_limited_unique, @offsale_at, @max_copies, @robux, @tix, @is_for_sale)", new
        {
            asset_id = assetId,
            actor_id = userId,
            is_limited = isLimited,
            is_limited_unique = isLimitedUnique,
            offsale_at = offsaleAt,
            max_copies = maxCopies,
            robux = priceRobux,
            tix = priceTickets,
            is_for_sale = isForSale,
        });
    }

    [HttpPatch("asset/product"), StaffFilter(Access.SetAssetProduct)]
    public async Task UpdateAssetProduct([Required, FromBody] UpdateProductRequest request)
    {
        var details = await services.assets.GetProductForAsset(request.assetId);
        var permissions = (await services.users.GetStaffPermissions(safeUserSession.userId)).Select(c => c.permission).ToArray();

        if (!StaffFilter.IsOwner(safeUserSession.userId))
        {
            if (!permissions.Contains(Access.MakeItemLimited))
            {
                // cannot update a limited item
                if (details.isLimited || details.isLimitedUnique)
                    throw new StaffException("You do not have permission to update a limited item");
                
                request.isLimited = false;
                request.isLimitedUnique = false;
                request.maxCopies = null;
            }

            // cannot update a product that is not owned by the admin account
            var extraInfo = await services.assets.GetAssetCatalogInfo(request.assetId);
            var allowedTypes = new List<Type>()
            {
                Type.Hat,
                Type.FrontAccessory,
                Type.BackAccessory,
                Type.NeckAccessory,
                Type.HairAccessory,
                Type.ShoulderAccessory,
                Type.BackAccessory,
                Type.FrontAccessory,
                Type.WaistAccessory,
                Type.NeckAccessory,
                Type.Package,
            };
        }
        // check if existing log exists (old products don't have logs)
        var existingLog = await db.QuerySingleOrDefaultAsync<Total>("SELECT count(*) as total FROM moderation_update_product WHERE asset_id = :asset_id", new
        {
            asset_id = request.assetId,
        });
        if (existingLog.total == 0)
        {
            // insert log
            await InsertProductLog(request.assetId, 1, details.isLimited, details.isLimitedUnique, details.offsaleAt, details.serialCount, details.priceRobux, details.priceTickets, details.isForSale);
        }
		
		DateTime? Offsale = null;

		if (request.offsaleDeadline != null)
		{
			var offsetValue = request.offsaleDeadline.value;
			var offsetUnit = request.offsaleDeadline.unit?.ToLower();
			
			Offsale = offsetUnit switch
			{
				"seconds" => DateTime.UtcNow.AddSeconds(offsetValue),
				"minutes" => DateTime.UtcNow.AddMinutes(offsetValue),
				"hours" => DateTime.UtcNow.AddHours(offsetValue),
				"days" => DateTime.UtcNow.AddDays(offsetValue),
				"weeks" => DateTime.UtcNow.AddDays(offsetValue * 7),
				"months" => DateTime.UtcNow.AddMonths(offsetValue),
				_ => throw new StaffException("Invalid time unit specified")
			};
		}

        await InsertProductLog(request.assetId, safeUserSession.userId, request.isLimited, request.isLimitedUnique, Offsale, request.maxCopies, request.priceRobux, request.priceTickets, request.isForSale);

        await services.assets.UpdateAssetMarketInfo(request.assetId, request.isForSale, request.isLimited,
            request.isLimitedUnique, request.maxCopies, Offsale);
		await services.assets.UpdateAssetVisibility(request.assetId, request.isVisible);
        await services.assets.SetItemPrice(request.assetId, request.priceRobux, request.priceTickets);
    }

    [HttpPatch("asset/name"), StaffFilter(Access.SetAssetProduct)]
    public async Task UpdateAssetInfo([Required, FromBody] UpdateNameRequest request)
    {
        await services.assets.UpdateAssetNameAndDesc(request.assetId, request.newName);
    }
	
	[HttpPatch("asset/modify"), StaffFilter(Access.SetAssetProduct)]
	public async Task ModifyAsset([Required, FromBody] ModifyAssetRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.name)) 
			throw new StaffException("Name cannot be empty");
		if (request.name.Length > 100)
			throw new StaffException("Name is too long (max 100 characters)");
		if (request.description != null && request.description.Length > 1000)
			throw new StaffException("Description is too long (max 1000 characters)");

		var assetInfo = await services.assets.GetAssetCatalogInfo(request.assetId);

		var canModify = true;
		if (assetInfo.creatorType == CreatorType.User && assetInfo.creatorTargetId == 1)
		{
			canModify = true;
		}
		else if (await services.assets.CanUserModifyItem(request.assetId, userSession.userId))
		{
			canModify = true;
		}

		if (!canModify)
			throw new StaffException("Not authorized to modify this item");

		await services.assets.UpdateAssetNameAndDesc(
			request.assetId, 
			request.name, 
			request.description ?? assetInfo.description
		);

		await db.ExecuteAsync(
			"INSERT INTO moderation_modify_asset (asset_id, actor_id, old_name, new_name, old_description, new_description) " +
			"VALUES (:asset_id, :actor_id, :old_name, :new_name, :old_description, :new_description)",
			new
			{
				asset_id = request.assetId,
				actor_id = userSession.userId,
				old_name = assetInfo.name,
				new_name = request.name,
				old_description = assetInfo.description,
				new_description = request.description ?? assetInfo.description
			});
	}

	[HttpPost("bundle/copy-from-roblox"), StaffFilter(Access.CreateBundleCopiedFromRoblox)]
    public async Task<dynamic> CopyBundle(long bundleId)
    {
        var details = await services.robloxApi.GetBundle(bundleId);
        if (details.bundleType != "BodyParts") throw new StaffException("Invalid bundleType " + details.bundleType);
        
        // Check if duplicate?
        var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
        {
            limit = 10,
            include18Plus = true,
            includeNotForSale = true,
            creatorType = CreatorType.User,
            creatorTargetId = 1,
            keyword = details.name,
        });
        if (alreadyExists._total > 0)
        {
            var existing = await services.assets.MultiGetInfoById(alreadyExists.data.Select(c => c.id));
            foreach (var ent in existing)
            {
                if (ent.assetType == Type.Package && ent.name == details.name)
                {
                    throw new StaffException("It looks like this bundle already exists: AssetID=" + ent.id);
                }
            }
        }

        var ids = new List<long>();
        foreach (var item in details.items)
        {
            if (item.type != "Asset") continue;
            Console.WriteLine("Getting {0}", item.id);
            var info = await services.robloxApi.GetProductInfo(item.id, false);
            
            var content = await services.robloxApi.GetAssetContent(item.id);
            var isOk = await services.assets.ValidateAssetFile(content, info.AssetTypeId.Value);
            if (!isOk)
                throw new StaffException("The asset file doesn't look correct. Please try again.");
            content.Position = 0;
            // Make the item!
            var assetDetails = await services.assets.CreateAsset(item.name, null, 1,
                CreatorType.User, 1, content, info.AssetTypeId.Value, Genre.All, ModerationStatus.ReviewApproved,
                DateTime.UtcNow, DateTime.UtcNow, item.id);
            ids.Add(assetDetails.assetId);
        }

        // Now make the bundle
        return await CreateAsset(new CreateAssetRequest()
        {
            assetTypeId = Type.Package,
            description = details.description,
            genre = Genre.All,
            isForSale = false,
			isVisible = true,
            isLimited = false,
            isLimitedUnique = false,
            maxCopies = null,
            name = details.name,
            offsaleDeadline = null,
            packageAssetIds = string.Join(",", ids.Select(c => c.ToString())),
        });
    }
	
	[HttpPost("bundle/copy-animation-bundle"), StaffFilter(Access.CreateBundleCopiedFromRoblox)]
	public async Task<dynamic> CopyAnimationBundle(long bundleId)
	{
		long? IdleAssetId = null;
		string BundleThumbHash = null;
		var details = await services.robloxApi.GetBundle(bundleId);
		if (details.bundleType != "AvatarAnimations") 
			throw new StaffException("Invalid bundleType " + details.bundleType + ". Only AvatarAnimations bundles are supported.");
		
		// Check if duplicate?
		var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
		{
			limit = 10,
			include18Plus = true,
			includeNotForSale = true,
			creatorType = CreatorType.User,
			creatorTargetId = 1,
			keyword = details.name,
		});
		
		if (alreadyExists._total > 0)
		{
			var existing = await services.assets.MultiGetInfoById(alreadyExists.data.Select(c => c.id));
			foreach (var ent in existing)
			{
				if (ent.assetType == Type.Package && ent.name == details.name)
				{
					throw new StaffException("It looks like this bundle already exists: AssetID=" + ent.id);
				}
			}
		}

		var ids = new List<long>();
		var ThumbTasks = new List<Task>();
		
		foreach (var item in details.items)
		{
			if (item.type != "Asset") continue;
			
			Console.WriteLine("Getting animation {0}", item.id);
            var info = await services.robloxApi.GetProductInfo(item.id, false);
            
            Type assetType = MapAnimType(info.AssetTypeId?.ToString());
            
            if (assetType != Type.Animation)
            {
                Console.WriteLine("Skipping non-animation item {0} with type {1}", item.id, info.AssetTypeId);
                continue;
            }
			
			var content = await services.robloxApi.GetAssetContent(item.id);
			var isOk = await services.assets.ValidateAssetFile(content, info.AssetTypeId.Value);
			if (!isOk)
				throw new StaffException("The asset file doesn't look correct. Please try again.");
			
			content.Position = 0;
			
			var assetDetails = await services.assets.CreateAsset(item.name, null, 1,
				CreatorType.User, 1, content, info.AssetTypeId.Value, Genre.All, 
				ModerationStatus.ReviewApproved, DateTime.UtcNow, DateTime.UtcNow, item.id);
			
			ids.Add(assetDetails.assetId);
			
			if (info.AssetTypeId?.ToString().Contains("IdleAnimation", StringComparison.OrdinalIgnoreCase) == true)
			{
				IdleAssetId = assetDetails.assetId;
				Console.WriteLine($"got IdleAnimation for bundle thumb: {assetDetails.assetId}");
			}
			
			ThumbTasks.Add(DownloadAndSetAnimThumb(item.id, assetDetails.assetId));
		}

		await Task.WhenAll(ThumbTasks);

		 if (ids.Count == 0)
				throw new StaffException("No valid animation assets were found in the bundle");
			
			var bundle = await CreateAsset(new CreateAssetRequest()
			{
				assetTypeId = Type.Package,
				description = details.description,
				genre = Genre.All,
				isForSale = false,
				isVisible = true,
				isLimited = false,
				isLimitedUnique = false,
				maxCopies = null,
				name = details.name,
				offsaleDeadline = null,
				packageAssetIds = string.Join(",", ids.Select(c => c.ToString())),
			});

			if (IdleAssetId.HasValue)
			{
			_ = Task.Run(async () =>
			{
                await Task.Delay(TimeSpan.FromSeconds(10));
                
                var idleThumb = await db.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT content_url FROM asset_thumbnail WHERE asset_id = :asset_id",
                    new { asset_id = IdleAssetId.Value });
				
				if (idleThumb != null && idleThumb.content_url != null)
				{
					BundleThumbHash = idleThumb.content_url;
					var LatestBundlever = await services.assets.GetLatestAssetVersion(bundle.assetId);
					
					var existingThumb = await db.QuerySingleOrDefaultAsync<dynamic>(
						"SELECT asset_id FROM asset_thumbnail WHERE asset_id = :asset_id",
						new { asset_id = bundle.assetId });

					if (existingThumb != null)
					{
						await db.ExecuteAsync(
							"UPDATE asset_thumbnail SET content_url = :content_url, asset_version_id = :asset_version_id, updated_at = NOW() WHERE asset_id = :asset_id",
							new
							{
								asset_id = bundle.assetId,
								asset_version_id = LatestBundlever.assetVersionId,
								content_url = BundleThumbHash
							});
					}
					else
					{
						await db.ExecuteAsync(
							"INSERT INTO asset_thumbnail (asset_id, asset_version_id, content_url, created_at, updated_at, moderation_status) " +
							"VALUES (:asset_id, :asset_version_id, :content_url, NOW(), NOW(), :moderation_status)",
							new
							{
								asset_id = bundle.assetId,
								asset_version_id = LatestBundlever.assetVersionId,
								content_url = BundleThumbHash,
								moderation_status = (int)ModerationStatus.ReviewApproved
							});
					}
					
					Console.WriteLine($"Set bundle thumbnail to IdleAnimation thumbnail");
				}
			});
		}
		
		return bundle;
	}

	private Type MapAnimType(string RBXtype)
	{
		if (string.IsNullOrEmpty(RBXtype))
			return Type.Animation;
		
		var Types = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Animation",
			"ClimbAnimation",
			"FallAnimation", 
			"IdleAnimation",
			"JumpAnimation",
			"RunAnimation",
			"SwimAnimation",
			"WalkAnimation",
			"PoseAnimation",
		};
		
		return Types.Contains(RBXtype) ? Type.Animation : Type.Animation;
	}

	private async Task DownloadAndSetAnimThumb(long robloxAssetId, long siteAssetId)
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
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
			client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
			client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        
			var response = await client.PostAsync("https://thumbnails.roblox.com/v1/batch",
				new StringContent(JsonConvert.SerializeObject(ThumbReq), 
				System.Text.Encoding.UTF8, "application/json"));
			
			if (!response.IsSuccessStatusCode)
			{
				var err = await response.Content.ReadAsStringAsync();
				Console.WriteLine($"thumb API error for {robloxAssetId}:");
				Console.WriteLine($"code: {response.StatusCode}");
				Console.WriteLine($"response: {err}");
				throw new Exception($"thumb API returned {response.StatusCode}: {err}");
			}
			
			var content = await response.Content.ReadAsStringAsync();
			var ThumbnailRes = JsonConvert.DeserializeObject<RobloxThumbnailBatchResponse>(content);
			
			if (ThumbnailRes?.data == null || ThumbnailRes.data.Length == 0)
				throw new Exception("No thumb data received or bad response");
			
			var ThumbData = ThumbnailRes.data[0];
			if (ThumbData.state != "Completed" || string.IsNullOrEmpty(ThumbData.imageUrl))
				throw new Exception($"Thumbnail not available: {ThumbData.errorMessage}");
			
			var ImageRes = await client.GetAsync(ThumbData.imageUrl);
			ImageRes.EnsureSuccessStatusCode();
			
			using var Stream = await ImageRes.Content.ReadAsStreamAsync();
			
			// get hash
			using var sha256 = SHA256.Create();
			var hashBytes = await sha256.ComputeHashAsync(Stream);
			var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
			
			var Thumbpath = Path.Combine(Configuration.ThumbnailsDirectory, hash + ".png");
			Stream.Position = 0;
			
			using (var FStream = System.IO.File.Create(Thumbpath))
			{
				await Stream.CopyToAsync(FStream);
			}

			var latestVersion = await services.assets.GetLatestAssetVersion(siteAssetId);
			
			var existingThumb = await db.QuerySingleOrDefaultAsync<dynamic>(
				"SELECT asset_id FROM asset_thumbnail WHERE asset_id = :asset_id",
				new { asset_id = siteAssetId });

			if (existingThumb != null)
			{
				await db.ExecuteAsync(
					"UPDATE asset_thumbnail SET content_url = :content_url, asset_version_id = :asset_version_id, updated_at = NOW() WHERE asset_id = :asset_id",
					new
					{
						asset_id = siteAssetId,
						content_url = hash,
						asset_version_id = latestVersion.assetId
					});
			}
			else
			{
				await db.ExecuteAsync(
					"INSERT INTO asset_thumbnail (asset_id, asset_version_id, content_url, created_at, updated_at, moderation_status) " +
					"VALUES (:asset_id, :asset_version_id, :content_url, NOW(), NOW(), :moderation_status)",
					new
					{
						asset_id = siteAssetId,
						asset_version_id = latestVersion.assetId,
						content_url = hash,
						moderation_status = (int)ModerationStatus.ReviewApproved
					});
			}
			
			Console.WriteLine($"Successfully set thumbnail for asset {siteAssetId} from Roblox asset {robloxAssetId}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to download thumbnail for animation {robloxAssetId}: {ex.Message}");
		}
	}

    [HttpPost("asset/copy-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<dynamic> CopyAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        if (!request.force)
        {
            // Check duplicate id first
            try
            {
                // Check if already exists
                var ourAssetId = await services.assets.GetAssetIdFromRobloxAssetId(request.assetId);
                return new
                {
                    assetId = ourAssetId,
                };
            }
            catch (RecordNotFoundException)
            {
                // Don't care
            }
        }
        
        var details = await services.robloxApi.GetProductInfo(request.assetId, true);
        var allowedTypes = new List<Models.Assets.Type>()
        {
            Type.Hat,
            Type.HairAccessory,
            Type.FrontAccessory,
            Type.BackAccessory,
            Type.WaistAccessory,
            Type.NeckAccessory,
            Type.Gear,
            Type.Face,
            Type.ShoulderAccessory,
            Type.FaceAccessory,
            Type.Head,
			Type.EmoteAnimation
        };
        if (details.AssetTypeId == null || !allowedTypes.Contains(details.AssetTypeId.Value))
            throw new StaffException("Cannot copy this assetType: " + details.AssetTypeId);
        if (string.IsNullOrWhiteSpace(details.Name))
            throw new StaffException("Name cannot be null or empty");
        if (details.IsLimited == null || details.IsLimitedUnique == null)
            throw new StaffException("Product details were invalid for this item. Try again");
        
        if (!request.force)
        {
            // Check if duplicate?
            var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
            {
                limit = 10,
                include18Plus = true,
                includeNotForSale = true,
                creatorType = CreatorType.User,
                creatorTargetId = 1,
                keyword = details.Name,
            });
            if (alreadyExists._total != 0 && alreadyExists.data != null)
                foreach (var item in alreadyExists.data)
                {
                    var info = await services.assets.GetAssetCatalogInfo(item.id);
                    if (info.assetType == details.AssetTypeId)
                        throw new StaffException("It looks like this item already exists: AssetID=" + info.id +
                                                 "\nIf this is incorrect, click the 'force' button to upload this item anyway.");
                }
        }
        var content = await services.robloxApi.GetAssetContent(request.assetId);
        var isOk = await services.assets.ValidateAssetFile(content, details.AssetTypeId.Value);
        if (!isOk)
            throw new StaffException("The asset file doesn't look correct. Please try again.");
        content.Position = 0;
        // Now make the item!
        var assetDetails = await services.assets.CreateAsset(details.Name, details.Description, 1,
            CreatorType.User, 1, content, details.AssetTypeId.Value, Genre.All, ModerationStatus.ReviewApproved,
            DateTime.UtcNow, DateTime.UtcNow, request.assetId);
			
		if (details.AssetTypeId == Type.EmoteAnimation)
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(2));
					await DownloadAndSetAnimThumb(request.assetId, assetDetails.assetId);
				}
				catch (Exception ex)
				{
					Writer.Info(LogGroup.AdminApi, $"failed to set emote thumb for {assetDetails.assetId}: {ex}");
				}
			});
		}
	
        await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetId = assetDetails.assetId,
                robloxAssetId = request.assetId,
                actorId = safeUserSession.userId,
            });
        
        return new
        {
            assetId = assetDetails.assetId,
        };
    }

    [HttpPost("asset/create"), StaffFilter(Access.CreateAsset)]
    public async Task<dynamic> CreateAsset([Required, FromForm] CreateAssetRequest request)
	{
		if (request.isLimitedUnique) request.isLimited = true;
		if (!Enum.IsDefined(request.assetTypeId))
			throw new StaffException("Bad assetTypeId");

		var isPackage = request.assetTypeId == Type.Package;
		var disableRender = isPackage;
		IEnumerable<long>? packageAssetIds = null;

		if (!isPackage && request.rbxm == null)
			throw new StaffException("No file specified");

		if (isPackage)
		{
			packageAssetIds = request.packageAssetIds.Split(",").Select(long.Parse);
			if (request.packageAssetIds == null)
				throw new StaffException("Must specify assetIds when creating a package");

			var packages = (await services.assets.MultiGetAssetDeveloperDetails(packageAssetIds)).ToList();
			var result = new Dictionary<Type, int>();

			foreach (var item in packages)
			{
				var type = (Type) item.typeId;
				if (!result.ContainsKey(type))
				{
					result[type] = 0;
				}
				result[type]++;
			}

			var optionalOneOf = new List<Type>() {Type.LeftArm, Type.LeftLeg, Type.RightLeg, Type.RightArm, Type.Torso, Type.Head, Type.Gear, Type.Shirt, Type.Pants, Type.Face, Type.ClimbAnimation, Type.FallAnimation, Type.IdleAnimation, Type.JumpAnimation, Type.RunAnimation, Type.SwimAnimation, Type.WalkAnimation};
			var optionalCanHaveMoreThanOne = new List<Type>() {Type.Hat, Type.HairAccessory, Type.ShoulderAccessory, Type.BackAccessory, Type.FrontAccessory, Type.WaistAccessory, Type.NeckAccessory};

			foreach (var type in optionalOneOf)
			{
				if (result.ContainsKey(type) && result[type] > 1)
					throw new StaffException("Package has too many of this type: " + type);
			}

			packageAssetIds = packages
				.Where(c => optionalOneOf.Contains((Type)c.typeId) || optionalCanHaveMoreThanOne.Contains((Type)c.typeId))
				.Select(c => c.assetId);
		}

		Stream? file = null;
		if (request.rbxm != null)
		{
			var fileData = request.rbxm.OpenReadStream();
			
			if (request.assetTypeId != Type.Audio && request.assetTypeId != Type.Image && request.assetTypeId != Type.Mesh && request.assetTypeId != Type.Place)
			{
				var isOk = await services.assets.ValidateAssetFile(fileData, request.assetTypeId);
				if (!isOk)
					throw new StaffException("The asset file doesn't look correct. Please try again.");
			}

			fileData.Position = 0;
			file = fileData;
		}
		
		request.description ??= "No description provided.";
		var assetDetails = await services.assets.CreateAsset(
			request.name, 
			request.description, 
			1, 
			CreatorType.User, 
			1, 
			file, 
			request.assetTypeId, 
			request.genre, 
			ModerationStatus.ReviewApproved, 
			DateTime.UtcNow, 
			DateTime.UtcNow, 
			request.robloxAssetId, 
			disableRender
		);

		if (request.assetTypeId == Type.Package)
		{
			if (packageAssetIds == null)
				throw new StaffException("packageAssetIds cannot be null when creating a package");

			foreach (var id in packageAssetIds.Distinct())
			{
				await services.assets.InsertPackageAsset(assetDetails.assetId, id);
			}
			services.assets.RenderAsset(assetDetails.assetId, request.assetTypeId);
		}
		
		DateTime? Offsale = null;

		if (request.offsaleDeadline != null)
		{
			var offsetValue = request.offsaleDeadline.value;
			var offsetUnit = request.offsaleDeadline.unit?.ToLower();
			
			Offsale = offsetUnit switch
			{
				"seconds" => DateTime.UtcNow.AddSeconds(offsetValue),
				"minutes" => DateTime.UtcNow.AddMinutes(offsetValue),
				"hours" => DateTime.UtcNow.AddHours(offsetValue),
				"days" => DateTime.UtcNow.AddDays(offsetValue),
				"weeks" => DateTime.UtcNow.AddDays(offsetValue * 7),
				"months" => DateTime.UtcNow.AddMonths(offsetValue),
				_ => throw new StaffException("Invalid time unit specified")
			};
		}

		await services.assets.SetItemPrice(assetDetails.assetId, request.price, null);
		await services.assets.UpdateAssetVisibility(assetDetails.assetId, request.isVisible);
		await services.assets.UpdateAssetMarketInfo(assetDetails.assetId, request.isForSale, request.isLimited, request.isLimitedUnique, request.maxCopies, Offsale);

		return assetDetails;
	}

    [HttpPost("asset/create/clothing"), StaffFilter(Access.CreateClothingAsset)]
    public async Task<dynamic> CreateClothingAsset([Required, FromForm] CreateClothingRequest request)
    {
        if (request.file == null)
            throw new StaffException("No file specified");
        
        var buf = request.file.OpenReadStream();
        var ok = await services.assets.ValidateClothing(buf, request.assetTypeId);
        if (ok == null) throw new StaffException("Invalid file provided");
        // create clothing asset
        buf.Position = 0;
        var texture = await services.assets.CreateAsset(request.file.FileName, $"{request.assetTypeId} Image", 1,
            CreatorType.User, 1, buf, Type.Image, Genre.All, ModerationStatus.ReviewApproved, DateTime.UtcNow, DateTime.UtcNow);
        // create the asset itself
        var asset = await services.assets.CreateAsset(request.name, request.description, 1, CreatorType.User, 1,
            null, request.assetTypeId, request.genre, ModerationStatus.ReviewApproved, DateTime.UtcNow,
            DateTime.UtcNow, request.robloxAssetId, false, texture.assetId);
        // add market data
        await services.assets.SetItemPrice(asset.assetId, request.price, null);
        await services.assets.UpdateAssetMarketInfo(asset.assetId, request.isForSale, false, false,
            null, null);
        return asset;
    }
	
	// this is for Not a Bad Bubba
	// basically does the same thing as copy ugc but with a custom RBXM the user uploads
	[HttpPost("asset/create-custom-asset")]
	[StaffFilter(Access.CreateAsset)]
	public async Task<IActionResult> UploadCustomAsset([FromForm] UploadCustomAssetReq request)
	{
		string rbxmx = null;
		string Mesh = null;
		string OBJ = null;
		var filestoclean = new List<string>();
		string CWD = null;

		try
		{
			CWD = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(CWD);
			Writer.Info(LogGroup.AdminApi, $"made temp folder: {CWD}");

			if (request.OBJ == null || request.OBJ.Length == 0)
				throw new StaffException("OBJ file is required");

			if (request.RBXM == null || request.RBXM.Length == 0)
				throw new StaffException("RBXM file is required");

			var rbxm = Path.Combine(CWD, $"{Guid.NewGuid()}.rbxm");
			await using (var stream = System.IO.File.Create(rbxm))
			{
				await request.RBXM.CopyToAsync(stream);
			}
			filestoclean.Add(rbxm);
			Writer.Info(LogGroup.AdminApi, $"saved uploaded RBXM to: {rbxm}");

			OBJ = Path.Combine(CWD, $"{Guid.NewGuid()}.obj");
			await using (var stream = System.IO.File.Create(OBJ))
			{
				await request.OBJ.CopyToAsync(stream);
			}
			filestoclean.Add(OBJ);

			Mesh = await ProcessOBJ(OBJ, CWD);
			if (!System.IO.File.Exists(Mesh))
			{
				throw new StaffException($"Mesh was not created, your OBJ file may be invalid!");
			}
			filestoclean.Add(Mesh);

			var newmesh = await UploadUGCMesh(Mesh);

			rbxmx = await ConvertRBXM(rbxm, CWD);
			filestoclean.Add(rbxmx);
			Writer.Info(LogGroup.AdminApi, $"converted RBXM to RBXMX at: {rbxmx}");

			await UpdateRBXM(rbxmx, newmesh);
			Writer.Info(LogGroup.AdminApi, $"updated mesh ID in RBXMX");
			var description = string.IsNullOrEmpty(request.Description) ? "No description provided." : request.Description;
			
			var result = await services.assets.CreateAsset(
				request.Name,
				description,
				1,
				CreatorType.User,
				1,
				System.IO.File.OpenRead(rbxmx),
				request.AssetType,
				Genre.All,
				ModerationStatus.ReviewApproved,
				DateTime.UtcNow,
				DateTime.UtcNow
			);

			Writer.Info(LogGroup.AdminApi, $"uploaded custom item successfully");

			return Ok(new MigrationResponse
			{
				success = true,
				meshId = newmesh,
				message = "Asset created successfully",
			});
		}
		catch (Exception ex)
		{
			Writer.Info(LogGroup.AdminApi, $"failed: {ex}");
			throw new StaffException($"failed: {ex.Message}");
		}
		finally
		{
			CleanupFiles(filestoclean);
			try
			{
				if (CWD != null && Directory.Exists(CWD))
				{
					Directory.Delete(CWD, true);
					Writer.Info(LogGroup.AdminApi, $"cleaned up: {CWD}");
				}
			}
			catch (Exception ex)
			{
				Writer.Info(LogGroup.AdminApi, $"failed to clean up CWD: {ex}");
			}
		}
	}

	[HttpPost("asset/copy-ugc")]
	[StaffFilter(Access.MigrateAssetFromRoblox)]
	public async Task<IActionResult> CopyUGCFromRoblox([FromForm] MigrateAssetRequest request)
	{
		string rbxm = null;
		string rbxmx = null;
		string Mesh = null;
		string OBJ = null;
		string luapath = null;
		var filestoclean = new List<string>();
		string CWD = null;

		try
		{
			CWD = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(CWD);
			Writer.Info(LogGroup.AdminApi, $"made temp folder: {CWD}");

			if (request.OBJ == null || request.OBJ.Length == 0)
				throw new StaffException("OBJ file is required");

			var assetId = GetAssetIDFromURL(request.rbxURL);
			Writer.Info(LogGroup.AdminApi, $"got asset ID: {assetId}");
			if (assetId <= 0)
				throw new StaffException("Bad Roblox URL! Is it valid?");

			rbxm = Path.Combine(CWD, $"{Guid.NewGuid()}.rbxm");
			await DownloadRobloxAsset(assetId, rbxm);
			filestoclean.Add(rbxm);
			Writer.Info(LogGroup.AdminApi, $"downloaded RBXM to: {rbxm}");

			OBJ = Path.Combine(CWD, $"{Guid.NewGuid()}.obj");
			await using (var stream = System.IO.File.Create(OBJ))
			{
				await request.OBJ.CopyToAsync(stream);
			}
			filestoclean.Add(OBJ);

			Mesh = await ProcessOBJ(OBJ, CWD);
			if (!System.IO.File.Exists(Mesh))
			{
				throw new StaffException($"Mesh was not created, please try again! Your UGC/OBJ file may be invalid.");
			}
			filestoclean.Add(Mesh);

			var newmesh = await UploadUGCMesh(Mesh);

			// convert cause i can't update the mesh in RBXM
			rbxmx = await ConvertRBXM(rbxm, CWD);
			filestoclean.Add(rbxmx);
			Writer.Info(LogGroup.AdminApi, $"converted RBXM to RBXMX at: {rbxmx}");

			await UpdateRBXM(rbxmx, newmesh); // use RBXMX cause i HATE RBXM SO MUCH!!!
			Writer.Info(LogGroup.AdminApi, $"updated mesh ID in RBXMX");

			// get asset details from roblox
			var assetDetails = await GetRBXAssetInfo(assetId);
			Writer.Info(LogGroup.AdminApi, $"got asset details for: {assetId}");

			var result = await UploadUGC(rbxmx, assetDetails);
			Writer.Info(LogGroup.AdminApi, $"uploaded UGC successfully");

			return Ok(new MigrationResponse
			{
				success = true,
				meshId = newmesh,
				message = "Asset copied successfully",
			});
		}
		catch (Exception ex)
		{
			Writer.Info(LogGroup.AdminApi, $"(If this says 429 (Too Many Requests), then just wait a few minutes then try again.) Copy failed: {ex}");
			throw new StaffException($"Copy failed: {ex.Message}");
		}
		finally
		{
			CleanupFiles(filestoclean);
			try
			{
				if (CWD != null && Directory.Exists(CWD))
				{
					Directory.Delete(CWD, true);
					Writer.Info(LogGroup.AdminApi, $"cleaned up: {CWD}");
				}
			}
			catch (Exception ex)
			{
				Writer.Info(LogGroup.AdminApi, $"failed to clean up CWD: {ex}");
			}
		}
	}

	private void CleanupFiles(List<string> files)
	{
		foreach (var file in files.Where(System.IO.File.Exists))
		{
			try
			{
				System.IO.File.Delete(file);
			}
			catch (Exception ex)
			{
				Writer.Info(LogGroup.AdminApi, $"failed to delete temp file {file}: {ex}");
			}
		}
	}
		
	private long GetAssetIDFromURL(string url)
	{
		var match = Regex.Match(url, @"catalog/(\d+)");
		return match.Success ? long.Parse(match.Groups[1].Value) : 0;
	}
		
	private async Task<string> ConvertRBXM(string rbxm, string CWD)
	{
		var rbxmkdir = Path.Combine(Configuration.PublicDirectory, "rbxmk");
		if (!Directory.Exists(rbxmkdir))
		{
			throw new DirectoryNotFoundException($"rbxmk directory not found at: {rbxmkdir}");
		}

		// copy RBXM to rbxmk
		var rbxmname = Path.GetFileName(rbxm);
		var destrbxm = Path.Combine(rbxmkdir, rbxmname);
		System.IO.File.Copy(rbxm, destrbxm, true);
		Writer.Info(LogGroup.AdminApi, $"copied RBXM to: {destrbxm}");

		var luacont = $@"
			local input = './{rbxmname}'
			local output = './{Path.GetFileNameWithoutExtension(rbxmname)}.rbxmx'
			local file = fs.read(input)
			fs.write(output, file, 'rbxmx')
		";

		var script = $"convert_{Path.GetFileNameWithoutExtension(rbxmname)}.lua";
		var luapath = Path.Combine(rbxmkdir, script);
		await System.IO.File.WriteAllTextAsync(luapath, luacont);

		// convert!!!!!!!
		var rbxmkpath = Path.Combine(rbxmkdir, "rbxmk.exe");
		if (!System.IO.File.Exists(rbxmkpath))
		{
			throw new FileNotFoundException($"rbxmk not found at: {rbxmkpath}");
		}

		var RBXMKProcess = new ProcessStartInfo
		{
			FileName = rbxmkpath,
			Arguments = $"run \"{script}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = rbxmkdir
		};

		using var process = Process.Start(RBXMKProcess);
		var output = await process.StandardOutput.ReadToEndAsync();
		var error = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		if (!string.IsNullOrEmpty(error))
		{
			Writer.Info(LogGroup.AdminApi, $"RBXMK error: {error}");
		}

		if (process.ExitCode != 0)
		{
			throw new Exception($"rbxmk process exited. code: {process.ExitCode}, error: {error}");
		}

		var outFile = Path.GetFileNameWithoutExtension(rbxmname) + ".rbxmx";
		var outPath = Path.Combine(rbxmkdir, outFile);
		if (!System.IO.File.Exists(outPath))
		{
			throw new FileNotFoundException($"RBXMX not found! This probably means that the catalog link you provided was invalid as it could not convert/find any valid RBXM.");
		}

		var finaloutPath = Path.Combine(CWD, outFile);
		System.IO.File.Move(outPath, finaloutPath);

		try
		{
			System.IO.File.Delete(destrbxm);
			System.IO.File.Delete(luapath);
			Writer.Info(LogGroup.AdminApi, $"cleaned up temp files in rbxmk dir");
		}
		catch (Exception ex)
		{
			Writer.Info(LogGroup.AdminApi, $"failed to clean up RBXMK files: {ex}");
		}

		return finaloutPath;
	}
	
	// TODO: does this already exist? idk but it's easier so i don't care
	private async Task<string> DownloadRobloxAsset(long assetId, string outPath)
	{
		//var assetendpoint = $"{Configuration.GSUrl}/asset/roblox/?id={assetId}";
		var assetendpoint = $"{Configuration.AssetUrl}/asset/?id={assetId}";
		
		using var client = new HttpClient();
		var response = await client.GetAsync(assetendpoint);
		response.EnsureSuccessStatusCode();
		
		await using var stream = System.IO.File.Create(outPath);
		await response.Content.CopyToAsync(stream);
		
		return outPath;
	}

	private async Task<string> ProcessOBJ(string OBJ, string CWD)
	{
		// OBJToRBXMesh does .obj.mesh for some reason
		var convertedmesh = Path.Combine(CWD, $"{Path.GetFileName(OBJ)}.mesh");

		var meshconvertpath = Path.Combine(Configuration.PublicDirectory, "OBJToRBXMesh.exe");
		if (!System.IO.File.Exists(meshconvertpath))
		{
			throw new FileNotFoundException($"OBJ converter not found at: {meshconvertpath}");
		}

		var OBJProcess = new ProcessStartInfo
		{
			FileName = meshconvertpath,
			Arguments = $"\"{OBJ}\" 1.00",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = CWD
		};
		
		using var process = Process.Start(OBJProcess);
		var output = await process.StandardOutput.ReadToEndAsync();
		var error = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();
		
		if (!string.IsNullOrEmpty(error))
		{
			Writer.Info(LogGroup.AdminApi, $"OBJ convert error: {error}");
		}
		
		if (process.ExitCode != 0)
		{
			throw new Exception($"OBJ convert process exited. code: {process.ExitCode}, error: {error}");
		}
		 
		if (!System.IO.File.Exists(convertedmesh))
		{
			Writer.Info(LogGroup.AdminApi, $"OBJ out, mesh was not found: {output}");
			throw new FileNotFoundException($"Mesh was not found! Is your OBJ valid?");
		}
		
		return convertedmesh;
	}

	private async Task<long> UploadUGCMesh(string Mesh)
	{
		await using var stream = System.IO.File.OpenRead(Mesh);
		var result = await services.assets.CreateAsset(
			Path.GetFileNameWithoutExtension(Mesh),
			"Mesh for custom asset",
			1,
			CreatorType.User,
			1,
			stream,
			Type.Mesh,
			Genre.All,
			ModerationStatus.ReviewApproved,
			DateTime.UtcNow,
			DateTime.UtcNow
		);
		
		return result.assetId;
	}

	private async Task UpdateRBXM(string rbxm, long newmesh)
	{
		// i hope there's not some obscure other meshID xml thing so it works fine
		var content = await System.IO.File.ReadAllTextAsync(rbxm);
		content = Regex.Replace(content, @"<string name=""MeshId"">(rbxassetid://|https?://(www\.)?roblox\.com/asset/?\?id=|https?://assetdelivery\.roblox\.com/v1/asset/?\?id=)\d+</string>", 
            $@"<string name=""MeshId"">rbxassetid://{newmesh}</string>");

		content = Regex.Replace(content, @"<Content name=""MeshId""><url>(rbxassetid://|https?://(www\.)?roblox\.com/asset/?\?id=|https?://assetdelivery\.roblox\.com/v1/asset/?\?id=)\d+</url></Content>", 
            $@"<Content name=""MeshId""><url>rbxassetid://{newmesh}</url></Content>");
		
		await System.IO.File.WriteAllTextAsync(rbxm, content);
	}
	
	// TODO: does this also exist somewhere?
	private async Task<RBXAssetDetails> GetRBXAssetInfo(long assetId)
	{
		using var client = new HttpClient();
		//var response = await client.GetAsync($"https://economy.roblox.com/v2/assets/{assetId}/details");
		var response = await client.GetAsync($"https://economy.roproxy.com/v2/assets/{assetId}/details");
		response.EnsureSuccessStatusCode();
		
		var content = await response.Content.ReadAsStringAsync();
		return JsonConvert.DeserializeObject<RBXAssetDetails>(content);
	}
	
	// this SHOULD work with the existing DTO model i thnink
	private async Task<CreateResponse> UploadUGC(string rbxm, RBXAssetDetails details)
	{
		await using var stream = System.IO.File.OpenRead(rbxm);
		return await services.assets.CreateAsset(
			details.Name,
			details.Description,
			1,
			CreatorType.User,
			1,
			stream,
			// sometimes the assettypeid is like a left shoe or something so idk what to do about that
			// TODO: add invalid assetypeids here
			(Type)details.AssetTypeId,
			Genre.All,
			ModerationStatus.ReviewApproved,
			DateTime.UtcNow,
			DateTime.UtcNow
		);
	}

    [HttpPost("create-game"), StaffFilter(Access.CreateGameForUser)]
    public async Task<dynamic> CreateGame([Required, FromBody] UserIdRequest request)
    {
        var asset = await services.assets.CreatePlace(request.userId, CreatorType.User, request.userId);
        var universe = await services.games.CreateUniverse(asset.placeId);
        return new
        {
            asset.placeId, universe.universeId,
        };
    }

    [HttpPost("asset/version/create"), StaffFilter(Access.CreateAssetVersion)]
    public async Task<dynamic> CreateAssetVersion([Required, FromForm] CreateAssetVersionRequest request)
    {
        if (request.rbxm == null)
            throw new StaffException("No file specified");
        
        var info = await services.assets.GetAssetCatalogInfo(request.assetId);
        var canUpload = false;
        if (info.creatorType is CreatorType.User && info.creatorTargetId == 1)
        {
            canUpload = true;
        }
        else if (await services.assets.CanUserModifyItem(info.id, userSession.userId))
        {
            canUpload = true;
        }

        if (canUpload == false) throw new StaffException("Not authorized to modify this item");
        if (info.assetType == Type.Package) throw new StaffException("Cannot create an asset version for this type");
        var result = await services.assets.CreateAssetVersion(request.assetId, 1, request.rbxm.OpenReadStream());
        services.assets.RenderAsset(request.assetId, info.assetType);
        return result;
    }

    [HttpPost("infrastructure/request-update"), StaffFilter(Access.RequestWebsiteUpdate)]
    public dynamic RequestUpdate()
    {
        throw new StaffException("Feature has been removed");
    }

    [HttpGet("feature-flags/all"), StaffFilter(Access.ManageFeatureFlags)]
    public dynamic GetAllFlags()
    {
        return FeatureFlags.GetAllFlags();
    }

    [HttpPost("feature-flags/enable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task EnableFlag(string featureFlag)
    {
        await FeatureFlags.EnableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }
    
    [HttpPost("feature-flags/disable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task DisableFlag(string featureFlag)
    {
        await FeatureFlags.DisableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }

    [HttpGet("players/total"), StaffFilter(Access.GetStats)]
    public async Task<dynamic> GetTotalSignedUp()
    {
        var t = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(60));
        var result = await db.QuerySingleOrDefaultAsync("SELECT COUNT(*) as total FROM \"user\"", new
        {
            t,
        });
        return new
        {
            total = (long) result.total,
        };
    }


    [HttpGet("players/in-game"), StaffFilter(Access.GetUsersInGame)]
    public async Task<dynamic> GetInGamePlayers()
    {
        return await db.QueryAsync("SELECT s.user_id, s.asset_id, s.server_id, u.username, a.name as asset_name FROM asset_server_player s INNER JOIN \"user\" u ON u.id = s.user_id INNER JOIN asset a ON a.id = s.asset_id LIMIT 1000");
    }

    [HttpGet("players/online-count"), StaffFilter(Access.GetUsersOnline)]
    public async Task<dynamic> GetOnlinePlayersCount()
    {
        var t = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(60));
        var result = await db.QuerySingleOrDefaultAsync("SELECT COUNT(*) as total FROM \"user\" WHERE online_at >= :t", new
        {
            t,
        });
        return new
        {
            total = (long) result.total,
        };
    }

    [HttpGet("users/{userId:long}/transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetUserTransactions(long userId, PurchaseType type, int offset, int limit)
    {
        return await services.economy.GetTransactions(userId, CreatorType.User, type, limit, offset);
    }
    
    [HttpGet("users/{userId:long}/all-transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetAllUserTransactions(long userId, int offset, int limit)
    {
        return await services.economy.GetTransactions(userId, CreatorType.User, limit, offset);
    }

    // another custom feature by Aep

    [HttpGet("users/{userId:long}/moderation-history"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetFullModerationHistory(long userId, int offset, int limit)
    {
        var result = await db.QueryAsync(
        "SELECT * FROM moderation_user_ban WHERE user_id = :userid ORDER BY id DESC",
        new
        {
            userid = userId
        });

        return result;
    }

    [HttpGet("users/{userId:long}/trades"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetUserTrades(long userId, TradeType type, int offset, int limit)
    {
        var response = new List<dynamic>();
        var result = await services.trades.GetTradesOfType(userId, type, limit, offset);
        foreach (var item in result)
        {
            var tradeDbEntry = await services.trades.GetTradeById(item.id);
            var items = await services.trades.GetTradeItems(item.id);
            response.Add(new
            {
                trade = item,
                db = tradeDbEntry,
                items,
            });
        }
        return response;
    }

    [HttpPost("users/{userId:long}/reset-description"), StaffFilter(Access.ResetDescription)]
    public async Task ResetDescription(long userId)
    {
        var rlKey = "ResetDescriptionV1";
        if ((await redis.StringGetAsync(rlKey)) !=null)
        {
            throw new StaffException("Someone already reset a description recently. Try again in a few seconds.");
        }

        await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        await services.users.SetUserDescription(userId, "[ Content Deleted ]");
    }
	
	// make it generate a random password
	[HttpPost("users/{userId:long}/reset-password"), StaffFilter(Access.ResetUsername | Access.ResetDescription)]
	public async Task ResetPassword(long userId)
	{
		var permissions = await services.users.GetStaffPermissions(userSession.userId);
		if (!permissions.Any(p => p.permission == Access.ResetUsername)) 
			throw new StaffException("Missing permissions");
		if (!permissions.Any(p => p.permission == Access.ResetDescription))
			throw new StaffException("Missing permissions");

		var userInfo = await services.users.GetUserById(userId);
		if (await IsStaff(userId) && !StaffFilter.IsOwner(userSession.userId))
			throw new StaffException("Cannot reset password for staff members");

		// password is "changeme"
		await db.ExecuteAsync(
			"UPDATE \"user\" SET password = :password WHERE id = :userId",
			new 
			{
				password = "$argon2id$v=19$m=16,t=2,p=1$TXJyOXFBYXRqWVplOVFkQQ$tyjrJrKMsjVIqTN42FkafQ",
				userId
			});

		await db.ExecuteAsync(
			"INSERT INTO moderation_reset_password (user_id, actor_id) VALUES (:userId, :actorId)",
			new 
			{
				userId,
				actorId = userSession.userId
			});
	}

    [HttpPost("users/{userId:long}/reset-username"), StaffFilter(Access.ResetUsername)]
    public async Task ResetUsername(long userId, bool? banUsername)
    {
        if (!StaffFilter.IsOwner(userSession.userId))
        {
            var rlKey = "ResetUsernameV1";
            if ((await redis.StringGetAsync(rlKey)) != null)
            {
                throw new StaffException("Someone already reset a username recently. Try again in a few seconds.");
            }

            await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        }

        // get user data
        var userData = await services.users.GetUserById(userId);
        if (userData.isModerator || userData.isAdmin || await IsStaff(userData.userId))
            throw new StaffException("Cannot change this user's username");
        // ban the username
        await services.users.AddBadUsername(userData.username);
        // reset
        await services.users.ResetUsername(userId, userSession.userId);
        // message
        await services.privateMessages.CreateMessage(userId, 1, "Username Reset",
            "Hello,\n\nYour username has been reset due to abuse concerns. You can request a new username by contacting a staff member.\n\n-The Roblox Team");
       
    }

    [HttpGet("applications/update-lock"), StaffFilter(Access.ManageApplications)]
    public async Task UpdateLocks(string ids)
    {
        var parsed = ids.Split(",");
        if (parsed.Length is < 0 or > 10)
            return;
        
        await services.users.AcquireApplicationLocks(userSession.userId, parsed);
    }

    [HttpGet("applications/list"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> GetApplications(UserApplicationStatus? status, int offset, SortOrder sortOrder, string? searchQuery = null, ApplicationSearchColumn? searchColumn = null)
    {
        return await services.users.GetApplications(status, offset, sortOrder, status == UserApplicationStatus.Pending ? userSession.userId : null, searchQuery, searchColumn);
    }

    [HttpGet("applications/details"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> GetApplicationById(string id)
    {
        var result = await services.users.GetApplicationById(id);
        if (result == null)
            throw new StaffException("Application ID is invalid or does not exist");
        return result;
    }

    [HttpPost("applications/{applicationId}/approve"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> ApproveApplication(string applicationId)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            await AwardCommissionForApplicationReview();
        }
        var result = await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.Approved);
        return new
        {
            joinId = result,
        };
    }
    
    [HttpPost("applications/{applicationId}/decline"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplication(string applicationId, string reason)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            await AwardCommissionForApplicationReview();
        }
        await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.Rejected, reason);
    }
    
    [HttpPost("applications/{applicationId}/decline-silent"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplicationSilently(string applicationId)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            await AwardCommissionForApplicationReview();
        }
        await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.SilentlyRejected);
    }
    
    [HttpPost("applications/{applicationId}/clear"), StaffFilter(Access.ClearApplications)]
    public async Task ClearApplication(string applicationId)
    {
        await services.users.ClearApplication(applicationId);
    }

    [HttpGet("invites/{userId:long}"), StaffFilter(Access.ManageInvites)]
    public async Task<dynamic> GetInvitesByUser(long userId)
    {
        return await services.users.GetInvitesByUser(userId);
    }

    [HttpGet("text-moderation/get-latest"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<dynamic> GetLatestIdsForTextMod()
    {
        var forumPosts = await services.forums.GetAllPosts(0, 1, "desc", null);
        var comments = await GetAllAssetComments(1, 0, "desc");
        var wall = await GetAllWallPosts(1, 0, "desc");
        var status = await GetAllUserStatuses(0, 1, "desc");
        var groupStatus = await GetGroupStatuses(0, 1, "desc");

        return new
        {
            ForumPost = forumPosts.Last().postId,
            AssetComment = comments.Last().id,
            GroupWallPost = wall.Last().id,
            UserStatusPost = status.Last().id,
            GroupStatusPost = groupStatus.Last().id,
        };
    }

    [HttpGet("assets/comments"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<IEnumerable<StaffAssetCommentEntry>> GetAllAssetComments(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = 0)
    {
        var q = new Dapper.SqlBuilder();
        var t = q.AddTemplate(
            "SELECT asset_comment.id as id, asset.id as assetId, asset.name, asset_comment.comment as comment, u.id as userId, u.username as username, asset_comment.created_at as createdAt FROM asset_comment INNER JOIN asset ON asset_comment.asset_id = asset.id INNER JOIN \"user\" u ON asset_comment.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new {
                limit,
                offset,
            });
        
        if (exclusiveStartId != null)
            q.Where("asset_comment.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });
        q.OrderBy(sortOrder == "desc" ? "asset_comment.id DESC" : "asset_comment.id ASC");
        
        return await db.QueryAsync<StaffAssetCommentEntry>(t.RawSql, t.Parameters);
    }

    [HttpGet("groups/wall"), StaffFilter(Access.GetGroupWall)]
    public async Task<IEnumerable<StaffWallEntry>> GetAllWallPosts(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT gw.id, gw.content as post, gw.group_id as groupId, gw.user_id as userId, u.username, gw.created_at as createdAt FROM group_wall gw INNER JOIN \"user\" u ON gw.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        if (exclusiveStartId != null)
            q.Where("gw.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });

        q.OrderBy(sortOrder == "desc" ? "gw.id desc" : "gw.id asc");
        
        return await db.QueryAsync<StaffWallEntry>(t.RawSql, t.Parameters);
    }

    [HttpPost("groups/wall/remove"), StaffFilter(Access.DeleteGroupWallPost)]
    public async Task RemoveWallPost(long id)
    {
        await db.ExecuteAsync("UPDATE group_wall SET \"content\" = '[ Content Deleted ]' WHERE id = :id", new
        {
            id,
        });
    }

    [HttpGet("groups/status"), StaffFilter(Access.GetGroupStatus)]
    public async Task<IEnumerable<GroupWallPostStaff>> GetGroupStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id, s.group_id, s.status, s.user_id, g.name, u.username, s.created_at FROM group_status s INNER JOIN \"group\" g ON s.group_id = g.id INNER JOIN \"user\" u ON g.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });
        
        return await db.QueryAsync<GroupWallPostStaff>(t.RawSql, t.Parameters);
    }

    [HttpPost("groups/status/delete"), StaffFilter(Access.DeleteGroupStatus)]
    public async Task DeleteGroupStatus(long id)
    {
        await db.ExecuteAsync("DELETE FROM group_status WHERE id = :id", new
        {
            id,
        });
    }

    [HttpGet("users/status"), StaffFilter(Access.GetAllUserStatuses)]
    public async Task<IEnumerable<StaffUserStatusEntry>> GetAllUserStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id as id, s.user_id as userId, s.status as post, u.username, s.created_at as createdAt FROM user_status s INNER JOIN \"user\" u ON s.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });
        
        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        return await db.QueryAsync<StaffUserStatusEntry>(t.RawSql, t.Parameters);
    }

    private readonly List<string> allowedGroupSortColumns = new List<string>
    {
        "id",
    };
    
    [HttpGet("groups/list"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupList(int offset, int limit, string sortColumn, string sortOrder)
    {
        if (!allowedGroupSortColumns.Contains(sortColumn))
        {
            sortColumn = allowedGroupSortColumns[0];
        }

        if (sortOrder is not "asc" or "desc")
        {
            sortOrder = "asc";
        }

        var sql = new SqlBuilder();
        var t = sql.AddTemplate("SELECT * FROM \"group\" g /**orderby**/");
        sql.OrderBy($"{sortColumn} {sortOrder} LIMIT :limit OFFSET :offset", new
        {
            limit,
            offset,
        });

        return await db.QueryAsync(t.RawSql, t.Parameters);
    }

    [HttpGet("groups/get-by-name"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupByName(string name)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT id FROM \"group\" g WHERE g.name = :name", new
        {
            name,
        });
        if (result == null)
            throw new StaffException("Group name is invalid or does not exist");
        return await GetGroupModerationInfo(result.id);
    }

    [HttpGet("groups/audit-log"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetEntireAuditLog(long groupId)
    {
        return await db.QueryAsync(
            "SELECT * FROM group_audit_log WHERE group_id = :gid ORDER BY group_audit_log.id DESC", new
            {
                gid = groupId,
            });
    }

    [HttpPost("groups/toggle-lock-status"), StaffFilter(Access.LockAndUnlockGroup)]
    public async Task ToggleGroupLockStatus(long groupId, bool locked)
    {
        await db.ExecuteAsync("UPDATE \"group\" g SET locked = :t WHERE g.id = :id", new
        {
            id = groupId,
            t = locked,
        });
    }

    [HttpPost("groups/reset"), StaffFilter(Access.ResetGroup)]
    public async Task ResetGroup(long groupId)
    {
        var newName = "[ Content Deleted (" + groupId + ") ]";
        if (await services.groups.IsGroupNameTaken(newName))
        {
            newName = Guid.NewGuid().ToString();
        }
        await db.ExecuteAsync(
            "UPDATE \"group\" SET name = :name, description = '[ Content Deleted ]' WHERE id = :id",
            new
            {
                id = groupId,
                name = newName,
            });
        // delete all status entries...
        foreach (var entry in await services.groups.MultiGetGroupStatus(new []{groupId}, 100000))
        {
            await db.ExecuteAsync("UPDATE group_status SET status = '[ Content Deleted ]' WHERE id = :id", new
            {
                id = entry.feedId,
            });
        }
        // delete all roles...
        foreach (var item in await services.groups.GetRolesInGroup(groupId))
        {
            if (item.rank == 0)
                continue;
            var name = "Role" + item.id + "";
            await db.ExecuteAsync(
                "UPDATE group_role SET name = :name, description = '[ Content Deleted ]' WHERE id =:id", new
                {
                    item.id,
                    name,
                });
        }
        // moderate icons
        await db.ExecuteAsync("UPDATE group_icon SET is_approved = 0 WHERE group_id = :id", new
        {
            id = groupId,
        });
        // clear audit logs - could contain personal info
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_description = '[ Content Deleted ]', old_description = '[ Content Deleted ]' WHERE new_description IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_name = '[ Content Deleted ]', old_name = '[ Content Deleted ]' WHERE new_name IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET post_desc = '[ Content Deleted ]' WHERE post_desc IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        // wall
        await db.ExecuteAsync("UPDATE group_wall SET content = '[ Content Deleted ]' WHERE group_id = :id", new
        {
            id = groupId,
        });
    }

    [HttpGet("games/play-history"), StaffFilter(Access.GetUsersInGame)]
    public async Task<dynamic> GetPlayHistory(int limit, int offset)
    {
        return await db.QueryAsync(
            "SELECT p.asset_id, p.user_id, p.created_at, p.ended_at, a.name, u.username FROM asset_play_history p INNER JOIN asset a ON p.asset_id = a.id INNER JOIN \"user\" u ON p.user_id = u.id ORDER BY p.id DESC LIMIT :limit OFFSET :offset", new
            {
                limit,
                offset,
            });
    }
	
	[HttpGet("games/list"), StaffFilter(Access.ManageGames)]
	public async Task<dynamic> GetGamesList(int limit = 10, int offset = 0, string? query = null)
	{
		if (limit is > 100 or < 1) limit = 10;

		var sql = new SqlBuilder();
		var t = sql.AddTemplate(
			@"SELECT 
				  a.id AS placeId,
				  a.name AS placeName,
				  a.description,
				  COALESCE(ap.visit_count, 0) AS visits,
				  COALESCE(ap.year, 2016) AS year,
				  COALESCE(asp_counts.currentPlayers, 0) AS currentPlayers,
				  u.id AS creatorId,
				  u.username AS creatorName
			  FROM asset a
			  INNER JOIN asset_place ap ON a.id = ap.asset_id
			  LEFT JOIN (
				  SELECT asset_id, COUNT(user_id) AS currentPlayers
				  FROM asset_server_player
				  GROUP BY asset_id
			  ) asp_counts ON a.id = asp_counts.asset_id
			  LEFT JOIN ""user"" u ON a.creator_id = u.id
			  /**where**/
			  ORDER BY a.id DESC
			  LIMIT :limit OFFSET :offset",
			new { limit, offset });

		if (!string.IsNullOrEmpty(query))
		{
			sql.Where("(a.name ILIKE :query OR a.description ILIKE :query)", new
			{
				query = $"%{query}%"
			});
		}

		sql.Where("a.asset_type = :type", new { type = Type.Place });

		var result = await db.QueryAsync(t.RawSql, t.Parameters);
/* 		if (result.Any())
		{
			var item = result.First();
			Console.WriteLine($"returned: {string.Join(", ", ((IDictionary<string, object>)item).Keys)}");
		} */

		return result.Select(game => new
		{
			placeId = game.placeid,
			placeName = game.placename,
			game.description,
			game.visits,
			currentPlayers = game.currentplayers,
			creatorId = game.creatorid,
			creatorName = game.creatorname,
			game.year,
		});
	}

	[HttpGet("games/{placeId}/details"), StaffFilter(Access.ManageGames)]
	public async Task<dynamic> GetGameDetails(long placeId)
	{
		var Exists = await db.QuerySingleOrDefaultAsync<bool>(
			"SELECT COUNT(1) FROM asset WHERE id = :placeId",
			new { placeId });
		
		if (!Exists)
			throw new StaffException("Place not found");

		var result = await db.QuerySingleOrDefaultAsync(
			@"SELECT 
				  a.id,
				  a.name,
				  a.description,
				  ap.visit_count as visits,
				  ap.year as year,
				  COALESCE(asp_counts.currentPlayers, 0) AS currentPlayers,
				  u.id as creatorId,
				  u.username as creatorName
			  FROM asset a
			  INNER JOIN asset_place ap ON a.id = ap.asset_id
			  LEFT JOIN (
				  SELECT asset_id, COUNT(user_id) AS currentPlayers
				  FROM asset_server_player
				  GROUP BY asset_id
			  ) asp_counts ON a.id = asp_counts.asset_id
			  LEFT JOIN ""user"" u ON a.creator_id = u.id
			  WHERE a.id = :placeId",
			new { placeId });

		if (result == null)
			throw new StaffException("Place data not found");

		//Console.WriteLine($"returned: {string.Join(", ", ((IDictionary<string, object>)result).Keys)}");

		return new
		{
			placeId = result.id,
			placeName = result.name,
			description = result.description,
			visits = result.visits,
			currentPlayers = result.currentplayers,
			creatorId = result.creatorid,
			creatorName = result.creatorname,
			year = result.year
		};
	}

	[HttpPost("games/{placeId}/reset-name"), StaffFilter(Access.ManageGames)]
	public async Task ResetPlaceName(long placeId)
	{
		var Reset = "Place";
		
		var affected = await db.ExecuteAsync(
			"UPDATE asset SET name = :name WHERE id = :placeId AND asset_type = :type",
			new
			{
				name = Reset,
				placeId,
				type = Type.Place
			});
		
		if (affected == 0)
			throw new StaffException("Place not found or not a valid place");
	}

	[HttpPost("games/{placeId}/reset-description"), StaffFilter(Access.ManageGames)]
	public async Task ResetPlaceDescription(long placeId)
	{
		var affected = await db.ExecuteAsync(
			"UPDATE asset SET description = :description WHERE id = :placeId AND asset_type = :type",
			new
			{
				description = "No description provided.",
				placeId,
				type = Type.Place
			});
		
		if (affected == 0)
			throw new StaffException("Place not found or not a valid place");
	}

	[HttpDelete("games/{placeId}/delete"), StaffFilter(Access.ManageGames)]
	public async Task DeletePlace(long placeId)
	{
		var universeInfo = await db.QuerySingleOrDefaultAsync(
			"SELECT universe_id FROM asset WHERE id = :placeId",
			new { placeId });
		
		if (universeInfo != null && universeInfo.universe_id != null)
		{
			await db.ExecuteAsync(
				"DELETE FROM universe WHERE id = :universeId",
				new { universeId = universeInfo.universe_id });
		}
		
		var affected = await db.ExecuteAsync(
			"DELETE FROM asset WHERE id = :placeId AND asset_type = :type",
			new { placeId, type = Type.Place });
		
		if (affected == 0)
			throw new StaffException("Place not found or not a valid place");
	}

	[HttpGet("games/{placeId}/download"), StaffFilter(Access.ManageGames)]
	public async Task<IActionResult> DownloadPlace(long placeId)
	{
		var Version = await services.assets.GetLatestAssetVersion(placeId);
		
		if (Version.contentUrl == null)
			throw new StaffException("No content available for this place");
		
		var RBXL = Path.Combine(Configuration.AssetDirectory, Version.contentUrl);
		
		if (!System.IO.File.Exists(RBXL))
			throw new StaffException("Place file not found");
		
		var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
		var File = $"{placeInfo.name.Replace(" ", "_")}_{placeId}.rbxl";
		
		return PhysicalFile(RBXL, "application/octet-stream", File);
	}

    [HttpPost("text-moderation/request-payment"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<dynamic> RequestPayment()
    {
        var redisKey = "TextModerator:Clock:v2:" + userSession.userId;
        var lastTimeStr = await redis.StringGetAsync(redisKey);
        var lastClock = DateTime.UtcNow;
        if (lastTimeStr != null)
        {
            // ran into TZ issues if I just used "ToString()". No clue how I should be actually doing this...
            var result = JsonConvert.DeserializeObject<DateTimeSerialized>(lastTimeStr);
            if (result != null)
                lastClock = result.clock;
        }
		await redis.StringSetAsync(redisKey, JsonConvert.SerializeObject(new DateTimeSerialized()
		{
			clock = DateTime.UtcNow,
		}));
        
        var forumPosts = await services.forums.GetAllPosts(0, 100, "desc", null);
        var comments = await GetAllAssetComments(100, 0, "desc");
        var wall = await GetAllWallPosts(100, 0, "desc");
        var status = await GetAllUserStatuses(0, 100, "desc");
        var groupStatus = await GetGroupStatuses(0, 100, "desc");

        var validForumPosts = forumPosts.Count(c => c.createdAt > lastClock);
        var validComments = comments.Count(c => c.createdAt > lastClock);
        var validWall = wall.Count(c => c.createdAt > lastClock);
        var validStatus = status.Count(c => c.createdAt > lastClock);
        var validGroupStatus = groupStatus.Count(c => c.created_at > lastClock);
        const int robuxMultiplier = 5;

        var Robux = (validComments + validForumPosts + validWall + validStatus + validGroupStatus);
        if (Robux == 0)
            return new
            {
                Robux,
            };
        
        Robux *= robuxMultiplier;
        
        if (Robux > 5000)
        {
            Robux = 5000;
        }

        await services.economy.IncrementCurrency(userSession.userId, CurrencyType.Robux, Robux);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = Robux,
            // details
            sub_type = TransactionSubType.StaffTextModeration,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
        
        return new
        {
            Robux,
        };
    }

    [HttpGet("reports/list"), StaffFilter(Access.ManageReports)]
    public async Task<dynamic> GetReports(AbuseReportStatus status)
    {
        return await services.abuseReport.GetReports(status);
    }

    private async Task RewardForReportReview()
    {
        const int Robux = 25;
        await services.economy.IncrementCurrency(userSession.userId, CurrencyType.Robux, Robux);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = Robux,
            // details
            sub_type = TransactionSubType.StaffReportReview,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    [HttpPost("reports/{id}/accept"), StaffFilter(Access.ManageReports)]
    public async Task AcceptReport(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.Valid, safeUserSession.userId);
        await RewardForReportReview();
    }
    
    [HttpPost("reports/{id}/decline"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReport(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidGood, safeUserSession.userId);
        await RewardForReportReview();
    }
    
    [HttpPost("reports/{id}/invalid"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReportInvalid(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidBad, safeUserSession.userId);
        await RewardForReportReview();
    }

    [HttpGet("assets/{assetId}/owners"), StaffFilter(Access.GetAllAssetOwners)]
    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetLiterallyAllOwnersKindaUnsafe(long assetId)
    {
        return await db.QueryAsync<CollectibleUserAssetEntry>("SELECT id as userAssetId, asset_id as assetId, user_id as userId, price, serial, created_at as createdAt, updated_at as updatedAt FROM user_asset WHERE asset_id = :asset_id", new
        {
            asset_id = assetId,
        });
    }
	
	[HttpGet("assets/total"), StaffFilter(Access.TrackItem)]
	public async Task<dynamic> GetTotalAssetCount()
	{
		var total = await db.QuerySingleOrDefaultAsync<Total>("SELECT COUNT(*) as total FROM asset");
		return new
		{
			total = total.total
		};
	}

    private Regex matchAssetThumbRegex = new Regex("\\/images\\/thumbnails\\/([a-zA-Z0-9]+)", RegexOptions.Compiled);
    private Regex matchUserThumbRegex = new Regex("(\\/images\\/thumbnails\\/[a-zA-Z0-9\\.\\\\_]+)", RegexOptions.Compiled);
    private Regex matchGroupIconRegex = new Regex("\\/images\\/thumbnails\\/([a-zA-Z0-9\\.]+)", RegexOptions.Compiled);

    [HttpGet("moderation/get-by-thumbnail"), StaffFilter(Access.GetDetailsFromThumbnail)]
    public async Task<StaffAssetResolveThumbnailResponse> GetDetailsFromThumbnail(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new();

        var response = new StaffAssetResolveThumbnailResponse();
        
        var assetUrl = matchAssetThumbRegex.Match(url);
        if (assetUrl.Success)
        {
            // thumbs
            var groupData = assetUrl.Groups[1].Value;
            var matchingThumbs = await db.QueryAsync<ResolveThumbAssetEntry>("SELECT asset_id as assetId FROM asset_thumbnail WHERE content_url = :url",
                new
                {
                    url = groupData,
                });
            response.assets = matchingThumbs;
            // versions
            var matchingVersions = await db.QueryAsync<ResolveThumbAssetEntry>(
                "SELECT asset_id as assetId FROM asset_version WHERE content_url = :url", new
                {
                    url = groupData,
                });
            var list = response.assets.ToList();
            list.AddRange(matchingVersions);
            response.assets = list;
        }

        var thumbOrHeadshotUrl = matchUserThumbRegex.Match(url);
        if (thumbOrHeadshotUrl.Success)
        {
            var groupData = thumbOrHeadshotUrl.Groups[1].Value;
            var matchingUserThumbs = await db.QueryAsync<ResolveThumbUsersEntry>(
                "SELECT user_id as userId FROM user_avatar WHERE thumbnail_url = :url OR headshot_thumbnail_url = :url", new
                {
                    url = groupData,
                });
            response.users = matchingUserThumbs;
        }

        var groupUrl = matchGroupIconRegex.Match(url);
        if (groupUrl.Success)
        {
            var groupData = groupUrl.Groups[1].Value;
            var matchingGroups = await db.QueryAsync<ResolveThumbGroupsEntry>("SELECT group_id as groupId FROM group_icon WHERE name = :url", new
            {
                url = groupData,
            });
            response.groups = matchingGroups;
        }

        return response;
    }
	
	// move to DTO when working
	public class ShutdownGSReq
	{
		public string ServerId { get; set; }
	}
	
	[HttpGet("game-servers/status"), StaffFilter(Access.ManageRunningGameServers)]
	public async Task<dynamic> GetGameServerStatus()
	{
		return await services.gameServer.GetStatus();
	}

	[HttpGet("game-servers/running"), StaffFilter(Access.ManageRunningGameServers)]
	public async Task<dynamic> GetRunningGameServers()
	{
		var servers = new List<dynamic>();
		
		foreach (var kvp in services.gameServer.JobRccs)
		{
			var jobId = kvp.Key;
			var process = kvp.Value;
			var placeId = GameServerService.GetPlaceIdByJobId(jobId);
			
			var ServerInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
				"SELECT asset_id, port, created_at, updated_at FROM asset_server WHERE id = :id::uuid",
				new { id = jobId });
				
			var Players = await db.QueryFirstOrDefaultAsync<Total>(
				"SELECT COUNT(*) as total FROM asset_server_player WHERE server_id = :id::uuid",
				new { id = jobId });
				
			var Place = "Unknown";
			if (placeId > 0)
			{
				var PlaceInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
					"SELECT name FROM asset WHERE id = :id",
					new { id = placeId });
				Place = PlaceInfo?.name ?? "Unknown";
			}
			
			servers.Add(new
			{
				JobId = jobId,
				PlaceId = placeId,
				PlaceName = Place,
				Port = ServerInfo?.port ?? -1,
				ProcessId = process.Id,
				MemoryUsage = process.WorkingSet64 / 1024 / 1024,
				StartTime = process.StartTime,
				PlayerCount = Players?.total ?? 0,
				CreatedAt = ServerInfo?.created_at,
				LastPing = ServerInfo?.updated_at
			});
		}
		
		return servers;
	}

	[HttpPost("game-servers/shutdown"), StaffFilter(Access.ManageRunningGameServers)]
	public async Task<dynamic> ShutdownGameServer([Required, FromBody] ShutdownGSReq request)
	{
		try
		{
			services.gameServer.ShutDownServer(request.ServerId);
			Task.Delay(1000);
			
			var Players = await db.QueryFirstOrDefaultAsync<Total>(
                "SELECT COUNT(*) as total FROM asset_server_player WHERE server_id = :id::uuid",
                new { id = request.ServerId });
            
            if (Players != null && Players.total > 0)
            {
                await db.ExecuteAsync(
                    "DELETE FROM asset_server_player WHERE server_id = :id::uuid",
                    new { id = request.ServerId });
			}		
			
			return new
			{
				Success = true,
				Message = $"Server {request.ServerId} shutdown successfully"
			};
		}
		catch (Exception ex)
		{
			return new
			{
				Success = false,
				Message = $"Failed to shutdown server: {ex.Message}"
			};
		}
	}

	[HttpPost("game-servers/cleanup-orphaned"), StaffFilter(Access.ManageRunningGameServers)]
	public async Task<dynamic> CleanupOrphanedServers()
	{
		int Cleaned = 0;
		
		var DBServers = await db.QueryAsync<dynamic>(
			"SELECT id::text, asset_id FROM asset_server WHERE updated_at > NOW() - INTERVAL '10 minutes'");
		
		foreach (var server in DBServers)
		{
			string serverId = server.id;
			
			if (!services.gameServer.JobRccs.ContainsKey(serverId))
			{
				try
				{
					await db.ExecuteAsync(
						"DELETE FROM asset_server_player WHERE server_id = :id::uuid",
						new { id = serverId });
					
					await db.ExecuteAsync(
						"DELETE FROM asset_server WHERE id = :id::uuid",
						new { id = serverId });
					
					Cleaned++;
				}
				catch (Exception ex)
				{
					Writer.Info(LogGroup.AdminApi, $"failed to cleanup stupid orphan {serverId}: {ex.Message}");
				}
			}
		}
		
		return new
		{
			Success = true,
			Message = $"Cleaned up {Cleaned} orphaned servers"
		};
	}
}