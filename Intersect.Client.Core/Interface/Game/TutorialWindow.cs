using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Newtonsoft.Json;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Compact contextual guide. The separate screen-space spotlight is input
/// transparent, allowing the real menu/game actions to remain usable.
/// </summary>
internal sealed class TutorialWindow : Window
{
    private readonly Canvas _canvas;
    private readonly TutorialSpotlight _spotlight;
    private TutorialStatePacket _state = new();
    private TutorialHighlightTarget _target = TutorialHighlightTarget.None;
    private TutorialHighlightStyle _style = TutorialHighlightStyle.Outline;
    private readonly Label _heading;
    private readonly Label _progress;
    private readonly Panel _progressTrack, _progressFill;
    private double _progressFraction;
    private readonly Action<TutorialRequestPacket> _send;
    private readonly Label _body;
    private readonly ScrollControl _scroll;
    private readonly Button _next, _scene, _restart, _pause;
    internal bool RequestedOpen { get; private set; }
    private bool _combatDrill;
    private bool _combatGuideExplicitlyOpened;
    internal static string T(string source) => TutorialText.Get(source, ClientLanguages.Current);

    internal TutorialWindow(Canvas canvas, Action<TutorialRequestPacket>? send = null) : base(canvas, T("Tutorial"), false, nameof(TutorialWindow))
    {
        _canvas = canvas;
        _send = send ?? (packet => Networking.Network.SendPacket(packet));
        _spotlight = new TutorialSpotlight(canvas);
        var width = Math.Min(460, Math.Max(240, canvas.Width - 20));
        var height = Math.Min(315, Math.Max(225, canvas.Height - 20));
        SetSize(width, height);
        Alignment = [];
        DeleteOnClose = false;
        DisableResizing();
        IsClosable = true;
        DrawShadow = true;
        _progress = NavalManagementWindow.Text(this, "RoyalGuideProgress", "", 20, 36, Width - 40, 24, 10);
        _progress.TextColorOverride = new Color(a: 255, r: 250, g: 202, b: 113);
        _progressTrack = new Panel(this, "RoyalGuideProgressTrack")
        {
            BackgroundColor = new Color(a: 255, r: 75, g: 53, b: 42),
        };
        _progressTrack.SetBounds(20, 62, Width - 40, 5);
        _progressFill = new Panel(_progressTrack, "RoyalGuideProgressFill")
        {
            BackgroundColor = new Color(a: 255, r: 250, g: 202, b: 113),
        };
        _progressFill.SetBounds(0, 0, 0, 5);
        _heading = NavalManagementWindow.Text(this, "TutorialHeading", "", 20, 77, Width - 40, 36, 14);
        _heading.WrappingBehavior = WrappingBehavior.Wrapped;
        _scroll = new ScrollControl(this, "TutorialScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(20, 113, Width - 40, Height - 205);
        _body = NavalManagementWindow.Text(_scroll, "TutorialBody", "", 0, 0, Width - 66, 700, 11);
        _body.WrappingBehavior = WrappingBehavior.Wrapped;
        _body.TextAlign = Pos.Left | Pos.Top;
        _scene = Add("Play cinematic", 20, Height - 87, () => Send(TutorialAction.PlayCinematic));
        _restart = Add("Replay tutorial", 20, Height - 51, () => Send(TutorialAction.Restart));
        _pause = Add("Pause guide", Width - 340, Height - 51, () => Send(TutorialAction.Pause));
        _next = Add("Next", Width - 170, Height - 51, () => Send(TutorialAction.Next));
        _scene.SetSize(160, 28);
        _restart.SetSize(160, 28);
        _pause.SetSize(160, 30);
        _next.SetSize(150, 30);
        Closed += (_, _) =>
        {
            _spotlight.Hide();
            if (_combatDrill)
                _combatGuideExplicitlyOpened = false;
            else if (RequestedOpen)
                Send(TutorialAction.Pause);
        };
        Hide();
        _spotlight.Hide();
        ResizeGuide();
    }

    private Button Add(string text, int x, int y, Action action)
    {
        var button = NavalManagementWindow.Button(this, "Tutorial" + text.Replace(" ", ""), "", x, y, 160, 30, action);
        button.Text = T(text);
        return button;
    }

    protected override void EnsureInitialized() { }

    private void Send(TutorialAction action) =>
        _send(new TutorialRequestPacket
        {
            Action = action,
            StepId = _state.StepId,
            Revision = _state.Revision,
        });

    internal void Apply(TutorialStatePacket state)
    {
        var changedStep = _state.StepId != state.StepId;
        _state = state;
        var config = JsonConvert.DeserializeObject<TutorialConfiguration>(state.ConfigurationJson);
        if (config == null)
            return;
        var step = config.Steps.FirstOrDefault(s => s.Id == state.StepId);
        _combatDrill = step != null && AdvancedCombatTutorialLessons.IsCombatObjective(step.Objective);
        if (changedStep)
        {
            _combatGuideExplicitlyOpened = false;
            _scroll.ScrollToTop();
        }

        string Content(string source) => ClientLanguages.ContentText(config.Translations.Get(source, ClientLanguages.Current));
        var number = step == null ? config.Steps.Count : config.Steps.IndexOf(step) + 1;
        _progress.Text = $"{T("Royal Guide")}  •  {number}/{config.Steps.Count}";
        _progressFraction = (double)number / Math.Max(1, config.Steps.Count);
        _heading.Text = step == null ? T("Tutorial complete") : Content(step.Title);
        _body.Text = step == null ? T("You can replay this guide at any time.") : Content(step.Text);
        _next.IsDisabled = !state.CanAdvance;
        _next.Text = T("Next");
        _scene.IsHidden = step == null || step.CinematicEventId == Guid.Empty;
        _restart.IsHidden = !state.Finished;
        _pause.IsHidden = state.Finished;

        _target = step?.HighlightTarget ?? TutorialHighlightTarget.None;
        _style = step?.HighlightStyle ?? TutorialHighlightStyle.Outline;
        RequestedOpen = state.Open;
        RefreshVisibility();
    }

    internal void CompleteOnHudAction(TutorialHighlightTarget action)
    {
        if (!IsVisibleInTree || !RequestedOpen || _combatDrill || !_state.CanAdvance || _target != action)
            return;

        Send(TutorialAction.Next);
    }

    private Base? ResolveTarget()
    {
        var ui = Interface.InGameUiOrNull;
        if (ui == null)
            return null;
        return _target switch
        {
            TutorialHighlightTarget.Menu or TutorialHighlightTarget.Inventory or
                TutorialHighlightTarget.Quests or TutorialHighlightTarget.Character =>
                ui.GameMenu?.TutorialMenuTarget(_target),
            TutorialHighlightTarget.Minimap => ui.MinimapHud,
            TutorialHighlightTarget.WorldMap => ui.MinimapHud?.TutorialWorldMapTarget,
            TutorialHighlightTarget.Hotbar => ui.Hotbar,
            _ => null,
        };
    }

    internal void RefreshVisibility()
    {
        ResizeGuide();
        _body.SetSize(_body.Width, Math.Max(50, _body.TextHeight + 12));
        var display = RequestedOpen && (!_combatDrill || _combatGuideExplicitlyOpened) &&
            !Cinematics.CinematicControls.Active && !_canvas.Children.OfType<Modal>().Any(modal => modal.IsVisibleInTree);

        if (!display)
        {
            _spotlight.Hide();
            Hide();
            return;
        }

        // Combat drills show instructions only on demand; no focus is stolen.
        var highlighted = _combatDrill ? null : ResolveTarget();
        _spotlight.Highlight(highlighted, _style);
        PositionGuide(_spotlight.GetTargetBounds());
        if (IsHidden)
        {
            Show();
            _spotlight.BringToFront();
            BringToFront();
        }
        // Do not raise the card every frame over windows opened by the player.
    }

    private void ResizeGuide()
    {
        var width = Math.Min(460, Math.Max(240, _canvas.Width - 24));
        var height = Math.Min(340, Math.Max(280, _canvas.Height - 24));
        SetSize(width, height);
        _progress.SetBounds(20, 36, width - 40, 24);
        _progressTrack.SetBounds(20, 62, width - 40, 5);
        _heading.SetBounds(20, 77, width - 40, 36);
        _scroll.SetBounds(20, 113, width - 40, height - 225);
        _body.SetSize(width - 66, _body.Height);
        _scene.SetBounds(20, height - 102, width - 40, 28);
        var buttonWidth = (width - 48) / 2;
        _restart.SetBounds(20, height - 66, buttonWidth, 30);
        _pause.SetBounds(20, height - 66, buttonWidth, 30);
        _next.SetBounds(28 + buttonWidth, height - 66, buttonWidth, 30);
        _progressFill.SetSize((int)(_progressTrack.Width * _progressFraction), 5);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_canvas.IsDisposed && !_spotlight.IsDisposed)
        {
            _spotlight.Parent = null;
            _spotlight.Dispose();
        }
        base.Dispose(disposing);
    }

    private void PositionGuide(Rectangle? highlighted)
    {
        var maxX = Math.Max(0, _canvas.Width - Width - 8);
        var maxY = Math.Max(0, _canvas.Height - Height - 8);
        var x = maxX;
        var y = Math.Max(8, Math.Min(maxY, 75));
        if (highlighted is { } target)
        {
            // Left menu: show the explanation beside the active row.
            // Top-right minimap: move the card to the opposite side.
            var targetCenterX = target.X + target.Width / 2;
            if (targetCenterX > _canvas.Width / 2)
                x = 12;
            else
                x = Math.Min(maxX, target.X + target.Width + 55);

            y = Math.Clamp(target.Y + target.Height / 2 - Height / 2, 8, Math.Max(8, maxY));
        }
        if (highlighted is { } focus)
        {
            var choices = new[] { new Point(x,y), new Point(focus.X-Width-14,y),
                new Point(x,focus.Y-Height-14),new Point(x,focus.Y+focus.Height+14) };
            int Overlap(Point point) => Math.Max(0,Math.Min(point.X+Width,focus.Right)-Math.Max(point.X,focus.X)) *
                Math.Max(0,Math.Min(point.Y+Height,focus.Bottom)-Math.Max(point.Y,focus.Y));
            var best = choices.Select(point=>new Point(Math.Clamp(point.X,8,Math.Max(8,maxX)),
                Math.Clamp(point.Y,8,Math.Max(8,maxY)))).OrderBy(Overlap).First();
            x=best.X;y=best.Y;
        }
        SetPosition(x, y);
    }

    internal void OpenGuide()
    {
        if (_state.Finished)
        {
            RequestedOpen = true;
            RefreshVisibility();
        }
        else if (_combatDrill && RequestedOpen)
        {
            _combatGuideExplicitlyOpened = true;
            RefreshVisibility();
        }
        else
        {
            Send(TutorialAction.Resume);
        }
    }
}
