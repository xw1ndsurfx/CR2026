using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.WorldEvents.Invasions;

internal static class InvasionEditorTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var previous = InvasionConfiguration.Instance.ToJson();
        var map = Guid.NewGuid();
        var invasion = new InvasionDefinition
        {
            TargetMapId = map,
            AdditionalTargets = [new() { Name = "Royal gate", MapId = map, EventId = Guid.NewGuid(), X = 7, Y = 8, Health = 900 }],
            Reminder60Message = string.Join("\n\n", Enumerable.Repeat("**Hear me, brave adventurers of Corps Royaux!**", 40)),
            Waves = [new() { Spawns = [new() { NpcId = Guid.NewGuid(), SpawnMapId = map }] }]
        };
        var reminders = new[] { 60, 30, 15, 5 };
        var events = reminders.Select(minutes => new EventDescriptor(Guid.NewGuid(), true) { Name = "Alert " + minutes, Pages = [new()] }).ToArray();
        var mapEvent = new EventDescriptor(Guid.NewGuid(), false) { Name = "Map only" };
        foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup[evt.Id] = evt;
        for (var i = 0; i < reminders.Length; i++)
            typeof(InvasionDefinition).GetProperty($"Reminder{reminders[i]}CommonEventId")!.SetValue(invasion, events[i].Id);
        try
        {
            InvasionConfiguration.Load(new InvasionConfiguration { Invasions = [invasion] }.ToJson());
            using var editor = new FrmInvasionConfiguration();
            object Field(string name) => editor.GetType().GetField(name, flags)!.GetValue(editor)!;
            var message = (TextBox)Field("_reminder60Message");
            Check(message.Multiline && message.ScrollBars == ScrollBars.Vertical && message.Text == invasion.Reminder60Message && message.MaxLength == 16384,
                "Editor retains long multiline speeches and exposes scrolling");
            Check(((ListBox)Field("_additionalTargets")).Items.Count == 1 && ((NumericUpDown)Field("_environmentLead")).Value == 0,
                "Editor loads additional targets and first-warning ambience default");
            for (var i = 0; i < reminders.Length; i++)
            {
                var picker = (ComboBox)Field($"_reminder{reminders[i]}CommonEvent");
                Guid Id(object choice) => (Guid)choice.GetType().GetProperty("Id")!.GetValue(choice)!;
                Check(picker.DropDownStyle == ComboBoxStyle.DropDownList && Id(picker.SelectedItem!) == events[i].Id &&
                    picker.Items.Cast<object>().All(choice => Id(choice) != mapEvent.Id), "Each alert loads its own Common Event and excludes map events");
            }
            ((ComboBox)Field("_reminder5CommonEvent")).SelectedIndex = 0;
            ((CheckBox)Field("_loseAnyTarget")).Checked = true;
            editor.GetType().GetMethod("PersistCurrent", flags)!.Invoke(editor, null);
            var stored = ((List<InvasionDefinition>)Field("_invasions"))[0];
            Check(stored.DefeatOnAnyTargetDestroyed && stored.AdditionalTargets[0].EventId == invasion.AdditionalTargets[0].EventId && stored.Reminder60Message == invasion.Reminder60Message,
                "Editing defeat rules preserves configured targets and complete speeches");
            Check(stored.Reminder60CommonEventId == events[0].Id && stored.Reminder30CommonEventId == events[1].Id &&
                stored.Reminder15CommonEventId == events[2].Id && stored.Reminder5CommonEventId == Guid.Empty,
                "Saving preserves independent selections and allows removing an alert event");
            var json = new InvasionConfiguration { Invasions = [stored] }.ToJson();
            Check(InvasionConfiguration.FromJson(json).Invasions[0].AdditionalTargets[0].Health == 900,
                "Configuration round trip retains individual target HP");
            var dialogType = typeof(FrmInvasionConfiguration).Assembly.GetType("Intersect.Editor.Forms.Editors.InvasionTargetDialog", true)!;
            using var dialog = (Form)Activator.CreateInstance(dialogType, flags, null, [stored.AdditionalTargets[0], map], null)!;
            dialogType.GetMethod("Save", flags)!.Invoke(dialog, null);
            var target = (InvasionTargetDefinition)dialogType.GetProperty("Result")!.GetValue(dialog)!;
            Check(target.Id == stored.AdditionalTargets[0].Id && target.EventId == stored.AdditionalTargets[0].EventId && target.X == 7 && target.Y == 8,
                "Objective editor preserves event IDs and coordinates for maps not currently loaded");
        }
        finally { InvasionConfiguration.Load(previous); foreach (var evt in events.Append(mapEvent)) EventDescriptor.Lookup.DeleteAt(evt.Id); }
        System.Console.WriteLine("PASS invasion editor multiline alerts, first-warning ambience, multiple targets, persistence, unloaded map event IDs and independent alert Common Event selectors");
    }
}
