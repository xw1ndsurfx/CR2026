using DarkUI.Controls;

namespace Intersect.Editor.Forms.Styling;

// Retain DarkUI's keyboard, selection and click behavior; only replace its
// fixed grey painting in the workspace's existing controls.
public class LogiklikButton : DarkButton
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var active = BackColor == EditorTheme.Selection;
        var hovered = ClientRectangle.Contains(PointToClient(MousePosition));
        using var fill = new SolidBrush(hovered ? EditorTheme.Selection : BackColor);
        using var border = new Pen(active || Focused ? EditorTheme.Accent : EditorTheme.Border);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        if (Image != null) e.Graphics.DrawImage(Image, new Rectangle(6, (Height - 16) / 2, 16, 16));
        var textBounds = new Rectangle(Image == null ? 3 : 25, 0, Width - (Image == null ? 6 : 28), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, !Enabled ? EditorTheme.Muted : active ? EditorTheme.Accent : ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}

public class LogiklikComboBox : DarkComboBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill = new SolidBrush(EditorTheme.Input);
        using var border = new Pen(Focused ? EditorTheme.Accent : EditorTheme.Border);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(7, 0, Width - 28, Height), Enabled ? EditorTheme.Text : EditorTheme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var arrow = new SolidBrush(Enabled ? EditorTheme.Muted : EditorTheme.Border);
        var x = Width - 14; var y = Height / 2;
        e.Graphics.FillPolygon(arrow, new System.Drawing.Point[] {new System.Drawing.Point(x - 4, y - 2), new System.Drawing.Point(x + 4, y - 2), new System.Drawing.Point(x, y + 3) });
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? EditorTheme.Selection : EditorTheme.Input);
        e.Graphics.FillRectangle(fill, e.Bounds);
        if (e.Index >= 0 && e.Index < Items.Count)
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, e.Bounds,
                Enabled ? EditorTheme.Text : EditorTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

public class LogiklikGroupBox : DarkGroupBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(EditorTheme.Border);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        var size = TextRenderer.MeasureText(Text, Font);
        e.Graphics.DrawRectangle(border, 0, size.Height / 2, Math.Max(0, Width - 1), Math.Max(0, Height - size.Height / 2 - 1));
        e.Graphics.FillRectangle(fill, 8, 0, size.Width, size.Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, new System.Drawing.Point(8, 0), EditorTheme.Muted);
    }
}

public class LogiklikStatusStrip : DarkStatusStrip
{
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(EditorTheme.Surface);
}
