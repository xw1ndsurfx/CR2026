using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Chrome = Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style = Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
namespace Intersect.Client.Interface.Game;
internal abstract class EventResultPanelWindow : Window
{
    private readonly Label _heading, _reward;
    private readonly ScrollControl _details;
    private readonly RichLabel _body;
    private readonly Button _continue;
    private readonly Chrome _banner, _rewardFrame;
    private string _headingText = "", _bodyText = "", _rewardText = "";
    protected EventResultPanelWindow(Canvas canvas, string title, string name) : base(canvas, ClientLanguages.Text(title), false, name)
    {
        SetSize(620, 570); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _banner = new Chrome(this, "ResultBanner", Style.Ribbon);
        _rewardFrame = new Chrome(this, "ResultRewardFrame", Style.Preview);
        Label Label(string id) => new(this, id) { Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 14, AutoSizeToContents = false, TextAlign = Pos.Center, TextColorOverride = Color.White };
        _heading = Label("ResultHeadline"); _reward = Label("ResultExperience");
        _details = new ScrollControl(this, "ResultDetails") { ShouldDrawBackground = false };
        _body = EventPanelText.CreateBody(_details, "ResultText");
        _continue = new Button(this, "ResultContinue") { Text = ClientLanguages.Text("Continue"), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 11 };
        _continue.Clicked += (_, _) => Hide();
        Arrange(); Hide();
    }
    protected override void EnsureInitialized() { }
    protected override void Layout(SkinBase skin)
    {
        var width = Math.Max(300, Math.Min(620, (Parent?.Width ?? 644) - 24));
        var height = Math.Max(300, Math.Min(570, (Parent?.Height ?? 594) - 24));
        if (Width != width || Height != height) { SetSize(width, height); Arrange(); }
        base.Layout(skin);
    }
    private void Arrange()
    {
        if (_details == null) return;
        var contentHeight = Height - Titlebar.Height - 8;
        _banner.SetBounds(20, 38, Width - 40, 65); _heading.SetBounds(30, 49, Width - 60, 40);
        _details.SetBounds(24, 116, Width - 48, Math.Max(50, contentHeight - 238));
        _rewardFrame.SetBounds(22, contentHeight - 110, Width - 44, 48); _reward.SetBounds(30, contentHeight - 105, Width - 60, 38);
        _continue.SetBounds((Width - 180) / 2, contentHeight - 48, 180, 32);
        EventPanelText.Fit(_heading, _headingText, 19); EventPanelText.Fit(_reward, _rewardText, 17);
        EventPanelText.SetBody(_details, _body, _bodyText);
    }
    protected void Present(string heading, string body, string reward, bool success)
    {
        _headingText = heading; _bodyText = body; _rewardText = reward;
        _heading.TextColorOverride = success ? new Color(245, 214, 110) : new Color(235, 105, 90);
        _reward.TextColorOverride = new Color(150, 225, 155);
        SetSize(Math.Max(300, Math.Min(620, (Parent?.Width ?? 644) - 24)), Math.Max(300, Math.Min(570, (Parent?.Height ?? 594) - 24)));
        Arrange(); _details.ScrollToTop(); Show(); BringToFront();
    }
}
