using Intersect.Client.WorldEvents;
using Intersect.Framework.Core.WorldEvents;
using NUnit.Framework;
namespace Intersect.Tests.Client;
[TestFixture,NonParallelizable]
public sealed class NavalMusicTests
{
    [TestCase("../music.ogg")]
    [TestCase(@"..\music.ogg")]
    [TestCase("C:music.ogg")]
    [TestCase(null)]
    public void NavalMusicRejectsPathsAndMalformedConfiguration(string? filename)
    {Assert.That(new WorldActivitiesConfiguration {NavalCombatMusic=filename!}.IsValid,Is.False);}

    [Test]
    public void NavalMusicOverridesOnlyNavalCombatAndOtherSectionSavesKeepIt()
    {
        var previous=WorldMusic.Configuration;
        try
        {
            var map=Guid.NewGuid();WorldMusic.Configuration=new(){CombatMusic="land.ogg",NavalCombatMusic="naval.ogg"};
            Assert.That(WorldMusic.TracksFor(map,true),Is.EqualTo(new[]{"naval.ogg"}));
            Assert.That(WorldMusic.TracksFor(map,false),Is.EqualTo(new[]{"land.ogg"}));
            Assert.That(WorldMusic.Select("ambient.ogg","invasion.ogg","naval.ogg",true),Is.EqualTo("invasion.ogg"));
            Assert.That(WorldMusic.Select("ambient.ogg","","naval.ogg",false),Is.EqualTo("ambient.ogg"));
            WorldMusic.Configuration.ApplySection(new(){NavalCombatMusic="lost.ogg"},WorldActivitiesSection.Bank);
            Assert.That(WorldMusic.Configuration.NavalCombatMusic,Is.EqualTo("naval.ogg"));
            WorldMusic.Configuration.ApplySection(new(){NavalCombatMusic="new.ogg"},WorldActivitiesSection.CombatMusic);
            Assert.That(WorldMusic.TracksFor(map,true),Is.EqualTo(new[]{"new.ogg"}));
            WorldMusic.Configuration.NavalCombatMusic="";WorldMusic.Configuration.CombatMusic="fallback.ogg";
            Assert.That(WorldMusic.TracksFor(map,true),Is.EqualTo(new[]{"fallback.ogg"}));
        }
        finally {WorldMusic.Configuration=previous;}
    }
}
