using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc
{
    private readonly DarkGroupBox _grpNpcSounds = new();
    private readonly ComboBox _npcDeathSound = new() { Name = "NpcDeathSound", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _npcProximitySound = new() { Name = "NpcProximitySound", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _npcSoundRadius = new() { Name = "NpcSoundRadius", Minimum = 1, Maximum = 100 };
    private bool _loadingNpcSounds;

    private void BuildNpcSoundControls()
    {
        _grpNpcSounds.Text = TelegraphText("Sound ambience");
        _grpNpcSounds.Size = new Size(302, 290);
        void Add(string caption, Control control, int y)
        {
            _grpNpcSounds.Controls.Add(new Label { Text = TelegraphText(caption), AutoSize = false,
                Location = new System.Drawing.Point(12, y), Size = new Size(278, 23), ForeColor = System.Drawing.Color.Gainsboro });
            control.SetBounds(12, y + 24, 278, 26);
            _grpNpcSounds.Controls.Add(control);
        }
        Add("Death sound", _npcDeathSound, 24);
        Add("Player proximity sound", _npcProximitySound, 84);
        Add("Trigger distance (tiles)", _npcSoundRadius, 144);
        _grpNpcSounds.Controls.Add(new Label { Text = TelegraphText("Plays on entering range; resets after leaving. Empty sound disables it."),
            Location = new System.Drawing.Point(12, 208), Size = new Size(278, 66), ForeColor = System.Drawing.Color.Silver });
        _npcDeathSound.SelectedIndexChanged += (_, _) =>
        {
            if (!_loadingNpcSounds && mEditorItem != null) mEditorItem.DeathSound = _npcDeathSound.SelectedItem as string ?? "";
        };
        _npcProximitySound.SelectedIndexChanged += (_, _) =>
        {
            if (!_loadingNpcSounds && mEditorItem != null) mEditorItem.ProximitySound = _npcProximitySound.SelectedItem as string ?? "";
        };
        _npcSoundRadius.ValueChanged += (_, _) =>
        {
            if (!_loadingNpcSounds && mEditorItem != null) mEditorItem.ProximitySoundRadius = (int)_npcSoundRadius.Value;
        };
    }

    private void LoadNpcSounds()
    {
        if (mEditorItem == null) return;
        _loadingNpcSounds = true;
        try
        {
            var folder = Path.Combine("resources", "sounds");
            var names = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.wav").Select(Path.GetFileName).OfType<string>().ToArray() : [];
            void Fill(ComboBox box, string? current)
            {
                box.Items.Clear();
                box.Items.Add("");
                box.Items.AddRange(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
                current ??= "";
                if (!box.Items.Contains(current)) box.Items.Add(current);
                box.SelectedItem = current;
                Styling.SearchableChoices.SetResourceDirectory(box, folder);
            }
            Fill(_npcDeathSound, mEditorItem.DeathSound);
            Fill(_npcProximitySound, mEditorItem.ProximitySound);
            _npcSoundRadius.Value = Math.Clamp(mEditorItem.ProximitySoundRadius, 1, 100);
        }
        finally { _loadingNpcSounds = false; }
    }
}
