using System.Runtime.CompilerServices;
using Intersect.Editor.Forms.Editors;

namespace Intersect.Editor.Forms.Styling;

/// <summary>Preserves a legacy dialog's usable content size inside a scrolling viewport.</summary>
internal static class EditorScrollLayout
{
    private static readonly ConditionalWeakTable<Form, object> Attached = new();

    internal static void Attach(Form window)
    {
        if (Attached.TryGetValue(window, out _)) return;
        Attached.Add(window, new object());
        // These workspaces already keep their action footer outside scrolling sections.
        if (window.Controls.Find("EditorWorkspaceSplit", true).Length > 0) return;
        // Responsive tab editors arrange fields against the form's client size.
        // Keep their native resize handlers active down to their supported minimum.
        var minimum = window is FrmItem or FrmNpc or FrmSpell
            ? new Size(Math.Max(1, window.MinimumSize.Width - (window.Width - window.ClientSize.Width)),
                Math.Max(1, window.MinimumSize.Height - (window.Height - window.ClientSize.Height)))
            : window.ClientSize;
        Panel? viewport = null;
        Panel? canvas = null;
        var updating = false;
        var ready = window.Visible;
        void ResizeContent()
        {
            if (!ready || updating || window.IsDisposed || window.WindowState == FormWindowState.Minimized) return;
            updating = true;
            try
            {
                if (viewport == null)
                {
                    if (window.ClientSize.Width >= minimum.Width && window.ClientSize.Height >= minimum.Height) return;
                    window.SuspendLayout();
                    try
                    {
                        var children = window.Controls.Cast<Control>().ToArray();
                        // Start at the current size so bottom/right anchors retain their offsets.
                        canvas = new Panel { Name = "EditorScrollContent", Size = window.ClientSize,
                            BackColor = window.BackColor, Margin = Padding.Empty };
                        viewport = new Panel { Name = "EditorScrollViewport", Dock = DockStyle.Fill,
                            AutoScroll = true, BackColor = window.BackColor, Margin = Padding.Empty };
                        canvas.SuspendLayout();
                        foreach (var child in children) canvas.Controls.Add(child);
                        for (var i = 0; i < children.Length; i++) canvas.Controls.SetChildIndex(children[i], i);
                        canvas.ResumeLayout(true);
                        window.AutoScroll = false;
                        viewport.Controls.Add(canvas);
                        window.Controls.Add(viewport);
                    }
                    finally { window.ResumeLayout(true); }
                }
                // Neither right/bottom anchoring nor Dock.Fill can suppress this overflow.
                canvas!.Size = new Size(Math.Max(minimum.Width, viewport.ClientSize.Width),
                    Math.Max(minimum.Height, viewport.ClientSize.Height));
            }
            finally { updating = false; }
        }
        window.Shown += (_, _) => { ready = true; ResizeContent(); };
        window.ClientSizeChanged += (_, _) => ResizeContent();
        ResizeContent();
    }
}
