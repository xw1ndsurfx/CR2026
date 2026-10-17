using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
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
    public FrmHousingConfiguration(string json) : this(json, null) { }
    internal FrmHousingConfiguration(string json, Action<FurnitureDefinition>? graphicPicker)
    {
        graphicPicker ??= ChooseGraphic;
        _draft = JsonConvert.DeserializeObject<HousingConfiguration>(json) ?? new();
        Text = "Housing & Monthly Furniture Catalog"; Size = new Size(1160, 760); MinimumSize = new Size(960, 620); StartPosition = FormStartPosition.CenterParent;
        // Map descriptors are loaded on demand; the project map list includes unopened maps.
        var maps = MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name))
            .Concat(MapDescriptor.Lookup.Values.OfType<MapDescriptor>().Select(m => new Choice(m.Id, m.Name)))
            .DistinctBy(m => m.Id).OrderBy(m => m.Name).ToList();
        if (_draft.TemplateMapId != Guid.Empty && maps.All(m => m.Id != _draft.TemplateMapId))
            maps.Add(new Choice(_draft.TemplateMapId, _draft.TemplateMapId.ToString()));
        _map.Items.AddRange(maps.Cast<object>().ToArray());
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
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _catalog.Columns[e.ColumnIndex].Name is not ("ChooseGraphic" or "ChooseTileset")) return;
            QueueGraphicSelection(e.RowIndex, graphicPicker);
        };
        _catalog.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Premium required", DataPropertyName = "PremiumRequired" });
        TextColumn("Name", "Furniture"); TextColumn("Months", "Catalog months"); TextColumn("Price", "Price");
        var currencies = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.Stackable || i.ItemType == ItemType.Currency).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)).ToList();
        _catalog.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Currency", DataPropertyName = "CurrencyItemId", DataSource = currencies, DisplayMember = "Name", ValueMember = "Id" });
        // Open the same visual tile-rectangle picker as Event Editor directly from the Tileset cell.
        _catalog.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "ChooseTileset", HeaderText = "Tileset", DataPropertyName = "Tileset", MinimumWidth = 180,
            UseColumnTextForButtonValue = false
        });
        _catalog.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex >= 0 && _catalog.Columns[e.ColumnIndex].Name == "ChooseTileset" && string.IsNullOrWhiteSpace(e.Value as string))
            {
                e.Value = "Select graphic...";
                e.FormattingApplied = true;
            }
        };
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
    private bool _graphicSelectionPending;
    private void QueueGraphicSelection(int rowIndex, Action<FurnitureDefinition> graphicPicker)
    {
        if (_graphicSelectionPending) return;
        _graphicSelectionPending = true;
        // Finish the button mouse-up before changing the grid's new-record state or opening a modal.
        BeginInvoke(new Action(() =>
        {
            try
            {
                if (IsDisposed || _catalog.IsDisposed || rowIndex >= _catalog.Rows.Count) return;
                if (!_catalog.EndEdit()) return;
                var row = _catalog.Rows[rowIndex];
                FurnitureDefinition? furniture = row.DataBoundItem as FurnitureDefinition;
                if (row.IsNewRow && _catalog.DataSource is BindingList<FurnitureDefinition> catalog)
                {
                    // Remove the placeholder while inserting a regular record. AddNew would
                    // start a second pending-row transaction inside DataGridView's own one.
                    _catalog.CurrentCell = null;
                    _catalog.AllowUserToAddRows = false;
                    try
                    {
                        furniture = new FurnitureDefinition();
                        catalog.Add(furniture);
                        BindingContext[catalog].EndCurrentEdit();
                    }
                    finally { _catalog.AllowUserToAddRows = true; }
                }
                if (furniture != null) graphicPicker(furniture);
            }
            finally { _graphicSelectionPending = false; }
        }));
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
