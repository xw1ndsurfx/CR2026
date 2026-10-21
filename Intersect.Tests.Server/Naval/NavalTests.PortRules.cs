using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.Achievements;
using Intersect.GameObjects;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [TestCase(1L)][TestCase(4L)][TestCase(275L)][TestCase(200000000000000L)]
    public void ParticipationSharesConserveTheExactPoolAndExcludeInactiveMembers(long pool)
    {
        var captain=Guid.NewGuid();var gunner=Guid.NewGuid();var idle=Guid.NewGuid();
        var result=NavalRewardSharing.Divide(pool,new Dictionary<Guid,long>{{captain,200},{gunner,60},{idle,0}});
        Assert.That(result.Values.Sum(),Is.EqualTo(pool));Assert.That(result.ContainsKey(idle),Is.False);
        Assert.That(result[captain],Is.GreaterThanOrEqualTo(result[gunner]));
    }
    [Test] public void SealingCargoPreservesQualityAndIdentityButNeverLeaksContents()
    {
        var source=Parcel(4);var sealedCargo=NavalRewardSharing.Seal(source);
        Assert.That(sealedCargo.Id,Is.EqualTo(source.Id));Assert.That(sealedCargo.Quality,Is.EqualTo(source.Quality));
        Assert.That(sealedCargo.ItemId,Is.EqualTo(Guid.Empty));Assert.That(sealedCargo.Quantity,Is.Zero);
        Assert.That(source.ItemId,Is.Not.EqualTo(Guid.Empty));
    }
    [Test] public void NavalShopTypesAndPerShopCataloguesPersistWithoutChangingLegacyShopLists()
    {
        var item=Guid.NewGuid();var shop=new ShopDescriptor {ConfigurationKind=ShopConfigurationKind.NavalShipsAndSkills,BuyingItems=[new(item,Guid.NewGuid(),20)],NavalCatalogue=new(){Enabled=true,Offers=[new(){Id="existing-model",Price=123}]},NavalSkills=[new(){Kind=NavalSkillKind.FullSail,Price=250}]};
        var restored=new ShopDescriptor {JsonBuyingItems=shop.JsonBuyingItems};
        Assert.That(restored.ConfigurationKind,Is.EqualTo(shop.ConfigurationKind));Assert.That(restored.BuyingItems.Single().ItemId,Is.EqualTo(item));Assert.That(restored.NavalCatalogue.Offers.Single().Price,Is.EqualTo(123));Assert.That(restored.NavalSkills.Single().Price,Is.EqualTo(250));
        var legacy=new ShopDescriptor {JsonBuyingItems="[]"};Assert.That(legacy.ConfigurationKind,Is.EqualTo(ShopConfigurationKind.Normal));Assert.That(legacy.JsonBuyingItems,Is.EqualTo("[]"));
    }
    [Test] public void NavalAchievementsMirrorAllNineQuestTaskTypesAndPreserveExistingIds()
    {
        Assert.That((int)AchievementObjectiveType.InvasionDefeats,Is.EqualTo(18));
        foreach(var kind in Enum.GetValues<NavalTaskKind>())
        {var objective=(AchievementObjectiveType)(19+(int)kind);Assert.That(objective.ToString(),Is.EqualTo("Naval"+kind));}
    }
    [Test] public void PresentButInactiveCrewDoesNotReceiveAnEnemySinkingReward()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);var idle=Guid.NewGuid();
        AddCrewPresence(world,ship,idle,Intersect.Framework.Core.Timing.Global.Milliseconds);world.Set(enemy,"Health",0);
        world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,world.State(ship).CaptainId);
        Assert.That(world.Reward(idle),Is.Zero);Assert.That(world.Reward(world.State(ship).CaptainId),Is.EqualTo(world.Field<HerboniaPatrol>(enemy,"Enemy").SailingExperienceReward));
    }
    [Test] public void MultipleSingleItemLotsRespectTheWholePoolQuotaInsteadOfAllGoingToTheCaptain()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);var gunner=Guid.NewGuid();
        AddCrewPresence(world,ship,gunner,Intersect.Framework.Core.Timing.Global.Milliseconds);
        world.Field<Dictionary<Guid,long>>(enemy,"Contributions")[gunner]=50;
        world.Field<HerboniaPatrol>(enemy,"Enemy").Loot=Enumerable.Range(0,3).Select(_=>new NavalLootEntry {ItemId=Guid.NewGuid(),MinimumQuantity=1,MaximumQuantity=1,ChancePercent=100}).ToArray();
        world.Set(enemy,"Health",0);world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,world.State(ship).CaptainId);
        long Units(Guid id)=>((Array)world.Rewards[id]!.GetType().GetProperty("Cargo")!.GetValue(world.Rewards[id])!).Cast<object>().Sum(award=>((NavalCargoParcel)award.GetType().GetProperty("Parcel")!.GetValue(award)!).Quantity);
        Assert.That(Units(world.State(ship).CaptainId),Is.EqualTo(2));Assert.That(Units(gunner),Is.EqualTo(1));
    }

    [Test] public void ExpiredDelegatedHelmCannotReceiveLootOrBreakTheSinkingAllocation()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);var departed=Guid.NewGuid();
        AddCrewPresence(world,ship,departed,Intersect.Framework.Core.Timing.Global.Milliseconds-3000);world.Set(ship,"HelmsmanId",departed);
        world.Field<HerboniaPatrol>(enemy,"Enemy").Loot=[new(){ItemId=Guid.NewGuid(),MinimumQuantity=4,MaximumQuantity=4,ChancePercent=100}];
        world.Set(enemy,"Health",0);Assert.DoesNotThrow(()=>world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,world.State(ship).CaptainId));
        Assert.That(world.Reward(departed),Is.Zero);Assert.That(world.Reward(world.State(ship).CaptainId),Is.GreaterThan(0));
    }

}
