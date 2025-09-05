using MVC = Microsoft.AspNetCore.Mvc;
using CsvHelper;
using System.Xml;
using Roblox.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using ServiceProvider = Roblox.Services.ServiceProvider;

using Roblox.Dto.Marketplace;
namespace Roblox.Website.Controllers
{

    [MVC.ApiController]
    [MVC.Route("/")]
    public class Marketplace: ControllerBase
    {

        [HttpGetBypass("marketplace/productinfo")]
        public async Task<dynamic> GetProductInfo(long assetId)
        {
            try
            {
                long Remaining = 0;
                var details = await services.assets.GetAssetCatalogInfo(assetId);
            
                if(details.itemRestrictions.Contains("Limited") || details.itemRestrictions.Contains("LimitedUnique"))
                {
                    var resale = await services.assets.GetResaleData(assetId);
                    Remaining = resale.numberRemaining;
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
                    IsNew = true,
                    IsForSale = details.isForSale,
                    IsPublicDomain = details.isForSale && details.price == 0,
                    IsLimited = details.itemRestrictions.Contains("Limited"),
                    IsLimitedUnique = details.itemRestrictions.Contains("LimitedUnique"),
                    Remaining,
                    MinimumMembershipLevel = 0
                };
            }
            catch(RecordNotFoundException)
            {
                return Redirect($"https://economy.roblox.com/v2/assets/{assetId}/details");
            };
        }
        [HttpGetBypass("v2/assets/{assetId:long}/details")]
        public async Task<dynamic> GetProductInfoNew(long assetId)
        {
            long Remaining = 0;
            var details = await services.assets.GetAssetCatalogInfo(assetId);
            if(details.itemRestrictions.Contains("Limited") || details.itemRestrictions.Contains("LimitedUnique"))
            {
                var resale = await services.assets.GetResaleData(assetId);
                Remaining = resale.numberRemaining;
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
                IsNew = true,
                IsForSale = details.isForSale,
                IsPublicDomain = details.isForSale && details.price == 0,
                IsLimited = details.itemRestrictions.Contains("Limited"),
                IsLimitedUnique = details.itemRestrictions.Contains("LimitedUnique"),
                Remaining,
                MinimumMembershipLevel = 0
            };
        }
        [HttpPostBypass("marketplace/purchase")]
        public async Task <dynamic> PurchaseProductMarket([FromForm] Dto.Marketplace.PurchaseRequest purchaseRequest)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.EconomyEnabled);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            // some sanity checks
            var productInfo = await services.assets.GetProductForAsset(purchaseRequest.productId);
            if (purchaseRequest.productId is 0 or < 0)
                purchaseRequest.productId = 0;
            if(productInfo.isLimited || productInfo.isLimitedUnique){
                return new
                {
                    status = "error",
                };
            }
            
            // Confirm asset is buyable
            var user18Plus = await services.users.Is18Plus(safeUserSession.userId);
            if (!user18Plus)
            {
                if (await services.assets.Is18Plus(purchaseRequest.productId))
                    throw new RobloxException(400, 0,
                        "You cannot purchase 18+ items until you confirm you are 18 or over.");
            }
            
            await services.users.PurchaseNormalItem(safeUserSession.userId, purchaseRequest.productId, purchaseRequest.currencyTypeId);
            stopwatch.Stop();
            Metrics.EconomyMetrics.ReportItemPurchaseTime(stopwatch.ElapsedMilliseconds,
                false);
            return new 
            {
                success = true,
                status = "Bought",
                receipt = "test"
            };
            // Report time

        }

    }
}