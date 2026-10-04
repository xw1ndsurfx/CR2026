using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class DungeonRunHudWindow : Base
{
    private readonly Label _title;
    private readonly Label _objective;
    private readonly Label _lives;
    private readonly Label _timer;

    private DungeonRunStatePacket? _state;
    private long _resultHideAt;

    public DungeonRunHudWindow(Canvas parent) : base(parent, nameof(DungeonRunHudWindow))
    {
        SetSize(430, 126);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _title = new Label(this, "DungeonRunTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 15,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 225, g: 198, b: 128),
        };
        _title.SetBounds(10, 8, 410, 24);

        _objective = new Label(this, "DungeonRunObjective")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _objective.SetBounds(10, 38, 410, 20);

        _lives = new Label(this, "DungeonRunLives")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 225, g: 198, b: 128),
        };
        _lives.SetBounds(10, 62, 410, 18);

        _timer = new Label(this, "DungeonRunTimer")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 225, g: 198, b: 128),
        };
        _timer.SetBounds(10, 88, 410, 22);

        Hide();
    }

    public void Apply(DungeonRunStatePacket state)
    {
        _state = state;
        _resultHideAt = 0;

        _title.Text = $"RANK {state.DungeonRank} • {state.DungeonName}";
        _objective.Text = state.Message;
        _lives.Text = state.MaxLives > 0
            ? $"LIVES {Math.Max(0, state.LivesRemaining)} / {state.MaxLives}"
            : string.Empty;

        if (state.Status == DungeonRunStatus.Active)
        {
            _timer.Text = BuildTimeText(state.EndTimeUnixMilliseconds);
        }
        else
        {
            _timer.Text = state.Status == DungeonRunStatus.Completed
                ? "DUNGEON CLEARED"
                : "DUNGEON FAILED";
            _resultHideAt = Timing.Global.Milliseconds + 6_000;
        }

        PositionAtTop();
        Show();
        BringToFront();
    }

    public void Update()
    {
        if (_state == null)
            return;

        if (_state.Status == DungeonRunStatus.Active)
        {
            _timer.Text = BuildTimeText(_state.EndTimeUnixMilliseconds);
            return;
        }

        if (_resultHideAt > 0 && Timing.Global.Milliseconds >= _resultHideAt)
        {
            _state = null;
            _resultHideAt = 0;
            Hide();
        }
    }

    private static string BuildTimeText(long endTimeUnixMilliseconds)
    {
        if (endTimeUnixMilliseconds <= 0)
            return "NO TIME LIMIT";

        var remaining = DateTimeOffset.FromUnixTimeMilliseconds(endTimeUnixMilliseconds) - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return "00:00";

        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";

        return $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private void PositionAtTop()
    {
        X = Math.Max(8, (Parent?.Width ?? Width) / 2 - Width / 2);
        Y = 142;
    }

    protected override void Render(SkinBase skin)
    {
        if (_state == null)
            return;

        var renderer = skin.Renderer;
        var bounds = RenderBounds;

        renderer.DrawColor = new Color(a: 230, r: 24, g: 22, b: 28);
        renderer.DrawFilledRect(bounds);

        var border = _state.Status switch
        {
            DungeonRunStatus.Completed => new Color(a: 255, r: 100, g: 205, b: 115),
            DungeonRunStatus.Failed => new Color(a: 255, r: 205, g: 78, b: 72),
            _ => new Color(a: 255, r: 194, g: 151, b: 72),
        };

        renderer.DrawColor = border;
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));
    }
}
