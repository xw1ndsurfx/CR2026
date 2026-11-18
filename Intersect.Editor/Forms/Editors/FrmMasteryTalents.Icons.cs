using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Archery;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmMasteryTalents
{
    private readonly Panel _skillIconPanel = new() { Dock = DockStyle.Bottom, Height = 100 };
    private readonly ComboBox _skillIcon = new() { Name = "WeaponSkillIcon", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly PictureBox _skillIconImage = new() { Dock = DockStyle.Right, Width = 82, SizeMode = PictureBoxSizeMode.Zoom };

    private Control BuildWeaponSkillIconPicker()
    {
        var choices = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3) };
        var directory = Path.Combine("resources", "spells");
        _skillIcon.Items.Add("");
        if (Directory.Exists(directory))
            _skillIcon.Items.AddRange(Directory.EnumerateFiles(directory).Where(path =>
                Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName).OfType<string>().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        SearchableChoices.SetResourceDirectory(_skillIcon, directory);
        choices.Controls.Add(_skillIcon);
        choices.Controls.Add(new Label { Text = T("Skill icon (resources/spells)"), Dock = DockStyle.Top, Height = 24 });
        _skillIconPanel.Controls.Add(choices); _skillIconPanel.Controls.Add(_skillIconImage);
        _skillIcon.SelectedIndexChanged += (_, _) =>
        {
            if (!_refreshing && _nodes.SelectedItem is MasteryTalent talent)
                talent.Skill.Icon = _skillIcon.SelectedItem as string ?? "";
            RefreshWeaponSkillIcon();
        };
        Disposed += (_, _) => { _skillIconImage.Image?.Dispose(); _skillIconImage.Image = null; };
        return _skillIconPanel;
    }

    private void SelectWeaponSkillIcon(MasteryTalent? selected)
    {
        _skillIconPanel.Visible = selected?.Skill.Enabled == true;
        var icon = selected?.Skill.Icon ?? "";
        if (!_skillIcon.Items.Contains(icon)) _skillIcon.Items.Add(icon);
        _skillIcon.SelectedItem = icon;
        RefreshWeaponSkillIcon();
    }

    private void RefreshWeaponSkillIcon()
    {
        using var preview = EditorObjectPreview.IconFile(_skillIcon.SelectedItem as string ?? "", "spells");
        var previous = _skillIconImage.Image;
        _skillIconImage.Image = preview.Render(); previous?.Dispose();
    }
}
