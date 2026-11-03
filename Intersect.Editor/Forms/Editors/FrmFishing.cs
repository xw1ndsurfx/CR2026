using System.Text.Json;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.Professions;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms.Editors;

/// <summary>A detached fishing draft edited with resource selectors and bounded controls.</summary>
public sealed partial class FrmFishing : Form
{
    private FishingConfiguration _config;
    private FishingZoneDefinition? _zone;
    private FishingToolDefinition? _tool;
    private FishingCatchDefinition? _catch;
    private readonly ListBox _zones = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 44, AutoEllipsis = true, Padding = new Padding(8) };
    private readonly Button _save = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _saveTimeout = new() { Interval = 15000 };
    private readonly List<TabPage> _pages = [];
    private bool _loading, _dirty;
    private string? _pendingSaveJson;
    private int _soundScope;
    private string _soundKey = "cast";
    private static string T(string value) => FishingEditorText.Get(value);
    private sealed record Choice(Guid Id, string Caption) { public override string ToString() => Caption; }
    private sealed record Named<TValue>(TValue Value, Func<TValue, string> Caption)
    { public override string ToString() => Caption(Value); }

    public FrmFishing(string json)
    {
        _config = FishingConfiguration.FromJson(json);
        FishingRules.Validate(_config);
        Text = T("Fishing Editor"); Size = new(1180, 790); MinimumSize = new(940, 620);
        StartPosition = FormStartPosition.CenterParent;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        _save.Text = T("Save"); _save.Click += (_, _) => Save(); toolbar.Controls.Add(_save);
        AddButton(toolbar, "Validate", () => { if (ValidateDraft()) _status.Text = T("Configuration is valid."); });
        AddButton(toolbar, "Reload resources", RebuildPages);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1160, 660), SplitterDistance = 260 };
        var zoneButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };
        AddButton(zoneButtons, "New zone", NewZone);
        AddButton(zoneButtons, "Duplicate", DuplicateZone);
        AddButton(zoneButtons, "Delete", DeleteZone);
        _search.PlaceholderText = T("Search zones");
        split.Panel1.Controls.Add(_zones); split.Panel1.Controls.Add(_search); split.Panel1.Controls.Add(zoneButtons);
        split.Panel2.Controls.Add(_tabs);
        foreach (var title in new[] { "Zones", "Tools", "Catches", "Legendary creatures", "Animations", "Sounds", "Skills and progression" })
        {
            var page = new TabPage(T(title)) { AutoScroll = true, Padding = new Padding(8) };
            _pages.Add(page); _tabs.TabPages.Add(page);
        }
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(_status);
        _search.TextChanged += (_, _) => RefreshZoneList();
        _zones.SelectedIndexChanged += (_, _) => {
            if (_loading) return;
            _zone = (_zones.SelectedItem as Named<FishingZoneDefinition>)?.Value;
            _catch = _zone?.Catches.FirstOrDefault(); RebuildPages();
        };
        _tabs.SelectedIndexChanged += (_, _) => { if (!_loading) BuildSelectedPage(); };
        _saveTimeout.Tick += (_, _) => { _saveTimeout.Stop(); _save.Enabled = true; _pendingSaveJson = null; _status.Text = T("No server response. Your draft is still available."); };
        FormClosing += (_, e) => {
            if (_pendingSaveJson is not null)
            { e.Cancel = true; _status.Text = T("A save is pending. Wait for the server response before closing."); return; }
            if (_dirty && MessageBox.Show(this, T("Discard unsaved fishing changes?"), T("Fishing Editor"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true;
        };
        Disposed += (_, _) => { StopPreview(); _saveTimeout.Stop(); _saveTimeout.Dispose(); };
        _tool = _config.Tools.FirstOrDefault(); _zone = _config.Zones.FirstOrDefault();
        _catch = _zone?.Catches.FirstOrDefault();
        RefreshZoneList();
        // Native handle creation may select the first tab without raising SelectedIndexChanged.
        // Select it explicitly after the draft selection exists, before building its controls.
        _tabs.SelectedIndex = 0;
        RebuildPages();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keys)
    {
        if (keys == (Keys.Control | Keys.S))
        {
            if (_save.Enabled) { _save.Focus(); Save(); }
            return true;
        }
        if (keys == Keys.Escape && !HasOpenChoice(this)) { Close(); return true; }
        return base.ProcessCmdKey(ref message, keys);
    }
    private static bool HasOpenChoice(Control root) => root.Controls.Cast<Control>()
        .Any(control => control is ComboBox { DroppedDown: true } || HasOpenChoice(control));

    private void Changed() { if (!_loading) { _dirty = true; _status.Text = T("Unsaved changes"); } }
    private void RefreshZoneList()
    {
        _loading = true; _zones.BeginUpdate();
        _zones.Items.Clear();
        foreach (var zone in _config.Zones.Where(z => z.Name.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase)))
            _zones.Items.Add(new Named<FishingZoneDefinition>(zone, z => z.Name));
        var selected = _zones.Items.OfType<Named<FishingZoneDefinition>>().FirstOrDefault(n => n.Value == _zone);
        _zones.SelectedItem = selected;
        _zones.EndUpdate(); _loading = false;
    }
    private void RebuildPages() { StopPreview(); BuildSelectedPage(); }
    private void BuildSelectedPage()
    {
        if (_tabs.SelectedIndex < 0) return;
        _loading = true;
        try
        {
            switch (_tabs.SelectedIndex)
            {
                case 0: BuildZone(); break;
                case 1: BuildTools(); break;
                case 2: BuildCatches(false); break;
                case 3: BuildCatches(true); break;
                case 4: BuildAnimations(); break;
                case 5: BuildSounds(); break;
                case 6: BuildSkills(); break;
            }
        }
        finally { _loading = false; }
    }
    private TableLayoutPanel Page(int index)
    {
        var page = _pages[index];
        foreach (Control control in page.Controls.Cast<Control>().ToArray()) control.Dispose();
        page.Controls.Clear();
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(table); return table;
    }
    private void NewZone()
    {
        _zone = new() { Name = T("New fishing zone"), MapId = Globals.CurrentMap?.Id ?? Guid.Empty, Enabled = false };
        _zone.LocalizedNames = new() { ["en"] = "New fishing zone", ["fr"] = "Nouvelle zone de pêche", ["es"] = "Nueva zona de pesca" };
        _config.Zones.Add(_zone); _catch = null; _search.Text = ""; RefreshZoneList(); RebuildPages(); Changed();
    }
    private void DuplicateZone()
    {
        if (_zone is null) return;
        _zone = JsonSerializer.Deserialize<FishingZoneDefinition>(JsonSerializer.Serialize(_zone))!;
        _zone.Id = Guid.NewGuid(); _zone.Name = string.Format(T("Copy of {0}"), _zone.Name); _zone.Enabled = false;
        foreach (var entry in _zone.Catches) entry.Id = Guid.NewGuid();
        _config.Zones.Add(_zone); _catch = _zone.Catches.FirstOrDefault();
        RefreshZoneList(); RebuildPages(); Changed();
    }
    private void DeleteZone()
    {
        if (_zone is null || MessageBox.Show(this, T("Delete this zone? Events referencing it must be updated."),
                T("Fishing Editor"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _config.Zones.Remove(_zone); _zone = _config.Zones.FirstOrDefault(); _catch = _zone?.Catches.FirstOrDefault();
        RefreshZoneList(); RebuildPages(); Changed();
    }
    private void BuildZone()
    {
        var table = Page(0);
        if (_zone is not { } zone) { Note(table, "Create or select a fishing zone."); return; }
        TextField(table, "Name", zone.Name, s => { zone.Name = s; _zones.Refresh(); });
        foreach (var language in new[] { "en", "fr", "es" })
        {
            var locale = language;
            TextField(table, "Name (" + locale + ")", zone.LocalizedNames.GetValueOrDefault(locale, ""), s => zone.LocalizedNames[locale] = s);
        }
        Check(table, "Enabled", zone.Enabled, b => zone.Enabled = b);
        GuidField(table, "Map", zone.MapId, MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name)), id => zone.MapId = id);
        Number(table, "X (tiles)", zone.X, 0, 10000, v => zone.X = (int)v);
        Number(table, "Y (tiles)", zone.Y, 0, 10000, v => zone.Y = (int)v);
        Number(table, "Zone radius (tiles)", zone.Radius, 0, 64, v => zone.Radius = (int)v);
        var current = new FlowLayoutPanel { AutoSize = true };
        AddButton(current, "Use current map", () => { if (Globals.CurrentMap is not null) { zone.MapId = Globals.CurrentMap.Id; Changed(); BuildSelectedPage(); } });
        Row(table, "", current);
        FileField(table, "Background", zone.Assets.Background, "backgrounds", s => zone.Assets.Background = s);
        FileField(table, "Foreground", zone.Assets.Foreground, "backgrounds", s => zone.Assets.Foreground = s, true);
        FileField(table, "Decorations", zone.Assets.Decorations, "gui", s => zone.Assets.Decorations = s, true);
        Check(table, "Allow automatic fishing", zone.AllowAutomatic, b => zone.AllowAutomatic = b);
        Check(table, "Legendary encounters during automatic fishing", zone.LegendaryDuringAutomatic, b => zone.LegendaryDuringAutomatic = b);
        Number(table, "Legendary chance (%)", zone.LegendaryChance * 100, 0, 100, v => zone.LegendaryChance = (double)v / 100, 2);
        Number(table, "Minimum wait (ms)", zone.MinimumWaitMs, 1000, 120000, v => zone.MinimumWaitMs = (int)v);
        Number(table, "Maximum wait (ms)", zone.MaximumWaitMs, 1000, 120000, v => zone.MaximumWaitMs = (int)v);
        Number(table, "Automatic catch time (ms)", zone.AutomaticCatchMs, 5000, 600000, v => zone.AutomaticCatchMs = (int)v);
        Number(table, "Active time limit (ms)", zone.ActiveLimitMs, 10000, 180000, v => zone.ActiveLimitMs = (int)v);
        Number(table, "Legendary invitation time (ms)", zone.LegendaryPromptMs, 5000, 120000, v => zone.LegendaryPromptMs = (int)v);
        Note(table, "An Event opens this zone. The equipped tool and catch conditions are checked by the server.");
    }
    private void ToolSelector(TableLayoutPanel table)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var tool in _config.Tools) box.Items.Add(new Named<FishingToolDefinition>(tool, t => ItemName(t.ItemId)));
        box.SelectedItem = box.Items.OfType<Named<FishingToolDefinition>>().FirstOrDefault(n => n.Value == _tool);
        box.SelectedIndexChanged += (_, _) => { if (_loading) return; _tool = (box.SelectedItem as Named<FishingToolDefinition>)?.Value; BuildSelectedPage(); };
        Row(table, "Tool", box);
    }
    private void BuildTools()
    {
        var table = Page(1); ToolSelector(table);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        AddButton(buttons, "New tool", () => { _tool = new(); _config.Tools.Add(_tool); Changed(); BuildSelectedPage(); });
        AddButton(buttons, "Delete tool", () => {
            if (_tool is null) return;
            if (_tool.ItemId != Guid.Empty && _config.Zones.Any(z => z.Catches.Any(c => c.ToolItemIds.Contains(_tool.ItemId))))
            { _status.Text = T("This tool is used by catches. Remove those associations first."); return; }
            _config.Tools.Remove(_tool); _tool = _config.Tools.FirstOrDefault(); Changed(); BuildSelectedPage();
            _status.Text = T("Tool deleted. Save to apply the change.");
        }); Row(table, "", buttons);
        if (_tool is not { } tool) { Note(table, "Create or select an equipped fishing tool."); return; }
        GuidField(table, "Equipment item", tool.ItemId,
            ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Equipment &&
                !_config.Tools.Any(other => other != tool && other.ItemId == i.Id)).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)), id => {
                var previous = tool.ItemId; tool.ItemId = id;
                if (previous != Guid.Empty) foreach (var entry in _config.Zones.SelectMany(z => z.Catches))
                    if (entry.ToolItemIds.Remove(previous)) { if (id != Guid.Empty && !entry.ToolItemIds.Contains(id)) entry.ToolItemIds.Add(id); }
            }, allowEmpty: tool.ItemId == Guid.Empty);
        EnumField(table, "Tool type", tool.Type, type => tool.Type = type);
        GuidField(table, "Bait item", tool.BaitItemId,
            ItemChoices().Where(item => item.Id != tool.ItemId && ItemDescriptor.Get(item.Id)?.IsStackable == true),
            id => tool.BaitItemId = id, "No bait");
        Number(table, "Bait consumed per attempt", tool.BaitQuantity, 1, 100, v => tool.BaitQuantity = (int)v);
        Number(table, "Tool power", tool.Power, .25, 5, v => tool.Power = (double)v, 2);
        Note(table, "The item must be equipped. Merely carrying it does not allow fishing.");
    }
    private void BuildCatches(bool legendaryOnly)
    {
        var table = Page(legendaryOnly ? 3 : 2);
        if (_zone is not { } zone) { Note(table, "Create or select a fishing zone."); return; }
        var entries = zone.Catches.Where(c => !legendaryOnly || c.Legendary).ToList();
        if (_catch is null || !entries.Contains(_catch)) _catch = entries.FirstOrDefault();
        var selection = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var option in entries) selection.Items.Add(new Named<FishingCatchDefinition>(option, c => ItemName(c.ItemId)));
        selection.SelectedItem = selection.Items.OfType<Named<FishingCatchDefinition>>().FirstOrDefault(n => n.Value == _catch);
        selection.SelectedIndexChanged += (_, _) => { if (_loading) return; _catch = (selection.SelectedItem as Named<FishingCatchDefinition>)?.Value; BuildSelectedPage(); };
        Row(table, "Catch", selection);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        AddButton(buttons, "New catch", () => { _catch = new() { Legendary = legendaryOnly }; zone.Catches.Add(_catch); Changed(); BuildSelectedPage(); });
        AddButton(buttons, "Duplicate catch", () => {
            if (_catch is null) return;
            _catch = JsonSerializer.Deserialize<FishingCatchDefinition>(JsonSerializer.Serialize(_catch))!; _catch.Id = Guid.NewGuid();
            zone.Catches.Add(_catch); Changed(); BuildSelectedPage();
        });
        AddButton(buttons, "Delete catch", () => {
            if (_catch is null) return;
            zone.Catches.Remove(_catch); _catch = null; Changed(); BuildSelectedPage();
            _status.Text = T("Catch deleted. Save to apply the change.");
        });
        Row(table, "", buttons);
        if (_catch is not { } entry) { Note(table, "Add a catch and select its allowed equipment."); return; }
        GuidField(table, "Reward item", entry.ItemId, ItemChoices(), id => entry.ItemId = id);
        var allowed = new CheckedListBox { CheckOnClick = true, Height = 110, Dock = DockStyle.Fill };
        foreach (var tool in _config.Tools.Where(t => t.ItemId != Guid.Empty))
            allowed.Items.Add(new Choice(tool.ItemId, ItemName(tool.ItemId)), entry.ToolItemIds.Contains(tool.ItemId));
        allowed.ItemCheck += (_, e) => {
            if (_loading || allowed.Items[e.Index] is not Choice choice) return;
            if (e.NewValue == CheckState.Checked) { if (!entry.ToolItemIds.Contains(choice.Id)) entry.ToolItemIds.Add(choice.Id); }
            else entry.ToolItemIds.Remove(choice.Id); Changed();
        }; Row(table, "Allowed equipment", allowed);
        Number(table, "Relative weight", entry.Weight, .001, 1000000, v => entry.Weight = (double)v, 3);
        Number(table, "Minimum profession level", entry.MinimumLevel, 1, 500, v => entry.MinimumLevel = (int)v);
        Number(table, "Minimum quantity", entry.MinimumQuantity, 1, 1000, v => entry.MinimumQuantity = (int)v);
        Number(table, "Maximum quantity", entry.MaximumQuantity, 1, 1000, v => entry.MaximumQuantity = (int)v);
        Number(table, "Experience", entry.Experience, 0, 1000000, v => entry.Experience = (int)v);
        Number(table, "Difficulty", entry.Difficulty, 1, 10, v => entry.Difficulty = (int)v);
        Number(table, "Minimum depth", entry.MinimumDepth, 0, 100, v => entry.MinimumDepth = (int)v);
        Number(table, "Maximum depth", entry.MaximumDepth, 0, 100, v => entry.MaximumDepth = (int)v);
        Number(table, "Start hour (UTC)", entry.UtcStartHour, 0, 23, v => entry.UtcStartHour = (int)v);
        Number(table, "End hour (UTC, exclusive)", entry.UtcEndHour, 0, 24, v => entry.UtcEndHour = (int)v);
        GuidField(table, "Required player variable", entry.RequiredVariableId,
            PlayerVariableDescriptor.Lookup.Values.OfType<PlayerVariableDescriptor>().OrderBy(v => v.Name).Select(v => new Choice(v.Id, v.Name)),
            id => entry.RequiredVariableId = id, "No condition");
        NumberExact(table, "Required variable value", entry.RequiredVariableValue, long.MinValue, long.MaxValue, v => entry.RequiredVariableValue = (long)v);
        Check(table, "Legendary", entry.Legendary, b => entry.Legendary = b);
        Number(table, "Challenge steps", entry.ChallengeSteps, 1, 8, v => entry.ChallengeSteps = (int)v);
        Number(table, "Challenge response window (ms)", entry.ChallengeWindowMs, 800, 10000, v => entry.ChallengeWindowMs = (int)v);
        FileField(table, "Sprite sheet", entry.Sprite, "items", s => entry.Sprite = s, true);
        Number(table, "Sprite X (pixels)", entry.SpriteX, 0, 16384, v => entry.SpriteX = (int)v);
        Number(table, "Sprite Y (pixels)", entry.SpriteY, 0, 16384, v => entry.SpriteY = (int)v);
        Number(table, "Sprite width", entry.SpriteWidth, 1, 2048, v => entry.SpriteWidth = (int)v);
        Number(table, "Sprite height", entry.SpriteHeight, 1, 2048, v => entry.SpriteHeight = (int)v);
        Note(table, "Only eligible catches participate in the weighted draw. Reward items must support inventory stacks.");
        if (legendaryOnly) Note(table, "Automatic fishing pauses for a legendary encounter. A player must accept and complete the challenge.");
    }
    private void BuildAnimations()
    {
        var table = Page(4); ToolSelector(table);
        if (_tool is not { } tool) { Note(table, "Create or select an equipped fishing tool."); return; }
        var animations = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name).Select(a => new Choice(a.Id, a.Name)).ToArray();
        GuidField(table, "Cast animation", tool.CastAnimationId, animations, id => tool.CastAnimationId = id, "No animation");
        GuidField(table, "Use animation", tool.UseAnimationId, animations, id => tool.UseAnimationId = id, "No animation");
        GuidField(table, "Catch animation", tool.CatchAnimationId, animations, id => tool.CatchAnimationId = id, "No animation");
        Number(table, "Use animation interval (ms)", tool.AnimationIntervalMs, 250, 30000, v => tool.AnimationIntervalMs = (int)v);
        BuildPaperdollFields(table, tool);
        Note(table, "These animations play on the real player in the world. The fishing window contains no character or dock.");
        Note(table, "Fishing Net sheets use 128 × 128 cells. B/F are complementary back/front layers; confirm direction order and anchors in game.");
    }
    private void BuildSkills()
    {
        var table = Page(6);
        GuidField(table, "Fishing profession", _config.ProfessionId,
            ProfessionConfiguration.Instance.Professions.Where(p => !NativeProfessions.IsNative(p.Id)).Select(p => new Choice(p.Id, p.Name)), id => _config.ProfessionId = id,
            "Automatic detection / built-in progression");
        if (Enum.GetValues<FishingSkillType>().Any(type => _config.Skills.All(skill => skill.Skill != type)))
        {
            var actions = new FlowLayoutPanel { AutoSize = true };
            AddButton(actions, "Add missing skills", () => {
                foreach (var skill in FishingSkillDefinition.Defaults())
                    if (_config.Skills.All(existing => existing.Skill != skill.Skill)) _config.Skills.Add(skill);
                Changed(); BuildSelectedPage();
            });
            Row(table, "", actions);
        }
        foreach (var type in Enum.GetValues<FishingSkillType>())
        {
            var skill = _config.Skills.FirstOrDefault(s => s.Skill == type);
            if (skill is null) { Note(table, string.Format(T("{0} is not configured."), T(type.ToString()))); continue; }
            var current = skill; Note(table, T(type.ToString()));
            Check(table, "Enabled", current.Enabled, v => current.Enabled = v);
            Number(table, "Minimum profession level", current.MinimumLevel, 1, 500, v => current.MinimumLevel = (int)v);
            Number(table, "Cooldown (ms)", current.CooldownMs, 1000, 600000, v => current.CooldownMs = (int)v);
            Number(table, "Duration (ms)", current.DurationMs, 0, 30000, v => current.DurationMs = (int)v);
            Number(table, "Effect amount", current.Amount, 0, 100, v => current.Amount = (double)v, 2);
            Number(table, "Tension cost", current.TensionCost, 0, 100, v => current.TensionCost = (double)v, 2);
        }
    }
    private bool ValidateDraft()
    {
        try
        {
            if (_config.Tools.Any(t => t.ItemId == Guid.Empty)) throw new InvalidDataException(T("Select an equipment item for every tool."));
            if (_config.Tools.Select(t => t.ItemId).Distinct().Count() != _config.Tools.Count) throw new InvalidDataException(T("Each equipment item can have only one fishing tool profile."));
            foreach (var zone in _config.Zones)
            {
                if (zone.Enabled && (zone.MapId == Guid.Empty || zone.Catches.Count == 0))
                    throw new InvalidDataException(string.Format(T("Enabled zone {0} needs a map and at least one catch."), zone.Name));
                foreach (var entry in zone.Catches)
                {
                    if (entry.ItemId == Guid.Empty || entry.ToolItemIds.Count == 0)
                        throw new InvalidDataException(string.Format(T("A catch in {0} needs a reward item and allowed equipment."), zone.Name));
                    if (ItemDescriptor.Get(entry.ItemId) is not { IsStackable: true, MaxInventoryStack: > 0 })
                        throw new InvalidDataException(string.Format(T("Reward item {0} must allow inventory stacks."), ItemName(entry.ItemId)));
                }
            }
            FishingRules.Validate(_config); return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        { _status.Text = ex.Message == "Invalid fishing configuration." ? T("Invalid fishing configuration. Check references, time ranges, quantities and sound settings.") : ex.Message; return false; }
    }
    private void Save()
    {
        if (!ValidateDraft()) return;
        _pendingSaveJson = _config.ToJson();
        _save.Enabled = false; _saveTimeout.Start(); _status.Text = T("Saving...");
        Networking.Network.SendPacket(new FishingRequestPacket { Action = FishingAction.SaveConfiguration, ConfigurationJson = _pendingSaveJson });
    }
    internal void Response(FishingStatePacket packet)
    {
        // A refresh for the Event picker must not acknowledge an in-flight save.
        if (!packet.Saved && string.IsNullOrEmpty(packet.Message)) return;
        if (packet.Saved)
        {
            if (_pendingSaveJson is null || packet.ConfigurationJson != _pendingSaveJson) return;
            _dirty = _config.ToJson() != _pendingSaveJson;
            _status.Text = T(_dirty ? "Saved. Newer changes are still unsaved." : "Saved");
        }
        else _status.Text = FishingUiText.Get(packet.Message,
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        _saveTimeout.Stop(); _save.Enabled = true; _pendingSaveJson = null;
    }
}
