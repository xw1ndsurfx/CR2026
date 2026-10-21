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
    private readonly Button _close,_mute;
    private bool _muted;
    private long _started;
    private int _shown=-1;
    private string _headingText = "", _bodyText = "";
    internal InvasionAlertWindow(Canvas parent) : base(parent, ClientLanguages.Text("Royal alert"), false, nameof(InvasionAlertWindow))
    {
        Alignment = []; DeleteOnClose = false; DisableResizing();
        _banner = new Chrome(this, "RoyalAlertBanner", Style.Ribbon);
        _heading = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 17, AutoSizeToContents = false, TextColorOverride = new Color(240, 210, 140), TextAlign = Pos.Center };
        _scroll = new ScrollControl(this, "RoyalAlertScroll") { ShouldDrawBackground = false };
        _body = EventPanelText.CreateBody(_scroll, "RoyalAlertBody", 12);
        _close = new Button(this) { Text = ClientLanguages.Text("Close"), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 12 };
        _close.Clicked += (_, _) => Next(); Closed += (_, _) => Next();
        _mute=NavalManagementWindow.Button(this,"MuteAlerts",Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Mute announcements",ClientLanguages.Current),20,160,160,30,()=>{_muted=true;_pending.Clear();Hide();});
        NavalManagementWindow.StyleButton(_close);
        Titlebar.Hide();
        Arrange(); Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Apply(InvasionAlertPacket packet)
    {
        if(_muted)return;
        if(_pending.Count>=16)_pending.Dequeue();
        _pending.Enqueue(packet);
        if (IsHidden) Next();
    }
    private void Next()
    {
        if (!_pending.TryDequeue(out var packet)) { Hide(); return; }
        _headingText = ClientLanguages.Format("ROYAL ALERT - {0}", ClientLanguages.ContentText(packet.Name));
        _bodyText = FormatMessage(packet);
        _started=Environment.TickCount64;_shown=-1;
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
    private int DesiredHeight => Math.Max(180, Math.Min(225, (Parent?.Height ?? 744) - 24));
    private void Arrange()
    {
        SetSize(DesiredWidth, DesiredHeight);
        SetPosition(((Parent?.Width??Width)-Width)/2,76);
        var bottom = Height - 8;
        _banner.SetBounds(8,8,Width-16,42);_heading.SetBounds(20,12,Width-40,30);
        _scroll.SetBounds(20,56,Width-40,Math.Max(60,bottom-110));
        _close.SetBounds(Width-160,bottom-38,140,30);_mute.SetBounds(20,bottom-38,180,30);
        EventPanelText.Fit(_heading, _headingText, 17);
        var count=Math.Clamp((int)((Environment.TickCount64-_started)*35/1000),0,_bodyText.Length);
        var visible=_bodyText[..count];
        if(count>0 && char.IsHighSurrogate(visible[^1]))visible=visible[..^1];
        _shown=count;
        _body.SetSize(Math.Max(1, _scroll.Width - 22), 20); _body.ClearText();
        var position = 0; var bold = false;
        while (position < visible.Length)
        {
            var marker = visible.IndexOf("**", position, StringComparison.Ordinal);
            var end = marker < 0 ? visible.Length : marker;
            if (end > position) _body.AddText(visible[position..end], bold ? new Color(245, 216, 140) : Color.White, Alignments.Left,
                GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont, 12);
            if (marker < 0) break;
            bold = !bold; position = marker + 2;
        }
        _body.ForceImmediateRebuild(); _scroll.UpdateScrollBars();
    }
    public override void Think()
    {
        base.Think();
        if(IsHidden)return;
        var elapsed=Environment.TickCount64-_started;
        if(elapsed>Math.Max(12000,_bodyText.Length*1000L/35+8000)){Next();return;}
        var count=Math.Clamp((int)(elapsed*35/1000),0,_bodyText.Length);
        if(count!=_shown)Arrange();
    }

}
