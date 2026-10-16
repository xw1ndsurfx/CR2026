using DarkUI.Forms;
using Intersect.Editor.Content;

namespace Intersect.Editor.Forms;

public sealed partial class FrmSoundImport
{
    private SoundImportKind TargetKind => _targetKind.SelectedIndex == 1 ? SoundImportKind.VoiceSound : SoundImportKind.Sound;

    private void BuildTypeBar(TableLayoutPanel root)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 0), WrapContents = false };
        bar.Controls.Add(new Label { Text = "Library:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        _libraryKind.DropDownStyle = ComboBoxStyle.DropDownList; _libraryKind.Width = 180;
        _libraryKind.Items.AddRange(["All audio", "Sound (effects)", "Voice Sound (dialogue)"]);
        _libraryKind.SelectedIndex = 1;
        _libraryKind.SelectedIndexChanged += (_, _) =>
        {
            if (_libraryKind.SelectedIndex > 0) _targetKind.SelectedIndex = _libraryKind.SelectedIndex - 1;
            StopPlayback(); _selectionCancellation?.Cancel(); _selectedAsset = null;
            _name.Clear(); _details.Text = "Select an audio file to preview it.";
            _page = 0; PopulateSoundList(); UpdateFavoriteButton();
        };
        bar.Controls.Add(_libraryKind);
        bar.Controls.Add(new Label { Text = "Import as:", AutoSize = true, Margin = new Padding(18, 6, 8, 0) });
        _targetKind.DropDownStyle = ComboBoxStyle.DropDownList; _targetKind.Width = 180;
        _targetKind.Items.AddRange(["Sound (effects)", "Voice Sound (dialogue)"]); _targetKind.SelectedIndex = 0;
        _targetKind.SelectedIndexChanged += (_, _) => UpdateDestination();
        bar.Controls.Add(_targetKind);
        _destination.AutoSize = true; _destination.Margin = new Padding(18, 6, 0, 0); bar.Controls.Add(_destination);
        UpdateDestination(); root.Controls.Add(bar, 0, 0);
    }

    private void UpdateDestination() => _destination.Text = $"Game folder: resources/{SoundImporter.Folder(TargetKind)}";

    private bool ChooseImportKind(out SoundImportKind kind)
    {
        kind = TargetKind;
        if (_operationCancellation != null) return false;
        using var dialog = new DarkForm
        {
            Text = "Choose audio type", Size = new Size(530, 250), StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = "What kind of audio are you importing?", AutoSize = true });
        var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 460 };
        choice.Items.AddRange(["Sound — effects (resources/sounds)", "Voice Sound — dialogue (resources/voicesound)"]);
        choice.SelectedIndex = (int)kind; layout.Controls.Add(choice);
        layout.Controls.Add(new Label { Text = "Files are staged in separate library folders, converted to WAV,\nand added to the selected game folder with IMPORT TO GAME.", AutoSize = true, Margin = new Padding(0, 12, 0, 12) });
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var next = new Button { Text = "Continue", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([next, cancel]); layout.Controls.Add(buttons); dialog.Controls.Add(layout);
        dialog.AcceptButton = next; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        kind = (SoundImportKind)choice.SelectedIndex;
        _targetKind.SelectedIndex = (int)kind; _libraryKind.SelectedIndex = (int)kind + 1;
        return true;
    }
}
