using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;

internal static class InvasionObjectiveUiTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Verify()
    {
        var old = Globals.Database;
        var apply = typeof(ClientLanguages).GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish",BindingFlags.Static|BindingFlags.NonPublic)!;
        var format = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.InvasionQuestText",true)!.GetMethod("Format",BindingFlags.Static|BindingFlags.NonPublic)!;
        try
        {
            foreach(var (code,expected) in new[] {
                ("en","2 / 5 invasion victories - All invasions"),
                ("fr","2 / 5 victoires d’invasion - Toutes les invasions"),
                ("es","2 / 5 victorias de invasión - Todas las invasiones") })
            {
                restore.Invoke(null,null); Globals.Database = new InterfaceRefreshTests.MemoryPreferences { LanguageCode = code }; apply.Invoke(null,null);
                var task = new QuestTaskDescriptor(Guid.NewGuid()) { Objective = QuestObjective.WinInvasions, Quantity = 5 };
                var actual = (string)format.Invoke(null,[task,2L])!;
                if(actual != expected) throw new Exception("Invasion quest localization: " + actual);
                task.TargetId = Guid.NewGuid(); task.TargetName = "Azrakel [Polonya]";
                foreach(var objective in new[] { QuestObjective.WinInvasions, QuestObjective.ParticipateInInvasions, QuestObjective.ClearInvasionWaves, QuestObjective.LoseInvasions })
                {
                    task.Objective = objective; actual = (string)format.Invoke(null,[task,2L])!;
                    if(!actual.Contains("2 / 5") || !actual.EndsWith(task.TargetName)) throw new Exception("Invasion quest counts and authored target name must remain intact");
                }
            }
        }
        finally { Globals.Database = old; restore.Invoke(null,null); apply.Invoke(null,null); }
        System.Console.WriteLine("PASS invasion quest progress: English/French/Spanish, all metrics, wildcard target and authored invasion names");
    }
}
