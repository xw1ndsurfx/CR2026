using System.Drawing.Imaging;
using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms;

public sealed partial class FrmCharacterGenerator
{
    private sealed record BaseChoice(PartFamily Family)
    {
        public override string ToString() => Family.Name;
    }

    private static string BaseExportText(string text) => QualityOfLifeText.Get(text,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private static bool BaseFitsSex(string name, bool female)
    {
        if (name.StartsWith("Female_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("F_", StringComparison.OrdinalIgnoreCase)) return female;
        if (name.StartsWith("Male_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("M_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("B_", StringComparison.OrdinalIgnoreCase)) return !female;
        return true;
    }

    private static bool IsLegacyBodyName(string name)
    {
        name = GenderNeutralName(name);
        return name.Equals("Base", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Base_", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Body", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Body_", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<PartFamily> GetClassBaseFamilies() => _partsByCategory
        .Where(pair => pair.Key.Equals("Base", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("Body", StringComparison.OrdinalIgnoreCase)
            || pair.Key.Equals("Female", StringComparison.OrdinalIgnoreCase))
        .SelectMany(pair => pair.Value.Where(family => !pair.Key.Equals("Female", StringComparison.OrdinalIgnoreCase) || IsLegacyBodyName(family.Name)))
        .GroupBy(family => family.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First());

    private void ExportClassBases()
    {
        var bases = GetClassBaseFamilies().ToArray();
        var male = bases.Where(f => BaseFitsSex(f.Name, false)).OrderBy(f => f.Name).ToArray();
        var female = bases.Where(f => BaseFitsSex(f.Name, true)).OrderBy(f => f.Name).ToArray();
        if (male.Length == 0 || female.Length == 0)
        {
            MessageBox.Show(this, BaseExportText("Import a male base and a female base into Base or Body first."),
                BaseExportText("Class base sprites"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new DarkForm { Text = BaseExportText("Class base sprites"), Width = 660, Height = 360,
            MinimumSize = new Size(560, 340), StartPosition = FormStartPosition.CenterParent };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        var name = new TextBox { Dock = DockStyle.Fill, Text = MakeFileStem(_exportName.Text) };
        if (string.IsNullOrWhiteSpace(name.Text)) name.Text = "class_base";
        ComboBox Choices(IEnumerable<PartFamily> families)
        {
            var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var family in families) box.Items.Add(new BaseChoice(family));
            box.SelectedIndex = 0;
            return box;
        }
        var maleChoice = Choices(male);
        var femaleChoice = Choices(female);
        var selected = _selectedPartByCategory.GetValueOrDefault("Base");
        if (TryGetSelectedPaperdoll(out var category, out var selectedName, out _) &&
            (category.Equals("Base", StringComparison.OrdinalIgnoreCase) || category.Equals("Body", StringComparison.OrdinalIgnoreCase)
                || category.Equals("Female", StringComparison.OrdinalIgnoreCase) && IsLegacyBodyName(selectedName)))
            selected = selectedName;
        if (!string.IsNullOrWhiteSpace(selected))
        {
            foreach (var box in new[] { maleChoice, femaleChoice })
            {
                var match = box.Items.Cast<BaseChoice>().FirstOrDefault(c => c.Family.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))
                    ?? box.Items.Cast<BaseChoice>().FirstOrDefault(c => GenderNeutralName(c.Family.Name).Equals(GenderNeutralName(selected), StringComparison.OrdinalIgnoreCase));
                if (match != null) box.SelectedItem = match;
            }
        }
        void Row(int row, string label, Control control)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = BaseExportText(label), AutoSize = true, Margin = new Padding(3, 8, 3, 8) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
        Row(0, "Export name", name);
        Row(1, "Male base", maleChoice);
        Row(2, "Female base", femaleChoice);
        var hint = new Label { AutoSize = true, MaximumSize = new Size(600, 0), Padding = new Padding(3, 12, 3, 12),
            Text = BaseExportText("Exports only the two bodies and their six animations to resources/entities. Clothing, hair and equipment stay separate. Choose the male and female sprites in Class Editor afterwards.") };
        layout.Controls.Add(hint, 0, 3); layout.SetColumnSpan(hint, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = BaseExportText("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var export = new Button { Text = BaseExportText("Export both bases"), AutoSize = true };
        export.Click += (_, _) =>
        {
            if (!ValidBaseStem(name.Text.Trim()) || maleChoice.SelectedItem is not BaseChoice m || femaleChoice.SelectedItem is not BaseChoice f || m.Family == f.Family)
            {
                MessageBox.Show(dialog, BaseExportText("Enter a valid filename and select distinct male and female bases."), dialog.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            dialog.DialogResult = DialogResult.OK;
        };
        actions.Controls.Add(cancel); actions.Controls.Add(export);
        layout.Controls.Add(actions, 0, 4); layout.SetColumnSpan(actions, 2);
        dialog.Controls.Add(layout); dialog.AcceptButton = export; dialog.CancelButton = cancel;
        Styling.EditorTheme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var stem = name.Text.Trim();
        var exists = ClassBaseFilenames(stem).Any(file => File.Exists(Path.Combine(_entitiesRoot, file)));
        if (exists && MessageBox.Show(this, BaseExportText("Some class base files already exist. Replace this male/female export?"),
                BaseExportText("Class base sprites"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            WriteClassBaseSprites(((BaseChoice)maleChoice.SelectedItem!).Family, ((BaseChoice)femaleChoice.SelectedItem!).Family,
                _entitiesRoot, stem, exists);
            GameContentManager.ReloadEntityTextures();
            _afterExport?.Invoke();
            _status.Text = string.Format(BaseExportText("Exported class bases: {0}_male.png and {0}_female.png (+ animation variants)."), stem);
            MessageBox.Show(this, _status.Text + "\n\n" + BaseExportText("Choose these two sprites for the male and female appearances in Class Editor."),
                BaseExportText("Class base sprites"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, string.Format(BaseExportText("Class base export failed: {0}"), error.Message),
                BaseExportText("Class base sprites"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool ValidBaseStem(string stem) => !string.IsNullOrWhiteSpace(stem) && stem == Path.GetFileName(stem) &&
        stem.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !stem.EndsWith('.') && stem is not "." and not "..";

    private static IEnumerable<string> ClassBaseFilenames(string stem) =>
        from sex in new[] { "male", "female" }
        from animation in AnimationDefinitions
        select stem + "_" + sex + animation.Suffix + ".png";

    private static void WriteClassBaseSprites(PartFamily male, PartFamily female, string directory, string stem, bool overwrite)
    {
        if (!ValidBaseStem(stem) || male == female) throw new InvalidDataException(BaseExportText("Invalid class base export."));
        var rendered = new List<(string File, Bitmap Bitmap)>();
        var staged = new List<(string Temporary, string Destination)>();
        try
        {
            // Resolve both actual body families completely before creating any output.
            foreach (var pair in new[] { (Family: male, Sex: "male"), (Family: female, Sex: "female") })
            foreach (var animation in AnimationDefinitions)
            {
                var bitmap = RenderPaperdollFamily(pair.Family, animation.Animation)
                    ?? throw new InvalidDataException(string.Format(BaseExportText("Missing body animation: {0}"), animation.Label));
                rendered.Add((stem + "_" + pair.Sex + animation.Suffix + ".png", bitmap));
            }
            if (!overwrite && rendered.Any(item => File.Exists(Path.Combine(directory, item.File))))
                throw new IOException(BaseExportText("Class base export already exists."));
            Directory.CreateDirectory(directory);
            foreach (var item in rendered)
            {
                var destination = Path.Combine(directory, item.File);
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                staged.Add((temporary, destination));
                item.Bitmap.Save(temporary, ImageFormat.Png);
            }
            foreach (var item in staged) File.Move(item.Temporary, item.Destination, overwrite);
        }
        finally
        {
            foreach (var item in rendered) item.Bitmap.Dispose();
            foreach (var item in staged) if (File.Exists(item.Temporary)) File.Delete(item.Temporary);
        }
    }
}
