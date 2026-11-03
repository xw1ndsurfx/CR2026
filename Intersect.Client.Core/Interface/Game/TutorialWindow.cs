using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Newtonsoft.Json;
namespace Intersect.Client.Interface.Game;
internal sealed class TutorialWindow : Window
{
    private TutorialStatePacket _state = new();
    private readonly Label _heading, _body;
    private readonly ScrollControl _scroll;
    private readonly Button _next, _scene, _restart;
    internal bool RequestedOpen { get; private set; }
    internal static string T(string source) => TutorialText.Get(source, ClientLanguages.Current);
    internal TutorialWindow(Canvas canvas) : base(canvas, T("Tutorial"), false, nameof(TutorialWindow))
    {
        SetSize(Math.Min(590, canvas.Width - 20), Math.Min(440, canvas.Height - 20));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _heading = NavalManagementWindow.Text(this, "TutorialHeading", "", 20, 40, Width - 40, 50, 14);
        var scroll = _scroll = new ScrollControl(this, "TutorialScroll") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        scroll.SetBounds(20, 94, Width - 40, Height - 210);
        _body = NavalManagementWindow.Text(scroll, "TutorialBody", "", 0, 0, Width - 65, 700);
        _body.WrappingBehavior = WrappingBehavior.Wrapped;
        _body.TextAlign = Pos.Left | Pos.Top;
        _scene = Add("Play cinematic", 20, Height - 100, () => Send(TutorialAction.PlayCinematic));
        _restart = Add("Replay tutorial", 20, Height - 60, () => Send(TutorialAction.Restart));
        Add("Pause guide", 200, Height - 60, () => Send(TutorialAction.Pause));
        _next = Add("Next", Width - 170, Height - 60, () => Send(TutorialAction.Next));
        Closed += (_, _) => { if (RequestedOpen) Send(TutorialAction.Pause); };
        Hide();
    }
    private Button Add(string text, int x, int y, Action action)
    {
        var button = NavalManagementWindow.Button(this, "Tutorial" + text.Replace(" ", ""), "", x, y, 160, 30, action);
        button.Text = T(text); return button;
    }
    protected override void EnsureInitialized() { }
    private void Send(TutorialAction action) => Networking.Network.SendPacket(new TutorialRequestPacket { Action = action, StepId = _state.StepId, Revision = _state.Revision });
    internal void Apply(TutorialStatePacket state)
    {
        var changedStep = _state.StepId != state.StepId;
        _state = state;
        var config = JsonConvert.DeserializeObject<TutorialConfiguration>(state.ConfigurationJson);
        if (config == null) return;
        var step = config.Steps.FirstOrDefault(s => s.Id == state.StepId);
        string Content(string source) => ClientLanguages.ContentText(config.Translations.Get(source, ClientLanguages.Current));
        _heading.Text = step == null ? T("Tutorial complete") : $"{config.Steps.IndexOf(step) + 1}/{config.Steps.Count} · {Content(step.Title)}";
        _body.Text = step == null ? T("You can replay this guide at any time.") : Content(step.Text);
        _next.IsDisabled = !state.CanAdvance; _next.Text = T("Next");
        _scene.IsHidden = step == null || step.CinematicEventId == Guid.Empty;
        _restart.IsHidden = !state.Finished;
        if (changedStep) _scroll.ScrollToTop();
        RequestedOpen = state.Open;
        RefreshVisibility();
    }
    internal void RefreshVisibility()
    {
        _body.SetSize(_body.Width, Math.Max(50, _body.TextHeight + 12));
        if (RequestedOpen && !Cinematics.CinematicControls.Active) Show(); else Hide();
    }
    internal void OpenGuide()
    {
        if (_state.Finished) { RequestedOpen = true; Show(); BringToFront(); }
        else Send(TutorialAction.Resume);
    }
}
