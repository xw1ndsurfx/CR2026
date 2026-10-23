using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Achievements;

internal static class MonthlyNpcMissionEditorTests
{
    internal static void Run()
    {
        var previous = AchievementConfiguration.Instance.ToJson();
        try
        {
            AchievementConfiguration.Load(new AchievementConfiguration { Achievements = [new()
            {
                Monthly = true, ObjectiveType = AchievementObjectiveType.NpcKills, TargetKey = "old code"
            }] }.ToJson());
            using var editor = new FrmAchievementConfiguration();
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(FrmAchievementConfiguration);
            var key = (TextBox)type.GetField("_targetKey", hidden)!.GetValue(editor)!;
            var objective = (ComboBox)type.GetField("_objective", hidden)!.GetValue(editor)!;
            if (key.Enabled) throw new Exception("NPC kill missions must not offer an irrelevant custom counter key.");
            objective.SelectedItem = AchievementObjectiveType.CustomCounter;
            if (!key.Enabled || key.Text != "old code") throw new Exception("Switching back to custom counters preserves their authored code.");
            objective.SelectedItem = AchievementObjectiveType.NpcKills;
            if (key.Enabled) throw new Exception("Switching to NPC kills disables the custom key.");
            System.Console.WriteLine("PASS monthly NPC mission editor: NPC selection uses IDs, custom key disabled for kills and preserved for custom counters");
        }
        finally { AchievementConfiguration.Load(previous); }
    }
}
