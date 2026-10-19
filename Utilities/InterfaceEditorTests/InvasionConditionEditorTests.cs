using System.Reflection;
using Intersect.Editor.Forms.Editors.Events.Event_Commands;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;

internal static class InvasionConditionEditorTests
{
    internal static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var condition = new InvasionCondition { InvasionId = Guid.NewGuid(), Mode = InvasionConditionMode.WavesClearedAtLeast, Amount = 7 };
        using var branch = new EventCommandConditionalBranch(condition,new EventPage(),null!,null!);
        branch.GetType().GetMethod("SaveFormValues",flags)!.Invoke(branch,[condition]);
        if (condition.Mode != InvasionConditionMode.WavesClearedAtLeast || condition.Amount != 7 || condition.InvasionId == Guid.Empty)
            throw new Exception("Invasion condition editor must preserve missing saved IDs and thresholds");
        var text = Intersect.Editor.Localization.Strings.GetEventConditionalDesc(condition);
        if(!text.Contains("WavesClearedAtLeast")) throw new Exception("Condition description must use the specific overload");
        foreach(var objective in new[] { QuestObjective.WinInvasions, QuestObjective.ParticipateInInvasions, QuestObjective.ClearInvasionWaves, QuestObjective.LoseInvasions })
        {
            var task = new QuestTaskDescriptor(Guid.NewGuid()) { Objective = objective, Quantity = 4, TargetId = condition.InvasionId, TargetName = "Configured invasion", EditingEvent = new EventDescriptor(Guid.NewGuid(),true) };
            using var editor = new Intersect.Editor.Forms.Editors.Quest.QuestTaskEditor(new QuestDescriptor(Guid.NewGuid()),task);
            var picker = (ComboBox)editor.GetType().GetField("cmbItem",flags)!.GetValue(editor)!;
            if(picker.SelectedIndex < 1 || picker.SelectedItem?.ToString() != "Configured invasion") throw new Exception("Quest editor must retain a configured invasion not yet loaded");
        }
        var oldConfig = Intersect.Framework.Core.WorldEvents.Invasions.InvasionConfiguration.Instance.ToJson();
        try
        {
            var fixture = new Intersect.Framework.Core.WorldEvents.Invasions.InvasionDefinition { TargetMapId = Guid.NewGuid(), Name = "Test invasion", Waves = [new() { Spawns = [new() { NpcId = Guid.NewGuid(), SpawnMapId = Guid.NewGuid() }] }] };
            Intersect.Framework.Core.WorldEvents.Invasions.InvasionConfiguration.Load(new Intersect.Framework.Core.WorldEvents.Invasions.InvasionConfiguration { Invasions = [fixture] }.ToJson());
            using var editor = new Intersect.Editor.Forms.Editors.FrmAchievementConfiguration();
            var objectivePicker = (ComboBox)editor.GetType().GetField("_objective",flags)!.GetValue(editor)!;
            var targets = (ComboBox)editor.GetType().GetField("_target",flags)!.GetValue(editor)!;
            foreach(var objective in new[] { Intersect.Framework.Core.Achievements.AchievementObjectiveType.InvasionCompletions, Intersect.Framework.Core.Achievements.AchievementObjectiveType.InvasionParticipations, Intersect.Framework.Core.Achievements.AchievementObjectiveType.InvasionWavesCleared, Intersect.Framework.Core.Achievements.AchievementObjectiveType.InvasionDefeats })
            {
                objectivePicker.SelectedItem = objective;
                if(!targets.Items.Cast<object>().Any(item => (Guid)item.GetType().GetProperty("Id")!.GetValue(item)! == fixture.Id)) throw new Exception("Achievement invasion objectives must offer configured invasion targets");
            }
        }
        finally { Intersect.Framework.Core.WorldEvents.Invasions.InvasionConfiguration.Load(oldConfig); }
        System.Console.WriteLine("PASS invasion condition branch editor and all four quest objective editors retain thresholds and unresolved invasion references");
    }
}
