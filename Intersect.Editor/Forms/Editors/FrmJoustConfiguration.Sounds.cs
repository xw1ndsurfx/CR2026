using Intersect.Editor.Content;
using Intersect.Framework.Core.Jousting;
namespace Intersect.Editor.Forms.Editors;

public partial class FrmJoustConfiguration
{
    private readonly Dictionary<JoustSoundCue, ComboBox> _soundFiles = [];
    private readonly Dictionary<JoustSoundCue, NumericUpDown> _soundVolumes = [];
    private sealed record SoundChoice(string File, string Caption) { public override string ToString() => Caption; }
    private void BuildSounds(TabControl tabs)
    {
        var page = Page(tabs, "7. Sounds");
        Row(page, "", new Label { AutoSize = true, MaximumSize = new(320, 0), Text = T("Choose sounds from the game's sound resources. Empty means silent. Gallop and crowd loop automatically and stop with the activity. Volume is a percentage.") });
        foreach (var cue in Enum.GetValues<JoustSoundCue>())
        {
            var choice = new ComboBox { Name = "JoustSound" + cue, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 400 };
            var volume = new NumericUpDown { Name = "JoustSoundVolume" + cue, Minimum = 0, Maximum = 100, Value = 100, Dock = DockStyle.Fill };
            _soundFiles.Add(cue, choice); _soundVolumes.Add(cue, volume);
            Row(page, "Joust sound: " + cue, choice); Row(page, "Volume (%)", volume);
            void ChangedSound()
            {
                if (_loading || Arena is not { } arena || choice.SelectedItem is not SoundChoice selected) return;
                arena.Sounds.Cues[cue] = new() { File = selected.File, Volume = (int)volume.Value }; Changed();
            }
            choice.SelectedIndexChanged += (_, _) => ChangedSound(); volume.ValueChanged += (_, _) => ChangedSound();
        }
    }
    private void LoadSounds()
    {
        var names = (GameContentManager.SoundNames ?? []).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var (cue, choice) in _soundFiles)
        {
            var sound = Arena?.Sounds.Cues.GetValueOrDefault(cue) ?? new();
            choice.Items.Clear(); choice.Items.Add(new SoundChoice("", T("None")));
            foreach (var name in names) choice.Items.Add(new SoundChoice(name, name));
            if (sound.File.Length > 0 && !names.Contains(sound.File, StringComparer.OrdinalIgnoreCase))
                choice.Items.Add(new SoundChoice(sound.File, string.Format(T("Missing sound: {0}"), sound.File)));
            choice.SelectedItem = choice.Items.Cast<SoundChoice>().FirstOrDefault(c => c.File.Equals(sound.File, StringComparison.OrdinalIgnoreCase)) ?? choice.Items[0];
            _soundVolumes[cue].Value = sound.Volume;
        }
    }
}
