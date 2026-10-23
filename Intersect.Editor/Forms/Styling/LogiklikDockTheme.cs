using WeifenLuo.WinFormsUI.Docking;

namespace Intersect.Editor.Forms.Styling;
internal sealed class LogiklikDockTheme : VS2015DarkTheme
{
    internal LogiklikDockTheme()
    {
        var p = ColorPalette;
        p.MainWindowActive.Background = EditorTheme.Background;
        p.ToolWindowBorder = p.ToolWindowSeparator = EditorTheme.Border;
        p.TabSelectedActive.Background = p.ToolWindowTabSelectedActive.Background = EditorTheme.Selection;
        p.TabSelectedActive.Text = p.ToolWindowTabSelectedActive.Text = EditorTheme.Text;
        p.TabSelectedActive.Button = EditorTheme.Accent;
        p.TabSelectedInactive.Background = p.ToolWindowTabSelectedInactive.Background = EditorTheme.Surface;
        p.TabSelectedInactive.Text = p.ToolWindowTabSelectedInactive.Text = EditorTheme.Muted;
        p.TabSelectedInactive.Button = EditorTheme.Muted;
        p.TabUnselected.Background = p.ToolWindowTabUnselected.Background = EditorTheme.Background;
        p.TabUnselected.Text = p.ToolWindowTabUnselected.Text = EditorTheme.Muted;
        p.TabUnselectedHovered.Background = p.ToolWindowTabUnselectedHovered.Background = EditorTheme.Input;
        p.TabUnselectedHovered.Text = p.ToolWindowTabUnselectedHovered.Text = EditorTheme.Text;
        p.ToolWindowCaptionActive.Background = EditorTheme.Selection;
        p.ToolWindowCaptionActive.Text = p.ToolWindowCaptionActive.Button = EditorTheme.Accent;
        p.ToolWindowCaptionActive.Grip = EditorTheme.Border;
        p.ToolWindowCaptionInactive.Background = EditorTheme.Surface;
        p.ToolWindowCaptionInactive.Text = p.ToolWindowCaptionInactive.Button = EditorTheme.Muted;
        p.ToolWindowCaptionInactive.Grip = EditorTheme.Border;
        p.AutoHideStripDefault.Background = EditorTheme.Background;
        p.AutoHideStripDefault.Border = EditorTheme.Border; p.AutoHideStripDefault.Text = EditorTheme.Muted;
        p.AutoHideStripHovered.Background = EditorTheme.Selection;
        p.AutoHideStripHovered.Border = EditorTheme.Primary; p.AutoHideStripHovered.Text = EditorTheme.Accent;
        foreach (var button in new[] { p.TabButtonSelectedActiveHovered, p.TabButtonSelectedActivePressed,
            p.TabButtonSelectedInactiveHovered, p.TabButtonSelectedInactivePressed,
            p.TabButtonUnselectedTabHoveredButtonHovered, p.TabButtonUnselectedTabHoveredButtonPressed,
            p.ToolWindowCaptionButtonActiveHovered, p.ToolWindowCaptionButtonInactiveHovered,
            p.ToolWindowCaptionButtonPressed, p.OverflowButtonHovered, p.OverflowButtonPressed })
        { button.Background = EditorTheme.Selection; button.Border = EditorTheme.Border; button.Glyph = EditorTheme.Accent; }
        p.DockTarget.Background = EditorTheme.Selection; p.DockTarget.Border = EditorTheme.Accent;
        Skin.DockPaneStripSkin.TextFont = EditorTheme.BodyFont;
        Skin.AutoHideStripSkin.TextFont = EditorTheme.BodyFont;
    }
}
