using Intersect.Framework.Core.Naval;
using Intersect.Server.Database;
using Intersect.Server.Naval;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test] public void AmmoPurchasesRespectBalanceTotalCapacityAndTransactionLimit()
    {
        var settings=new NavalAmmoInventorySettings();var career=new NavalCareerData{Ammo=[new(){ItemId=Guid.NewGuid(),Quantity=850}]};
        Assert.That(settings.MaximumPurchase(career,100000,10),Is.EqualTo(150));
        Assert.That(settings.MaximumPurchase(career,230,10),Is.EqualTo(23));
        settings.PurchaseLimit=12;Assert.That(settings.MaximumPurchase(career,100000,10),Is.EqualTo(12));
        career.Ammo[0].Quantity=1100;Assert.That(settings.MaximumPurchase(career,100000,10),Is.Zero);
        Assert.That(career.IsValid(),Is.True,"Legacy over-capacity inventories must never be deleted");
        career.AmmoUpgradeLevel=1;Assert.That(settings.Capacity(1),Is.EqualTo(2000));
        Assert.That(settings.MaximumPurchase(career,100000,10),Is.EqualTo(12));
    }
    [Test] public void AmmoUpgradeUnlocksByLevelOrQuestAndScalesPriceAndRequiredLevel()
    {
        var settings=new NavalAmmoInventorySettings{UnlockQuestId=Guid.NewGuid()};
        Assert.That(settings.Unlocked(0,4,false),Is.False);Assert.That(settings.Unlocked(0,5,false),Is.True);
        Assert.That(settings.Unlocked(1,5,false),Is.False);Assert.That(settings.Unlocked(1,1,true),Is.True);
        Assert.That(settings.Price(1),Is.EqualTo(2000));Assert.That(settings.LevelRequired(1),Is.EqualTo(10));
        settings.UnlockRule=NavalAmmoUnlockRule.CompletedQuest;Assert.That(settings.Unlocked(0,200,false),Is.False);
        settings.UnlockRule=NavalAmmoUnlockRule.GunneryLevel;Assert.That(settings.Unlocked(0,1,true),Is.False);
    }
    [Test] public void LargeAmmoPaymentsConsumeRealCurrencyQuantitiesAcrossStacksAndRollbackOnSaveFailure()
    {
        using var world=new EnemyTestWorld();var currency=Guid.NewGuid();var ammo=Guid.NewGuid();var p=CargoPlayer();
        p.Items[0].Set(new Item(currency,4000));p.Items[1].Set(new Item(currency,6000));
        var planned=NavalSystem.PlanNavalPayment([new Item(currency,4000),new Item(currency,6000)],currency,8500);
        Assert.That(planned[0].ItemId,Is.EqualTo(Guid.Empty));Assert.That(planned[1].Quantity,Is.EqualTo(1500));
        Assert.That(NavalSystem.CommitAmmoPurchase(p,ammo,currency,10,800,()=>false),Is.False);
        Assert.That(p.Items[0].Quantity,Is.EqualTo(4000));Assert.That(p.Items[1].Quantity,Is.EqualTo(6000));Assert.That(p.NavalCareer.Ammo,Is.Empty);
        Assert.That(NavalSystem.CommitAmmoPurchase(p,ammo,currency,10,800,()=>true),Is.True);
        Assert.That(p.NavalCareer.Ammo.Single().Quantity,Is.EqualTo(800));Assert.That(p.Items[1].Quantity,Is.EqualTo(2000));
        Assert.That(NavalSystem.CommitAmmoPurchase(p,ammo,currency,1,201,()=>throw new Exception("Over-cap purchase must never save")),Is.False);
    }
    [Test] public void PaidAmmoUpgradePersistsItsTierAndRollsBackCurrencyWhenSaveFails()
    {
        using var world=new EnemyTestWorld();
        var currency=new Intersect.Framework.Core.GameObjects.Items.ItemDescriptor(Guid.NewGuid()){ItemType=Intersect.Framework.Core.GameObjects.Items.ItemType.Currency};
        Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Set(currency.Id,currency);
        try
        {
            world.Configure(new NavalConfiguration{AmmoInventory=new(){RequiredGunneryLevel=1,UpgradeCurrencyItemId=currency.Id}});
            var p=CargoPlayer();p.Items[0].Set(new Item(currency.Id,2500));
            Assert.That(NavalSystem.CommitAmmoUpgrade(p,()=>false),Is.False);
            Assert.That(p.NavalCareer.AmmoUpgradeLevel,Is.Zero);Assert.That(p.Items[0].Quantity,Is.EqualTo(2500));
            Assert.That(NavalSystem.CommitAmmoUpgrade(p,()=>true),Is.True);
            Assert.That(p.NavalCareer.AmmoUpgradeLevel,Is.EqualTo(1));Assert.That(p.Items[0].Quantity,Is.EqualTo(1500));
            var restored=CargoPlayer();restored.NavalFleetJson=p.NavalFleetJson;
            Assert.That(restored.NavalCareer.AmmoUpgradeLevel,Is.EqualTo(1));
            Assert.That(NavalSystem.CommitAmmoUpgrade(p,()=>throw new Exception("Locked upgrade must not save")),Is.False);
        }
        finally{Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.DeleteAt(currency.Id);}
    }

}
