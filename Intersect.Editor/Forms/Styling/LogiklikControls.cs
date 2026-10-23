using DarkUI.Controls;

namespace Intersect.Editor.Forms.Styling;

// Retain DarkUI's keyboard, selection and click behavior; only replace its
// fixed grey painting in the workspace's existing controls.
public class LogiklikButton : global::DarkUI.Controls.DarkButton
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var active = BackColor == EditorTheme.Selection || BackColor == EditorTheme.Primary;
        var hovered = Enabled && ButtonState != DarkControlState.Normal;
        var background = BackColor == SystemColors.Control ? EditorTheme.Input : BackColor;
        using var fill = new SolidBrush(ButtonState == DarkControlState.Pressed ? EditorTheme.Primary : hovered ? EditorTheme.Selection : background);
        using var border = new Pen(active || Focused ? EditorTheme.Accent : EditorTheme.Border);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var image = Image ?? (ImageList != null && ImageIndex >= 0 && ImageIndex < ImageList.Images.Count ? ImageList.Images[ImageIndex] : null);
        var imageWidth = image == null ? 0 : Math.Min(image.Width, Math.Max(1, Height - 8));
        if (image != null)
        {
            var imageBounds = new Rectangle(string.IsNullOrEmpty(Text) ? (Width - imageWidth) / 2 : 6,
                (Height - imageWidth) / 2, imageWidth, imageWidth);
            if (Enabled) e.Graphics.DrawImage(image, imageBounds);
            else ControlPaint.DrawImageDisabled(e.Graphics, image, imageBounds.X, imageBounds.Y, background);
        }
        var textBounds = new Rectangle(image == null ? 3 : imageWidth + 12, 0, Math.Max(0, Width - (image == null ? 6 : imageWidth + 15)), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, !Enabled ? EditorTheme.Muted : active ? EditorTheme.Accent : ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}

public class LogiklikComboBox : global::DarkUI.Controls.DarkComboBox
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

public class LogiklikGroupBox : global::DarkUI.Controls.DarkGroupBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(EditorTheme.Border);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        var size = TextRenderer.MeasureText(Text, Font);
        e.Graphics.DrawRectangle(border, 0, size.Height / 2, Math.Max(0, Width - 1), Math.Max(0, Height - size.Height / 2 - 1));
        e.Graphics.FillRectangle(fill, 8, 0, size.Width, size.Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, new System.Drawing.Point(8, 0), Enabled ? EditorTheme.Accent : EditorTheme.Muted);
    }
}

public class LogiklikStatusStrip : global::DarkUI.Controls.DarkStatusStrip
{
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(EditorTheme.Surface);
}

public class LogiklikToolStrip : global::DarkUI.Controls.DarkToolStrip
{
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);
}

public class LogiklikMenuStrip : global::DarkUI.Controls.DarkMenuStrip
{
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);
}

public class LogiklikNumericUpDown : global::DarkUI.Controls.DarkNumericUpDown
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(ContainsFocus ? EditorTheme.Accent : EditorTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }
}

public class LogiklikCheckBox : global::DarkUI.Controls.DarkCheckBox
{
    public LogiklikCheckBox() => Font = EditorTheme.ChoiceFont;
    protected override void OnPaint(PaintEventArgs e) => EditorChoiceRenderer.Draw(this, e.Graphics, Checked, CheckState == CheckState.Indeterminate, false);
}

public class LogiklikRadioButton : global::DarkUI.Controls.DarkRadioButton
{
    public LogiklikRadioButton() => Font = EditorTheme.ChoiceFont;
    protected override void OnPaint(PaintEventArgs e) => EditorChoiceRenderer.Draw(this, e.Graphics, Checked, false, true);
}

internal static class EditorChoiceRenderer
{
    internal static void Draw(ButtonBase control, System.Drawing.Graphics graphics, bool selected, bool indeterminate, bool radio)
    {
        graphics.Clear(control.BackColor);
        var size = Math.Min(13, Math.Max(8, control.Height - 4));
        var right = control.RightToLeft == RightToLeft.Yes;
        var glyph = new Rectangle(right ? control.Width - size - 2 : 2, (control.Height - size) / 2, size, size);
        using var fill = new SolidBrush(selected ? EditorTheme.Primary : EditorTheme.Input);
        using var border = new Pen(control.Enabled && (selected || control.Focused) ? EditorTheme.Accent : EditorTheme.Border);
        if (radio)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillEllipse(fill, glyph); graphics.DrawEllipse(border, glyph);
            if (selected)
            {
                using var dot = new SolidBrush(control.Enabled ? EditorTheme.Accent : EditorTheme.Muted);
                graphics.FillEllipse(dot, Rectangle.Inflate(glyph, -4, -4));
            }
        }
        else
        {
            graphics.FillRectangle(fill, glyph); graphics.DrawRectangle(border, glyph);
            using var mark = new Pen(control.Enabled ? EditorTheme.Accent : EditorTheme.Muted, 2);
            if (indeterminate) graphics.DrawLine(mark, glyph.Left + 4, glyph.Top + size / 2, glyph.Right - 4, glyph.Top + size / 2);
            else if (selected) graphics.DrawLines(mark, [new(glyph.Left + 3, glyph.Top + size / 2), new(glyph.Left + size / 2 - 1, glyph.Bottom - 4), new(glyph.Right - 3, glyph.Top + 4)]);
        }
        var textBounds = right ? new Rectangle(2, 0, Math.Max(0, glyph.Left - 4), control.Height)
            : new Rectangle(glyph.Right + 4, 0, Math.Max(0, control.Width - glyph.Right - 6), control.Height);
        TextRenderer.DrawText(graphics, control.Text, control.Font, textBounds,
            control.Enabled ? control.ForeColor : EditorTheme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (right ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
        if (control.Focused) ControlPaint.DrawFocusRectangle(graphics, textBounds);
    }
}

public class LogiklikTabControl : System.Windows.Forms.TabControl
{
    public LogiklikTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(EditorTheme.Background);
        using var border = new Pen(EditorTheme.Border);
        var content = DisplayRectangle;
        e.Graphics.DrawRectangle(border, Math.Max(0, content.X - 1), Math.Max(0, content.Y - 1),
            Math.Max(0, content.Width + 1), Math.Max(0, content.Height + 1));
        for (var index = 0; index < TabCount; index++)
        {
            var bounds = GetTabRect(index);
            if (!ClientRectangle.IntersectsWith(bounds)) continue;
            OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, bounds, index,
                index == SelectedIndex ? DrawItemState.Selected : DrawItemState.None));
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}

public class LogiklikTreeView : global::DarkUI.Controls.DarkTreeView
{
    protected override void PaintContent(System.Drawing.Graphics graphics)
    {
        foreach (var node in Nodes) Draw(node);
        void Draw(DarkTreeNode node)
        {
            var selected = SelectedNodes.Contains(node);
            using var fill = new SolidBrush(selected ? EditorTheme.Selection : EditorTheme.Input);
            graphics.FillRectangle(fill, node.FullArea);
            if (node.Nodes.Count > 0)
            {
                var box = node.ExpandArea;
                var x = box.Left + box.Width / 2; var y = box.Top + box.Height / 2;
                using var pen = new Pen(EditorTheme.Muted, 1.5f);
                if (node.Expanded) graphics.DrawLines(pen, [new(x - 3, y - 2), new(x, y + 1), new(x + 3, y - 2)]);
                else graphics.DrawLines(pen, [new(x - 2, y - 3), new(x + 1, y), new(x - 2, y + 3)]);
            }
            var icon = node.Expanded ? node.ExpandedIcon ?? node.Icon : node.Icon;
            if (ShowIcons && icon != null) graphics.DrawImage(icon, node.IconArea);
            TextRenderer.DrawText(graphics, node.Text, Font, node.TextArea, selected ? EditorTheme.Accent : EditorTheme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (node.Expanded) foreach (var child in node.Nodes) Draw(child);
        }
    }
}
