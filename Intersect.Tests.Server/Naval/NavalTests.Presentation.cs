using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Runtime.CompilerServices;
namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void LegacyFleetLoadsWithoutLosingShipsAndNewDataPreservesSailingAndUnknownFields()
    {
        var ship=new OwnedShip {Name="L’Étoile",ModelId="royal",Health=45,ReturnMapId=Guid.NewGuid(),ReturnDockX=4,ReturnDockY=6};
        var data=NavalPlayerData.Parse(JsonConvert.SerializeObject(new[]{ship}));
        Assert.That(data.SailingExperience,Is.Zero);Assert.That(data.Ships[0].Id,Is.EqualTo(ship.Id));Assert.That(data.Ships[0].ReturnMapId,Is.EqualTo(ship.ReturnMapId));
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(player);
        player.NavalFleetJson=JsonConvert.SerializeObject(data);player.SailingExperience=234;
        var raw=Newtonsoft.Json.Linq.JObject.Parse(player.NavalFleetJson);raw["FutureField"]=42;
        player.NavalFleetJson=raw.ToString();
        Assert.That(player.SailingExperience,Is.EqualTo(234));Assert.That(player.NavalFleet[0].Name,Is.EqualTo("L’Étoile"));
        Assert.That((int)Newtonsoft.Json.Linq.JObject.Parse(player.NavalFleetJson)["FutureField"]!,Is.EqualTo(42));
        player.NavalFleetJson=null!;Assert.That(player.NavalFleet,Is.Empty);Assert.That(player.SailingExperience,Is.Zero);
    }
    [TestCase("Le Royal","Le Royal")]
    [TestCase("  L'Etoile   d'Or  ","L'Etoile d'Or")]
    [TestCase("Étoile-2","Étoile-2")]
    [TestCase("<script>",null)]
    [TestCase("Bateau\nPirate",null)]
    [TestCase("",null)]
    [TestCase("Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",null)]
    public void ShipNamesAreSafeAndBounded(string name,string? expected)=>Assert.That(ShipNames.Normalize(name),Is.EqualTo(expected));
    [Test]
    public void FailedOrThrowingRenameRestoresTheStoredName()
    {
        var ship=new OwnedShip {Name="Royal"};
        Assert.That(NavalSystem.RenameOwnedShip(ship,"Étoile",()=>false),Is.False);Assert.That(ship.Name,Is.EqualTo("Royal"));
        Assert.Throws<IOException>(()=>NavalSystem.RenameOwnedShip(ship,"Étoile",()=>throw new IOException()));Assert.That(ship.Name,Is.EqualTo("Royal"));
        Assert.That(NavalSystem.RenameOwnedShip(ship,"Étoile",()=>true),Is.True);Assert.That(ship.Name,Is.EqualTo("Étoile"));
    }
    [Test]
    public void SailingThresholdsAreContinuousBoundedAndConfigurable()
    {
        var settings=new SailingSettings {MaximumLevel=3,BaseExperience=100,ExperienceGrowth=2};
        Assert.That(settings.Progress(99),Is.EqualTo(new SailingProgress(1,99,100)));
        Assert.That(settings.Progress(100),Is.EqualTo(new SailingProgress(2,0,200)));
        Assert.That(settings.Progress(299),Is.EqualTo(new SailingProgress(2,199,200)));
        Assert.That(settings.Progress(300),Is.EqualTo(new SailingProgress(3,0,0)));Assert.That(settings.Cap,Is.EqualTo(300));
        Assert.That(settings.Progress(long.MaxValue).Level,Is.EqualTo(3));
        settings.MaximumLevel=200;settings.BaseExperience=1000000000;settings.ExperienceGrowth=2;
        Assert.That(settings.Cap,Is.InRange(0L,200000000000000L));
    }
    [Test]
    public void AssetsRejectPathsAndSettingsSurviveThePacketSerializer()
    {
        foreach(var unsafeName in new[]{"../test.wav","C:\\test.wav","sub/test.png","a\nb.wav"}) Assert.That(NavalPresentation.SafeAsset(unsafeName),Is.False);
        var packet=new NavalSnapshotPacket {Presentation=new() {PortIcon="broadside.png",CannonFireSound="fire.wav",HullImpactSound="hull.wav"},
            SailingLevel=12,SailingExperience=45,SailingRequired=200,Ships=[new() {Name="Étoile"}],
            Effects=[new(){Id=Guid.NewGuid(),Kind=NavalEffectKind.WaterImpact,AgeMilliseconds=100,PlaySound=false}]};
        var restored=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(restored.Presentation.PortIcon,Is.EqualTo("broadside.png"));Assert.That(restored.SailingLevel,Is.EqualTo(12));
        Assert.That(restored.Effects[0].PlaySound,Is.False);Assert.That(restored.Ships[0].Name,Is.EqualTo("Étoile"));
        var config=new NavalConfiguration();config.Presentation.EffectPixelsPerTile=double.NaN;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
        config.Presentation.EffectPixelsPerTile=128;config.Sailing.ExperiencePerTile=-1;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
    }
    [Test]
    public void ShippedEffectAtlasesHaveExplicitValidAnchorsAndExpectedSequences()
    {
        var root=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);while(root!=null && !File.Exists(Path.Combine(root.FullName,"Intersect.sln"))) root=root.Parent;
        foreach(var file in new[]{"Ship_Canon_Fire_Animation.png","Ship_Canon_Fire_impact.png"})
        {
            var atlas=JsonConvert.DeserializeObject<NavalEffectAtlas>(File.ReadAllText(Path.Combine(root!.FullName,"resources","ocean",file+".effects.json")))!;
            Assert.That(atlas.IsValid(1448,1086),Is.True);Assert.That(atlas.IsValid(1024,1024),Is.False);
            if(file.Contains("impact")) for(var i=0;i<8;i++) Assert.That(atlas.Clips["Projectile"+i],Has.Length.EqualTo(4));
            else Assert.That(atlas.Clips["Fire"],Has.Length.EqualTo(3));
            var frame=atlas.Clips.First().Value[0];frame.AnchorX=double.NaN;Assert.That(atlas.IsValid(1448,1086),Is.False);
        }
    }

    [Test]
    public void SailingAwardsOnlyMeasuredMovementAndKeepsFractionalDistanceUntilAnExperiencePointIsEarned()
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        var type=typeof(NavalSystem);var field=type.GetField("_settings",flags)!;var saved=field.GetValue(null);
        var effects=(System.Collections.IList)type.GetField("Effects",flags)!.GetValue(null)!;var savedEffects=effects.Cast<object>().ToArray();
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(player);player.NavalFleetJson="[]";
        try
        {
            field.SetValue(null,new NavalConfiguration {Sailing=new(){ExperiencePerTile=2,BaseExperience=5,ExperienceGrowth=1,MaximumLevel=3}});
            var vessel=Activator.CreateInstance(type.GetNestedType("Vessel",System.Reflection.BindingFlags.NonPublic)!,
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance,null,
                new object[]{new ShipDefinition {MapId=Map}},null)!;
            var award=type.GetMethod("AwardSailing",flags)!;
            award.Invoke(null,[player,vessel,0d,1000L]);award.Invoke(null,[player,vessel,double.NaN,1000L]);Assert.That(player.SailingExperience,Is.Zero);
            award.Invoke(null,[player,vessel,.25d,1000L]);Assert.That(player.SailingExperience,Is.Zero);
            award.Invoke(null,[player,vessel,.25d,1000L]);Assert.That(player.SailingExperience,Is.EqualTo(1));
            award.Invoke(null,[player,vessel,4d,1000L]);Assert.That(player.SailingExperience,Is.EqualTo(9));
            Assert.That(NavalPlayerData.Parse(player.NavalFleetJson).SailingExperience,Is.EqualTo(9));
            award.Invoke(null,[player,vessel,1000d,1000L]);Assert.That(player.SailingExperience,Is.EqualTo(10));
            player.SailingExperience=50;award.Invoke(null,[player,vessel,1d,1000L]);Assert.That(player.SailingExperience,Is.EqualTo(50));
        }
        finally {field.SetValue(null,saved);effects.Clear();foreach(var effect in savedEffects) effects.Add(effect);}
    }
}
