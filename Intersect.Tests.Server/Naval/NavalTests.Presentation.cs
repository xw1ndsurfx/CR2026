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
        var animationId=Guid.NewGuid();
        var packet=new NavalSnapshotPacket {Presentation=new() {LevelUpAnimationId=animationId,PortIcon="broadside.png",CannonFireSound="fire.wav",HullImpactSound="hull.wav"},
            SailingLevel=12,SailingExperience=45,SailingRequired=200,Ships=[new() {Name="Étoile"}],
            Effects=[new(){Id=Guid.NewGuid(),Kind=NavalEffectKind.WaterImpact,AgeMilliseconds=100,PlaySound=false}]};
        var restored=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(restored.Presentation.LevelUpAnimationId,Is.EqualTo(animationId));
        Assert.That(new NavalPresentation().LevelUpAnimationId,Is.EqualTo(Guid.Empty));
        Assert.That(restored.Presentation.PortIcon,Is.EqualTo("broadside.png"));Assert.That(restored.SailingLevel,Is.EqualTo(12));
        Assert.That(restored.Effects[0].PlaySound,Is.False);Assert.That(restored.Ships[0].Name,Is.EqualTo("Étoile"));
        var config=new NavalConfiguration();config.Presentation.EffectPixelsPerTile=double.NaN;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
        config.Presentation.EffectPixelsPerTile=128;config.Sailing.ExperienceGrowth=-1;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void NavalLevelUpUsesConfiguredAnimationAtTheActiveVesselForCaptainAndCrew(bool crew)
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);
        var animationId=Guid.NewGuid();world.Configure(new() {Presentation=new(){LevelUpAnimationId=animationId}});
        var player=crew?GunnerPlayer(world,ship):CargoPlayer();
        if(!crew){player.Id=world.State(ship).CaptainId;player.CaptainedShipId=world.State(ship).Id;}
        var effects=(System.Collections.IList)typeof(NavalSystem).GetField("Effects",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        world.Invoke("EmitLevelUp",player,1,1,1000L);Assert.That(effects.Count,Is.Zero);
        world.Invoke("EmitLevelUp",player,1,3,1000L);Assert.That(effects.Count,Is.EqualTo(1));
        var effect=(NavalEffectSnapshot)effects[0]!.GetType().GetProperty("Snapshot")!.GetValue(effects[0])!;
        Assert.That(effect.AnimationId,Is.EqualTo(animationId));Assert.That(effect.Kind,Is.EqualTo(NavalEffectKind.LevelUp));
        Assert.That((effect.MapId,effect.X,effect.Y),Is.EqualTo((world.Map.Id,8.5,10.5)));
        effects.Clear();world.Set(ship,"Health",0);world.Invoke("EmitLevelUp",player,1,2,1000L);Assert.That(effects.Count,Is.Zero);
        world.Set(ship,"Health",100);player.CaptainedShipId=player.BoardedShipId=Guid.Empty;
        world.Invoke("EmitLevelUp",player,1,2,1000L);Assert.That(effects.Count,Is.Zero,"Delayed rewards on shore do not animate a departed vessel.");
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

}
