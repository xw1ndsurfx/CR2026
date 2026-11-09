using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Network.Packets.Client;
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
        var page = Field<TabControl>(form, "_tabs").SelectedTab
            ?? throw new InvalidOperationException("Show the fishing editor before accessing its selected tab.");
        var layouts = page.Controls.OfType<TableLayoutPanel>().ToArray();
        Check(layouts.Length == 1 && layouts[0].Controls.Count > 0,
            "The selected fishing page must own its populated editor layout after activation: " + page.Text);
        var table = layouts[0];
        var label = table.Controls.OfType<Label>().Single(l => l.Text == FishingEditorText.Get(source));
        return (T)table.GetControlFromPosition(1, table.GetPositionFromControl(label).Row)!;
    }
    private static void Tab(FrmFishing form, int index)
    {
        var tabs = Field<TabControl>(form, "_tabs");
        tabs.SelectedIndex = index;
        Application.DoEvents();
        Check(tabs.SelectedTab == tabs.TabPages[index], "Selecting a fishing tab must activate the requested native page.");
    }
    private static void Select(ComboBox picker, string caption) => picker.SelectedItem = picker.Items.Cast<object>().Single(i => i.ToString() == caption);
    private static FrmFishing Open(string json, Form? owner = null)
    {
        var form = new FrmFishing(json) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        if (owner is null) form.Show();
        else EditorWindows.Show(form, owner);
        Application.DoEvents();
        return form;
    }

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
        var north = new MapListMap { MapId = Guid.NewGuid(), Name = "Fishing fixture north" };
        var south = new MapListMap { MapId = Guid.NewGuid(), Name = "Fishing fixture south" };
        var current = new MapListMap { MapId = Guid.NewGuid(), Name = "Fishing fixture current" };
        var mapChoices = new[] { north, south, current };
        MapList.OrderedMaps.AddRange(mapChoices);
        try
        {
            Globals.CurrentMap = null;
            CultureInfo.CurrentUICulture = new("de");
            Check(FishingEditorText.Get("Save draft") == "Save draft" && FishingEditorText.Get("Apply to game") == "Apply to game",
                "Unsupported editor locales fall back to the English draft and activation labels.");
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = new(language);
                var source = new FishingConfiguration
                {
                    Tools = [new() { ItemId = rod.Id }, new() { ItemId = net.Id, Type = FishingToolType.Net }],
                    Zones = [new() { Name = "Fixture lagoon", Enabled = false, MapIds = [north.MapId, south.MapId],
                        Catches = [new() { ItemId = fish.Id, ToolItemIds = [rod.Id] }] }],
                };
                CheckDraftWorkflow(source, replacement, fish);
                CheckActivationFeedback(source);
                // TabControl selects its initial page during native handle creation.
                // Exercise the same Show lifecycle as EditorWindows.Show before reading controls.
                using var form = Open(source.ToJson());
                var tabs = Field<TabControl>(form, "_tabs");
                Check(form.IsHandleCreated && tabs.IsHandleCreated && tabs.SelectedTab == tabs.TabPages[0],
                    "Showing the fishing editor must create its native controls and select the Zones page.");
                var draft = Field<FishingConfiguration>(form, "_config");
                Check(form.Text == FishingEditorText.Get("Fishing Editor") && Field<TabControl>(form, "_tabs").TabCount == 7,
                    "All seven fishing tabs open in every supported language.");
                Input<TextBox>(form, "Name").Text = "Authored lagoon";
                Input<NumericUpDown>(form, "Legendary chance (%)").Value = 7.25m;
                Input<NumericUpDown>(form, "Automatic failure chance (%)").Value = 65.25m;
                Input<NumericUpDown>(form, "Automatic XP (% of active)").Value = 15m;
                Check(Math.Abs(draft.Zones[0].AutomaticFailureChance - .6525) < 0.00001 &&
                    Math.Abs(draft.Zones[0].AutomaticExperienceMultiplier - .15) < 0.00001,
                    "Localized fishing-zone controls persist independent automatic failure and XP settings.");
                Check(source.Zones[0].Name == "Fixture lagoon", "Editing a draft cannot mutate the server snapshot.");
                var mapSearch = (TextBox)form.Controls.Find("FishingMapSearch", true).Single();
                var maps = (CheckedListBox)form.Controls.Find("FishingMapChoices", true).Single();
                Check(!draft.Zones[0].UseTileRadius && !All(tabs.SelectedTab!).OfType<Label>().Any(label => label.Text == FishingEditorText.Get("X (tiles)")),
                    "New multimap zones cover entire maps without mandatory tile coordinates.");
                mapSearch.Text = north.Name;
                Check(maps.Items.Count == 1 && maps.GetItemChecked(0), "The map filter displays the actual checked resource.");
                maps.SetItemChecked(0, false);
                Check(draft.Zones[0].MapIds.SequenceEqual([south.MapId]), "Unchecking a filtered map preserves selected maps hidden by the filter.");
                Globals.CurrentMap = new MapInstance(current.MapId) { Name = current.Name };
                Click(tabs.SelectedTab!, "Add current map"); Click(tabs.SelectedTab!, "Add current map");
                Check(draft.Zones[0].MapIds.Count == 2 && draft.Zones[0].MapIds.Contains(south.MapId) && draft.Zones[0].MapIds.Contains(current.MapId),
                    "Adding the current map extends the selection once instead of replacing it.");
                mapSearch.Text = south.Name; maps.SelectedIndex = 0; Click(tabs.SelectedTab!, "Remove selected map");
                Check(draft.Zones[0].MapIds.SequenceEqual([current.MapId]), "Removing a visible map leaves hidden selected maps untouched.");
                mapSearch.Text = "";
                Input<CheckBox>(form, "Limit each map to a tile radius").Checked = true;
                Input<NumericUpDown>(form, "X (tiles)").Value = 9;
                Input<CheckBox>(form, "Limit each map to a tile radius").Checked = false;
                Check(draft.Zones[0].X == 9 && !draft.Zones[0].UseTileRadius, "Switching to whole maps preserves the explicit legacy radius values without applying them.");
                Globals.CurrentMap = null;

                Tab(form, 1);
                var equipment = Input<ComboBox>(form, "Equipment item");
                Check(!equipment.Items.Cast<object>().Any(i => i.ToString() == net.Name), "An item used by another profile cannot be selected twice.");
                Select(equipment, replacement.Name);
                Check(draft.Zones[0].Catches[0].ToolItemIds.SequenceEqual([replacement.Id]), "Changing tool identity migrates the existing catch references.");
                Click(Field<TabControl>(form, "_tabs").SelectedTab!, "Delete tool");
                Check(draft.Tools.Count == 2 && Field<Label>(form, "_status").Text == FishingEditorText.Get("This tool is used by catches. Remove those associations first."),
                    "Deleting a referenced tool is blocked with an actionable diagnosis.");
                Click(tabs.SelectedTab!, "New tool"); var emptyTool = draft.Tools.Last();
                Tab(form, 0);
                Check(!(bool)Call(form, "ValidateDraft")! && tabs.SelectedIndex == 1 && ReferenceEquals(Field<FishingToolDefinition>(form, "_tool"), emptyTool),
                    "Validation opens the incomplete tool profile instead of leaving a generic error on another tab.");
                Check(draft.Zones[0].Name == "Authored lagoon" && draft.Zones[0].MapIds.SequenceEqual([current.MapId]) && draft.Tools.Contains(emptyTool),
                    "Navigating to an incomplete profile preserves both that profile and the user's zone changes.");
                Click(tabs.SelectedTab!, "Delete tool");

                Tab(form, 2);
                Input<NumericUpDown>(form, "Required variable value").Value = long.MaxValue - 1m;
                Input<NumericUpDown>(form, "Start hour (UTC)").Value = 22;
                Input<NumericUpDown>(form, "End hour (UTC, exclusive)").Value = 4;
                Check(All(form).OfType<Label>().Any(control => control.Text == FishingEditorText.Get("The fish uses the selected item icon, enlarged and animated in the mini-game.")),
                    "Fish appearance comes from the selected item.");
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
                    roundTrip.Tools[0].WorldProfile.FramesPerSecond == 9 &&
                    roundTrip.Tools[0].WorldProfile.OffsetX == -16 && roundTrip.Zones[0].MapIds.SequenceEqual([current.MapId]) && !roundTrip.Zones[0].UseTileRadius,
                    "Exact variable values, overnight gates, item references, world anchors and multimap area settings survive the actual save format.");

                Tab(form, 0); Call(form, "DuplicateZone");
                Check(draft.Zones.Select(z => z.Id).Distinct().Count() == 2 && draft.Zones.SelectMany(z => z.Catches).Select(c => c.Id).Distinct().Count() == 2,
                    "Duplicated zones and their catches receive independent identities.");
                draft.Zones[1].Catches[0].Weight = 3;
                Check(draft.Zones[0].Catches[0].Weight == 1, "Duplicated catches do not share mutable state.");
                fish.Stackable = false;
                Check(!(bool)Call(form, "ValidateDraft")! && Field<Label>(form, "_status").Text.Contains(fish.Name), "A non-stackable reward is rejected before network save.");
                fish.Stackable = true;
                Tab(form, 0);
                Call(form, "Save"); var pending = Field<string>(form, "_pendingSaveJson");
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                typeof(Form).GetMethod("OnFormClosing", Hidden)!.Invoke(form, [closing]);
                Check(closing.Cancel, "Closing cannot claim to discard a save already in flight.");
                form.Response(new() { Editor = true, ConfigurationJson = pending });
                Check(!Field<Button>(form, "_save").Enabled && !Field<Button>(form, "_apply").Enabled,
                    "An Event-picker refresh cannot acknowledge a pending save or unlock either save action.");
                Input<TextBox>(form, "Name").Text = "Edited after send";
                form.Response(new() { Editor = true, Saved = true, ConfigurationJson = pending });
                Check(Field<bool>(form, "_dirty"), "A saved older snapshot cannot mark newer edits as saved.");
                Call(form, "Save"); pending = Field<string>(form, "_pendingSaveJson");
                form.Response(new() { Editor = true, Saved = true, ConfigurationJson = pending });
                Check(!Field<bool>(form, "_dirty") && Field<Button>(form, "_save").Enabled && Field<Button>(form, "_apply").Enabled,
                    "The matching acknowledgement completes the current save and unlocks both actions.");
                FishingEditorState.Apply(pending);
                Call(form, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape);
                Check(form.IsDisposed, "Escape closes a clean fishing editor through its normal disposal path.");
            }
            var cached = FishingEditorState.Configuration.ToJson();
            try { FishingEditorState.Apply("{\"Zones\":null}"); throw new Exception("Invalid configuration was accepted."); }
            catch (InvalidDataException) { }
            Check(FishingEditorState.Configuration.ToJson() == cached, "A malformed reply leaves the last valid Event-zone cache intact.");
            CheckEntryPoints();
            Console.WriteLine("PASS fishing editor: translated controls, filtered multimap selection, incomplete draft resume, strict activation, live Event activation, persistent refusal feedback, failed refresh recovery, active cache isolation, exact persistence, save acknowledgements and menu/hub dispatch.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture; Globals.CurrentMap = oldMap;
            FishingEditorState.Apply(oldState);
            foreach (var item in items) ItemDescriptor.Lookup.Delete(item);
            foreach (var map in mapChoices) MapList.OrderedMaps.Remove(map);
        }
    }

    private static void CheckDraftWorkflow(FishingConfiguration source, ItemDescriptor replacement, ItemDescriptor fish)
    {
        using var main = new FrmMain();
        using var form = Open(source.ToJson());
        var draft = Field<FishingConfiguration>(form, "_config");
        Tab(form, 1); Click(form, "New tool");
        Tab(form, 2); Click(form, "New catch");
        var unfinishedCatchId = draft.Zones[0].Catches.Last().Id;
        Tab(form, 0); Input<TextBox>(form, "Name").Text = "Unfinished lagoon";
        Click(form, "Apply to game");
        Check(Field<string?>(form, "_pendingSaveJson") is null && Field<TabControl>(form, "_tabs").SelectedIndex == 1 &&
            draft.Tools.Count == 3 && draft.Tools.Last().ItemId == Guid.Empty && draft.Zones[0].Catches.Count == 2,
            "Applying an incomplete draft opens its offending tool without publishing or deleting unfinished entries.");

        Tab(form, 0); Click(form, "Save draft");
        var pending = Field<string>(form, "_pendingSaveJson");
        Check(Field<bool>(form, "_pendingSaveIsDraft") && !Field<Button>(form, "_save").Enabled && !Field<Button>(form, "_apply").Enabled &&
            FishingConfiguration.FromJson(pending).Zones[0].Catches.Last().ItemId == Guid.Empty,
            "The draft action sends and preserves incomplete profiles and catches while disabling both save actions.");
        var active = FishingEditorState.Configuration;
        var activeJson = active.ToJson();
        main.ApplyFishingConfiguration(new() { Editor = true, Draft = true, ConfigurationJson = pending });
        Check(ReferenceEquals(active, FishingEditorState.Configuration) && FishingEditorState.Configuration.ToJson() == activeJson,
            "A draft reply cannot replace the active zone cache used by Events.");
        form.Response(new() { Editor = true, Saved = true, ConfigurationJson = pending });
        form.Response(new() { Editor = true, Message = "Ignored active-configuration response" });
        form.Response(new() { Editor = true, Draft = true, ConfigurationJson = pending });
        Check(Field<string>(form, "_pendingSaveJson") == pending && !Field<Button>(form, "_save").Enabled && !Field<Button>(form, "_apply").Enabled,
            "An active acknowledgement, active error or draft refresh cannot acknowledge a draft save.");
        form.Response(new() { Editor = true, Draft = true, Message = "Fixture draft storage failure" });
        Check(Field<string?>(form, "_pendingSaveJson") is null && Field<bool>(form, "_dirty") &&
            Field<Button>(form, "_save").Enabled && Field<Button>(form, "_apply").Enabled && draft.Tools.Count == 3 && draft.Zones[0].Catches.Count == 2,
            "A matching draft error restores both actions and preserves all unsaved entries for retry.");
        Click(form, "Save draft"); pending = Field<string>(form, "_pendingSaveJson");
        Input<TextBox>(form, "Name").Text = "Draft edited after send";
        form.Response(new() { Editor = true, Draft = true, Saved = true, ConfigurationJson = pending });
        Check(Field<bool>(form, "_dirty") && Field<Button>(form, "_save").Enabled && Field<Button>(form, "_apply").Enabled,
            "A draft acknowledgement preserves edits made after sending its snapshot.");
        Call(form, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.S);
        pending = Field<string>(form, "_pendingSaveJson");
        Check(Field<bool>(form, "_pendingSaveIsDraft"), "Ctrl+S saves the draft instead of applying unfinished work.");
        form.Response(new() { Editor = true, Draft = true, Saved = true, ConfigurationJson = pending });
        Check(!Field<bool>(form, "_dirty") && Field<Label>(form, "_status").Text == FishingEditorText.Get("Draft saved. Apply to the game when ready."),
            "The matching draft acknowledgement reports preservation rather than live activation.");
        Call(form, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape);
        Check(form.IsDisposed, "An acknowledged unfinished draft can close normally without discarding its entries.");

        using var reopened = Open(pending);
        var resumed = Field<FishingConfiguration>(reopened, "_config");
        Check(resumed.Tools.Count == 3 && resumed.Tools.Last().ItemId == Guid.Empty && resumed.Zones[0].Name == "Draft edited after send" &&
            resumed.Zones[0].MapIds.SequenceEqual(source.Zones[0].MapIds) && resumed.Zones[0].Catches.Last().Id == unfinishedCatchId &&
            resumed.Zones[0].Catches.Last().ItemId == Guid.Empty && resumed.Zones[0].Catches.Last().ToolItemIds.Count == 0,
            "Reopening a saved draft restores all maps, authored text and unfinished entry identities.");
        Click(reopened, "Apply to game");
        Check(Field<string?>(reopened, "_pendingSaveJson") is null && Field<TabControl>(reopened, "_tabs").SelectedIndex == 1,
            "An incomplete reopened profile still cannot be activated.");
        Select(Input<ComboBox>(reopened, "Equipment item"), replacement.Name);
        Click(reopened, "Apply to game");
        Check(Field<TabControl>(reopened, "_tabs").SelectedIndex == 2 && Field<FishingCatchDefinition>(reopened, "_catch").Id == unfinishedCatchId,
            "After fixing its tool, activation guides the author to the next incomplete catch.");
        Select(Input<ComboBox>(reopened, "Reward item"), fish.Name);
        var allowed = Input<CheckedListBox>(reopened, "Allowed equipment");
        allowed.SetItemChecked(Enumerable.Range(0, allowed.Items.Count).Single(i => allowed.Items[i].ToString() == replacement.Name), true);
        Tab(reopened, 0); Input<CheckBox>(reopened, "Enabled").Checked = true;
        Click(reopened, "Apply to game");
        var applied = Field<string>(reopened, "_pendingSaveJson");
        Check(!Field<bool>(reopened, "_pendingSaveIsDraft") && !Field<Button>(reopened, "_save").Enabled && !Field<Button>(reopened, "_apply").Enabled,
            "A completed draft uses the strict activation action and locks both save buttons.");
        reopened.Response(new() { Editor = true, Draft = true, Saved = true, ConfigurationJson = applied });
        Check(Field<string>(reopened, "_pendingSaveJson") == applied && !Field<Button>(reopened, "_apply").Enabled,
            "A draft acknowledgement cannot complete an active-configuration save.");
        main.ApplyFishingConfiguration(new() { Editor = true, Saved = true, ConfigurationJson = applied });
        reopened.Response(new() { Editor = true, Saved = true, ConfigurationJson = applied });
        Check(!Field<bool>(reopened, "_dirty") && FishingEditorState.Configuration.ToJson() == applied &&
            Field<Label>(reopened, "_status").Text == FishingEditorText.Get("Applied to game."),
            "Only a valid active reply updates the Event cache and reports successful activation.");
    }

    private static void CheckActivationFeedback(FishingConfiguration source)
    {
        FishingEditorState.Apply(source.ToJson());
        using var main = new FrmMain();
        using var form = Open(source.ToJson(), main);
        typeof(FrmMain).GetField("_fishingEditor", Hidden)!.SetValue(main, form);
        using var eventEditor = new FrmEvent(new MapDescriptor(Guid.NewGuid()))
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000),
        };
        var zoneId = source.Zones[0].Id;
        var command = new OpenFishingCommand { ZoneId = zoneId };
        var page = new EventPage(); page.CommandLists.Values.First().Add(command);
        eventEditor.CurrentPage = page;
        eventEditor.MyEvent = new EventDescriptor(Guid.NewGuid(), true) { Pages = [page] };
        eventEditor.Show(); Application.DoEvents();
        Call(eventEditor, "OpenEditCommand", command);
        using var picker = All(eventEditor).OfType<EventCommandFishing>().Single();
        var zones = All(picker).OfType<ComboBox>().Single();
        Guid SelectedId() => (Guid)zones.SelectedItem!.GetType().GetProperty("Id")!.GetValue(zones.SelectedItem)!;
        string Caption() => zones.SelectedItem!.ToString()!;
        var state = (TextBox)form.Controls.Find("FishingActivationState", true).Single();
        var failure = form.Controls.Find("FishingApplyFailure", true).Single();
        var reason = (TextBox)form.Controls.Find("FishingApplyFailureMessage", true).Single();
        var title = form.Controls.Find("FishingApplyFailureTitle", true).Single();
        var draft = Field<FishingConfiguration>(form, "_config");
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.GetConfiguration, ConfigurationJson = source.ToJson() });
        Check(FishingEditorState.HasConfiguration && !FishingEditorState.IsRefreshing && FishingEditorState.LastError is null &&
            SelectedId() == zoneId && Caption().Contains(FishingEditorText.Get("Disabled")),
            "The already-open Event picker starts with the actual disabled server zone and its selected identity.");
        Input<CheckBox>(form, "Enabled").Checked = true;
        Check(draft.Zones[0].Enabled && !FishingEditorState.Configuration.Zones[0].Enabled &&
            state.Text.Contains(string.Format(FishingEditorText.Get("Draft: {0}"), FishingEditorText.Get("Enabled"))) &&
            state.Text.Contains(string.Format(FishingEditorText.Get("Last confirmed on server: {0}"), FishingEditorText.Get("Disabled"))),
            "Checking Enabled changes only the draft; the visible comparison still identifies the disabled server state.");
        Click(form, "Apply to game");
        var applied = Field<string>(form, "_pendingSaveJson");
        Check(FishingConfiguration.FromJson(applied).Zones.Single(z => z.Id == zoneId).Enabled &&
            Caption().Contains(FishingEditorText.Get("Disabled")),
            "The sent application contains Enabled=true without optimistically activating the Event before the server reply.");
        const string concurrentReadFailure = "Fixture earlier active read failed while the application was pending.";
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.GetConfiguration, Message = concurrentReadFailure });
        Check(Field<string>(form, "_pendingSaveJson") == applied && !Field<Button>(form, "_apply").Enabled &&
            !failure.Visible && FishingEditorState.LastError == concurrentReadFailure && state.Text.Contains(concurrentReadFailure),
            "An earlier Get error cannot reject or acknowledge the subsequent pending application, even when both replies are ordered.");
        main.ApplyFishingConfiguration(new() { Editor = true, Saved = true, ConfigurationAction = FishingAction.SaveConfiguration, ConfigurationJson = applied });
        Check(FishingEditorState.Configuration.Zones.Single(z => z.Id == zoneId).Enabled && SelectedId() == zoneId &&
            !Caption().Contains(FishingEditorText.Get("Disabled")) && !failure.Visible &&
            Field<Label>(form, "_status").Text == FishingEditorText.Get("Applied to game.") &&
            state.Text.Contains(string.Format(FishingEditorText.Get("Last confirmed on server: {0}"), FishingEditorText.Get("Enabled"))),
            "The actual Main reply path removes Disabled in an already-open Event and confirms the applied state in the editor.");
        main.ApplyFishingConfiguration(new() { Editor = true, Draft = true, ConfigurationAction = FishingAction.GetDraftConfiguration, ConfigurationJson = source.ToJson() });
        Check(FishingEditorState.Configuration.Zones[0].Enabled && SelectedId() == zoneId &&
            !Caption().Contains(FishingEditorText.Get("Disabled")),
            "A subsequent disabled draft reply cannot undo the confirmed Event activation.");

        const string readFailure = "Fixture active configuration could not be read.";
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.GetConfiguration, Message = readFailure });
        Check(FishingEditorState.HasConfiguration && !FishingEditorState.IsRefreshing && FishingEditorState.LastError == readFailure &&
            FishingEditorState.Configuration.Zones[0].Enabled && SelectedId() == zoneId &&
            Field<Label>(picker, "_status").Text.Contains(readFailure) && state.Text.Contains(readFailure) && !failure.Visible,
            "A failed active refresh is visible in both windows without discarding the last confirmed state or claiming an Apply was refused.");
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.GetConfiguration, ConfigurationJson = "{\"Zones\":null}" });
        Check(FishingEditorState.Configuration.Zones[0].Enabled &&
            FishingEditorState.LastError == FishingEditorText.Get("The server returned an invalid active fishing configuration. The last confirmed state was preserved."),
            "A malformed refresh retains the confirmed active zone and exposes the invalid response diagnostic.");
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.GetConfiguration, ConfigurationJson = source.ToJson() });
        Check(FishingEditorState.LastError is null && !FishingEditorState.Configuration.Zones[0].Enabled,
            "A valid later server response clears the refresh error and supplies the current server state.");

        Click(form, "Apply to game");
        var rejection = "Fixture tool has no valid equipment slot: " + source.Tools[0].ItemId + Environment.NewLine +
            string.Join(Environment.NewLine, Enumerable.Repeat("Fixture detail: choose an equipment slot available on the server before applying this profile.", 14));
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationAction = FishingAction.SaveConfiguration, Message = rejection });
        Check(Field<string?>(form, "_pendingSaveJson") is null && failure.Visible && reason.Text == rejection &&
            title.Text == FishingEditorText.Get("Apply to game refused") && reason.ReadOnly && reason.Multiline && reason.WordWrap && reason.ScrollBars == ScrollBars.Vertical &&
            draft.Zones[0].Enabled && !FishingEditorState.Configuration.Zones[0].Enabled && Caption().Contains(FishingEditorText.Get("Disabled")),
            "A rejected application displays the complete reason while preserving enabled draft and disabled active state.");
        Input<TextBox>(form, "Name").Text = "Changed after rejected application";
        Click(form, "Save draft");
        var savedDraft = Field<string>(form, "_pendingSaveJson");
        main.ApplyFishingConfiguration(new() { Editor = true, Draft = true, ConfigurationAction = FishingAction.GetDraftConfiguration, Message = "Fixture earlier draft read failure." });
        Check(Field<string>(form, "_pendingSaveJson") == savedDraft && !Field<Button>(form, "_save").Enabled,
            "An earlier draft read failure cannot acknowledge the subsequent pending draft save.");
        main.ApplyFishingConfiguration(new() { Editor = true, Draft = true, Saved = true, ConfigurationAction = FishingAction.SaveDraftConfiguration, ConfigurationJson = savedDraft });
        Check(failure.Visible && reason.Text == rejection && !FishingEditorState.Configuration.Zones[0].Enabled &&
            Field<Label>(form, "_status").Text == FishingEditorText.Get("Draft saved. Apply to the game when ready."),
            "Editing and saving a draft never conceal an unresolved application refusal or activate its zone.");
        Application.DoEvents();
        var eventViewport = (Panel)picker.Controls.Find("FishingEventStatusViewport", true).Single();
        var eventActions = picker.Controls.Find("FishingEventActions", true).Single();
        var actionsBounds = picker.RectangleToClient(eventActions.RectangleToScreen(eventActions.ClientRectangle));
        Check(eventViewport.AutoScroll && eventViewport.ClientSize.Height > 0 &&
            eventViewport.DisplayRectangle.Height >= Field<Label>(picker, "_status").Bottom &&
            actionsBounds.Top >= 0 && actionsBounds.Bottom <= picker.ClientSize.Height &&
            Field<Label>(picker, "_status").Text.Contains(rejection),
            "A long Event diagnostic can be scrolled without pushing the refresh and save actions outside the visible control.");
        foreach (var size in new[] { form.MinimumSize, new Size(1180, 790) })
        {
            form.Size = size; Application.DoEvents();
            var feedback = form.Controls.Find("FishingFeedback", true).Single();
            var failureBounds = form.RectangleToClient(failure.RectangleToScreen(failure.ClientRectangle));
            Check(failure.Visible && failureBounds.Top >= 0 && failureBounds.Bottom <= form.ClientSize.Height &&
                failureBounds.Right <= form.ClientSize.Width && reason.Width > 100 && reason.Height >= reason.Font.Height &&
                feedback.Bottom < Field<Label>(form, "_status").Top && !form.Controls.Find("EditorScrollContent", true).Any(),
                "The refusal panel remains visible and readable above the editor body at " + size);
        }

        Click(form, "Apply to game");
        Check(!failure.Visible, "A new application attempt replaces the previous refusal instead of displaying it as a new result.");
        Call(form, "SaveTimedOut");
        Check(failure.Visible && title.Text == FishingEditorText.Get("Application acknowledgement timed out") &&
            Field<string?>(form, "_pendingSaveJson") is null && Field<Button>(form, "_apply").Enabled &&
            !FishingEditorState.Configuration.Zones[0].Enabled,
            "An application timeout reports missing confirmation, leaves the last confirmed state untouched and permits retry.");
        Click(form, "Apply to game");
        applied = Field<string>(form, "_pendingSaveJson");
        main.ApplyFishingConfiguration(new() { Editor = true, Saved = true, ConfigurationAction = FishingAction.SaveConfiguration, ConfigurationJson = applied });
        Check(!failure.Visible && !Field<bool>(form, "_dirty") && FishingEditorState.LastError is null &&
            FishingEditorState.Configuration.Zones[0].Enabled && SelectedId() == zoneId &&
            !Caption().Contains(FishingEditorText.Get("Disabled")),
            "A successful retry clears the refusal and updates the same Event zone without reopening either window.");
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
        main.ApplyFishingConfiguration(new() { Editor = true, ConfigurationJson = FishingEditorState.Configuration.ToJson() });
        Check(Field<FrmFishing?>(main, "_fishingEditor") is null && Field<bool>(main, "_fishingEditorRequested"),
            "An active configuration refresh cannot open the editor while its separate draft response is pending.");

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
