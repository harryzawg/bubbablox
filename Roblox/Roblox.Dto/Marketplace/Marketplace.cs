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