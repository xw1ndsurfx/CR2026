using DarkUI.Controls;
namespace Intersect.Editor.Forms.Editors;

public partial class FrmShop
{
    private DarkNumericUpDown _buybackDiscount = null!;
    private void InitializeBuybackDiscount()
    {
        // The unused left side of the sound rows leaves room without shrinking shop lists.
        var label = new Label { Text = "Buyback reduction (%)", AutoSize = true, Location = new System.Drawing.Point(8, 75) };
        _buybackDiscount = new DarkNumericUpDown { Minimum = 0, Maximum = 99, Location = new System.Drawing.Point(176, 71), Size = new Size(70, 23) };
        grpGeneral.Controls.Add(label);
        grpGeneral.Controls.Add(_buybackDiscount);
        new ToolTip(components).SetToolTip(_buybackDiscount, "Reduce the amount this shop pays players. Applies to explicit whitelist prices and base-price blacklist purchases. 0 preserves current prices.");
        _buybackDiscount.ValueChanged += (_, _) => { if (mEditorItem != null) mEditorItem.BuybackDiscountPercent = (int)_buybackDiscount.Value; };
    }
}
