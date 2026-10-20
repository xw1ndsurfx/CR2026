using System.Globalization;
using System.Reflection;
using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmNavalConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly NavalConfiguration _navigation;
    private readonly HarbourCatalog _catalogue;
    private string _navigationVersion, _catalogueVersion;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 45 };
    private readonly Button _save = new() { AutoSize = true };
    private readonly bool[] _dirty = new bool[2];
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 10000 };
    private byte _pending;
    private Guid _pendingRequest;
    internal static string T(string source) => NavalSetupText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public FrmNavalConfiguration(NavalConfigurationPacket packet)
    {
        _navigation = JsonConvert.DeserializeObject<NavalConfiguration>(packet.NavigationJson) ?? new();
        _catalogue = JsonConvert.DeserializeObject<HarbourCatalog>(packet.CatalogueJson) ?? new();
        _navigationVersion = packet.NavigationVersion; _catalogueVersion = packet.CatalogueVersion;
        Text = T("Naval System Editor"); Size = new(1050, 780); MinimumSize = new(850, 620);
        StartPosition = FormStartPosition.CenterParent;
        var general = Page("Navigation and harbour");
        general.Controls.Add(Fields(_navigation, 0, ["Ships", "SourceJson", "AdditionalData"]));
        AddListPage("Test ships", _navigation.Ships.ToList(), 0, () => new ShipDefinition
            { Graphics = "Ship_CorpsRoyaux_002.png", DamagedGraphics = "Ship_CorpsRoyaux_005.png" },
            ships => _navigation.Ships = ships.ToArray());
        AddListPage("Ship shop and repairs", _catalogue.Offers.ToList(), 1,
            () => new HarbourOffer { ForSale = false, Id = "ship-" + Guid.NewGuid().ToString("N")[..12],
                Ship = new() { Graphics = "Ship_CorpsRoyaux_002.png", DamagedGraphics = "Ship_CorpsRoyaux_005.png" } },
            offers => _catalogue.Offers = offers.ToArray());
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
        _save.Text = T("Save current section");
        var close = new Button { Text = T("Close"), AutoSize = true };
        footer.Controls.AddRange([close, _save]);
        var help = new Label { Dock = DockStyle.Bottom, Height = 52,
            Text = T("Use Q docks for your fleet. In an NPC event, use Open ship shop and repairs."), Padding = new(8) };
        Controls.Add(_tabs); Controls.Add(help); Controls.Add(_status); Controls.Add(footer);
        _save.Click += (_, _) => SaveSection(); close.Click += (_, _) => Close();
        _tabs.SelectedIndexChanged += (_, _) => SetStatus();
        _timeout.Tick += (_, _) => { _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
            _status.Text = T("No reply yet. Changes are kept; retry or reload the editor."); };
        FormClosing += (_, e) =>
        {
            if (_pending != 0) { e.Cancel = true; return; }
            if (_dirty.Any(d => d) && MessageBox.Show(this, T("Discard unsaved changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                e.Cancel = true;
        };
        FormClosed += (_, _) => _timeout.Dispose();
        MiniGameEditorTheme.Apply(this); SetStatus();
    }

    private TabPage Page(string title)
    {
        var tab = new TabPage(T(title)); _tabs.TabPages.Add(tab); return tab;
    }
    private void Changed(int section) { _dirty[section] = true; SetStatus(); }
    private void SetStatus() => _status.Text = _dirty.Any(d => d) ? T("Unsaved changes") : T("Settings received from the server.");

    private void AddListPage<TItem>(string title, List<TItem> items, int section, Func<TItem> create, Action<List<TItem>> store)
        where TItem : class
    {
        var tab = Page(title);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1000, 620), SplitterDistance = 275 };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Format += (_, e) => e.Value = e.ListItem is HarbourOffer offer ? offer.Name :
            e.ListItem is ShipDefinition ship ? $"{MapList.OrderedMaps.FirstOrDefault(m => m.MapId == ship.MapId)?.Name ?? ship.MapId.ToString()} ({ship.X}, {ship.Y})" : "";
        list.FormattingEnabled = true;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
        var add = new Button { Text = T("Add"), AutoSize = true };
        var remove = new Button { Text = T("Remove"), AutoSize = true };
        buttons.Controls.AddRange([add, remove]); split.Panel1.Controls.Add(list); split.Panel1.Controls.Add(buttons);
        void Populate(TItem? selected)
        {
            list.Items.Clear(); list.Items.AddRange(items.Cast<object>().ToArray()); list.SelectedItem = selected;
        }
        list.SelectedIndexChanged += (_, _) =>
        {
            foreach (Control control in split.Panel2.Controls.Cast<Control>().ToArray()) control.Dispose();
            if (list.SelectedItem is not TItem item) return;
            var fields = Fields(item, section, item is HarbourOffer ? ["Ship", "AdditionalData"] : ["AdditionalData"]);
            if (item is HarbourOffer offer)
            {
                var model = Fields(offer.Ship, section, ["Id", "MapId", "X", "Y", "DockX", "DockY", "AdditionalData"]);
                model.Dock = DockStyle.Top; model.AutoSize = true; model.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                var group = new GroupBox { Text = T("Ship model and movement"), Dock = DockStyle.Top, AutoSize = true, Padding = new(6, 24, 6, 6) };
                group.Controls.Add(model); fields.Controls.Add(group, 0, fields.RowCount++); fields.SetColumnSpan(group, 2);
            }
            split.Panel2.Controls.Add(fields); MiniGameEditorTheme.Apply(split.Panel2);
        };
        add.Click += (_, _) =>
        {
            if (items.Count >= (section == 0 ? 64 : 32)) return;
            var item = create(); items.Add(item); store(items); Changed(section); Populate(item);
        };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not TItem item) return;
            items.Remove(item); store(items); Changed(section); Populate(items.FirstOrDefault());
        };
        tab.Controls.Add(split); Populate(items.FirstOrDefault());
        // An empty catalogue must still be enableable before adding the first offer.
        if (section == 1)
        {
            var enabled = new CheckBox { Text = T("Enable shop"), Dock = DockStyle.Top, Checked = _catalogue.Enabled, Height = 32 };
            enabled.CheckedChanged += (_, _) => { _catalogue.Enabled = enabled.Checked; Changed(1); };
            tab.Controls.Add(enabled); enabled.SendToBack();
        }
    }

    private TableLayoutPanel Fields(object target, int section, string[] skip)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new(12), AutoSize = false };
        panel.ColumnStyles.Add(new(SizeType.Absolute, 230)); panel.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var property in target.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite && !skip.Contains(p.Name)))
        {
            var row = panel.RowCount++; var value = property.GetValue(target); Control field;
            void Set(object? changed) { property.SetValue(target, changed); Changed(section); }
            if (property.PropertyType == typeof(bool))
            {
                var box = new CheckBox { Checked = (bool)value!, Dock = DockStyle.Fill };
                box.CheckedChanged += (_, _) => Set(box.Checked); field = box;
            }
            else if (property.PropertyType == typeof(Guid) && property.Name != "Id")
            {
                var choices = new List<Choice> { new(Guid.Empty, T("Automatic / not selected")) };
                if (property.Name == "MapId") choices.AddRange(MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name)));
                else choices.AddRange(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Currency).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)));
                var id = (Guid)value!; if (!choices.Any(c => c.Id == id)) choices.Add(new(id, id.ToString()));
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                box.Items.AddRange(choices.ToArray()); box.SelectedItem = choices.First(c => c.Id == id);
                box.SelectedIndexChanged += (_, _) => Set(((Choice)box.SelectedItem!).Id); field = box;
            }
            else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
            {
                var number = new NumericUpDown { Minimum = property.Name == "Heading" ? -360 : 0, Maximum = int.MaxValue, DecimalPlaces = property.PropertyType == typeof(double) ? 3 : 0,
                    Increment = property.PropertyType == typeof(double) ? .1m : 1, Dock = DockStyle.Fill };
                number.Value = Math.Clamp(Convert.ToDecimal(value, CultureInfo.InvariantCulture), number.Minimum, number.Maximum);
                number.ValueChanged += (_, _) => { if (property.PropertyType == typeof(double)) Set((double)number.Value); else Set((int)number.Value); }; field = number;
            }
            else if (property.Name is "Graphics" or "DamagedGraphics" or "SinkingGraphics")
            {
                var box = new ComboBox { Dock = DockStyle.Fill, Text = value?.ToString() ?? "" };
                var directory = Path.Combine("resources", "ocean");
                if (Directory.Exists(directory))
                    box.Items.AddRange(Directory.GetFiles(directory, "*.naval.json").Select(Path.GetFileName)
                        .Select(n => n![..^11]).Where(n => n.EndsWith("_sink.png", StringComparison.OrdinalIgnoreCase) ==
                            (property.Name == "SinkingGraphics")).OrderBy(n => n).Cast<object>().ToArray());
                box.TextChanged += (_, _) => Set(box.Text); field = box;
            }
            else if (property.Name == "Currency")
            {
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                box.Items.AddRange(["Aureons", "Auragems"]); box.SelectedItem = value;
                box.SelectedIndexChanged += (_, _) => Set(box.SelectedItem?.ToString()); field = box;
            }
            else
            {
                var text = new TextBox { Text = value?.ToString() ?? "", Dock = DockStyle.Fill, ReadOnly = property.Name == "Id" };
                text.TextChanged += (_, _) => Set(text.Text); field = text;
            }
            field.Name = property.Name;
            panel.RowStyles.Add(new(SizeType.Absolute, 35));
            panel.Controls.Add(new Label { Text = T(property.Name), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            panel.Controls.Add(field, 1, row);
        }
        return panel;
    }

    internal NavalConfigurationRequestPacket PrepareSave()
    {
        var section = _tabs.SelectedIndex < 2 ? (byte)1 : (byte)2;
        if (section == 1) _navigation.Validate(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
        else _catalogue.Validate();
        return new() { Section = section, ExpectedVersion = section == 1 ? _navigationVersion : _catalogueVersion,
            ConfigurationJson = JsonConvert.SerializeObject(section == 1 ? (object)_navigation : _catalogue) };
    }
    private void SaveSection()
    {
        try
        {
            ValidateChildren(); var request = PrepareSave();
            _pending = request.Section; _pendingRequest = request.RequestId; _save.Enabled = false; _tabs.Enabled = false; _timeout.Start();
            Intersect.Editor.Networking.Network.SendPacket(request); _status.Text = T("Saving...");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or JsonException)
        { _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true; _status.Text = T(e.Message); }
        catch
        {
            _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
            _status.Text = T("The naval configuration could not be read or saved. Existing data was preserved.");
        }
    }
    public void AcceptResponse(NavalConfigurationPacket packet)
    {
        if (_pendingRequest == Guid.Empty || packet.RequestId != _pendingRequest) return;
        _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
        if (packet.Error.Length > 0) { _status.Text = T(packet.Error); return; }
        if (packet.SavedSection is 1 or 2) _dirty[packet.SavedSection - 1] = false;
        if (packet.SavedSection == 1) _navigationVersion = packet.NavigationVersion;
        if (packet.SavedSection == 2) _catalogueVersion = packet.CatalogueVersion;
        _status.Text = T("Saved on the server.");
    }
}
