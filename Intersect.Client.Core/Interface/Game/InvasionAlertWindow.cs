using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.WorldEvents;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Chrome = Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style = Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
namespace Intersect.Client.Interface.Game;
internal sealed class InvasionAlertWindow : Window
{
    private readonly Queue<InvasionAlertPacket> _pending = [];
    private readonly Chrome _banner;
    private readonly Label _heading;
    private readonly ScrollControl _scroll;
    private readonly RichLabel _body;
    private readonly Button _close;
    private string _headingText = "", _bodyText = "";
    internal InvasionAlertWindow(Canvas parent) : base(parent, ClientLanguages.Text("Royal alert"), false, nameof(InvasionAlertWindow))
    {
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _banner = new Chrome(this, "RoyalAlertBanner", Style.Ribbon);
        _heading = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 17, AutoSizeToContents = false, TextColorOverride = new Color(240, 210, 140), TextAlign = Pos.Center };
        _scroll = new ScrollControl(this, "RoyalAlertScroll") { ShouldDrawBackground = false };
        _body = EventPanelText.CreateBody(_scroll, "RoyalAlertBody", 12);
        _close = new Button(this) { Text = ClientLanguages.Text("Continue"), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 12 };
        _close.Clicked += (_, _) => Next(); Closed += (_, _) => Next();
        Arrange(); Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Apply(InvasionAlertPacket packet)
    {
        _pending.Enqueue(packet);
        if (IsHidden) Next();
    }
    private void Next()
    {
        if (!_pending.TryDequeue(out var packet)) { Hide(); return; }
        _headingText = ClientLanguages.Format("ROYAL ALERT - {0}", ClientLanguages.ContentText(packet.Name));
        _bodyText = FormatMessage(packet);
        Arrange(); _scroll.ScrollToTop(); Show(); BringToFront();
    }
    internal static string FormatMessage(InvasionAlertPacket packet)
    {
        var remaining = packet.MinutesBefore == 60 ? ClientLanguages.Text("1 hour") : ClientLanguages.Format("{0} minutes", packet.MinutesBefore);
        var text = string.IsNullOrWhiteSpace(packet.Message)
            ? ClientLanguages.Text("{name} will begin in {remaining} near {island}.")
            : ClientLanguages.ContentText(packet.Message);
        return text.Replace("{name}", ClientLanguages.ContentText(packet.Name))
            .Replace("{island}", ClientLanguages.ContentText(packet.Island)).Replace("{target}", ClientLanguages.ContentText(packet.Target))
            .Replace("{minutes}", packet.MinutesBefore.ToString()).Replace("{remaining}", remaining).Replace("{time}", packet.StartTime);
    }
    protected override void Layout(SkinBase skin)
    {
        if (Width != DesiredWidth || Height != DesiredHeight) Arrange();
        base.Layout(skin);
    }
    private int DesiredWidth => Math.Max(280, Math.Min(820, (Parent?.Width ?? 844) - 24));
    private int DesiredHeight => Math.Max(280, Math.Min(720, (Parent?.Height ?? 744) - 24));
    private void Arrange()
    {
        SetSize(DesiredWidth, DesiredHeight);
        var bottom = Height - Titlebar.Height - 8;
        _banner.SetBounds(16, 24, Width - 32, 56); _heading.SetBounds(24, 31, Width - 48, 40);
        _scroll.SetBounds(20, 92, Width - 40, Math.Max(60, bottom - 150));
        _close.SetBounds((Width - 180) / 2, bottom - 46, 180, 32);
        EventPanelText.Fit(_heading, _headingText, 17);
        _body.SetSize(Math.Max(1, _scroll.Width - 22), 20); _body.ClearText();
        var position = 0; var bold = false;
        while (position < _bodyText.Length)
        {
            var marker = _bodyText.IndexOf("**", position, StringComparison.Ordinal);
            var end = marker < 0 ? _bodyText.Length : marker;
            if (end > position) _body.AddText(_bodyText[position..end], bold ? new Color(245, 216, 140) : Color.White, Alignments.Left,
                GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont, 12);
            if (marker < 0) break;
            bold = !bold; position = marker + 2;
        }
        _body.ForceImmediateRebuild(); _scroll.UpdateScrollBars();
    }
}
