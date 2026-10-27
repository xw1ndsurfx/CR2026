using Intersect.Editor.General;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Render = Intersect.Editor.Core.Graphics;

namespace Intersect.Editor.Forms.Editors;

/// <summary>Two focused views over a single validated server configuration.</summary>
public sealed class FrmRoyalCombatConfiguration : Form
{
    private RoyalCombatConfiguration _config;
    private bool _masteries;
    private readonly Panel _previewPanel;
    private readonly Button _new = new() { AutoSize = true };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _map = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly CheckedListBox _weapons = new() { Dock = DockStyle.Fill, CheckOnClick = true, DisplayMember = "Name" };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 30 };
    private readonly Button _save = new() { AutoSize = true };
    private Guid _request;
    private bool _refreshing;
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmRoyalCombatConfiguration(string json, bool masteries)
    {
        _masteries = masteries; _config = RoyalCombatConfiguration.FromJson(json);
        Text = T(masteries ? "Weapon Mastery Editor" : "Archery Mini-Game Editor");
        Size = new(1000, 650); MinimumSize = new(760, 480);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        void Button(string text, Action action) { var button = new Button { Text = T(text), AutoSize = true }; button.Click += (_, _) => action(); toolbar.Controls.Add(button); }
        toolbar.Controls.Add(_new);
        _new.Click += (_, _) => {
            if (_masteries)
            {
                var key = "mastery-" + Guid.NewGuid().ToString("N")[..8];
                // Keys deliberately use only lower-case letters and hyphens.
                key = new string(key.Select(c => char.IsDigit(c) ? (char)('a' + c - '0') : c).ToArray());
                _config.Masteries = [.._config.Masteries, new MasteryCategory { Key = key }];
            }
            else if (Globals.CurrentMap != null) _config.Ranges = [.._config.Ranges, new ArcheryRange { MapId = Globals.CurrentMap.Id }];
            RefreshList();
        };
        Button("Delete", () => {
            if (_list.SelectedItem is MasteryCategory category) _config.Masteries = _config.Masteries.Where(m => m != category).ToArray();
            if (_list.SelectedItem is ArcheryRange range) _config.Ranges = _config.Ranges.Where(r => r != range).ToArray();
            RefreshList();
        });
        Button("Mastery talent tree", () => {
            if (_list.SelectedItem is not MasteryCategory category) return;
            using var dialog = new FrmMasteryTalents(category);
            dialog.StartPosition = FormStartPosition.CenterParent;
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(dialog);
            if (dialog.ShowDialog(this) == DialogResult.OK) { category.Talents = dialog.Result.Talents; _properties.Refresh(); }
        });
        Button("Mastery reset settings", () => {
            using var dialog = new FrmMasteryRespec(_config) { StartPosition = FormStartPosition.CenterParent };
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(dialog);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            { _config.RespecEnabled = dialog.RespecEnabled; _config.RespecCurrencyId = dialog.CurrencyId; _config.RespecCost = dialog.Cost; }
        });
        Button("Ammunition compatibility", () => {
            using var dialog = new FrmWeaponAmmunition(_config.Ammunition);
            dialog.StartPosition = FormStartPosition.CenterParent;
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(dialog);
            if (dialog.ShowDialog(this) == DialogResult.OK) _config.Ammunition = dialog.Result;
        });
        Button("Range rules and targets", () => {
            if (_list.SelectedItem is not ArcheryRange range) return;
            using var dialog = new FrmArcheryRangeRules(range.Rules) { StartPosition = FormStartPosition.CenterParent };
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(dialog);
            if (dialog.ShowDialog(this) == DialogResult.OK) { range.Rules = dialog.Result; _properties.Refresh(); }
        });
        _save.Text = T("Save"); toolbar.Controls.Add(_save); _save.Click += (_, _) => Save();
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(960, 550), SplitterDistance = 200 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(760, 550), SplitterDistance = 400 };
        split.Panel1.Controls.Add(_list); split.Panel2.Controls.Add(right);
        _previewPanel = right.Panel1;
        right.Panel2.Controls.Add(_properties);
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(_status);
        _list.DisplayMember = "Name";
        _list.SelectedIndexChanged += (_, _) => SelectEntry();
        _properties.PropertyValueChanged += (_, _) => { Preview(); _list.Refresh(); };
        _weapons.ItemCheck += (_, e) => {
            if (_refreshing || _list.SelectedItem is not MasteryCategory category || _weapons.Items[e.Index] is not ItemDescriptor item) return;
            category.WeaponIds = e.NewValue == CheckState.Checked ? category.WeaponIds.Append(item.Id).Distinct().ToArray() : category.WeaponIds.Where(id => id != item.Id).ToArray();
        };
        _map.MouseClick += (_, e) => {
            if (_list.SelectedItem is not ArcheryRange range || _map.Image is not { } image) return;
            var scale = Math.Min(_map.Width / (float)image.Width, _map.Height / (float)image.Height);
            var x = (int)Math.Floor((e.X - (_map.Width - image.Width * scale) / 2) / scale / Intersect.Options.Instance.Map.TileWidth);
            var y = (int)Math.Floor((e.Y - (_map.Height - image.Height * scale) / 2) / scale / Intersect.Options.Instance.Map.TileHeight);
            if (x < 0 || y < 0 || x >= Intersect.Options.Instance.Map.MapWidth || y >= Intersect.Options.Instance.Map.MapHeight) return;
            range.StartX = x; range.StartY = y; _properties.Refresh(); Preview();
        };
        Disposed += (_, _) => _map.Image?.Dispose();
        ShowMode(masteries);
    }
    internal void ShowMode(bool masteries)
    {
        _masteries = masteries;
        Text = T(masteries ? "Weapon Mastery Editor" : "Archery Mini-Game Editor");
        _new.Text = T(masteries ? "New mastery category" : "New range on current map");
        _previewPanel.Controls.Clear(); _previewPanel.Controls.Add(masteries ? _weapons : _map);
        _status.Text = T(masteries ? "Select existing weapons for this mastery. A weapon belongs to one category." : "Click the map to place the shooting position. Enable the range, then save.");
        RefreshList();
    }
    private void RefreshList()
    { _list.DataSource = null; _list.DataSource = _masteries ? (object)_config.Masteries : _config.Ranges; }
    private void SelectEntry()
    {
        _properties.SelectedObject = _list.SelectedItem;
        if (_masteries && _list.SelectedItem is MasteryCategory category)
        {
            _refreshing = true; _weapons.Items.Clear();
            foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Equipment &&
                i.EquipmentSlot == Intersect.Options.Instance.Equipment.WeaponSlot).OrderBy(i => i.Name)) _weapons.Items.Add(item, category.WeaponIds.Contains(item.Id));
            _refreshing = false;
        }
        Preview();
    }
    private void Preview()
    {
        if (_masteries || _list.SelectedItem is not ArcheryRange range || Globals.CurrentMap?.Id != range.MapId) return;
        var image = Render.RenderCinematicMap(new CinematicScene { MapId = range.MapId }, 0);
        if (image == null) return;
        using (var graphics = System.Drawing.Graphics.FromImage(image))
        using (var pen = new Pen(System.Drawing.Color.Gold, 2)) graphics.DrawRectangle(pen,
            range.StartX * Intersect.Options.Instance.Map.TileWidth, range.StartY * Intersect.Options.Instance.Map.TileHeight,
            Intersect.Options.Instance.Map.TileWidth, Intersect.Options.Instance.Map.TileHeight);
        var previous = _map.Image; _map.Image = image; previous?.Dispose();
    }
    private void Save()
    {
        if (!_config.IsValid) { _status.Text = T("Invalid royal combat configuration."); return; }
        _request = Guid.NewGuid(); _save.Enabled = false;
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.SaveConfiguration,
            ConfigurationJson = _config.ToJson(), RequestId = _request });
    }
    internal void Response(ArcheryStatePacket packet)
    {
        if (_request != packet.RequestId) return;
        _save.Enabled = true; _status.Text = T(packet.Saved ? "Saved" : "Invalid royal combat configuration.");
    }
}
