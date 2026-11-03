using System.Runtime.CompilerServices;
using Intersect.Editor.Forms.Editors;
using WeifenLuo.WinFormsUI.Docking;

namespace Intersect.Editor.Forms.Styling;

// Adapt legacy fixed-coordinate workspaces once. Modern dock/table/flow layouts
// keep their own geometry; authored image previews and numeric fields stay fixed.
internal static class EditorResponsiveLayout
{
    private static readonly ConditionalWeakTable<Form, object> Applied = new();
    internal static void Apply(Form window)
    {
        if (window is FrmMain or FrmLogin or DockContent or FrmCraftingTables || Applied.TryGetValue(window, out _)) return;
        Applied.Add(window, new());
        Action? custom = window switch
        {
            FrmItem item => item.ArrangeResponsiveItemEditor,
            FrmNpc npc => npc.ResizeResponsiveNpcEditor,
            FrmSpell spell => spell.ArrangeSpellEditor,
            _ => null,
        };
        if (custom != null)
        {
            // These editors already own a complete responsive tab layout. Do
            // not reparent controls out from underneath their resize handlers.
            window.AutoScroll = false;
            window.ClientSizeChanged += (_, _) => custom();
            window.Shown += (_, _) => custom();
            custom(); return;
        }
        if (window.Controls.Find("EditorWorkspaceSplit", true).Length > 0) return;
        if (window.Controls.Cast<Control>().Any(c => c.Dock == DockStyle.Fill)) return;
        var roots = window.Controls.Cast<Control>().ToArray();
        var save = roots.SelectMany(Descendants).FirstOrDefault(c => c.Name == "btnSave");
        var cancel = roots.SelectMany(Descendants).FirstOrDefault(c => c.Name == "btnCancel");
        var content = roots.FirstOrDefault(c => c.Name == "pnlContainer");
        var navigation = roots.OfType<GroupBox>().FirstOrDefault(c => content != null && c.Right <= content.Left + 12);
        // The remaining legacy two-group dialogs (variables and condition lists)
        // use the same navigation/content pattern without a named panel.
        var groups = roots.OfType<GroupBox>().OrderBy(c => c.Left).ToArray();
        if (content == null && groups.Length == 2 && roots.All(c => c is GroupBox or Button or ToolStrip))
        { navigation = groups[0]; content = groups[1]; }
        if (navigation != null && content != null && save != null && cancel != null)
        {
            AnchorChildren(navigation); AnchorChildren(content);
            if (content is Panel panel) panel.AutoScroll = true;
            EditorWorkspaceLayout.Apply(window, navigation, content, save, cancel);
            return;
        }
        // Single-list selectors, options and time settings still need their
        // large controls to follow the frame, without rebuilding their forms.
        var baseline = window.ClientSize;
        foreach (var child in roots.Where(c => c.Dock == DockStyle.None))
        {
            if (child is Button && child.Top > baseline.Height * .65)
                child.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            else if (child is ListBox or TreeView or ListView or TabControl or PropertyGrid)
                child.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            else if (child is GroupBox or Panel)
            {
                child.Anchor = child.Left > baseline.Width * .4
                    ? AnchorStyles.Top | AnchorStyles.Right
                    : AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                AnchorChildren(child);
            }
        }
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls) foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static void AnchorChildren(Control parent)
    {
        if (parent is TableLayoutPanel or FlowLayoutPanel or SplitContainer or PictureBox) return;
        var lists = parent.Controls.Cast<Control>().Where(c => c is ListBox or TreeView or ListView or TabControl or PropertyGrid || c.Name == "lstGameObjects").ToArray();
        var fullList = lists.FirstOrDefault(c => c.Width >= parent.ClientSize.Width * .65);
        foreach (Control child in parent.Controls)
        {
            if (child.Dock != DockStyle.None) continue;
            if (lists.Contains(child))
                child.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | (child == fullList ? AnchorStyles.Right : AnchorStyles.None);
            else if (fullList != null && child.Top >= fullList.Bottom - 2)
                child.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | (child.Right >= parent.ClientSize.Width - 30 ? AnchorStyles.Right : AnchorStyles.None);
            else if (child is TextBox or ComboBox && child.Right >= parent.ClientSize.Width - 30)
                child.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            else if (child is Button && child.Left >= parent.ClientSize.Width * .7)
                child.Anchor = (fullList != null && child.Bottom >= fullList.Bottom - 4 && child.Top > fullList.Top ? AnchorStyles.Bottom : AnchorStyles.Top) | AnchorStyles.Right;
            else if (child is Panel or GroupBox)
            {
                AnchorChildren(child);
            }
        }
    }
}
