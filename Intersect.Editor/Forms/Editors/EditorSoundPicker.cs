namespace Intersect.Editor.Forms.Editors;

/// <summary>Effect sounds only; absent assets remain selected so editing never clears them silently.</summary>
public sealed class EditorSoundPicker : ComboBox
{
    private sealed record Choice(string File, string Label)
    {
        public override string ToString() => Label;
    }
    private readonly string _directory;
    public string FileName
    {
        get => (SelectedItem as Choice)?.File ?? string.Empty;
        set
        {
            value ??= string.Empty;
            var choice = Items.Cast<Choice>().FirstOrDefault(c => string.Equals(c.File, value, StringComparison.OrdinalIgnoreCase));
            if (choice == null) { choice = new(value, "Missing sound: " + value); Items.Add(choice); }
            SelectedItem = choice;
        }
    }
    public EditorSoundPicker() : this(Path.Combine("resources", "sounds")) { }
    public EditorSoundPicker(string directory)
    {
        _directory = directory;
        DropDownStyle = ComboBoxStyle.DropDownList;
        DropDownWidth = 440;
        Width = 210;
        ReloadSounds();
        DropDown += (_, _) => ReloadSounds();
    }
    public void ReloadSounds()
    {
        var selected = FileName;
        BeginUpdate();
        try
        {
            Items.Clear();
            Items.Add(new Choice("", "None"));
            if (Directory.Exists(_directory))
                foreach (var file in Directory.EnumerateFiles(_directory)
                    .Where(f => string.Equals(Path.GetExtension(f), ".wav", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName).OfType<string>().OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    Items.Add(new Choice(file, file));
            FileName = selected;
        }
        finally { EndUpdate(); }
    }
}
