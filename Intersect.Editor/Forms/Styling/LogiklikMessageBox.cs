using DarkUI.Forms;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Styling;

// Keep the existing modal result contract for save, delete and error dialogs.
public static class LogiklikMessageBox
{
    public static DialogResult Show(string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) =>
        Show(Form.ActiveForm, text, caption, buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window? owner, string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        using var dialog = Create(text, caption, buttons, icon, defaultButton);
        return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    internal static Form Create(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        var results = buttons switch
        {
            MessageBoxButtons.OKCancel => new[] { DialogResult.OK, DialogResult.Cancel },
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            MessageBoxButtons.CancelTryContinue => [DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue],
            _ => [DialogResult.OK],
        };
        var messageHeight = Math.Clamp(TextRenderer.MeasureText(text, EditorTheme.BodyFont, new Size(500, 2000),
            TextFormatFlags.WordBreak).Height + 30, 90, 390);
        var dialog = new Form
        {
            Text = caption, ClientSize = new Size(610, messageHeight + 76),
            FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, Font = EditorTheme.BodyFont,
            Icon = Intersect.Editor.Core.Program.Icon,
        };
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(16, 12, 16, 12),
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
        };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 12) };
        var message = new TextBox
        {
            Name = "DialogMessage", Text = text, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None, ScrollBars = messageHeight >= 390 ? ScrollBars.Vertical : ScrollBars.None,
            TabStop = false,
        };
        content.Controls.Add(message);
        var symbol = icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Information => SystemIcons.Information,
            MessageBoxIcon.Question => SystemIcons.Question,
            _ => null,
        };
        if (symbol != null)
        {
            var picture = new PictureBox { Dock = DockStyle.Left, Width = 52, Image = symbol.ToBitmap(), SizeMode = PictureBoxSizeMode.CenterImage };
            content.Controls.Add(picture);
            dialog.Disposed += (_, _) => picture.Image?.Dispose();
        }
        var actions = results.Select(result => new LogiklikButton
        {
            Name = "Dialog" + result, DialogResult = result, Width = 112, Height = 34,
            Margin = new Padding(8, 0, 0, 0), Text = Caption(result),
        }).ToArray();
        foreach (var action in Enumerable.Reverse(actions)) footer.Controls.Add(action);
        dialog.Controls.Add(content); dialog.Controls.Add(footer);
        dialog.AcceptButton = actions[Math.Clamp((int)defaultButton / 256, 0, actions.Length - 1)];
        dialog.CancelButton = actions.FirstOrDefault(action => action.DialogResult == DialogResult.Cancel)
            ?? actions.FirstOrDefault(action => action.DialogResult == DialogResult.No)
            ?? actions.FirstOrDefault(action => action.DialogResult == DialogResult.OK);
        dialog.FormClosing += (_, _) =>
        {
            if (dialog.DialogResult == DialogResult.Cancel && !results.Contains(DialogResult.Cancel))
                dialog.DialogResult = results.Contains(DialogResult.No) ? DialogResult.No : results[^1];
        };
        EditorTheme.Apply(dialog);
        message.BorderStyle = BorderStyle.None;
        return dialog;
    }

    private static string Caption(DialogResult result) => QualityOfLifeText.Get(result switch
    {
        DialogResult.OK => "OK", DialogResult.Yes => "Yes", DialogResult.No => "No",
        DialogResult.Cancel => "Cancel", DialogResult.Abort => "Abort", DialogResult.Retry => "Retry",
        DialogResult.Ignore => "Ignore", DialogResult.TryAgain => "Try again", _ => "Continue",
    }, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}

public static class LogiklikDarkMessageBox
{
    public static DialogResult ShowInformation(string text, string caption, DarkDialogButton buttons = DarkDialogButton.Ok, Icon? icon = null) =>
        Show(text, caption, buttons, MessageBoxIcon.Information);
    public static DialogResult ShowWarning(string text, string caption, DarkDialogButton buttons = DarkDialogButton.Ok, Icon? icon = null) =>
        Show(text, caption, buttons, MessageBoxIcon.Warning);
    public static DialogResult ShowError(string text, string caption, DarkDialogButton buttons = DarkDialogButton.Ok, Icon? icon = null) =>
        Show(text, caption, buttons, MessageBoxIcon.Error);
    private static DialogResult Show(string text, string caption, DarkDialogButton buttons, MessageBoxIcon icon) =>
        LogiklikMessageBox.Show(text, caption, buttons switch
        {
            DarkDialogButton.OkCancel => MessageBoxButtons.OKCancel,
            DarkDialogButton.YesNo => MessageBoxButtons.YesNo,
            DarkDialogButton.YesNoCancel => MessageBoxButtons.YesNoCancel,
            DarkDialogButton.AbortRetryIgnore => MessageBoxButtons.AbortRetryIgnore,
            DarkDialogButton.RetryCancel => MessageBoxButtons.RetryCancel,
            _ => MessageBoxButtons.OK,
        }, icon);
}
