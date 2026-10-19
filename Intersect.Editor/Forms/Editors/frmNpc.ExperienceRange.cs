using DarkUI.Controls;
namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc
{
    private DarkNumericUpDown _experienceMaximum = null!;
    private void BuildExperienceRange()
    {
        _experienceMaximum = new DarkNumericUpDown { Minimum = 0, Maximum = nudExp.Maximum, Location = new System.Drawing.Point(122, 216), Size = new Size(90, 23) };
        grpStats.Controls.Add(new Label { Text = "Max EXP:", Location = new System.Drawing.Point(12, 218), AutoSize = true });
        grpStats.Controls.Add(_experienceMaximum);
        _experienceMaximum.ValueChanged += (_, _) => { if (mEditorItem != null) mEditorItem.ExperienceMaximum = (long)_experienceMaximum.Value; };
        lblExp.Text = "Min EXP:";
    }
}
