using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class WorldBossStatusWindow : Base
{
    private readonly Label _title;
    private readonly Label _health;
    private readonly Label _place;
    private readonly Label _ranking;
    private readonly Label _timer;
    private WorldBossStatusPacket? _status;
    private int _activeCount;

    public WorldBossStatusWindow(Canvas parent) : base(parent, nameof(WorldBossStatusWindow))
    {
        SetSize(458, 236);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _title = Label("WorldBossTitle", 8, 8, 442, 25, 15);
        _health = Label("WorldBossHealth", 8, 43, 442, 20, 10);
        _place = Label("WorldBossPlace", 8, 93, 442, 20, 9);
        _ranking = Label("WorldBossRanking", 15, 120, 428, 92, 9);
        _timer = Label("WorldBossTimer", 8, 211, 442, 19, 9);
        Hide();
    }

    private Label Label(string name, int x, int y, int width, int height, int size)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = size,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    public void Apply(WorldBossStatusPacket packet, int activeCount)
    {
        _status = packet;
        _activeCount = activeCount;
        _title.Text = "WORLD BOSS  •  " + packet.Name +
            (activeCount > 1 ? $"  (+{activeCount - 1} active)" : "");
        _health.Text = $"HP: {packet.Health:N0} / {packet.MaxHealth:N0}";
        _place.Text = $"{packet.MapName} ({packet.SpawnX}, {packet.SpawnY})  •  " +
            $"{packet.ParticipantCount} participants";
        var lines = (packet.TopDamagers ?? [])
            .Take(5).Select((entry, i) =>
                $"{i + 1}. {entry.PlayerName}  —  {entry.Damage:N0}");
        _ranking.Text = "TOP DAMAGE  |  " +
            (packet.YourRank > 0 ? $"YOU #{packet.YourRank}: {packet.YourDamage:N0}" : "No contribution yet") +
            "\n" + string.Join("\n", lines);
        _ranking.FontSize = 8;
        X = Math.Max(8, (Parent?.Width ?? Width) - Width - 18);
        Y = 18;
        Show();
        BringToFront();
        Update();
    }

    public void Update()
    {
        if (_status == null || !_status.Active) return;
        var remaining = DateTimeOffset.FromUnixTimeMilliseconds(
            _status.ExpiresAtUnixMilliseconds) - DateTimeOffset.UtcNow;
        _timer.Text = remaining > TimeSpan.Zero
            ? $"DESPAWNS IN {Math.Max(0, (int)remaining.TotalMinutes):00}:{remaining.Seconds:00}"
            : "TIME EXPIRED";
    }

    protected override void Render(SkinBase skin)
    {
        if (_status == null || !_status.Active) return;
        var bounds = RenderBounds;
        var renderer = skin.Renderer;
        renderer.DrawColor = new Color(a: 240, r: 28, g: 18, b: 23);
        renderer.DrawFilledRect(bounds);
        renderer.DrawColor = new Color(a: 255, r: 208, g: 150, b: 68);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 3));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));

        var track = new Rectangle(bounds.X + 30, bounds.Y + 72, bounds.Width - 60, 13);
        renderer.DrawColor = new Color(a: 255, r: 54, g: 37, b: 38);
        renderer.DrawFilledRect(track);
        var ratio = Math.Clamp(_status.Health / (double)Math.Max(1, _status.MaxHealth), 0d, 1d);
        var width = (int)Math.Round(ratio * track.Width);
        if (width > 0)
        {
            renderer.DrawColor = new Color(a: 255, r: 195, g: 58, b: 62);
            renderer.DrawFilledRect(new Rectangle(track.X, track.Y, width, track.Height));
        }
    }
}
