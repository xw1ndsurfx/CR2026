using System.Globalization;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Lockpicking;

namespace Intersect.Editor.Forms.Editors.Events;

internal sealed class LockpickingLootDialog : Form
{
    private sealed record ItemChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly DataGridView _grid;
    private readonly ItemChoice[] _items;

    public LockpickingLootEntry[] Result { get; private set; }

    public LockpickingLootDialog(IEnumerable<LockpickingLootEntry>? entries)
    {
        Text = "Locksmith Chest Loot";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        Width = 1180;
        Height = 560;
        MinimumSize = new Size(900, 420);

        var itemChoices = ItemDescriptor.Lookup.Values
            .OfType<ItemDescriptor>()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ItemChoice(item.Id, item.Name))
            .ToList();

        foreach (var entry in entries ?? [])
        {
            if (entry.ItemId == Guid.Empty || itemChoices.Any(choice => choice.Id == entry.ItemId))
                continue;

            itemChoices.Add(new ItemChoice(entry.ItemId, $"Missing item: {entry.ItemId}"));
        }

        _items = itemChoices.ToArray();

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };

        var itemColumn = new DataGridViewComboBoxColumn
        {
            HeaderText = "Item",
            Name = "Item",
            DisplayMember = nameof(ItemChoice.Name),
            ValueMember = nameof(ItemChoice.Id),
            DataSource = _items,
            FillWeight = 190,
        };

        _grid.Columns.Add(itemColumn);
        AddTextColumn("Min", "Min Qty", 65);
        AddTextColumn("Max", "Max Qty", 65);
        AddTextColumn("Chance", "Base %", 70);
        AddTextColumn("PerLevel", "Bonus/Level %", 90);
        AddTextColumn("Perfect", "Perfect +%", 80);
        AddTextColumn("MinLevel", "Min Skill", 75);
        AddTextColumn("QtyEvery", "+1 Qty / Levels", 95);

        foreach (var entry in entries ?? [])
        {
            _grid.Rows.Add(
                entry.ItemId,
                entry.MinQuantity,
                entry.MaxQuantity,
                entry.BaseChancePercent,
                (entry.ChanceBonusBasisPointsPerLevel / 100m).ToString("0.##", CultureInfo.CurrentCulture),
                entry.PerfectBonusPercent,
                entry.MinimumProfessionLevel,
                entry.QuantityBonusEveryLevels
            );
        }

        var help = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 58,
            Padding = new Padding(8),
            Text =
                "Bonus loot is rolled only when a Chest lock is successfully picked. " +
                "Chance increases with Locksmith level; Perfect adds the configured bonus. " +
                "Quantity scaling adds +1 item for every N profession levels (0 disables it).",
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };

        var ok = new Button { Text = "Save", Width = 100, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var remove = new Button { Text = "Remove selected", Width = 130 };

        ok.Click += (_, _) => SaveAndClose();
        remove.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow)
                    _grid.Rows.Remove(row);
            }
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(remove);

        Controls.Add(_grid);
        Controls.Add(help);
        Controls.Add(buttons);

        AcceptButton = ok;
        CancelButton = cancel;
        Result = Clone(entries ?? []);
    }

    private void AddTextColumn(string name, string header, float weight)
    {
        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = weight,
            }
        );
    }

    private void SaveAndClose()
    {
        var result = new List<LockpickingLootEntry>();

        try
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                if (row.Cells["Item"].Value is not Guid itemId || itemId == Guid.Empty)
                    throw new InvalidOperationException("Every loot row must select an item.");

                var min = ReadInt(row, "Min", 1);
                var max = ReadInt(row, "Max", min);
                var chance = ReadInt(row, "Chance", 100);
                var perLevelPercent = ReadDecimal(row, "PerLevel", 0m);
                var perfect = ReadInt(row, "Perfect", 0);
                var minLevel = ReadInt(row, "MinLevel", 0);
                var qtyEvery = ReadInt(row, "QtyEvery", 0);

                var entry = new LockpickingLootEntry
                {
                    ItemId = itemId,
                    MinQuantity = min,
                    MaxQuantity = max,
                    BaseChancePercent = chance,
                    ChanceBonusBasisPointsPerLevel = (int)Math.Round(
                        perLevelPercent * 100m,
                        MidpointRounding.AwayFromZero
                    ),
                    PerfectBonusPercent = perfect,
                    MinimumProfessionLevel = minLevel,
                    QuantityBonusEveryLevels = qtyEvery,
                };

                if (!entry.IsValid)
                    throw new InvalidOperationException(
                        $"Invalid loot row for {ItemDescriptor.GetName(itemId)}."
                    );

                result.Add(entry);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Invalid Locksmith Loot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        Result = result.ToArray();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static int ReadInt(DataGridViewRow row, string column, int fallback)
    {
        var value = row.Cells[column].Value;
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return fallback;

        if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException($"'{column}' must be a whole number.");
    }

    private static decimal ReadDecimal(DataGridViewRow row, string column, decimal fallback)
    {
        var value = row.Cells[column].Value;
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return fallback;

        if (decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException($"'{column}' must be a number.");
    }

    private static LockpickingLootEntry[] Clone(IEnumerable<LockpickingLootEntry> source) =>
        source.Select(entry => new LockpickingLootEntry
        {
            ItemId = entry.ItemId,
            MinQuantity = entry.MinQuantity,
            MaxQuantity = entry.MaxQuantity,
            BaseChancePercent = entry.BaseChancePercent,
            ChanceBonusBasisPointsPerLevel = entry.ChanceBonusBasisPointsPerLevel,
            PerfectBonusPercent = entry.PerfectBonusPercent,
            MinimumProfessionLevel = entry.MinimumProfessionLevel,
            QuantityBonusEveryLevels = entry.QuantityBonusEveryLevels,
        }).ToArray();
}
