using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.IO.Compression;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Roblox.Dto.Games;
using Roblox.Dto.Persistence;
using Roblox.Dto.Users;
using Roblox.Dto.Friends;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Libraries.Assets;
using Roblox.Libraries.FastFlag;
using Roblox.Libraries.RobloxApi;
using Roblox.Logging;
using Roblox.Services.Exceptions;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.GameServer;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Filters;
using Roblox.Website.Middleware;
using Roblox.Website.WebsiteModels.Asset;
using Roblox.Website.WebsiteModels.Games;
using Roblox.Website.WebsiteModels.Promocodes;
using Roblox.Website.WebsiteModels.Discord;
using HttpGet = Roblox.Website.Controllers.HttpGetBypassAttribute;
using JsonSerializer = System.Text.Json.JsonSerializer;
using MultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;
using ServiceProvider = Roblox.Services.ServiceProvider;
using Type = Roblox.Models.Assets.Type;
using System.Data;
using Npgsql;
using Dapper;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class BypassController : ControllerBase
    {	
		[HttpGet("logout")]
        public MVC.IActionResult Logout()
        {
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }
            return Redirect("/");
        }

        [HttpGetBypass("abusereport/UserProfile"), HttpGetBypass("abusereport/asset"), HttpGetBypass("abusereport/user"), HttpGetBypass("abusereport/users")]
        public MVC.IActionResult ReportAbuseRedirect()
        {
            return new MVC.RedirectResult("/internal/report-abuse");
        }
		
        [HttpGet("internal/release-metadata")]
        public dynamic GetReleaseMetaData([Required] string requester)
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "BadRequest");
        }
		
		[HttpPostBypass("/v1.0/SequenceStatistics/AddToSequence")]
        [HttpPostBypass("/v1.1/Counters/Increment")]
        [HttpPostBypass("/v1.0/SequenceStatistics/BatchAddToSequencesV2")]
        [HttpPostBypass("v1.0/MultiIncrement")]
        [HttpPostBypass("/game/report-stats")]
        [HttpGetBypass("usercheck/show-tos")]
        [HttpGetBypass("/v1.1/Counters/Increment")]
        [HttpGetBypass("notifications/signalr/negotiate")]
        [HttpGetBypass("notifications/negotiate")]
        [HttpPostBypass("v1.1/Counters/BatchIncrement")]
        [HttpGetBypass("v1.1/Counters/BatchIncrement")]
        public MVC.OkResult TelemetryFunctions()
        {
            return Ok();
        }
		
        private void ValidateBotAuthorization()
        {
#if DEBUG == false
	        if (Request.Headers["bot-auth"].ToString() != Roblox.Configuration.BotAuthorization)
	        {
		        throw new Exception("Internal");
	        }
#endif
        }

        [HttpGetBypass("botapi/migrate-alltypes")]
        public async Task<dynamic> MigrateAllItemsBot([Required, MVC.FromQuery] string url)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(url, false, null, new List<Type>()
            {
                Type.Image,
                Type.Audio,
                Type.Mesh,
                Type.Lua,
                Type.Model,
                Type.Decal,
                Type.Animation,
                Type.SolidModel,
                Type.MeshPart,
                Type.ClimbAnimation,
                Type.DeathAnimation,
                Type.FallAnimation,
                Type.IdleAnimation,
                Type.JumpAnimation,
                Type.RunAnimation,
                Type.SwimAnimation,
                Type.WalkAnimation,
                Type.PoseAnimation,
            }, default, false);
        }

        [HttpGetBypass("botapi/migrate-clothing")]
        public async Task<dynamic> MigrateClothingBot([Required] string assetId)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(assetId, true, 5, new List<Models.Assets.Type>() { Models.Assets.Type.TeeShirt, Models.Assets.Type.Shirt, Models.Assets.Type.Pants });
        }
        
        [HttpGetBypass("BuildersClub/Upgrade.ashx")]
        public MVC.IActionResult UpgradeNow()
        {
            return new MVC.RedirectResult("/buildersclub");
        }
		
		[HttpPostBypass("buildersclub/membership")]
		public async Task<dynamic> UpdateMembership([Required, MVC.FromForm] string membershipType)
		{
			if (userSession == null)
				throw new RobloxException(401, 0, "Not authenticated");

			if (!Enum.TryParse<MembershipType>(membershipType, out var MembershipTypeParsed) || 
				!Enum.IsDefined(MembershipTypeParsed))
			{
				throw new RobloxException(400, 0, "Invalid membership type");
			}

			await services.users.InsertOrUpdateMembership(userSession.userId, MembershipTypeParsed);
			var metadata = MembershipMetadata.GetMetadata(MembershipTypeParsed);
			switch (MembershipTypeParsed)
			{
				case MembershipType.OutrageousBuildersClub:
					await services.users.GiveUserBadge(userSession.userId, 16); // OBC badge
					break;
				case MembershipType.TurboBuildersClub:
					await services.users.GiveUserBadge(userSession.userId, 15); // TBC badge
					break;
				case MembershipType.BuildersClub:
					await services.users.GiveUserBadge(userSession.userId, 11); // BC badge
					break;
			}
			
			await services.users.GiveUserBadge(userSession.userId, 18);
    
			return new
			{
				success = true,
				message = $"Membership updated to {metadata.displayName}. You will now receive {metadata.dailyRobux} Robux each day.",
			};
		}
	
		private static string FormatTimeSpan(TimeSpan span)
		{
			if (span.TotalDays >= 1)
				return $"{(int)span.TotalDays} day{(span.TotalDays >= 2 ? "s" : "")}";
			if (span.TotalHours >= 1)
				return $"{(int)span.TotalHours} hour{(span.TotalHours >= 2 ? "s" : "")}";
			if (span.TotalMinutes >= 1)
				return $"{(int)span.TotalMinutes} minute{(span.TotalMinutes >= 2 ? "s" : "")}";
			return $"{(int)span.TotalSeconds} second{(span.TotalSeconds >= 2 ? "s" : "")}";
		}
		
		[HttpGetBypass("promocodes/redeem")]
		public async Task<dynamic> RedeemPromoCode(
			[Required] string code,
			[MVC.FromServices] NpgsqlConnection db)
		{
			if (userSession == null)
			{
				throw new RobloxException(401, 0, "Not authenticated");
			}
			
			await db.OpenAsync();
			code = code.Trim().ToUpper();
			var userId = userSession.userId;

			PromoCodeEntry promocode;
			try
			{
				promocode = await db.QuerySingleOrDefaultAsync<PromoCodeEntry>(
					"SELECT * FROM promocodes WHERE code = @code",
					new { code });

				if (promocode == null)
				{
					throw new RobloxException(400, 0, "This promocode does not exist");
				}

				var UTCTime = DateTime.UtcNow;
				var ExpiresAtUtc = promocode.expires_at.HasValue 
					? DateTime.SpecifyKind(promocode.expires_at.Value, DateTimeKind.Utc)
					: (DateTime?)null;

				if (!promocode.active)
				{
					throw new RobloxException(400, 0, "This promocode is no longer active");
				}

				if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < UTCTime)
				{
					var timeSinceExpired = UTCTime - ExpiresAtUtc.Value;
					throw new RobloxException(400, 0, 
						/* $"This promocode expired {FormatTimeSpan(timeSinceExpired)} ago"); */
						"This promocode has expired");
				}

				if (promocode.expires_at.HasValue && promocode.expires_at.Value < DateTime.UtcNow)
				{
					throw new RobloxException(400, 0, "This promocode has expired");
				}

				if (promocode.uses >= promocode.maxuses)
				{
					throw new RobloxException(400, 0, "This promocode has reached it's maximum redemptions");
				}

				var hasRedeemed = await db.ExecuteScalarAsync<int>(
					"SELECT COUNT(*) FROM promocode_redemptions WHERE user_id = @userId AND promocode = @promocodeID",
					new { userId, promocodeID = promocode.id }) > 0;

				if (hasRedeemed)
				{
					throw new RobloxException(400, 0, "You have already redeemed this promocode");
				}

				using var transaction = await db.BeginTransactionAsync();
				try
				{
					await db.ExecuteAsync(
						"UPDATE promocodes SET uses = uses + 1 WHERE id = @id",
						new { id = promocode.id },
						transaction);

					await db.ExecuteAsync(
						"INSERT INTO promocode_redemptions (promocode, user_id, asset_id, robux) " +
						"VALUES (@promocodeID, @userId, @assetId, @robux)",
						new
						{
							promocodeID = promocode.id,
							userId,
							assetId = promocode.asset_id,
							robux = promocode.robux
						},
						transaction);

					if (promocode.asset_id.HasValue)
					{
						await db.ExecuteAsync(
							"INSERT INTO user_asset (user_id, asset_id) VALUES (@userId, @assetId)",
							new { userId, assetId = promocode.asset_id.Value },
							transaction);
					}

					if (promocode.robux.HasValue && promocode.robux.Value > 0)
					{
						await db.ExecuteAsync(
							"UPDATE user_economy SET balance_robux = balance_robux + @amount WHERE user_id = @userId",
							new { userId, amount = promocode.robux.Value },
							transaction);
					}

					await transaction.CommitAsync();
				}
				catch
				{
					await transaction.RollbackAsync();
					throw;
				}

				string name = null;
				if (promocode.asset_id.HasValue)
				{
					try
					{
						var assetInfo = await services.assets.GetAssetCatalogInfo(promocode.asset_id.Value);
						name = assetInfo.name;
					}
					catch { /* ignore */ }
				}

				return new
				{
					success = true,
					message = "Promocode redeemed successfully!",
					assetId = promocode.asset_id,
					name,
					robux = promocode.robux
				};
			}
			finally
			{
				// idk man do something here
			}
		}
    }
}
