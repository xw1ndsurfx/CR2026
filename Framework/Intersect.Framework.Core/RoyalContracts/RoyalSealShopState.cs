namespace Intersect.Framework.Core.RoyalContracts;
public sealed class RoyalSealShopState
{
    public Guid ShopId { get; set; }
    public long Seals { get; set; }
    public string Message { get; set; } = "";
    public List<RoyalSealShopChoice> Shops { get; set; } = [];
    public List<RoyalSealShopOffer> Offers { get; set; } = [];
}
public sealed class RoyalSealShopChoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class RoyalSealShopOffer
{
    public int Slot { get; set; }
    public Guid ItemId { get; set; }
    public int Price { get; set; }
}
