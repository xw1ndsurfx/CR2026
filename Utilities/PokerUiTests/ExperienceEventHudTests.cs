using System.Reflection;
using Intersect;
using System.Runtime.CompilerServices;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Core;
using Intersect.Client.Localization;
using Intersect.Client.General;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Network.Packets.Server;
internal static class ExperienceEventHudTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Verify()
    {
        var old=Globals.Database;
        var apply=typeof(ClientLanguages).GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
        var restore=typeof(ClientLanguages).GetMethod("RestoreEnglish",BindingFlags.Static|BindingFlags.NonPublic)!;
        var assembly=typeof(GameInterface).Assembly;
        var hud=assembly.GetType("Intersect.Client.Interface.Game.ExperienceEventHud",true)!;
        var status=hud.GetMethod("FormatStatus",BindingFlags.Static|BindingFlags.NonPublic)!;
        var handler=assembly.GetType("Intersect.Client.Networking.PacketHandler",true)!;
        var lines=handler.GetMethod("BuildNpcExperienceGainLines",BindingFlags.Static|BindingFlags.NonPublic)!;
        try
        {
            foreach(var (code,word) in new[]{("en","Remaining"),("fr","Temps restant"),("es","Tiempo restante")})
            {
                restore.Invoke(null,null); Globals.Database=new InterfaceRefreshTests.MemoryPreferences {LanguageCode=code}; apply.Invoke(null,null);
                var text=(string)status.Invoke(null,[50,90001000L])!;
                if(!text.Contains(word) || !text.Contains("25:00:01") || !text.Contains("50%")) throw new Exception("Localized multi-day XP countdown: "+text);
                var result=(List<(string Text, Intersect.Color Color)>)lines.Invoke(null,[new NpcExperienceGainBreakdown {BaseExperience=101,TotalExperience=152,EventBonusPercent=50,EventBonusExperience=51}])!;
                if(result.Count!=3 || !result[1].Text.Contains("50%") || !result[1].Text.Contains("51")) throw new Exception("NPC XP breakdown must expose event-only bonuses");
            }
            _ = new TestContentManager();
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) {Size=new Point(640,480)};
            var previousContext=ApplicationContext.Context.Value;
            ApplicationContext.Context.Value=DispatchProxy.Create<IApplicationContext,ActivitiesLogProxy>();
            try
            {
                var label=(Label)Activator.CreateInstance(hud,BindingFlags.Instance|BindingFlags.NonPublic,null,[canvas,"TestExperienceHud"],null)!;
                hud.GetMethod("ShowLine",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(label,[(string)status.Invoke(null,[1000,90001000L])!,56]);
                if(label.IsHidden || label.Y<52 || label.Right>canvas.Width || label.Font==null || label.MouseInputEnabled || label.KeyboardInputEnabled)
                    throw new Exception("XP status must remain visible below the house buttons, within a narrow screen, and pass input to gameplay");
            }
            finally {ApplicationContext.Context.Value=previousContext;}
            var state=assembly.GetType("Intersect.Client.WorldEvents.ExperienceEventState",true)!;
            var receive=state.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
            receive.Invoke(null,[new ExperienceEventAnnouncementPacket {BonusPercent=50,ServerTimeUnixMs=99999999,EndsAtUnixMs=100059999}]);
            if((int)state.GetProperty("Bonus",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!=50) throw new Exception("Countdown must use server duration independently of local clock");
            receive.Invoke(null,[new ExperienceEventAnnouncementPacket()]);
            if((int)state.GetProperty("Bonus",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!=0) throw new Exception("Expired event must hide immediately");
        }
        finally { Globals.Database=old; restore.Invoke(null,null); apply.Invoke(null,null); }
        System.Console.WriteLine("PASS XP HUD EN/FR/ES, multi-day countdown, server clock independence, expiry and NPC event breakdown");
    }
}
