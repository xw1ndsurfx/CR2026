using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;

namespace Intersect.Client.Interface.Game;

// Explicit layout: an outdated shared InputBox UI file cannot hide the actions.
internal sealed class DungeonAbandonConfirmationWindow : Window
{
    internal DungeonAbandonConfirmationWindow(Canvas canvas, Action confirm)
        : base(canvas, ClientLanguages.Text("Abandon dungeon"), true, nameof(DungeonAbandonConfirmationWindow))
    {
        SetSize(Math.Min(500, canvas.Width - 16), 230);
        Alignment = [Alignments.Center];
        DeleteOnClose = true;
        DisableResizing();
        Interface.InputBlockingComponents.Add(this);
        var font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;
        var prompt = new Label(this)
        {
            Text = ClientLanguages.Text("Leave this dungeon? You will return to the exit and forfeit unclaimed rewards. Other players can continue."),
            Font = font, FontSize = 12, TextColorOverride = Color.White,
            AutoSizeToContents = false, WrappingBehavior = WrappingBehavior.Wrapped,
        };
        prompt.SetBounds(16, 14, Width - 48, 112);
        var yes = new Button(this, "ConfirmDungeonAbandon")
        {
            Text = ClientLanguages.Text("Yes"), Font = font, FontSize = 12, TextColorOverride = Color.White,
        };
        yes.SetBounds(16, 144, (Width - 64) / 2, 36);
        var no = new Button(this, "CancelDungeonAbandon")
        {
            Text = ClientLanguages.Text("No"), Font = font, FontSize = 12, TextColorOverride = Color.White,
        };
        no.SetBounds(yes.Right + 16, 144, yes.Width, 36);
        yes.Clicked += (_, _) => { Close(); confirm(); };
        no.Clicked += (_, _) => Close();
        Show();
        BringToFront();
    }

    protected override void EnsureInitialized() { }

    protected override void Dispose(bool disposing)
    {
        Interface.InputBlockingComponents.Remove(this);
        base.Dispose(disposing);
    }
}
