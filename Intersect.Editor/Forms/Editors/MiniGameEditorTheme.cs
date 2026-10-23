namespace Intersect.Editor.Forms.Editors;

internal static class MiniGameEditorTheme
{
    public static void Apply(Control root)
    {
        Styling.EditorTheme.Apply(root);
        if (root is Form form) form.MaximizeBox = true;
    }
}
