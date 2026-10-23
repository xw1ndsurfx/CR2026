namespace Intersect.Editor.Forms.Editors;

/// <summary>Effect sounds only; absent assets remain selected so editing never clears them silently.</summary>
public sealed class EditorSoundPicker : Styling.LogiklikComboBox
{
    private sealed record Choice(string File, string Label)
    {
        public override string ToString() => Label;
    }
    private readonly string _directory;
    private bool _reloading;
    public string FileName
    {
        get => (SelectedItem as Choice)?.File ?? string.Empty;
        set
        {
            value ??= string.Empty;
            var choice = Items.Cast<Choice>().FirstOrDefault(c => string.Equals(c.File, value, StringComparison.OrdinalIgnoreCase));
            if (choice == null) { choice = new(value, string.Format(T("Missing sound: {0}"), value)); Items.Add(choice); }
            SelectedItem = choice;
        }
    }
    public EditorSoundPicker() : this(Path.Combine("resources", "sounds")) { }
    public EditorSoundPicker(string directory)
    {
        _directory = directory;
        Styling.SearchableChoices.SetResourceDirectory(this, directory);
        DropDownStyle = ComboBoxStyle.DropDownList;
        DropDownWidth = 440;
        Width = 210;
        ReloadSounds();
        DropDown += (_, _) => ReloadSounds();
    }
    public void ReloadSounds()
    {
        var selected = FileName;
        _reloading = true;
        BeginUpdate();
        try
        {
            Items.Clear();
            Items.Add(new Choice("", T("None")));
            if (Directory.Exists(_directory))
                foreach (var file in Directory.EnumerateFiles(_directory)
                    .Where(f => string.Equals(Path.GetExtension(f), ".wav", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName).OfType<string>().OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    Items.Add(new Choice(file, file));
            FileName = selected;
        }
        finally { EndUpdate(); _reloading = false; }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (!_reloading) base.OnSelectedIndexChanged(e);
    }

    private static string T(string source) => Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
