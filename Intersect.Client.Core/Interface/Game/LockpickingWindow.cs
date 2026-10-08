using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.Client;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed class LockpickingWindow : Base
{
    private readonly Canvas _canvas;
    private readonly Action<LockpickingRequestKind, int> _send;
    private readonly Label _title;
    private readonly Label _status;
    private readonly Label _angleText;
    private readonly Label _hint;
    private readonly Label _timer;
    private readonly Button _attempt;
    private readonly Button _left15;
    private readonly Button _left5;
    private readonly Button _right5;
    private readonly Button _right15;
    private readonly Button _cancel;
    private int _angle;
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public LockpickingWindow(Canvas canvas, Action<LockpickingRequestKind, int> send)
        : base(canvas, nameof(LockpickingWindow))
    {
        _canvas = canvas;
        _send = send;

        ShouldDrawBackground = false;
        MouseInputEnabled = true;
        KeyboardInputEnabled = false;

        SetPosition(0, 0);
        SetSize(canvas.Width, canvas.Height);

        _title = MakeLabel("LockTitle", "DUNGEON LOCK", 22);
        _status = MakeLabel("LockStatus", "", 13);
        _angleText = MakeLabel("LockAngle", "", 16);
        _hint = MakeLabel("LockHint", "Move the pick, then try to turn the cylinder.", 13);
        _timer = MakeLabel("LockTimer", "", 13);

        _left15 = MakeButton("LockLeft15", "<<", () => Move(-15));
        _left5 = MakeButton("LockLeft5", "<", () => Move(-5));
        _attempt = MakeButton("LockAttempt", "TURN LOCK", Attempt);
        _right5 = MakeButton("LockRight5", ">", () => Move(5));
        _right15 = MakeButton("LockRight15", ">>", () => Move(15));
        _cancel = MakeButton("LockCancel", "Give up", () => ExitRequested = true);

        Layout();
        RefreshAngle();
    }

    private Label MakeLabel(string name, string text, int size)
    {
        return new Label(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = size,
            Text = text,
            AutoSizeToContents = false,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };
    }

    private Button MakeButton(string name, string text, Action action)
    {
        var button = new Button(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = 12,
            Text = text,
        };
        button.Clicked += (_, _) => action();
        return button;
    }

    private void Layout()
    {
        SetPosition(0, 0);
        SetSize(_canvas.Width, _canvas.Height);

        var panelWidth = Math.Min(720, Math.Max(520, _canvas.Width - 80));
        var panelHeight = 500;
        var left = Math.Max(20, (_canvas.Width - panelWidth) / 2);
        var top = Math.Max(20, (_canvas.Height - panelHeight) / 2);

        _title.SetBounds(left + 30, top + 24, panelWidth - 60, 38);
        _status.SetBounds(left + 30, top + 70, panelWidth - 60, 30);
        _angleText.SetBounds(left + 30, top + 122, panelWidth - 60, 32);
        _hint.SetBounds(left + 30, top + 300, panelWidth - 60, 34);
        _timer.SetBounds(left + 30, top + 340, panelWidth - 60, 30);

        _left15.SetBounds(left + 60, top + 390, 80, 42);
        _left5.SetBounds(left + 150, top + 390, 80, 42);
        _attempt.SetBounds(left + panelWidth / 2 - 90, top + 384, 180, 54);
        _right5.SetBounds(left + panelWidth - 230, top + 390, 80, 42);
        _right15.SetBounds(left + panelWidth - 140, top + 390, 80, 42);
        _cancel.SetBounds(left + panelWidth / 2 - 70, top + 448, 140, 34);
    }

    public void Update(LockpickingClientModel model, long now)
    {
        Layout();

        if (model.Current is not { } state)
            return;

        _attempt.IsDisabled = model.Pending || model.RemainingMilliseconds(now) <= 0;
        _status.Text =
            $"Difficulty {state.Difficulty}/5  •  Mistakes {state.Mistakes}/{state.MaxMistakes}  •  Cylinder {state.TurnPercent}%";

        if (!string.IsNullOrWhiteSpace(state.Hint))
            _hint.Text = state.Hint + " — adjust the pick and try again.";

        var remaining = model.RemainingMilliseconds(now);
        _timer.Text = $"Time: {remaining / 1000.0:0.0}s";
    }

    private void Move(int delta)
    {
        _angle = Math.Clamp(_angle + delta, -90, 90);
        RefreshAngle();
    }

    private void RefreshAngle()
    {
        _angleText.Text = $"Pick angle: {_angle:+0;-0;0}°";
    }

    private void Attempt()
    {
        _send(LockpickingRequestKind.Attempt, _angle);
    }

    protected override void Render(SkinBase skin)
    {
        var panelWidth = Math.Min(720, Math.Max(520, _canvas.Width - 80));
        var panelHeight = 500;
        var left = Math.Max(20, (_canvas.Width - panelWidth) / 2);
        var top = Math.Max(20, (_canvas.Height - panelHeight) / 2);

        skin.Renderer.DrawColor = new Color(a: 245, r: 15, g: 17, b: 20);
        skin.Renderer.DrawFilledRect(new Rectangle(left, top, panelWidth, panelHeight));

        var lockX = left + panelWidth / 2 - 130;
        var lockY = top + 166;
        skin.Renderer.DrawColor = new Color(a: 255, r: 115, g: 105, b: 83);
        skin.Renderer.DrawFilledRect(new Rectangle(lockX, lockY, 260, 118));

        skin.Renderer.DrawColor = new Color(a: 255, r: 38, g: 40, b: 44);
        skin.Renderer.DrawFilledRect(new Rectangle(lockX + 75, lockY + 22, 110, 74));

        var indicatorX = lockX + 130 + (int)(_angle / 90f * 92f);
        skin.Renderer.DrawColor = new Color(a: 255, r: 220, g: 205, b: 150);
        skin.Renderer.DrawFilledRect(new Rectangle(indicatorX - 3, lockY - 18, 6, 130));

        base.Render(skin);
    }

    public void Destroy()
    {
        if (_destroyed)
            return;

        _destroyed = true;
        Hide();
        Parent?.RemoveChild(this, false);
        Dispose();
    }
}
