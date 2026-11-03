using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Server.Archery;
using System.Reflection;
using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Database;
using NUnit.Framework;

namespace Intersect.Tests.Server;

public sealed partial class RoyalCombatTests
{
    [TestCase("swords"), TestCase("daggers"), TestCase("axes"), TestCase("magic"), TestCase("whips")]
    public void EveryWeaponTreeHasPurchasablePersistentTalents(string key)
    {
        using var db=new ProgressDatabase();
        var category=new RoyalCombatConfiguration().Masteries.Single(c=>c.Key==key);
        Assert.That(MasteryRules.ValidTree(category),Is.True);
        Assert.That(category.Talents,Has.Length.EqualTo(6));
        Assert.That(category.Talents.All(t=>t.Configured),Is.True,"Every default talent must have a real combat effect");
        var character=Guid.NewGuid();var first=category.Talents[0];
        Assert.That(RoyalCombatRuntime.BuyTalent(character,category,first.Key,Guid.NewGuid()),Is.False);
        RoyalCombatRuntime.Award(character,category,category.Required(1));
        Assert.That(RoyalCombatRuntime.BuyTalent(character,category,first.Key,Guid.NewGuid()),Is.True);
        Assert.That(RoyalCombatRuntime.LoadRanks(character)[key][first.Key],Is.EqualTo(1));
        Assert.That(RoyalCombatRuntime.BuyTalent(character,category,first.Key,Guid.NewGuid()),Is.False,"The same point cannot be spent twice");
        Assert.That(MasteryRules.CanBuy(category,category.Talents[^1],category.Maximum,new Dictionary<string,int>()),Is.False,"Prerequisites remain enforced");
    }

    [Test]
    public void WeaponTalentMigrationFillsOnlyEmptyBuiltInTreesOnceAndPreservesAuthoredData()
    {
        var source=new RoyalCombatConfiguration { BuiltInMasteriesVersion=1 };
        foreach(var category in source.Masteries) category.Talents=[];
        var swords=source.Masteries.Single(c=>c.Key=="swords");
        swords.Cap=10;swords.WeaponIds=[Guid.NewGuid()];swords.LevelUpAnimationId=Guid.NewGuid();
        var daggers=source.Masteries.Single(c=>c.Key=="daggers");
        daggers.Talents=[new MasteryTalent { Key="authored",Name="Authored talent",DamagePercentPerRank=1 }];
        var migrated=RoyalCombatConfiguration.FromJson(source.ToJson());
        var copied=migrated.Masteries.Single(c=>c.Key=="swords");
        Assert.That(migrated.BuiltInMasteriesVersion,Is.EqualTo(2));
        Assert.That(MasteryRules.ValidTree(copied),Is.True,"Templates respect lower configured caps");
        Assert.That(copied.WeaponIds,Is.EqualTo(swords.WeaponIds));
        Assert.That(copied.LevelUpAnimationId,Is.EqualTo(swords.LevelUpAnimationId));
        Assert.That(migrated.Masteries.Single(c=>c.Key=="daggers").Talents.Single().Key,Is.EqualTo("authored"));
        Assert.That(migrated.Masteries.Single(c=>c.Key=="archery").Talents,Is.Empty,"An authored archery tree is not changed");
        copied.Talents=[];
        Assert.That(RoyalCombatConfiguration.FromJson(migrated.ToJson()).Masteries.Single(c=>c.Key=="swords").Talents,Is.Empty,"An editor may deliberately clear a migrated tree");
    }

    [Test]
    public void ActiveWeaponSkillsRequireLearnedRankWeaponRangeAndCooldownWithoutLearningSpells()
    {
        using var db=new ProgressDatabase();using var world=new World();var player=world.Player;
        var formulas=typeof(Intersect.Server.General.Formulas).GetField("_formulas",BindingFlags.Static|BindingFlags.NonPublic)!;
        var oldFormulas=formulas.GetValue(null);
        formulas.SetValue(null,new Intersect.Server.General.Formulas { PhysicalDamage="BaseDamage",MagicDamage="BaseDamage",TrueDamage="BaseDamage" });
        var weapon=new ItemDescriptor(Guid.NewGuid()) { ItemType=ItemType.Equipment,EquipmentSlot=Options.Instance.Equipment.WeaponSlot,Damage=40,Scaling=0,CritChance=0 };
        ItemDescriptor.Lookup.Set(weapon.Id,weapon);
        var category=new RoyalCombatConfiguration().Masteries.Single(c=>c.Key=="swords");category.WeaponIds=[weapon.Id];
        Config.SetValue(null,new RoyalCombatConfiguration { Masteries=[category] });
        var descriptor=new NPCDescriptor(Guid.NewGuid());descriptor.MaxVitals[(int)Vital.Health]=10000;
        var target=new Npc(descriptor) { MapId=world.Map.Id,MapInstanceId=player.MapInstanceId,X=5,Y=4 };
        GC.SuppressFinalize(target);
        var instance=new MapInstance(world.Map,player.MapInstanceId,player);GC.SuppressFinalize(instance);
        var instances=(IDictionary<Guid,MapInstance>)typeof(MapController).GetField("mInstances",Hidden)!.GetValue(world.Map)!;
        instances[player.MapInstanceId]=instance;instance.AddEntity(target);
        player.Items[0].Set(new Item(weapon.Id,1));player.Equipment[Options.Instance.Equipment.WeaponSlot]=0;
        // Use the first active technique; later techniques have separate prerequisites.
        var skill=category.Talents.First(t=>t.Skill.Enabled);
        var request=new ArcheryRequestPacket { Action=ArcheryAction.UseWeaponSkill,CategoryKey=category.Key,TalentKey=skill.Key,TargetId=target.Id };
        void ResetRequest()=>typeof(Player).GetField("_weaponSkillRequestAt",Hidden)!.SetValue(player,0L);
        try
        {
            ResetRequest();Assert.That(player.HandleWeaponSkill(request),Is.False,"Unlearned technique");
            RoyalCombatRuntime.Award(player.Id,category,category.Required(1)+category.Required(2));
            Assert.That(RoyalCombatRuntime.BuyTalent(player.Id,category,skill.Key,Guid.NewGuid()),Is.True);
            typeof(Player).GetField("_masteryRanks",Hidden)!.SetValue(player,null);
            typeof(Player).GetField("_masteryExperience",Hidden)!.SetValue(player,null);
            var spells=player.Spells.Select(s=>s.SpellId).ToArray();
            target.X=12;ResetRequest();Assert.That(player.HandleWeaponSkill(request),Is.False,"Target out of range");
            target.X=5;player.Equipment[Options.Instance.Equipment.WeaponSlot]=-1;
            ResetRequest();Assert.That(player.HandleWeaponSkill(request),Is.False,"Wrong weapon");
            player.Equipment[Options.Instance.Equipment.WeaponSlot]=0;
            var before=target.GetVital(Vital.Health);
            ResetRequest();Assert.That(player.HandleWeaponSkill(request),Is.True);
            Assert.That(target.GetVital(Vital.Health),Is.LessThan(before),"The separate skill deals real combat damage");
            Assert.That(player.Spells.Select(s=>s.SpellId),Is.EqualTo(spells),"Weapon techniques never consume spellbook slots");
            player.AttackTimer=0;ResetRequest();
            Assert.That(player.HandleWeaponSkill(request),Is.False,"Cooldown is authoritative even when the attack timer is cleared");
            Assert.That(target.GetVital(Vital.Health),Is.LessThan(before));
        }
        finally { formulas.SetValue(null,oldFormulas);instances.Clear();ItemDescriptor.Lookup.DeleteAt(weapon.Id); }
    }

    [Test]
    public void NativeSkillConfigurationAndNetworkStateRoundTripAndRejectInvalidValues()
    {
        var category=new RoyalCombatConfiguration().Masteries.Single(c=>c.Key=="axes");
        var skill=category.Talents.First(t=>t.Skill.Enabled).Skill;
        skill.AnimationId=Guid.NewGuid();skill.HitAnimationId=Guid.NewGuid();
        var copied=JsonSerializer.Deserialize<MasteryCategory>(JsonSerializer.Serialize(category))!;
        Assert.That(copied.Talents.First(t=>t.Skill.Enabled).Skill.AnimationId,Is.EqualTo(skill.AnimationId));
        Assert.That(category.Talents.Where(t=>t.Skill.Enabled).All(t=>t.RankSpellIds.Length==0),Is.True);
        skill.CooldownMilliseconds=0;Assert.That(MasteryRules.ValidTree(category),Is.False);skill.CooldownMilliseconds=3000;
        skill.Range=100;Assert.That(MasteryRules.ValidTree(category),Is.False);skill.Range=1;
        var state=new Intersect.Network.Packets.Server.ArcheryStatePacket { WeaponSkillCooldownsJson="{\"axes/power\":3000}" };
        var packet=MessagePack.MessagePackSerializer.Deserialize<Intersect.Network.Packets.Server.ArcheryStatePacket>(MessagePack.MessagePackSerializer.Serialize(state));
        Assert.That(packet.WeaponSkillCooldownsJson,Is.EqualTo(state.WeaponSkillCooldownsJson));
    }
}
