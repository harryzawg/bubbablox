using Roblox.Models.Economy;
namespace Roblox.Dto.Marketplace;

public class PurchaseRequest
{
    public long productId { get; set; }
    public CurrencyType currencyTypeId { get; set; }
    public long purchasePrice { get; set; }
    public string locationType { get; set; }
    public long locationId { get; set; }
}

public class ProductPurchaseRequest
{
    public long productId { get; set; }
    public CurrencyType currencyTypeId { get; set; }
    public long placeId { get; set; }
    public long expectedUnitPrice { get; set; }
    public string requestId { get; set; }
}