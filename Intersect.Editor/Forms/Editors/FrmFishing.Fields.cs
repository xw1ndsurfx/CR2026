using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmFishing
{
    private sealed record FileChoice(string Path, string Caption) { public override string ToString() => Caption; }
    private static string ItemName(Guid id) => id == Guid.Empty ? T("Unassigned item") :
        ItemDescriptor.Get(id)?.Name ?? string.Format(T("Missing item: {0}"), id);
    private static IEnumerable<Choice> ItemChoices() => ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
        .OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name));
    private static void AddButton(Control parent, string title, Action action)
    {
        var button = new Button { Text = T(title), AutoSize = true, Margin = new Padding(3) };
        button.Click += (_, _) => action(); parent.Controls.Add(button);
    }
    private static void Row(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = T(label), AutoSize = true, MaximumSize = new(225, 0),
            Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 4, 8) }, 0, row);
        control.Margin = new Padding(4, 5, 4, 5); table.Controls.Add(control, 1, row);
    }
    private static void Note(TableLayoutPanel table, string message)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = T(message), AutoSize = true, MaximumSize = new(780, 0),
            Margin = new Padding(4, 12, 4, 12) };
        table.Controls.Add(label, 0, row); table.SetColumnSpan(label, 2);
    }
    private void TextField(TableLayoutPanel table, string title, string value, Action<string> apply)
    {
        var box = new TextBox { Text = value, Dock = DockStyle.Fill, MaxLength = 96 };
        box.TextChanged += (_, _) => { if (!_loading) { apply(box.Text); Changed(); } }; Row(table, title, box);
    }
    private void Check(TableLayoutPanel table, string title, bool value, Action<bool> apply)
    {
        var box = new CheckBox { Checked = value, AutoSize = true };
        box.CheckedChanged += (_, _) => { if (!_loading) { apply(box.Checked); Changed(); } }; Row(table, title, box);
    }
    private void Number(TableLayoutPanel table, string title, double value, double min, double max,
        Action<decimal> apply, int decimals = 0) => NumberExact(table, title,
            (decimal)Math.Clamp(double.IsFinite(value) ? value : min, min, max), (decimal)min, (decimal)max, apply, decimals);
    private void NumberExact(TableLayoutPanel table, string title, decimal value, decimal min, decimal max,
        Action<decimal> apply, int decimals = 0)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max),
            DecimalPlaces = decimals, Width = 190, ThousandsSeparator = true, Increment = decimals > 0 ? .01m : 1 };
        box.ValueChanged += (_, _) => { if (!_loading) { apply(box.Value); Changed(); } }; Row(table, title, box);
    }
    private void GuidField(TableLayoutPanel table, string title, Guid value, IEnumerable<Choice> choices,
        Action<Guid> apply, string none = "Select a resource", bool allowEmpty = true)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
        if (allowEmpty || value == Guid.Empty) box.Items.Add(new Choice(Guid.Empty, T(none)));
        foreach (var choice in choices.DistinctBy(c => c.Id)) box.Items.Add(choice);
        if (value != Guid.Empty && !box.Items.OfType<Choice>().Any(c => c.Id == value))
            box.Items.Add(new Choice(value, string.Format(T("Missing resource: {0}"), value)));
        box.SelectedItem = box.Items.OfType<Choice>().First(c => c.Id == value);
        box.SelectedIndexChanged += (_, _) => { if (!_loading && box.SelectedItem is Choice choice) { apply(choice.Id); Changed(); } };
        Row(table, title, box);
    }
    private void EnumField<TEnum>(TableLayoutPanel table, string title, TEnum value, Action<TEnum> apply) where TEnum : struct, Enum
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var entry in Enum.GetValues<TEnum>()) box.Items.Add(new Named<TEnum>(entry, e => T(e.ToString())));
        box.SelectedItem = box.Items.OfType<Named<TEnum>>().FirstOrDefault(c => EqualityComparer<TEnum>.Default.Equals(c.Value, value));
        box.SelectedIndexChanged += (_, _) => { if (!_loading && box.SelectedItem is Named<TEnum> choice) { apply(choice.Value); Changed(); } };
        Row(table, title, box);
    }
    private static IEnumerable<string> GuiRoots()
    {
        return new[] { Path.Combine(AppContext.BaseDirectory, "resources", "gui"), Path.Combine("resources", "gui"), "resources" }
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);
    }
    private static IEnumerable<string> GuiFiles(string folder)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in GuiRoots())
        {
            var directory = Path.Combine(root, "fishing", folder);
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg"))
                paths.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            if (folder == "backgrounds")
            {
                var coral = Path.Combine(root, "fishing", "gui", "coral_foreground.png");
                if (File.Exists(coral)) paths.Add("fishing/gui/coral_foreground.png");
            }
        }
        return paths.Order(StringComparer.OrdinalIgnoreCase);
    }
    private static string? FindGuiFile(string relative) => GuiRoots().Select(root => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)))
        .FirstOrDefault(File.Exists);
    private void FileField(TableLayoutPanel table, string title, string value, string folder, Action<string> apply, bool optional = false)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 550 };
        box.Items.Add(new FileChoice("", T(optional ? "None" : "Select an image")));
        foreach (var path in GuiFiles(folder)) box.Items.Add(new FileChoice(path, path));
        if (!box.Items.OfType<FileChoice>().Any(c => c.Path.Equals(value, StringComparison.OrdinalIgnoreCase)))
            box.Items.Add(new FileChoice(value, string.Format(T("Missing resource: {0}"), value)));
        box.SelectedItem = box.Items.OfType<FileChoice>().First(c => c.Path.Equals(value, StringComparison.OrdinalIgnoreCase));
        box.SelectedIndexChanged += (_, _) => { if (!_loading && box.SelectedItem is FileChoice choice) { apply(choice.Path); Changed(); } };
        Row(table, title, box);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        AddButton(buttons, "Preview image", () => {
            if (box.SelectedItem is not FileChoice choice || FindGuiFile(choice.Path) is not { } path)
            { _status.Text = T("Image is not available in the local resources."); return; }
            try
            {
                using var source = System.Drawing.Image.FromFile(path);
                using var dialog = new Form { Text = T("Preview image"), Size = new(860, 590), StartPosition = FormStartPosition.CenterParent };
                using var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = source };
                dialog.Controls.Add(picture); dialog.ShowDialog(this);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
            { _status.Text = T("Image could not be opened."); }
        }); Row(table, "", buttons);
    }
    private void BuildPaperdollFields(TableLayoutPanel table, FishingToolDefinition tool)
    {
        var actions = new FlowLayoutPanel { AutoSize = true };
        AddButton(actions, "Use supplied Fishing Net profile", () => { tool.WorldProfile = FishingWorldProfile.Net(); Changed(); BuildSelectedPage(); });
        AddButton(actions, "Create empty world profile", () => {
            tool.WorldProfile = new FishingWorldProfile { Enabled = true,
                Clips = Enum.GetValues<FishingWorldAction>().Where(a => a != FishingWorldAction.Stop)
                    .ToDictionary(a => a, _ => new FishingWorldClip()) };
            Changed(); BuildSelectedPage();
        });
        Row(table, "World paperdoll profile", actions);
        var profile = tool.WorldProfile;
        if (profile is null) return;
        Check(table, "Enable world paperdoll", profile.Enabled, v => profile.Enabled = v);
        Number(table, "Frame width", profile.FrameWidth, 16, 1024, v => profile.FrameWidth = (int)v);
        Number(table, "Frame height", profile.FrameHeight, 16, 1024, v => profile.FrameHeight = (int)v);
        Number(table, "Frames per second", profile.FramesPerSecond, 1, 60, v => profile.FramesPerSecond = (int)v);
        Number(table, "Scale", profile.Scale, .1, 4, v => profile.Scale = (double)v, 2);
        Number(table, "Offset X", profile.OffsetX, -512, 512, v => profile.OffsetX = (int)v);
        Number(table, "Offset Y", profile.OffsetY, -512, 512, v => profile.OffsetY = (int)v);
        for (var i = 0; i < 4; i++)
        {
            var index = i;
            Number(table, string.Format(T("Direction {0}: row"), i), profile.DirectionRows[i], 0, 7, v => profile.DirectionRows[index] = (int)v);
        }
        foreach (var action in Enum.GetValues<FishingWorldAction>().Where(a => a != FishingWorldAction.Stop))
        {
            if (!profile.Clips.TryGetValue(action, out var clip)) continue;
            var current = clip; Note(table, action.ToString());
            FileField(table, "Front layer", current.Front, "paperdolls", v => current.Front = v, true);
            FileField(table, "Back layer", current.Back, "paperdolls", v => current.Back = v, true);
            Number(table, "Frame count", current.Frames, 1, 32, v => current.Frames = (int)v);
            Check(table, "Loop", current.Loop, v => current.Loop = v);
            var body = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            foreach (var name in new[] { "Normal", "Idle", "Attack", "Shoot", "Cast", "Weapon" })
                body.Items.Add(new FileChoice(name, T(name)));
            body.SelectedItem = body.Items.OfType<FileChoice>().FirstOrDefault(c => c.Path == current.BodyAnimation);
            body.SelectedIndexChanged += (_, _) => { if (!_loading && body.SelectedItem is FileChoice choice) { current.BodyAnimation = choice.Path; Changed(); } };
            Row(table, "Body animation", body);
        }
    }
}
