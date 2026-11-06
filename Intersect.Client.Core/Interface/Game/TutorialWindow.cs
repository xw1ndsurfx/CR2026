using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
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
    private readonly TutorialWorldMarker _worldMarker;
    private TutorialStatePacket _state = new();
    private TutorialHighlightTarget _target = TutorialHighlightTarget.None;
    private TutorialHighlightStyle _style = TutorialHighlightStyle.Outline;
    private readonly Label _heading;
    private readonly Label _progress;
    private readonly Label _interactionHint;
    private readonly Panel _progressTrack, _progressFill;
    private Guid _lastSubmittedUiStep;
    private readonly Label _body;
    private readonly ScrollControl _scroll;
    private readonly Button _next, _scene, _restart, _pause;
    internal bool RequestedOpen { get; private set; }
    private bool _combatDrill;
    private bool _combatGuideExplicitlyOpened;
    internal static string T(string source) => TutorialText.Get(source, ClientLanguages.Current);

    internal TutorialWindow(Canvas canvas) : base(canvas, T("Tutorial"), false, nameof(TutorialWindow))
    {
        _canvas = canvas;
        _spotlight = new TutorialSpotlight(canvas);
        _worldMarker = new TutorialWorldMarker(canvas);
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
        _interactionHint = NavalManagementWindow.Text(
            this, "RoyalGuideInteractionHint", "", Width - 166, Height - 70, 160, 52, 10);
        _interactionHint.WrappingBehavior = WrappingBehavior.Wrapped;
        _interactionHint.TextColorOverride = new Color(a: 255, r: 250, g: 202, b: 113);
        _interactionHint.IsHidden = true;
        Closed += (_, _) =>
        {
            _spotlight.Hide();
            _worldMarker.Hide();
            if (_combatDrill)
                _combatGuideExplicitlyOpened = false;
            else if (RequestedOpen)
                Send(TutorialAction.Pause);
        };
        Hide();
        _spotlight.Hide();
    }

    private Button Add(string text, int x, int y, Action action)
    {
        var button = NavalManagementWindow.Button(this, "Tutorial" + text.Replace(" ", ""), "", x, y, 160, 30, action);
        button.Text = T(text);
        return button;
    }

    protected override void EnsureInitialized() { }

    private void Send(TutorialAction action) =>
        Networking.Network.SendPacket(new TutorialRequestPacket
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
        _progressFill.SetSize((int)((long)_progressTrack.Width * number / Math.Max(1, config.Steps.Count)), 5);
        _heading.Text = step == null ? T("Tutorial complete") : Content(step.Title);
        _body.Text = step == null ? T("You can replay this guide at any time.") : Content(step.Text);
        _next.IsDisabled = !state.CanAdvance;
        _next.IsHidden = step?.Objective == TutorialObjective.InteractEvent;
        _next.Text = T("Next");
        _interactionHint.IsHidden = step?.Objective != TutorialObjective.InteractEvent;
        _interactionHint.Text = T("Interact with the highlighted target.");
        _scene.IsHidden = step == null || step.CinematicEventId == Guid.Empty;
        _restart.IsHidden = !state.Finished;
        _pause.IsHidden = state.Finished;

        _target = step?.HighlightTarget ?? TutorialHighlightTarget.None;
        _style = step?.HighlightStyle ?? TutorialHighlightStyle.Outline;
        // Old saved guides predate spotlight settings. Preserve their step IDs
        // and progression while applying context to known built-in lessons.
        if (step != null && _target == TutorialHighlightTarget.None)
            _target = step.Title switch
            {
                "Your interface" => TutorialHighlightTarget.Menu,
                "Inventory and equipment" => TutorialHighlightTarget.Inventory,
                "Combat and quests" => TutorialHighlightTarget.Quests,
                "Naval adventures" => TutorialHighlightTarget.Minimap,
                _ => TutorialHighlightTarget.None,
            };
        // Old imported guides with movement objectives also receive a visible
        // player-foot marker without changing persisted step identifiers.
        if (step?.Objective == TutorialObjective.Move && _target == TutorialHighlightTarget.None)
            _target = TutorialHighlightTarget.Player;

        if (_target == TutorialHighlightTarget.Minimap)
            _style = TutorialHighlightStyle.Circle;

        _worldMarker.SetStep(step == null ? null : new TutorialStep
        {
            HighlightTarget = _target,
            MapId = step.MapId,
            TargetX = step.TargetX,
            TargetY = step.TargetY,
            TargetNpcId = step.TargetNpcId,
            TargetEventId = step.TargetEventId,
        });

        RequestedOpen = state.Open;
        RefreshVisibility();
    }

    internal void CompleteOnHudAction(TutorialHighlightTarget action)
    {
        if (!RequestedOpen || _combatDrill || !_state.CanAdvance || _target != action ||
            _lastSubmittedUiStep == _state.StepId)
            return;

        _lastSubmittedUiStep = _state.StepId;
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
            TutorialHighlightTarget.Minimap or TutorialHighlightTarget.WorldMap =>
                ui.MinimapHud,
            TutorialHighlightTarget.Hotbar => ui.Hotbar,
            _ => null,
        };
    }

    internal void RefreshVisibility()
    {
        _body.SetSize(_body.Width, Math.Max(50, _body.TextHeight + 12));
        var display = RequestedOpen && (!_combatDrill || _combatGuideExplicitlyOpened) &&
            !Cinematics.CinematicControls.Active;

        if (!display)
        {
            _spotlight.Hide();
            _worldMarker.Hide();
            Hide();
            return;
        }

        // World cues replace the darkened HUD spotlight, leaving walking and
        // NPC interactions unobstructed. Combat always disables both overlays.
        var worldTarget = _target is TutorialHighlightTarget.Player or
            TutorialHighlightTarget.WorldTile or TutorialHighlightTarget.WorldNpc or
            TutorialHighlightTarget.WorldEvent;
        if (worldTarget && !_combatDrill)
        {
            _spotlight.Hide();
            _worldMarker.RefreshVisibility();
            PositionGuide(_worldMarker.GetTargetBounds());
        }
        else
        {
            _worldMarker.Hide();
            _spotlight.Highlight(_combatDrill ? null : ResolveTarget(), _style);
            PositionGuide(_spotlight.GetTargetBounds());
        }
        if (IsHidden)
            Show();
        // Keep the card above the translucent spotlight, which must never
        // intercept clicking the highlighted underlying menu item.
        BringToFront();
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
