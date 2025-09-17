using Roblox.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Roblox.Dto.Games;
using Roblox.Exceptions;
using Roblox.Models.Assets;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;
using Type = Roblox.Models.Assets.Type;
using System.ComponentModel.DataAnnotations;
using Roblox.Logging;

namespace Roblox.Website.Controllers 
{
    [ApiController]
    [Route("/")]
    public class Marketplace : ControllerBase 
    {
		// Everything in here is for marketplace/in game asset handling
		// stupid ass hack but works really well
		[HttpGetBypass("/currency/balance")]
		public async Task<dynamic> GetBalance()
		{
			long userId;
			
			var Session = Request.Headers["Roblox-Session-Id"].ToString();
			if (!string.IsNullOrEmpty(Session))
			{
				var parts = Session.Split('|');
				
				if (parts.Length >= 9)
				{
					var sessionJwt = parts[8];
					
					if (!string.IsNullOrEmpty(sessionJwt) && sessionJwt != "null")
					{
						try
						{
							var jwtEntry = SessionMiddleware.DecodeJwt<JwtEntry>(sessionJwt);
							var sessionInfo = await services.users.GetSessionById(jwtEntry.sessionId);
							
							if (sessionInfo != null && sessionInfo.userId > 0)
							{
								userId = sessionInfo.userId;
								return await services.economy.GetBalance(CreatorType.User, userId);
							}
						}
						catch (RecordNotFoundException)
						{
							// fallback to safeusersession
						}
						catch (Exception)
						{
							// fallback to safeusersession
						}
					}
				}
			}
			
			if (safeUserSession == null)
			{
				throw new RobloxException(401, 0, "Not authenticated");
			}
			
			userId = safeUserSession.userId;
			return await services.economy.GetBalance(CreatorType.User, userId);
		}

        [HttpGetBypass("/ownership/hasasset")]
        public async Task<string> DoesOwnAsset(long userId, long assetId)
        {
            return (await services.users.GetUserAssets(userId, assetId)).Any() ? "true" : "false";
        }
		
		[HttpGetBypass("asset/isowned")]
		public async Task<dynamic> CheckAssetOwnership([Required] long assetId)
		{
			if (userSession == null)
			{
				throw new RobloxException(401, 0, "Not authenticated");
			}

			var ownsAsset = (await services.users.GetUserAssets(userSession.userId, assetId)).Any();
			return new
			{
				isOwned = ownsAsset
			};
		}
		
        [HttpGetBypass("marketplace/productinfo")]
        public async Task<dynamic> GetProductInfo(long assetId) 
        {
            try 
            {
                var details = await services.assets.GetAssetCatalogInfo(assetId);
                long remaining = 0;
                
                if (details.itemRestrictions.Contains("Limited") ||
                    details.itemRestrictions.Contains("LimitedUnique")) {
                    var resale = await services.assets.GetResaleData(assetId);
                    remaining = resale.numberRemaining;
                }

                return new 
                {
                    TargetId = details.id,
                    AssetId = details.id,
                    ProductId = details.id,
                    Name = details.name,
                    Description = details.description,
                    AssetTypeId = (int)details.assetType,
                    Creator = new 
                    {
                        Id = details.creatorTargetId,
                        Name = details.creatorName,
                        CreatorType = details.creatorType,
                        CreatorTargetId = details.creatorTargetId
                    },
                    IconImageAssetId = 0,
                    Created = details.createdAt,
                    Updated = details.updatedAt,
                    PriceInRobux = details.price,
                    PriceInTickets = details.priceTickets,
                    Sales = details.saleCount,
                    IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                    IsForSale = details.isForSale,
                    IsPublicDomain = details.isForSale && details.price == 0,
                    IsLimited = details.itemRestrictions.Contains("Limited"),
                    IsLimitedUnique = details.itemRestrictions.Contains("LimitedUnique"),
                    Remaining = remaining,
                    MinimumMembershipLevel = 0
                };
            }
            catch (RecordNotFoundException) 
            {
                return Redirect($"https://economy.roproxy.com/v2/assets/{assetId}/details");
            };
        }

        [HttpGetBypass("v2/assets/{assetId:long}/details")]
        public async Task<dynamic> GetProductInfoNew(long assetId)
        {
            long Remaining = 0;
            var details = await services.assets.GetAssetCatalogInfo(assetId);
            if (details.itemRestrictions.Contains("Limited") || details.itemRestrictions.Contains("LimitedUnique"))
            {
                var resale = await services.assets.GetResaleData(assetId);
                Remaining = resale.numberRemaining;
            }
            try
            {
                return new
                {
                    TargetId = details.id,
                    AssetId = details.id,
                    ProductId = details.id,
                    Name = details.name,
                    Description = details.description,
                    AssetTypeId = (int)details.assetType,
                    Creator = new
                    {
                        Id = details.creatorTargetId,
                        Name = details.creatorName,
                        CreatorType = details.creatorType,
                        CreatorTargetId = details.creatorTargetId
                    },
                    IconImageAssetId = 0,
                    Created = details.createdAt,
                    Updated = details.updatedAt,
                    PriceInRobux = details.price,
                    PriceInTickets = details.priceTickets,
                    Sales = details.saleCount,
                    IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                    IsForSale = details.isForSale,
                    IsPublicDomain = details.isForSale && details.price == 0,
                    IsLimited = details.itemRestrictions.Contains("Limited"),
                    IsLimitedUnique = details.itemRestrictions.Contains("LimitedUnique"),
                    Remaining,
                    MinimumMembershipLevel = 0
                };
            }
            catch (RecordNotFoundException)
            {
                return Redirect($"https://economy.roproxy.com/v2/assets/{assetId}/details");
            }
        }

		[HttpPostBypass("marketplace/purchase")]
		public async Task<dynamic> PurchaseProductMarket([FromForm] Dto.Marketplace.PurchaseRequest purchaseRequest)
		{
			FeatureFlags.FeatureCheck(FeatureFlag.EconomyEnabled);
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			
			long userId;

			var Session = Request.Headers["Roblox-Session-Id"].ToString();
			if (!string.IsNullOrEmpty(Session))
			{
				var parts = Session.Split('|');
				
				if (parts.Length >= 9)
				{
					var sessionJwt = parts[8];
					
					if (!string.IsNullOrEmpty(sessionJwt) && sessionJwt != "null")
					{
						try
						{
							var jwtEntry = SessionMiddleware.DecodeJwt<JwtEntry>(sessionJwt);
							var sessionInfo = await services.users.GetSessionById(jwtEntry.sessionId);
							
							if (sessionInfo != null && sessionInfo.userId > 0)
							{
								userId = sessionInfo.userId;
							}
							else
							{
								throw new RobloxException(401, 0, "Invalid session");
							}
						}
						catch (RecordNotFoundException)
						{
							throw new RobloxException(401, 0, "Session not found");
						}
						catch (Exception)
						{
							throw new RobloxException(401, 0, "Session validation failed");
						}
					}
					else
					{
						throw new RobloxException(401, 0, "Bad session");
					}
				}
				else
				{
					throw new RobloxException(401, 0, "Bad session");
				}
			}
			else
			{
				if (safeUserSession == null)
				{
					throw new RobloxException(401, 0, "Not authenticated");
				}
				
				userId = safeUserSession.userId;
			}
			
			var productInfo = await services.assets.GetProductForAsset(purchaseRequest.productId);
			if (purchaseRequest.productId is 0 or < 0)
				purchaseRequest.productId = 0;
			if (productInfo.isLimited || productInfo.isLimitedUnique) 
				throw new BadRequestException(0, "Cannot purchase Limited/Limited Unique items in game");
			
			await services.users.PurchaseNormalItem(userId, purchaseRequest.productId,
				purchaseRequest.currencyTypeId);
			
			stopwatch.Stop();
			Metrics.EconomyMetrics.ReportItemPurchaseTime(stopwatch.ElapsedMilliseconds,
				false);
			
			return new
			{
				success = true,
				status = "Bought",
				receipt = "Hi"
			};
		}

        [HttpGetBypass("marketplace/productdetails")]
        public async Task<dynamic> GetProductDetailsMarketplace(long productId)
        {
            try
            {
                var details = await services.assets.GetAssetCatalogInfo(productId);
                return new
                {
                    TargetId = 180,
                    AssetId = 0,
                    ProductId = details.id,
                    ProductType = "Developer Product",
                    Name = details.name,
                    Description = details.description,
                    AssetTypeId = 0,
                    Creator = new
                    {
                        Id = 0,
                        Name = (string?)null,
                        CreatorType = details.creatorType
                    },
                    Created = details.createdAt,
                    Updated = details.updatedAt,
                    PriceInRobux = details.price,
                    PriceInTickets = (int?)null,
                    IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                    IsForSale = details.isForSale,
                    IsPublicDomain = details.isForSale && details.price == 0,
                    IsLimited = false,
                    IsLimitedUnique = false,
                    Remaining = (int?)null,
                    MinimumMembershipLevel = 0
                };
            }
            catch (RecordNotFoundException)
            {
				return Redirect($"/marketplace/productinfo?assetId={productId}");
            }
            throw new BadRequestException(0, "Asset " + productId + " does not exist.");
        }

        [HttpGetBypass("marketplace/game-pass-product-info")]
        public async Task<dynamic> GetPassInfo(long gamePassId)
        {
            var details = await services.assets.GetAssetCatalogInfo(gamePassId);

            return new
            {
                TargetId = 180,
                ProductType = "Game Pass",
                AssetId = details.id,
                ProductId = details.id,
                Name = details.name,
                Description = details.description,
                AssetTypeId = (int)details.assetType, 
                Creator = new
                {
                    Id = details.creatorTargetId,
                    Name = details.creatorName,
                    CreatorType = details.creatorType,
                    CreatorTargetId = details.creatorTargetId
                },
                IconImageAssetId = details.id,
                Created = details.createdAt,
                Updated = details.updatedAt,
                PriceInRobux = details.price,
                PriceInTickets = details.priceTickets,
                Sales = details.saleCount,
                IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                IsForSale = details.isForSale,
                IsPublicDomain = details.isForSale && details.price == 0,
                IsLimited = false,
                IsLimitedUnique = false,
                Remaining = 0,
                MinimumMembershipLevel = 0,
                ContentRatingTypeId = 0
            };
        }
    }
}