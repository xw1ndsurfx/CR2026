using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Progression;
using Intersect.Network.Packets.WorldEvents;
internal static class ExperienceEventRewardsTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
    private static void Verify()
    {
        var assembly = typeof(Player).Assembly;
        var runtime = assembly.GetType("Intersect.Server.WorldEvents.WorldActivitiesRuntime", true)!;
        var cache = runtime.GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        var old = cache.GetValue(null);
        var professions = assembly.GetType("Intersect.Server.Professions.ProfessionConfigurationRuntime", true)!;
        var professionCache = professions.GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        var oldProfessions = professionCache.GetValue(null);
        var oldOptions = Options.Instance;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            var now = new DateTimeOffset(2026,10,19,23,30,0,TimeSpan.FromHours(-4));
            var evt = new ExperienceEventDefinition { Enabled=true, Days=127, StartHour=23, DurationMinutes=120, BonusPercent=50 };
            var config = new WorldActivitiesConfiguration { ExperienceEvents=[evt] }; cache.SetValue(null,config);
            var snapshot = (ExperienceEventAnnouncementPacket)runtime.GetMethod("Snapshot",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[now,true])!;
            var wire = MessagePack.MessagePackSerializer.Deserialize<ExperienceEventAnnouncementPacket>(MessagePack.MessagePackSerializer.Serialize(snapshot));
            Check(wire.EndsAtUnixMs == snapshot.EndsAtUnixMs && wire.Silent, "Event duration and silent login survive network serialization");
            var reward = new Intersect.Network.Packets.Server.ActivityExperienceGainPacket {Activity="potions",TotalExperience=152,EventBonusPercent=50,EventBonusExperience=51};
            var rewardWire=MessagePack.MessagePackSerializer.Deserialize<Intersect.Network.Packets.Server.ActivityExperienceGainPacket>(MessagePack.MessagePackSerializer.Serialize(reward));
            Check(rewardWire.TotalExperience==152 && rewardWire.EventBonusExperience==51,"Activity XP award packet preserves authoritative totals");
            Check(snapshot.BonusPercent == 50 && snapshot.EndsAtUnixMs - snapshot.ServerTimeUnixMs == 90*60000 && snapshot.Silent, "Login snapshot includes authoritative overnight countdown");
            Check(!evt.IsActive(now.AddMinutes(90)), "Event expires precisely at end boundary");
            // A full-day event guarantees a live bonus independent of the host timezone.
            evt.StartHour=0; evt.DurationMinutes=1440;
            var store = new SqliteMiniGameProgressStore(path,experienceEvents:true);
            var character = Guid.NewGuid(); var receipt=Guid.NewGuid();
            var won=store.AwardWin(character,"poker",receipt,1);
            Check(won.Experience == 38 && won.Wins == 1, "Mini-game win adds rounded event XP");
            Check(store.AwardWin(character,"poker",receipt,1) == won, "Receipt retry cannot grant event XP twice");
            Check(store.AwardExperience(character,"potions",receipt,2,101).Experience == 152, "Potion completion receives event XP");
            evt.BonusPercent=1000;
            Check(store.AwardExperience(character,"potions",receipt,3,5000).Experience == 30000, "Boosted potion XP respects mini-game level cap");
            evt.BonusPercent=50;
            var definition=new ProfessionDefinition {Name="Alchemist",BaseExperience=1000};
            professionCache.SetValue(null,new ProfessionConfiguration {Professions=[definition]});
            var professionRuntime=assembly.GetType("Intersect.Server.Professions.ProfessionRuntime",true)!;
            var player=new Player(); GC.SuppressFinalize(player);
            professionRuntime.GetMethod("AwardActivity",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[player,definition.Id,101L]);
            var xp=(long)professionRuntime.GetMethod("GetExperience",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[player,definition.Id])!;
            Check(xp==152,"Real profession activity applies event bonus exactly once");
            var fighter=new Player {Level=1}; GC.SuppressFinalize(fighter);
            fighter.GiveExperience(10,fromNpcKill:true,npcBaseExperience:10);
            Check(fighter.Exp==15,"NPC experience receives the event bonus exactly once");
            fighter.GiveExperience(10);
            Check(fighter.Exp==30,"Other character XP awards receive the same bonus");
            fighter.Level=Options.Instance.Player.MaxLevel; fighter.GiveExperience(10);
            Check(fighter.Exp==0,"Character level cap does not bank boosted XP");
            evt.Enabled=false;
            Check(store.AwardWin(character,"blackjack",receipt,4).Experience==25,"Inactive events leave base awards unchanged");
        }
        finally
        {
            cache.SetValue(null,old); professionCache.SetValue(null,oldProfessions);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,oldOptions);
            if(File.Exists(path)) File.Delete(path);
        }
        System.Console.WriteLine("PASS XP event countdown, exact expiry, mini-game/potion/profession awards, receipt retries and level cap");
    }
}
