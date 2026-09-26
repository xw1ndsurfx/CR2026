using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Framework.Core.Professions;

namespace Intersect.Editor.Forms.Editors;

internal sealed class CookingRecipeDialog : DarkForm
{
    private sealed record ItemChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ProfessionChoice(Guid Id, string Name, int MaximumLevel)
    {
        public override string ToString() => Name;
    }

    private sealed record VariableChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record IngredientChoice(CookingIngredient Ingredient, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record StageChoice(CookingStageDefinition Stage, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record OutputChoice(CookingQualityOutput Output, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly TextBox _name = new() { Width = 310, MaxLength = 96 };
    private readonly ComboBox _profession = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly NumericUpDown _requiredLevel = new() { Minimum = 1, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _xp = new() { Minimum = 1, Maximum = 2_000_000_000, Width = 120 };
    private readonly ComboBox _unlockVariable = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };

    private readonly CheckBox _solo = new() { Text = "Solo allowed", AutoSize = true };
    private readonly CheckBox _coop = new() { Text = "2-player co-op allowed", AutoSize = true };
    private readonly CheckBox _requireCoop = new() { Text = "Require two players", AutoSize = true };

    private readonly ListBox _ingredients = new() { Width = 520, Height = 110 };
    private readonly ComboBox _ingredientItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _ingredientQuantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 100 };

    private readonly ListBox _stages = new() { Width = 520, Height = 125 };
    private readonly ComboBox _stageType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _difficulty = new() { Minimum = 1, Maximum = 5, Width = 65 };
    private readonly NumericUpDown _duration = new() { Minimum = 4, Maximum = 60, Width = 70 };
    private readonly NumericUpDown _actions = new() { Minimum = 1, Maximum = 20, Width = 70 };
    private readonly ComboBox _assignment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly TextBox _actionSound = new() { Width = 150, MaxLength = CookingStageDefinition.MaximumSoundFileLength };
    private readonly TextBox _perfectSound = new() { Width = 150, MaxLength = CookingStageDefinition.MaximumSoundFileLength };
    private readonly TextBox _mishapSound = new() { Width = 150, MaxLength = CookingStageDefinition.MaximumSoundFileLength };

    private readonly ListBox _outputs = new() { Width = 520, Height = 100 };
    private readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ComboBox _outputItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly NumericUpDown _outputQuantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 90 };

    private readonly List<CookingIngredient> _ingredientDraft = [];
    private readonly List<CookingStageDefinition> _stageDraft = [];
    private readonly List<CookingQualityOutput> _outputDraft = [];

    public CookingRecipeDefinition? Result { get; private set; }

    public CookingRecipeDialog(CookingRecipeDefinition? existing = null)
    {
        Text = existing == null ? "Add Cooking Recipe" : "Edit Cooking Recipe";
        StartPosition = FormStartPosition.CenterParent;
        Width = 800;
        Height = 760;
        MinimumSize = new Size(720, 620);
        MinimizeBox = false;
        MaximizeBox = false;

        FillItems(_ingredientItem, "Choose ingredient...");
        FillItems(_outputItem, "Choose output item...");
        FillProfessions();
        FillUnlockVariables();

        foreach (var value in Enum.GetValues<CookingStageType>()) _stageType.Items.Add(value);
        foreach (var value in Enum.GetValues<CookingStageAssignment>()) _assignment.Items.Add(value);
        foreach (var value in Enum.GetValues<CookingQuality>()) _quality.Items.Add(value);
        _stageType.SelectedIndex = 0;
        _assignment.SelectedItem = CookingStageAssignment.Auto;
        _quality.SelectedItem = CookingQuality.Decent;

        _solo.Checked = true;
        _coop.Checked = true;
        _difficulty.Value = 2;
        _duration.Value = 12;
        _actions.Value = 5;
        _ingredientQuantity.Value = 1;
        _outputQuantity.Value = 1;
        _requiredLevel.Value = 1;
        _xp.Value = 50;

        if (existing != null)
        {
            _name.Text = existing.Name;
            _requiredLevel.Value = Math.Clamp(existing.RequiredProfessionLevel, 1, 500);
            _xp.Value = Math.Clamp(existing.ProfessionExperience, 1, 2_000_000_000);
            _solo.Checked = existing.AllowSolo;
            _coop.Checked = existing.AllowCoop;
            _requireCoop.Checked = existing.RequireCoop;
            _ingredientDraft.AddRange(existing.Ingredients ?? []);
            _stageDraft.AddRange(existing.Stages ?? []);
            _outputDraft.AddRange(existing.Outputs ?? []);

            var profession = _profession.Items.Cast<ProfessionChoice>()
                .FirstOrDefault(choice => choice.Id == existing.ProfessionId);
            if (profession != null) _profession.SelectedItem = profession;

            var unlock = _unlockVariable.Items.Cast<VariableChoice>()
                .FirstOrDefault(choice => choice.Id == existing.UnlockPlayerVariableId);
            if (unlock != null) _unlockVariable.SelectedItem = unlock;
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 14,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 14; ++i) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddRow(root, 0, "Recipe name", _name);
        AddRow(root, 1, "Profession", _profession);

        var progression = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        progression.Controls.Add(new Label { Text = "Required level", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        progression.Controls.Add(_requiredLevel);
        progression.Controls.Add(new Label { Text = "Base profession XP", AutoSize = true, Margin = new Padding(14, 8, 3, 3) });
        progression.Controls.Add(_xp);
        AddRow(root, 2, "Progression", progression);

        AddRow(root, 3, "Event unlock", _unlockVariable);

        var players = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        players.Controls.Add(_solo);
        players.Controls.Add(_coop);
        players.Controls.Add(_requireCoop);
        AddRow(root, 4, "Players", players);

        var ingredientControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        ingredientControls.Controls.Add(_ingredientItem);
        ingredientControls.Controls.Add(_ingredientQuantity);
        var ingredientAdd = new Button { Text = "Add ingredient", AutoSize = true };
        var ingredientRemove = new Button { Text = "Remove selected", AutoSize = true };
        ingredientAdd.Click += (_, _) => AddIngredient();
        ingredientRemove.Click += (_, _) =>
        {
            if (_ingredients.SelectedItem is IngredientChoice choice)
                _ingredientDraft.Remove(choice.Ingredient);
            RefreshIngredients();
        };
        ingredientControls.Controls.Add(ingredientAdd);
        ingredientControls.Controls.Add(ingredientRemove);
        AddRow(root, 5, "Ingredient", ingredientControls);
        AddRow(root, 6, "Ingredients", _ingredients);

        var stageControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        stageControls.Controls.Add(_stageType);
        stageControls.Controls.Add(new Label { Text = "Difficulty", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        stageControls.Controls.Add(_difficulty);
        stageControls.Controls.Add(new Label { Text = "Seconds", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        stageControls.Controls.Add(_duration);
        stageControls.Controls.Add(new Label { Text = "Actions", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        stageControls.Controls.Add(_actions);
        stageControls.Controls.Add(_assignment);
        var stageAdd = new Button { Text = "Add stage", AutoSize = true };
        var stageRemove = new Button { Text = "Remove selected", AutoSize = true };
        stageAdd.Click += (_, _) => AddStage();
        stageRemove.Click += (_, _) =>
        {
            if (_stages.SelectedItem is StageChoice choice)
                _stageDraft.Remove(choice.Stage);
            RefreshStages();
        };
        stageControls.Controls.Add(stageAdd);
        stageControls.Controls.Add(stageRemove);
        AddRow(root, 7, "Cooking stage", stageControls);

        var soundControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        soundControls.Controls.Add(new Label { Text = "Action", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        soundControls.Controls.Add(_actionSound);
        soundControls.Controls.Add(new Label { Text = "Perfect", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        soundControls.Controls.Add(_perfectSound);
        soundControls.Controls.Add(new Label { Text = "Mishap", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        soundControls.Controls.Add(_mishapSound);
        AddRow(root, 8, "Stage sounds", soundControls);

        AddRow(root, 9, "Stages", _stages);

        var outputControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        outputControls.Controls.Add(_quality);
        outputControls.Controls.Add(_outputItem);
        outputControls.Controls.Add(_outputQuantity);
        var outputAdd = new Button { Text = "Add / replace", AutoSize = true };
        var outputRemove = new Button { Text = "Remove selected", AutoSize = true };
        outputAdd.Click += (_, _) => AddOutput();
        outputRemove.Click += (_, _) =>
        {
            if (_outputs.SelectedItem is OutputChoice choice)
                _outputDraft.Remove(choice.Output);
            RefreshOutputs();
        };
        outputControls.Controls.Add(outputAdd);
        outputControls.Controls.Add(outputRemove);
        AddRow(root, 10, "Quality output", outputControls);
        AddRow(root, 11, "Outputs", _outputs);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(590, 0),
            Text =
                "Royal Kitchen uses real inventory ingredients. The server consumes them only when the run starts. " +
                "Quality is scored 0-100: Burnt <40, Decent 40-69, Great 70-89, Perfect 90-100. " +
                "Auto stage assignment alternates players in co-op. Partner/Both stages require co-op.",
        };
        AddRow(root, 12, "Rules", help);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
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

        _requireCoop.CheckedChanged += (_, _) =>
        {
            if (_requireCoop.Checked) _coop.Checked = true;
        };
        _coop.CheckedChanged += (_, _) =>
        {
            if (!_coop.Checked) _requireCoop.Checked = false;
        };
        _profession.SelectedIndexChanged += (_, _) =>
        {
            if (_profession.SelectedItem is ProfessionChoice choice)
                _requiredLevel.Maximum = Math.Max(1, choice.MaximumLevel);
        };

        RefreshIngredients();
        RefreshStages();
        RefreshOutputs();
    }

    private void FillProfessions()
    {
        _profession.Items.Clear();
        foreach (var profession in (ProfessionConfiguration.Instance.Professions ?? [])
                     .Where(value => value.IsStructurallyValid)
                     .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
        {
            _profession.Items.Add(new ProfessionChoice(profession.Id, profession.Name, profession.MaximumLevel));
        }

        if (_profession.Items.Count > 0)
        {
            var cooking = _profession.Items.Cast<ProfessionChoice>()
                .FirstOrDefault(value => value.Name.Equals("Cooking", StringComparison.OrdinalIgnoreCase));
            _profession.SelectedItem = cooking ?? _profession.Items[0];
        }
    }

    private static void FillItems(ComboBox picker, string placeholder)
    {
        picker.Items.Clear();
        picker.Items.Add(new ItemChoice(Guid.Empty, placeholder));
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            picker.Items.Add(
                new ItemChoice(
                    item.Id,
                    string.IsNullOrWhiteSpace(item.Folder) ? item.Name : $"[{item.Folder}] / {item.Name}"
                )
            );
        }
        picker.SelectedIndex = 0;
    }

    private void FillUnlockVariables()
    {
        _unlockVariable.Items.Clear();
        _unlockVariable.Items.Add(new VariableChoice(Guid.Empty, "No event unlock required"));
        var names = PlayerVariableDescriptor.GetNamesByType(VariableDataType.Boolean);
        for (var index = 0; index < names.Length; ++index)
        {
            _unlockVariable.Items.Add(
                new VariableChoice(
                    PlayerVariableDescriptor.IdFromList(index, VariableDataType.Boolean),
                    names[index]
                )
            );
        }
        _unlockVariable.SelectedIndex = 0;
    }

    private void AddIngredient()
    {
        if (_ingredientItem.SelectedItem is not ItemChoice item || item.Id == Guid.Empty)
            return;

        _ingredientDraft.RemoveAll(value => value.ItemId == item.Id);
        if (_ingredientDraft.Count >= 12) return;
        _ingredientDraft.Add(new CookingIngredient(item.Id, (int)_ingredientQuantity.Value));
        RefreshIngredients();
    }

    private void AddStage()
    {
        if (_stageType.SelectedItem is not CookingStageType type ||
            _assignment.SelectedItem is not CookingStageAssignment assignment)
            return;

        if (_stageDraft.Count >= 12) return;
        _stageDraft.Add(
            new CookingStageDefinition(
                type,
                (int)_difficulty.Value,
                (int)_duration.Value,
                (int)_actions.Value,
                assignment,
                _actionSound.Text.Trim(),
                _perfectSound.Text.Trim(),
                _mishapSound.Text.Trim()
            )
        );
        RefreshStages();
    }

    private void AddOutput()
    {
        if (_quality.SelectedItem is not CookingQuality quality ||
            _outputItem.SelectedItem is not ItemChoice item ||
            item.Id == Guid.Empty)
            return;

        _outputDraft.RemoveAll(value => value.Quality == quality);
        _outputDraft.Add(new CookingQualityOutput(quality, item.Id, (int)_outputQuantity.Value));
        RefreshOutputs();
    }

    private void RefreshIngredients()
    {
        _ingredients.Items.Clear();
        foreach (var ingredient in _ingredientDraft)
        {
            _ingredients.Items.Add(
                new IngredientChoice(
                    ingredient,
                    $"{ingredient.Quantity:N0} x {ItemDescriptor.GetName(ingredient.ItemId)}"
                )
            );
        }
    }

    private void RefreshStages()
    {
        _stages.Items.Clear();
        for (var index = 0; index < _stageDraft.Count; ++index)
        {
            var stage = _stageDraft[index];
            _stages.Items.Add(
                new StageChoice(
                    stage,
                    $"{index + 1}. {stage.Type} | Difficulty {stage.Difficulty}/5 | " +
                    $"{stage.DurationSeconds}s | {stage.RequiredActions} action(s) | {stage.Assignment} | " +
                    $"SFX: {SoundSummary(stage)}"
                )
            );
        }
    }

    private static string SoundSummary(CookingStageDefinition stage)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(stage.ActionSound)) values.Add("action");
        if (!string.IsNullOrWhiteSpace(stage.PerfectSound)) values.Add("perfect");
        if (!string.IsNullOrWhiteSpace(stage.MishapSound)) values.Add("mishap");
        return values.Count == 0 ? "none" : string.Join("/", values);
    }

    private void RefreshOutputs()
    {
        _outputs.Items.Clear();
        foreach (var output in _outputDraft.OrderBy(value => value.Quality))
        {
            _outputs.Items.Add(
                new OutputChoice(
                    output,
                    $"{output.Quality}: {output.Quantity:N0} x {ItemDescriptor.GetName(output.ItemId)}"
                )
            );
        }
    }

    private void SaveRecipe(Guid id)
    {
        if (_profession.SelectedItem is not ProfessionChoice profession)
        {
            MessageBox.Show(
                this,
                "Create/select a profession first (normally Cooking).",
                "Cooking Recipe",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        var definition = new CookingRecipeDefinition(
            id,
            _name.Text.Trim(),
            profession.Id,
            (int)_requiredLevel.Value,
            (long)_xp.Value,
            _ingredientDraft.ToArray(),
            _stageDraft.ToArray(),
            _outputDraft.ToArray(),
            _solo.Checked,
            _coop.Checked,
            _requireCoop.Checked,
            (_unlockVariable.SelectedItem as VariableChoice)?.Id ?? Guid.Empty
        );

        if (!definition.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "The recipe is incomplete. Add ingredients, at least one cooking stage, at least one quality output, and choose a valid player mode.",
                "Cooking Recipe",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        Result = definition;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string text, Control control)
    {
        layout.Controls.Add(
            new Label { Text = text, AutoSize = true, Margin = new Padding(3, 8, 3, 8) },
            0,
            row
        );
        control.Margin = new Padding(3, 6, 3, 6);
        layout.Controls.Add(control, 1, row);
    }
}
