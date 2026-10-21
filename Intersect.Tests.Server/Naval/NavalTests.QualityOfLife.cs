using Intersect.Framework.Core;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Server.Networking;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    private static Intersect.Server.Entities.Player ConveniencePlayer()
    {
        var player=CargoPlayer();player.EntityLock=new();player.PetCollection=new();player.Variables=[];player.Appearance=new();return player;
    }
    [Test] public void MonthlyMissionsSeparateCalendarMonthsAndKeepOrdinaryAchievementsStable()
    {
        var mission=new AchievementDefinition {Monthly=true};
        var january=new DateTimeOffset(2026,1,31,23,59,59,TimeSpan.Zero);
        Assert.That(mission.ProgressIdentity(january),Is.EqualTo(mission.ProgressIdentity(january.AddDays(-15))));
        Assert.That(mission.ProgressIdentity(january),Is.Not.EqualTo(mission.ProgressIdentity(january.AddSeconds(1))));
        Assert.That(mission.ProgressIdentity(january),Is.Not.EqualTo(mission.ProgressIdentity(january.AddYears(1))));
        Assert.That(mission.ProgressIdentity(january),Is.EqualTo(mission.ProgressIdentity(january.ToOffset(TimeSpan.FromHours(14)))));
        mission.Monthly=false;Assert.That(mission.ProgressIdentity(january),Is.EqualTo(mission.Id));
        Assert.That(mission.ProgressIdentity(january.AddMonths(1)),Is.EqualTo(mission.Id));
    }
    [Test] public void SavedOutfitsCaptureOwnedAppearanceAndRollBackOnFailedPersistence()
    {
        var player=ConveniencePlayer();player.Appearance=new CharacterAppearance();
        var request=new PremiumConvenienceRequestPacket {Action=PremiumConvenienceAction.SaveOutfit,Name="Blue captain"};
        var saved=false;
        Assert.That(PremiumConvenienceTransactions.Apply(player,request,false,()=>saved=true),Is.EqualTo("Premium is required."));
        Assert.That(saved,Is.False);Assert.That(player.PetCollection.Convenience.Outfits,Is.Empty);
        Assert.That(PremiumConvenienceTransactions.Apply(player,request,true,()=>false),Is.EqualTo("Changes could not be saved."));
        Assert.That(player.PetCollection.Convenience.Outfits,Is.Empty);
        Assert.That(PremiumConvenienceTransactions.Apply(player,request,true,()=>true),Is.EqualTo("Saved."));
        var outfit=player.PetCollection.Convenience.Outfits.Single();
        Assert.That(outfit.Name,Is.EqualTo("Blue captain"));Assert.That(outfit.Appearance,Is.Not.SameAs(player.Appearance));
        var json=player.PetsJson;
        Assert.That(PremiumConvenienceTransactions.Apply(player,new(){Action=PremiumConvenienceAction.DeleteOutfit,Id=outfit.Id},false,()=>false),Is.EqualTo("Changes could not be saved."));
        Assert.That(player.PetsJson,Is.EqualTo(json));
        Assert.That(PremiumConvenienceTransactions.Apply(player,new(){Action=PremiumConvenienceAction.ApplyOutfit,Id=Guid.NewGuid()},true,()=>true),Is.EqualTo("Saved outfit not found."));
        Assert.That(player.PetCollection.Convenience.Outfits.Count,Is.EqualTo(1));
    }
    [Test] public void LegacyItemsShowValueAndLevelAndNewTypesPreserveExistingWireNumbers()
    {
        var item=JsonConvert.DeserializeObject<ItemDescriptor>("{\"Id\":\""+Guid.NewGuid()+"\",\"Name\":\"Old item\"}")!;
        Assert.That(item.Presentation.ShowValue && item.Presentation.ShowRequiredLevel,Is.True);
        Assert.That(item.MinimumLevel,Is.EqualTo(1));
        Assert.That((int)ItemType.Bag,Is.EqualTo(6));Assert.That((int)ItemType.Crafting,Is.EqualTo(7));Assert.That((int)ItemType.Ammo,Is.EqualTo(8));Assert.That((int)ItemType.Key,Is.EqualTo(9));
        item.ItemType=ItemType.Ammo;item.Icon="item.png";item.Presentation.NavalAmmoIcon="cannon.png";
        Assert.That(item.UsesSpellIcon,Is.True);Assert.That(item.DisplayIcon,Is.EqualTo("cannon.png"));
        var copy=new ItemDescriptor(Guid.NewGuid()){JsonPresentation=item.JsonPresentation};
        Assert.That(copy.Presentation.NavalAmmoIcon,Is.EqualTo("cannon.png"));
    }
    [Test] public void TimedLootBonusRejectsInvalidValuesAndWeakerBonusesAndExpiresOnServerTime()
    {
        var player=ConveniencePlayer();
        Assert.That(player.GrantEventBonus(true,0,30),Is.False);
        Assert.That(player.GrantEventBonus(true,25,30),Is.True);Assert.That(player.PersonalLootBonus,Is.EqualTo(25));
        var until=player.GetVariable(Intersect.Server.Entities.Player.LootBoostUntilId).Value.Integer;
        Assert.That(player.GrantEventBonus(true,10,30),Is.False);
        Assert.That(player.GetVariable(Intersect.Server.Entities.Player.LootBoostUntilId).Value.Integer,Is.EqualTo(until));
        Assert.That(player.GrantEventBonus(true,25,30),Is.True);
        Assert.That(player.GetVariable(Intersect.Server.Entities.Player.LootBoostUntilId).Value.Integer,Is.EqualTo(until+30000));
        player.SetVariableValue(Intersect.Server.Entities.Player.LootBoostUntilId,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-1);
        Assert.That(player.PersonalLootBonus,Is.Zero);
    }
}
