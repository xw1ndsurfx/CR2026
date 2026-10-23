using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Styling;
internal sealed class EditorStripRenderer : ToolStripProfessionalRenderer
{
    internal EditorStripRenderer() : base(new Palette()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? EditorTheme.Muted : e.Item.Selected || e.Item is ToolStripButton { Checked: true } ? EditorTheme.Accent : e.Item is ToolStripStatusLabel { IsLink: true } ? EditorTheme.Accent : e.Item.ForeColor;
        base.OnRenderItemText(e);
    }
    private sealed class Palette : ProfessionalColorTable
    {
        public override DrawingColor ToolStripGradientBegin => EditorTheme.Background;
        public override DrawingColor ToolStripGradientMiddle => EditorTheme.Background;
        public override DrawingColor ToolStripGradientEnd => EditorTheme.Background;
        public override DrawingColor MenuStripGradientBegin => EditorTheme.Background;
        public override DrawingColor MenuStripGradientEnd => EditorTheme.Background;
        public override DrawingColor StatusStripGradientBegin => EditorTheme.Surface;
        public override DrawingColor StatusStripGradientEnd => EditorTheme.Surface;
        public override DrawingColor ToolStripDropDownBackground => EditorTheme.Surface;
        public override DrawingColor ImageMarginGradientBegin => EditorTheme.Surface;
        public override DrawingColor ImageMarginGradientMiddle => EditorTheme.Surface;
        public override DrawingColor ImageMarginGradientEnd => EditorTheme.Surface;
        public override DrawingColor MenuItemSelected => EditorTheme.Selection;
        public override DrawingColor MenuItemBorder => EditorTheme.Primary;
        public override DrawingColor MenuItemSelectedGradientBegin => EditorTheme.Selection;
        public override DrawingColor MenuItemSelectedGradientEnd => EditorTheme.Selection;
        public override DrawingColor MenuItemPressedGradientBegin => EditorTheme.Selection;
        public override DrawingColor MenuItemPressedGradientMiddle => EditorTheme.Selection;
        public override DrawingColor MenuItemPressedGradientEnd => EditorTheme.Selection;
        public override DrawingColor ButtonSelectedHighlight => EditorTheme.Selection;
        public override DrawingColor ButtonCheckedHighlight => EditorTheme.Selection;
        public override DrawingColor ButtonPressedHighlight => EditorTheme.Primary;
        public override DrawingColor ButtonSelectedGradientBegin => EditorTheme.Selection;
        public override DrawingColor ButtonSelectedGradientMiddle => EditorTheme.Selection;
        public override DrawingColor ButtonSelectedGradientEnd => EditorTheme.Selection;
        public override DrawingColor ButtonCheckedGradientBegin => EditorTheme.Selection;
        public override DrawingColor ButtonCheckedGradientMiddle => EditorTheme.Selection;
        public override DrawingColor ButtonCheckedGradientEnd => EditorTheme.Selection;
        public override DrawingColor ButtonPressedGradientBegin => EditorTheme.Primary;
        public override DrawingColor ButtonPressedGradientMiddle => EditorTheme.Primary;
        public override DrawingColor ButtonPressedGradientEnd => EditorTheme.Primary;
        public override DrawingColor ButtonSelectedBorder => EditorTheme.Accent;
        public override DrawingColor ButtonPressedBorder => EditorTheme.Accent;
        public override DrawingColor ButtonCheckedHighlightBorder => EditorTheme.Accent;
        public override DrawingColor SeparatorDark => EditorTheme.Border;
        public override DrawingColor SeparatorLight => EditorTheme.Surface;
        public override DrawingColor ToolStripBorder => EditorTheme.Border;
        public override DrawingColor MenuBorder => EditorTheme.Border;
    }
}
