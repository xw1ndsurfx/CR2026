using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Framework.Core.Travel;
using Intersect.GameObjects;
using Intersect.Server.LogiCoins;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void PaidSlotGiftsBelongToTheRecipientAndJournalCopiesCannotMutateEntitlements()
    {
        Options.EnsureCreated();var buyer=Guid.NewGuid();var recipient=Guid.NewGuid();var offer=Guid.NewGuid();
        var record=new LogiCoinPurchaseRecord{PurchaseId=Guid.NewGuid(),UserId=buyer,RecipientUserId=recipient,OfferId=offer,Price=12,
            WalletDebited=true,CharacterSlotsGranted=2,OfferName="Two slots",RecipientName="Friend",OfferJson="{}"};
        var flags=BindingFlags.Static|BindingFlags.NonPublic;var field=typeof(LogiCoinPurchaseJournal).GetField("_records",flags)!;var before=field.GetValue(null);
        field.SetValue(null,new Dictionary<Guid,LogiCoinPurchaseRecord>{{record.PurchaseId,record}});
        try
        {
            Assert.That(LogiCoinPurchaseJournal.ExtraCharacterSlots(buyer),Is.Zero);
            Assert.That(LogiCoinPurchaseJournal.ExtraCharacterSlots(recipient),Is.EqualTo(2));
            Assert.That(LogiCoinPurchaseRuntime.CharacterLimit(recipient),Is.EqualTo(Options.Instance.Player.MaxCharacters+2));
            var copy=LogiCoinPurchaseJournal.GetOrCreate(record.PurchaseId,buyer,offer,12,recipient);
            copy.CharacterSlotsGranted=20;Assert.That(LogiCoinPurchaseJournal.ExtraCharacterSlots(recipient),Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>LogiCoinPurchaseJournal.GetOrCreate(record.PurchaseId,buyer,offer,12,Guid.NewGuid()));
            Assert.That(LogiCoinPurchaseJournal.History(buyer).Single().Recipient,Is.EqualTo("Friend"));
            Assert.That(LogiCoinPurchaseJournal.History(recipient),Is.Empty);
            Assert.That(LogiCoinPurchaseJournal.Pending(recipient).Single().OfferJson,Is.EqualTo("{}"));
            Assert.That(LogiCoinPurchaseJournal.Pending(buyer),Is.Empty);
        }
        finally{field.SetValue(null,before);}
    }
    [Test]
    public void SlotOffersAreAccountScopedAndTheirLanguageUrlKeepsOtherQueryValuesAndAnchors()
    {
        var slot=new LogiCoinShopOffer{Name="Extra slot",Type=LogiCoinOfferType.CharacterSlot,CharacterSlots=2,PriceLogiCoins=25};
        Assert.That(slot.IsStructurallyValid,Is.True);Assert.That(slot.AccountDelivery,Is.True);
        slot.CharacterSlots=0;Assert.That(slot.IsStructurallyValid,Is.False);
        slot.CharacterSlots=2;var config=new LogiCoinShopConfiguration{ShopUrl="https://example.test/shop?category=currency&lang=en#buy",Offers=[slot]};
        var copy=LogiCoinShopConfiguration.FromJson(config.ToJson());Assert.That(copy.Offers.Single().CharacterSlots,Is.EqualTo(2));
        var url=new Uri(copy.UrlForLanguage("fr-CA"));Assert.That(url.Query,Does.Contain("category=currency").And.Contain("lang=fr").And.Not.Contain("lang=en"));
        Assert.That(url.Fragment,Is.EqualTo("#buy"));Assert.That(copy.UrlForLanguage("xx"),Does.Contain("lang=en"));
        Assert.That((int)LogiCoinOfferType.Premium,Is.EqualTo(2));Assert.That((int)LogiCoinOfferType.CharacterSlot,Is.EqualTo(3));
    }
    [Test]
    public void SpecialShopOffersPersistWithoutChangingLegacyShopWireNumbers()
    {
        var shop=new ShopDescriptor(Guid.NewGuid()){ConfigurationKind=ShopConfigurationKind.Spells,ServiceOffers=[new(){Name="Fire",SpellId=Guid.NewGuid(),Price=15}]};
        var restored=new ShopDescriptor(Guid.NewGuid()){JsonBuyingItems=shop.JsonBuyingItems};
        Assert.That(restored.ConfigurationKind,Is.EqualTo(ShopConfigurationKind.Spells));Assert.That(restored.ServiceOffers.Single().SpellId,Is.EqualTo(shop.ServiceOffers[0].SpellId));
        Assert.That((int)ShopConfigurationKind.NavalShipsAndSkills,Is.EqualTo(2));
        var music=new ShopServiceOffer{Name="Theme",Music="../private.wav"};Assert.That(music.IsValid,Is.False);
        music.Music="theme.ogg";Assert.That(music.IsValid,Is.True);music.DurationSeconds=0;Assert.That(music.IsValid,Is.False);
    }
    [Test]
    public void FriendAndLobbyTeleportPoliciesRequirePermittedDestinationsAndRespectOriginRules()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var denied=Guid.NewGuid();var rules=new TeleportConfiguration{FriendsEnabled=true,AllowedMaps=[a,b]};
        Assert.That(rules.Allows(a,b,true),Is.True);Assert.That(rules.Allows(denied,b,true),Is.False);Assert.That(rules.Allows(a,denied,true),Is.False);
        Assert.That(rules.Allows(denied,denied,false),Is.True,"Existing lobby travel remains enabled until explicitly restricted.");
        rules.RestrictLobbyMaps=true;Assert.That(rules.Allows(a,denied,false),Is.False);
        rules.CheckOriginMap=false;Assert.That(rules.Allows(denied,b,true),Is.True);
        rules.FriendsEnabled=false;Assert.That(rules.Allows(a,b,true),Is.False);
    }
}
