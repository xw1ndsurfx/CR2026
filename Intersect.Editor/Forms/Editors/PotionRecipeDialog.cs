using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;

namespace Intersect.Editor.Forms.Editors;

internal sealed class PotionRecipeDialog : DarkForm
{
    private sealed record ItemChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record RequirementChoice(PotionRequirement Requirement, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record VariableChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly TextBox _name = new() { Width = 300, MaxLength = 64 };
    private readonly NumericUpDown _level = new() { Minimum = 1, Maximum = MiniGameProgression.MaximumLevel, Width = 90 };
    private readonly ComboBox _outputItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 120 };
    private readonly NumericUpDown _xp = new() { Minimum = 1, Maximum = 5_000, Width = 120 };
    private readonly ComboBox _unlockVariable = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly ListBox _requirements = new() { Width = 500, Height = 150 };
    private readonly ComboBox _family = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _tier = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _needed = new() { Minimum = 1, Maximum = 20, Width = 90 };
    private readonly List<PotionRequirement> _draft = [];

    public PotionRecipeDefinition? Result { get; private set; }

    public PotionRecipeDialog(PotionRecipeDefinition? existing = null)
    {
        Text = existing == null ? "Add Potion Recipe" : "Edit Potion Recipe";
        StartPosition = FormStartPosition.CenterParent;
        Width = 660;
        Height = 600;
        MinimizeBox = false;
        MaximizeBox = false;

        foreach (var family in Enum.GetValues<PotionFamily>()) _family.Items.Add(family);
        _family.SelectedIndex = 0;
        _tier.Items.AddRange(new object[] { 1, 2, 3, 4 });
        _tier.SelectedIndex = 1;

        FillItems();
        FillUnlockVariables();
        if (existing != null)
        {
            _name.Text = existing.Name;
            _level.Value = existing.RequiredLevel;
            _quantity.Value = existing.OutputQuantity;
            _xp.Value = existing.CompletionExperience;
            _draft.AddRange(existing.Requirements ?? []);
            var selected = _outputItem.Items.Cast<ItemChoice>().FirstOrDefault(item => item.Id == existing.OutputItemId);
            if (selected != null) _outputItem.SelectedItem = selected;
            var unlock = _unlockVariable.Items.Cast<VariableChoice>()
                .FirstOrDefault(variable => variable.Id == existing.UnlockPlayerVariableId);
            if (unlock != null) _unlockVariable.SelectedItem = unlock;
        }
        else
        {
            _level.Value = 1;
            _quantity.Value = 1;
            _xp.Value = 25;
        }

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 8; ++i) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddRow(root, 0, "Recipe name", _name);
        AddRow(root, 1, "Required level", _level);
        AddRow(root, 2, "Output item", _outputItem);

        var reward = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        reward.Controls.Add(new Label { Text = "Quantity", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        reward.Controls.Add(_quantity);
        reward.Controls.Add(new Label { Text = "Completion XP", AutoSize = true, Margin = new Padding(14, 8, 3, 3) });
        reward.Controls.Add(_xp);
        AddRow(root, 3, "Reward", reward);
        AddRow(root, 4, "Event unlock", _unlockVariable);

        var reqControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        reqControls.Controls.Add(_family);
        reqControls.Controls.Add(_tier);
        reqControls.Controls.Add(_needed);
        var add = new Button { Text = "Add requirement", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        add.Click += (_, _) => AddRequirement();
        remove.Click += (_, _) =>
        {
            if (_requirements.SelectedItem is RequirementChoice choice) _draft.Remove(choice.Requirement);
            RefreshRequirements();
        };
        reqControls.Controls.Add(add);
        reqControls.Controls.Add(remove);
        AddRow(root, 5, "Requirement", reqControls);
        AddRow(root, 6, "Requirements", _requirements);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(450, 0),
            Text = "Tier 1 = Shard, 2 = Extract, 3 = Essence, 4 = Soul. " +
                   "Optional Event unlock uses a BOOLEAN Player Variable. Set that variable to TRUE from any event or quest completion event to unlock the recipe for that character."
        };
        AddRow(root, 7, "Rules", help);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", Width = 100 };
        save.Click += (_, _) => SaveRecipe(existing?.Id ?? Guid.NewGuid());
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        RefreshRequirements();
    }

    private void FillItems()
    {
        _outputItem.Items.Clear();
        _outputItem.Items.Add(new ItemChoice(Guid.Empty, "Choose output item..."));
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            _outputItem.Items.Add(new ItemChoice(
                item.Id,
                string.IsNullOrWhiteSpace(item.Folder) ? item.Name : $"[{item.Folder}] / {item.Name}"
            ));
        }
        _outputItem.SelectedIndex = 0;
    }

    private void FillUnlockVariables()
    {
        _unlockVariable.Items.Clear();
        _unlockVariable.Items.Add(new VariableChoice(Guid.Empty, "No event unlock required"));
        var names = PlayerVariableDescriptor.GetNamesByType(VariableDataType.Boolean);
        for (var index = 0; index < names.Length; ++index)
        {
            _unlockVariable.Items.Add(new VariableChoice(
                PlayerVariableDescriptor.IdFromList(index, VariableDataType.Boolean),
                names[index]
            ));
        }

        _unlockVariable.SelectedIndex = 0;
    }

    private void AddRequirement()
    {
        if (_family.SelectedItem is not PotionFamily family || _tier.SelectedItem is not int tier) return;
        var requirement = new PotionRequirement(family, tier, (int)_needed.Value);
        if (!requirement.IsValid || _draft.Contains(requirement) || _draft.Count >= 6) return;
        _draft.Add(requirement);
        RefreshRequirements();
    }

    private void RefreshRequirements()
    {
        _requirements.Items.Clear();
        foreach (var requirement in _draft)
        {
            _requirements.Items.Add(new RequirementChoice(
                requirement,
                $"{requirement.Needed} x {requirement.Family} {TierName(requirement.Level)}"
            ));
        }
    }

    private void SaveRecipe(Guid id)
    {
        if (_outputItem.SelectedItem is not ItemChoice item || item.Id == Guid.Empty)
        {
            MessageBox.Show(this, "Choose an output item.", "Potion Recipe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var definition = new PotionRecipeDefinition(
            id,
            _name.Text.Trim(),
            (int)_level.Value,
            item.Id,
            (int)_quantity.Value,
            (int)_xp.Value,
            _draft.ToArray(),
            (_unlockVariable.SelectedItem as VariableChoice)?.Id ?? Guid.Empty
        );

        if (!definition.IsStructurallyValid)
        {
            MessageBox.Show(this, "The recipe is incomplete or invalid. Add at least one requirement.", "Potion Recipe",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = definition;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string TierName(int level) => level switch
    {
        1 => "Shard",
        2 => "Extract",
        3 => "Essence",
        _ => "Soul",
    };

    private static void AddRow(TableLayoutPanel layout, int row, string text, Control control)
    {
        layout.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(3, 8, 3, 8) }, 0, row);
        control.Margin = new Padding(3, 6, 3, 6);
        layout.Controls.Add(control, 1, row);
    }
}
