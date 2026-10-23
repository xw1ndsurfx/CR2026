using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Models;
using Newtonsoft.Json;

namespace Intersect.GameObjects;

public enum ShopConfigurationKind { Normal, NavalAmmunition, NavalShipsAndSkills, Spells, PremiumMusic }

public partial class ShopDescriptor : DatabaseObject<ShopDescriptor>, IFolderable
{
    [NotMapped]
    public List<ShopItemDescriptor> BuyingItems { get; set; } = [];

    [NotMapped]
    public List<ShopItemDescriptor> SellingItems { get; set; } = [];

    [JsonConstructor]
    public ShopDescriptor(Guid id) : base(id)
    {
        Name = "New Shop";
    }

    //EF is so damn picky about its parameters
    public ShopDescriptor()
    {
        Name = "New Shop";
    }

    [NotMapped] public Intersect.Framework.Core.Naval.HarbourCatalog NavalCatalogue {get;set;}=new();
    [NotMapped] public Intersect.Framework.Core.Naval.NavalSkillDefinition[] NavalSkills {get;set;}=[];
    [NotMapped] public ShopServiceOffer[] ServiceOffers {get;set;}=[];
    [NotMapped] public ShopConfigurationKind ConfigurationKind {get;set;}
    public int BuybackDiscountPercent {get;set;}
    public int BuybackPrice(int price) => price<=0 ? 0 : Math.Max(1,(int)((decimal)price*(100-Math.Clamp(BuybackDiscountPercent,0,99))/100));
    public bool BuyingWhitelist { get; set; } = true;

    //Spawn Info
    [Column("DefaultCurrency")]
    [JsonProperty]
    public Guid DefaultCurrencyId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public ItemDescriptor DefaultCurrency
    {
        get => ItemDescriptor.Get(DefaultCurrencyId);
        set => DefaultCurrencyId = value?.Id ?? Guid.Empty;
    }

    [Column("BuyingItems")]
    [JsonIgnore]
    public string JsonBuyingItems
    {
        get => ConfigurationKind==ShopConfigurationKind.Normal && !NavalCatalogue.Enabled && NavalSkills.Length==0?JsonConvert.SerializeObject(BuyingItems):JsonConvert.SerializeObject(new {ConfigurationKind,Items=BuyingItems,NavalCatalogue,NavalSkills,ServiceOffers});
        set
        {
            var token=Newtonsoft.Json.Linq.JToken.Parse(string.IsNullOrWhiteSpace(value)?"[]":value);
            if(token is Newtonsoft.Json.Linq.JArray) {BuyingItems=token.ToObject<List<ShopItemDescriptor>>()??[];ConfigurationKind=ShopConfigurationKind.Normal;ServiceOffers=[];NavalSkills=[];NavalCatalogue=new();}
            else {ServiceOffers=token["ServiceOffers"]?.ToObject<ShopServiceOffer[]>()??[];ConfigurationKind=token["ConfigurationKind"]?.ToObject<ShopConfigurationKind>()??ShopConfigurationKind.Normal;BuyingItems=token["Items"]?.ToObject<List<ShopItemDescriptor>>()??[];NavalCatalogue=token["NavalCatalogue"]?.ToObject<Intersect.Framework.Core.Naval.HarbourCatalog>()??new();NavalSkills=token["NavalSkills"]?.ToObject<Intersect.Framework.Core.Naval.NavalSkillDefinition[]>()??[];}
        }
    }

    [Column("SellingItems")]
    [JsonIgnore]
    public string JsonSellingItems
    {
        get => JsonConvert.SerializeObject(SellingItems);
        set => SellingItems = JsonConvert.DeserializeObject<List<ShopItemDescriptor>>(value);
    }

    public string BuySound { get; set; } = null;

    public string SellSound { get; set; } = null;

    /// <inheritdoc />
    public string Folder { get; set; } = string.Empty;
}