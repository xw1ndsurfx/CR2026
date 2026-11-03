using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Server;

internal static class FishingEditorTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static IEnumerable<Control> All(Control root)
    { yield return root; foreach (Control child in root.Controls) foreach (var control in All(child)) yield return control; }
    private static void Click(Control root, string source)
    {
        var button = All(root).OfType<Button>().Single(b => b.Text == FishingEditorText.Get(source));
        typeof(Button).GetMethod("OnClick", Hidden)!.Invoke(button, [EventArgs.Empty]);
    }
    private static T Input<T>(FrmFishing form, string source) where T : Control
    {
        var page = Field<TabControl>(form, "_tabs").SelectedTab!;
        var table = page.Controls.OfType<TableLayoutPanel>().Single();
        var label = table.Controls.OfType<Label>().Single(l => l.Text == FishingEditorText.Get(source));
        return (T)table.GetControlFromPosition(1, table.GetPositionFromControl(label).Row)!;
    }
    private static void Tab(FrmFishing form, int index) { Field<TabControl>(form, "_tabs").SelectedIndex = index; Call(form, "BuildSelectedPage"); }
    private static void Select(ComboBox picker, string caption) => picker.SelectedItem = picker.Items.Cast<object>().Single(i => i.ToString() == caption);

    internal static void Run()
    {
        var oldCulture = CultureInfo.CurrentUICulture; var oldMap = Globals.CurrentMap;
        var oldState = FishingEditorState.Configuration.ToJson();
        var rod = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing fixture rod", ItemType = ItemType.Equipment };
        var net = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing fixture net", ItemType = ItemType.Equipment };
        var replacement = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing fixture replacement", ItemType = ItemType.Equipment };
        var fish = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing fixture fish", Stackable = true, MaxInventoryStack = 9999 };
        var items = new[] { rod, net, replacement, fish };
        foreach (var item in items) ItemDescriptor.Lookup.Add(item);
        try
        {
            Globals.CurrentMap = null;
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = new(language);
                var source = new FishingConfiguration
                {
                    Tools = [new() { ItemId = rod.Id }, new() { ItemId = net.Id, Type = FishingToolType.Net }],
                    Zones = [new() { Name = "Fixture lagoon", Enabled = false,
                        Catches = [new() { ItemId = fish.Id, ToolItemIds = [rod.Id] }] }],
                };
                using var form = new FrmFishing(source.ToJson());
                var draft = Field<FishingConfiguration>(form, "_config");
                Check(form.Text == FishingEditorText.Get("Fishing Editor") && Field<TabControl>(form, "_tabs").TabCount == 7,
                    "All seven fishing tabs open in every supported language.");
                Input<TextBox>(form, "Name").Text = "Authored lagoon";
                Input<NumericUpDown>(form, "Legendary chance (%)").Value = 7.25m;
                Check(source.Zones[0].Name == "Fixture lagoon", "Editing a draft cannot mutate the server snapshot.");

                Tab(form, 1);
                var equipment = Input<ComboBox>(form, "Equipment item");
                Check(!equipment.Items.Cast<object>().Any(i => i.ToString() == net.Name), "An item used by another profile cannot be selected twice.");
                Select(equipment, replacement.Name);
                Check(draft.Zones[0].Catches[0].ToolItemIds.SequenceEqual([replacement.Id]), "Changing tool identity migrates the existing catch references.");
                Click(Field<TabControl>(form, "_tabs").SelectedTab!, "Delete tool");
                Check(draft.Tools.Count == 2 && Field<Label>(form, "_status").Text == FishingEditorText.Get("This tool is used by catches. Remove those associations first."),
                    "Deleting a referenced tool is blocked with an actionable diagnosis.");

                Tab(form, 2);
                Input<NumericUpDown>(form, "Required variable value").Value = long.MaxValue - 1m;
                Input<NumericUpDown>(form, "Start hour (UTC)").Value = 22;
                Input<NumericUpDown>(form, "End hour (UTC, exclusive)").Value = 4;
                Input<NumericUpDown>(form, "Sprite X (pixels)").Value = 64;
                Tab(form, 4); Click(Field<TabControl>(form, "_tabs").SelectedTab!, "Use supplied Fishing Net profile");
                Input<NumericUpDown>(form, "Frames per second").Value = 9;
                Input<NumericUpDown>(form, "Offset X").Value = -16;
                Tab(form, 5);
                Select(Input<ComboBox>(form, "Sound event"), "catch");
                Select(Input<ComboBox>(form, "Override"), FishingEditorText.Get("Silent"));
                Check(draft.Sounds.Cues["catch"].File == "", "Explicit silence persists as a cue rather than falling through inheritance.");
                Select(Input<ComboBox>(form, "Override"), FishingEditorText.Get("Inherit"));
                Check(!draft.Sounds.Cues.ContainsKey("catch"), "Returning to inheritance removes only the override.");
                Select(Input<ComboBox>(form, "Sound event"), "challenge_timing_prompt");
                Check(!Input<ComboBox>(form, "Override").Enabled, "Unimplemented challenge types cannot be configured as active hooks.");

                var roundTrip = FishingConfiguration.FromJson(draft.ToJson());
                Check(roundTrip.Zones[0].LegendaryChance == .0725 && roundTrip.Zones[0].Catches[0].RequiredVariableValue == long.MaxValue - 1 &&
                    roundTrip.Zones[0].Catches[0].UtcStartHour == 22 && roundTrip.Zones[0].Catches[0].UtcEndHour == 4 &&
                    roundTrip.Zones[0].Catches[0].SpriteX == 64 && roundTrip.Tools[0].WorldProfile.FramesPerSecond == 9 &&
                    roundTrip.Tools[0].WorldProfile.OffsetX == -16, "Exact variable values, overnight gates, sprites and world anchors survive the actual save format.");

                Tab(form, 0); Call(form, "DuplicateZone");
                Check(draft.Zones.Select(z => z.Id).Distinct().Count() == 2 && draft.Zones.SelectMany(z => z.Catches).Select(c => c.Id).Distinct().Count() == 2,
                    "Duplicated zones and their catches receive independent identities.");
                draft.Zones[1].Catches[0].Weight = 3;
                Check(draft.Zones[0].Catches[0].Weight == 1, "Duplicated catches do not share mutable state.");
                fish.Stackable = false;
                Check(!(bool)Call(form, "ValidateDraft")! && Field<Label>(form, "_status").Text.Contains(fish.Name), "A non-stackable reward is rejected before network save.");
                fish.Stackable = true;
                Call(form, "Save"); var pending = Field<string>(form, "_pendingSaveJson");
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                typeof(Form).GetMethod("OnFormClosing", Hidden)!.Invoke(form, [closing]);
                Check(closing.Cancel, "Closing cannot claim to discard a save already in flight.");
                form.Response(new() { Editor = true, ConfigurationJson = pending });
                Check(!Field<Button>(form, "_save").Enabled, "An Event-picker refresh cannot acknowledge a pending save.");
                Input<TextBox>(form, "Name").Text = "Edited after send";
                form.Response(new() { Editor = true, Saved = true, ConfigurationJson = pending });
                Check(Field<bool>(form, "_dirty"), "A saved older snapshot cannot mark newer edits as saved.");
                Call(form, "Save"); pending = Field<string>(form, "_pendingSaveJson");
                form.Response(new() { Editor = true, Saved = true, ConfigurationJson = pending });
                Check(!Field<bool>(form, "_dirty") && Field<Button>(form, "_save").Enabled, "The matching acknowledgement completes the current save.");
                FishingEditorState.Apply(pending);
                Call(form, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape);
                Check(form.IsDisposed, "Escape closes a clean fishing editor through its normal disposal path.");
            }
            var cached = FishingEditorState.Configuration.ToJson();
            try { FishingEditorState.Apply("{\"Zones\":null}"); throw new Exception("Invalid configuration was accepted."); }
            catch (InvalidDataException) { }
            Check(FishingEditorState.Configuration.ToJson() == cached, "A malformed reply leaves the last valid Event-zone cache intact.");
            CheckEntryPoints();
            Console.WriteLine("PASS fishing editor: translated controls, real resource references, exact draft persistence, independent duplicates, save acknowledgements, Event picker and menu/hub dispatch.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture; Globals.CurrentMap = oldMap;
            FishingEditorState.Apply(oldState);
            foreach (var item in items) ItemDescriptor.Lookup.Delete(item);
        }
    }

    private static void CheckEntryPoints()
    {
        using var main = new FrmMain();
        Call(main, "AddRewardConfigurationEditorMenu");
        Call(main, "AddFishingEditorMenu"); Call(main, "AddFishingEditorMenu");
        var menu = Field<MenuStrip>(main, "menuStrip").Items.OfType<ToolStripMenuItem>().Single(m => m.Name == "miniGameEditorsToolStripMenuItem");
        Check(menu.DropDownItems.Cast<ToolStripItem>().Count(i => i.Name == "miniGame.Fishing") == 1, "Fishing menu initialization cannot duplicate its command.");
        using var registry = new HubRegistry([menu], _ => { });
        Check(registry.Find("miniGame.Fishing") is { } entry && entry.Definition.Title.Count == 3 && registry.Open("miniGame.Fishing") && Field<bool>(main, "_fishingEditorRequested"),
            "The localized hub card dispatches the real fishing menu request.");

        using var editor = new FrmEvent(new MapDescriptor(Guid.NewGuid()));
        var nodes = Field<TreeView>(editor, "lstCommands").Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).ToArray();
        Check(nodes.Count(n => n.Tag is int tag && tag == (int)EventCommandType.OpenFishing) == 1, "Open Fishing is available once in the Event command tree.");
        var page = new EventPage(); var command = new OpenFishingCommand();
        page.CommandLists.Values.First().Add(command);
        editor.CurrentPage = page; editor.MyEvent = new EventDescriptor(Guid.NewGuid(), true) { Pages = [page] };
        Call(editor, "OpenEditCommand", command);
        using var picker = All(editor).OfType<EventCommandFishing>().Single();
        var zones = All(picker).OfType<ComboBox>().Single();
        zones.SelectedIndex = 1; Click(picker, "Save");
        Check(command.ZoneId == FishingEditorState.Configuration.Zones.OrderBy(z => z.Name).First().Id,
            "The actual Event dialog saves the selected existing zone identity.");
    }
}
