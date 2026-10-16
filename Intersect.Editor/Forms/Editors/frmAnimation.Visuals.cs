using DarkUI.Controls;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmAnimation
{
    private readonly NumericUpDown[] _lowerVisuals = new NumericUpDown[3];
    private readonly NumericUpDown[] _upperVisuals = new NumericUpDown[3];
    private bool _loadingVisuals;
    private void BuildVisualControls()
    {
        foreach (var upper in new[] { false, true })
        {
            var group = upper ? grpUpperExtraOptions : grpLowerExtraOptions;
            var controls = upper ? _upperVisuals : _lowerVisuals;
            group.Height = 192;
            var labels = new[] { "Offset X (pixels):", "Offset Y (pixels):", "Transparency (%):" };
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var label = new Label { Text = labels[i], AutoSize = true, Left = 8, Top = 100 + 28 * i, ForeColor = System.Drawing.Color.Gainsboro };
                var number = new DarkNumericUpDown { Left = 154, Top = 95 + 28 * i, Width = 88, Minimum = i == 2 ? 0 : -4096, Maximum = i == 2 ? 100 : 4096 };
                controls[i] = number; group.Controls.Add(label); group.Controls.Add(number);
                number.ValueChanged += (_, _) =>
                {
                    if (_loadingVisuals || mEditorItem == null) return;
                    var layer = upper ? mEditorItem.Upper : mEditorItem.Lower;
                    if (index == 0) layer.OffsetX = (int)number.Value;
                    else if (index == 1) layer.OffsetY = (int)number.Value;
                    else layer.TransparencyPercent = (int)number.Value;
                    if (upper) DrawUpperFrame(); else DrawLowerFrame();
                };
            }
            _tooltip.SetToolTip(controls[2], "0% = fully visible; 90% = mostly transparent; 100% = invisible.");
        }
    }
    private void LoadVisualControls(bool upper)
    {
        _loadingVisuals = true;
        var layer = upper ? mEditorItem.Upper : mEditorItem.Lower;
        var controls = upper ? _upperVisuals : _lowerVisuals;
        controls[0].Value = Math.Clamp(layer.OffsetX, -4096, 4096);
        controls[1].Value = Math.Clamp(layer.OffsetY, -4096, 4096);
        controls[2].Value = Math.Clamp(layer.TransparencyPercent, 0, 100);
        _loadingVisuals = false;
    }
}
