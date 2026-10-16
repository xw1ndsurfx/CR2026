using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Housing;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
public sealed class FrmHousingConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly HousingConfiguration _draft;
    private readonly ComboBox _map = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly CheckBox _enabled = new() { Text = "Enable player housing", AutoSize = true };
    private readonly NumericUpDown _x = new() { Minimum = 0, Maximum = 255, Width = 70 };
    private readonly NumericUpDown _y = new() { Minimum = 0, Maximum = 255, Width = 70 };
    private readonly NumericUpDown _max = new() { Minimum = 1, Maximum = 500, Width = 70 };
    private readonly NumericUpDown _pets = new() { Minimum = 1, Maximum = 50, Width = 70 };
    private readonly DataGridView _catalog = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 30, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    public FrmHousingConfiguration(string json)
    {
        _draft = JsonConvert.DeserializeObject<HousingConfiguration>(json) ?? new();
        Text = "Housing & Monthly Furniture Catalog"; Size = new Size(1160, 760); MinimumSize = new Size(960, 620); StartPosition = FormStartPosition.CenterParent;
        foreach (var map in MapDescriptor.Lookup.Values.OfType<MapDescriptor>().OrderBy(m => m.Name)) _map.Items.Add(new Choice(map.Id, map.Name));
        _map.SelectedItem = _map.Items.Cast<Choice>().FirstOrDefault(m => m.Id == _draft.TemplateMapId);
        _enabled.Checked = _draft.Enabled; _x.Value = _draft.EntryX; _y.Value = _draft.EntryY; _max.Value = _draft.MaximumFurniture; _pets.Value = _draft.MaximumPets;
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 102, Padding = new Padding(12), AutoScroll = true };
        header.Controls.AddRange([_enabled, new Label { Text = "Interior map", AutoSize = true }, _map, new Label { Text = "Entry X / Y", AutoSize = true }, _x, _y,
            new Label { Text = "Furniture / pets", AutoSize = true }, _max, _pets]);
        header.Controls.Add(new Label { Text = "Use an isolated interior map. Months: yyyy-MM separated by commas (UTC). Keep expired offers to preserve furniture IDs. Source X/Y use pixels; width/height use tiles. Furniture may run an optional common event when used nearby. Pet recovery: server config Npc.PetRecoverySeconds (default 300).", AutoSize = true, MaximumSize = new Size(1080, 0) });
        void TextColumn(string property, string title, bool readOnly = false) => _catalog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title, ReadOnly = readOnly });
        _catalog.Columns.Add(new DataGridViewButtonColumn { Name = "ChooseGraphic", HeaderText = "Graphic", Text = "Select graphic...", UseColumnTextForButtonValue = true });
        var events = new List<Choice> { new(Guid.Empty, "None") };
        events.AddRange(Intersect.Framework.Core.GameObjects.Events.EventDescriptor.Lookup.Values.OfType<Intersect.Framework.Core.GameObjects.Events.EventDescriptor>().Where(e => e.CommonEvent).OrderBy(e => e.Name).Select(e => new Choice(e.Id, e.Name)));
        _catalog.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "On interaction (optional)", DataPropertyName = "CommonEventId", DataSource = events, DisplayMember = "Name", ValueMember = "Id" });
        _catalog.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _catalog.Columns[e.ColumnIndex].Name != "ChooseGraphic") return;
            _catalog.EndEdit();
            if (_catalog.Rows[e.RowIndex].DataBoundItem is FurnitureDefinition furniture) ChooseGraphic(furniture);
        };
        TextColumn("Name", "Furniture"); TextColumn("Months", "Catalog months"); TextColumn("Price", "Price");
        var currencies = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.Stackable || i.ItemType == ItemType.Currency).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)).ToList();
        _catalog.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Currency", DataPropertyName = "CurrencyItemId", DataSource = currencies, DisplayMember = "Name", ValueMember = "Id" });
        var tilesets = Directory.Exists("resources/tilesets") ? Directory.GetFiles("resources/tilesets").Select(Path.GetFileName).OfType<string>().OrderBy(n => n).ToArray() : [];
        foreach (var name in _draft.Catalog.Select(f => f.Tileset).Where(n => !string.IsNullOrEmpty(n))) if (!tilesets.Contains(name)) tilesets = [..tilesets, name];
        _catalog.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Tileset", DataPropertyName = "Tileset", DataSource = tilesets });
        TextColumn("SourceX", "Source X"); TextColumn("SourceY", "Source Y"); TextColumn("Width", "Tiles wide"); TextColumn("Height", "Tiles tall");
        _catalog.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Floor decoration", DataPropertyName = "FloorDecoration" });
        TextColumn("Id", "Stable ID", true);
        _catalog.DataSource = new BindingList<FurnitureDefinition>(_draft.Catalog);
        _catalog.DataError += (_, e) => { e.ThrowException = false; if (e.RowIndex >= 0 && e.RowIndex < _catalog.Rows.Count) _catalog.Rows[e.RowIndex].ErrorText = "Choose a valid asset, currency or numeric value."; };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", Width = 110 }; cancel.Click += (_, _) => Close();
        var save = new Button { Text = "Save", Width = 110 }; save.Click += (_, _) =>
        {
            _catalog.EndEdit(); BindingContext[_catalog.DataSource].EndCurrentEdit();
            _draft.Enabled = _enabled.Checked; _draft.TemplateMapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty;
            _draft.EntryX = (int)_x.Value; _draft.EntryY = (int)_y.Value; _draft.MaximumFurniture = (int)_max.Value; _draft.MaximumPets = (int)_pets.Value;
            if (!_draft.IsValid) { MessageBox.Show(this, "Check the interior map, months, prices, currency and furniture graphics.", "Housing"); return; }
            Networking.PacketSender.SendHousingConfiguration(JsonConvert.SerializeObject(_draft)); Close();
        };
        footer.Controls.Add(cancel); footer.Controls.Add(save); Controls.Add(_catalog); Controls.Add(header); Controls.Add(footer);
        MiniGameEditorTheme.Apply(this);
        _catalog.BackgroundColor = System.Drawing.Color.FromArgb(45, 45, 48); _catalog.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(57, 60, 62); _catalog.DefaultCellStyle.ForeColor = System.Drawing.Color.Gainsboro;
    }
    private void ChooseGraphic(FurnitureDefinition furniture)
    {
        using var dialog = new DarkForm { Text = "Furniture graphic - select tiles", Size = new Size(780, 720), MinimumSize = new Size(540, 600), StartPosition = FormStartPosition.CenterParent };
        var tile = Options.Instance.Map;
        var filename = furniture.Tileset;
        if (string.IsNullOrWhiteSpace(filename))
            filename = Intersect.Editor.Content.GameContentManager.GetSmartSortedTextureNames(Intersect.Editor.Content.GameContentManager.TextureType.Tileset).FirstOrDefault() ?? string.Empty;
        var graphic = new Intersect.Framework.Core.GameObjects.Events.EventGraphic
        {
            Type = Intersect.Framework.Core.GameObjects.Events.EventGraphicType.Tileset, Filename = filename,
            X = furniture.SourceX / tile.TileWidth, Y = furniture.SourceY / tile.TileHeight,
            Width = furniture.Width - 1, Height = furniture.Height - 1
        };
        var selector = new Intersect.Editor.Forms.Editors.Events.Event_Commands.EventGraphicSelector(graphic, null, tilesetOnly: true) { Dock = DockStyle.Fill };
        selector.SelectionClosed += accepted =>
        {
            if (accepted)
            {
                var x = Math.Min(graphic.X, graphic.X + graphic.Width);
                var y = Math.Min(graphic.Y, graphic.Y + graphic.Height);
                var width = Math.Abs(graphic.Width) + 1; var height = Math.Abs(graphic.Height) + 1;
                if (x < 0 || y < 0 || width > 8 || height > 8 || string.IsNullOrWhiteSpace(graphic.Filename))
                { MessageBox.Show(dialog, "Select a tileset rectangle between 1 x 1 and 8 x 8 tiles."); return; }
                furniture.Tileset = graphic.Filename; furniture.SourceX = x * tile.TileWidth; furniture.SourceY = y * tile.TileHeight;
                furniture.Width = width; furniture.Height = height; _catalog.Refresh();
            }
            dialog.Close();
        };
        dialog.Controls.Add(selector); dialog.ShowDialog(this);
    }
}
