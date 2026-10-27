using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Jousting;
namespace Intersect.Client.Interface.Game;

internal sealed class JoustFullscreen : Base
{
    internal readonly JoustViewport Viewport;
    private readonly Label _score, _fall;
    private readonly List<Button> _buttons = [];
    internal Action? Tick;
    internal JoustFullscreen(Canvas canvas, bool spectator) : base(canvas)
    {
        MouseInputEnabled = true;
        Viewport = new(this) { ReadOnly = spectator, MouseInputEnabled = !spectator, KeyboardInputEnabled = false };
        Label Text()
        {
            return new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 14, TextColorOverride = Color.White, AutoSizeToContents = false, MouseInputEnabled = false,
                WrappingBehavior = WrappingBehavior.Wrapped };
        }
        _score = Text(); _fall = Text(); Hide(); ResizeToCanvas();
    }
    internal Button AddAction(string caption, Action action)
    {
        var button = NavalManagementWindow.Button(this, "JoustFullscreenAction" + _buttons.Count, ClientLanguages.Text(caption), 0, 0, 100, 36, action);
        _buttons.Add(button); ResizeToCanvas(); return button;
    }
    internal void Apply(JoustView state, string score)
    {
        Viewport.Apply(state); _score.Text = score;
        var fallen = new List<string>();
        if (state.Phase is JoustPhase.Impact or JoustPhase.Results)
        {
            if (state.OpponentStrike.Hit == JoustHit.Unhorsed) fallen.Add(state.SelfName);
            if (state.PlayerStrike.Hit == JoustHit.Unhorsed) fallen.Add(state.OpponentName);
        }
        _fall.Text = fallen.Count == 0 ? "" : ClientLanguages.Format("Knocked down: {0}", string.Join(" / ", fallen));
        ResizeToCanvas();
    }
    internal void ResizeToCanvas()
    {
        if (Parent is not { } parent) return;
        SetBounds(0, 0, parent.Width, parent.Height); Viewport.SetBounds(0, 0, Width, Height);
        _score.SetBounds(20, 16, Math.Max(1, Width - 40), 48);
        _fall.SetBounds(20, 68, Math.Max(1, Width - 40), 36);
        var width = Math.Max(1, (Width - 24) / Math.Max(1, _buttons.Count));
        for (var i = 0; i < _buttons.Count; i++) _buttons[i].SetBounds(12 + i * width, Math.Max(0, Height - 48), Math.Max(1, width - 6), 36);
    }
    public override void Think() { base.Think(); if (!IsHidden) { ResizeToCanvas(); Tick?.Invoke(); } }
}
