using Intersect.Framework.Core.Naval;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed class NavalPatrolAutoBalanceTests
{
    private static HerboniaPatrol Patrol()=>new(){Ship=new(){MapId=Guid.NewGuid(),X=10.5,Y=10.5}};

    [Test]
    public void AllLevelsAndProfilesProduceValidMonotonicStats()
    {
        var settings=new NavalConfiguration();
        foreach(var profile in Enum.GetValues<NavalPatrolDifficulty>())
        {
            var previous=Patrol();NavalPatrolAutoBalance.Apply(previous,1,profile,settings);
            for(var level=1;level<=200;level++)
            {
                var p=Patrol();NavalPatrolAutoBalance.Apply(p,level,profile,settings);
                Assert.That(p.IsValid(),Is.True,$"Level {level}, {profile}");
                Assert.That(p.MaximumHealth,Is.GreaterThanOrEqualTo(previous.MaximumHealth));
                Assert.That(p.CannonDamage,Is.GreaterThanOrEqualTo(previous.CannonDamage));
                Assert.That(p.SailingExperienceReward,Is.GreaterThanOrEqualTo(previous.SailingExperienceReward));
                Assert.That(p.Ship.MaxSpeed,Is.GreaterThanOrEqualTo(previous.Ship.MaxSpeed));
                previous=p;
            }
        }
    }

    [Test]
    public void GenerationPreservesAuthoredContentIdentityAndPlacement()
    {
        var p=Patrol();var id=p.Ship.Id;var map=p.Ship.MapId;var dungeon=Guid.NewGuid();
        p.BoardingDungeonId=dungeon;p.BoardingEntryMapId=Guid.NewGuid();p.Enabled=true;
        p.Ship.Graphics="custom.png";p.Ship.IdleGraphics="idle.png";p.Ship.Width=3;p.Ship.Name="Custom patrol";
        p.Ship.AdditionalData=new Dictionary<string,JToken>{{"custom",123}};
        p.Loot=[new(){ItemId=Guid.NewGuid(),ChancePercent=50,MinimumQuantity=2,MaximumQuantity=4}];
        var loot=p.Loot;
        NavalPatrolAutoBalance.Apply(p,50,NavalPatrolDifficulty.Elite,new());
        Assert.That((p.Ship.Id,p.Ship.MapId,p.Ship.X,p.Ship.Y),Is.EqualTo((id,map,10.5,10.5)));
        Assert.That(p.Ship.Graphics,Is.EqualTo("custom.png"));Assert.That(p.Ship.IdleGraphics,Is.EqualTo("idle.png"));
        Assert.That(p.Ship.Width,Is.EqualTo(3));Assert.That(p.Ship.Name,Is.EqualTo("Custom patrol"));
        Assert.That(p.Ship.AdditionalData["custom"].Value<int>(),Is.EqualTo(123));
        Assert.That(p.Loot,Is.SameAs(loot));Assert.That(p.BoardingDungeonId,Is.EqualTo(dungeon));Assert.That(p.Enabled,Is.True);
    }

    [Test]
    public void ProfilesAndCurrentCombatSettingsAffectTheGeneratedStats()
    {
        var normal=Patrol();var elite=Patrol();var boss=Patrol();var config=new NavalConfiguration();
        NavalPatrolAutoBalance.Apply(normal,30,NavalPatrolDifficulty.Normal,config);
        NavalPatrolAutoBalance.Apply(elite,30,NavalPatrolDifficulty.Elite,config);
        NavalPatrolAutoBalance.Apply(boss,30,NavalPatrolDifficulty.Boss,config);
        Assert.That(elite.MaximumHealth,Is.GreaterThan(normal.MaximumHealth));Assert.That(boss.MaximumHealth,Is.GreaterThan(elite.MaximumHealth));
        Assert.That(boss.SailingExperienceReward,Is.GreaterThan(elite.SailingExperienceReward));
        config.CannonDamage*=2;var stronger=Patrol();NavalPatrolAutoBalance.Apply(stronger,30,NavalPatrolDifficulty.Normal,config);
        Assert.That(stronger.MaximumHealth,Is.EqualTo(normal.MaximumHealth*2).Within(1));
        config.Sailing.BaseExperience*=2;NavalPatrolAutoBalance.Apply(stronger,30,NavalPatrolDifficulty.Normal,config);
        Assert.That(stronger.SailingExperienceReward,Is.EqualTo(normal.SailingExperienceReward*2).Within(1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>NavalPatrolAutoBalance.Apply(stronger,0,NavalPatrolDifficulty.Normal,config));
        Assert.Throws<ArgumentOutOfRangeException>(()=>NavalPatrolAutoBalance.Apply(stronger,201,NavalPatrolDifficulty.Normal,config));
    }
}
