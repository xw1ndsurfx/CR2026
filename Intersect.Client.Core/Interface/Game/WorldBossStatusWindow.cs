using Intersect.Client.Framework.File_Management;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class WorldBossStatusWindow : Base
{
    private readonly HudDisclosure _disclosure;
    private readonly Label _title;
    private readonly Label _health;
    private readonly Label _place;
    private readonly RichLabel _ranking;
    private readonly ScrollControl _rankings;
    private string _rankingText = string.Empty;
    private string _titleText = string.Empty, _placeText = string.Empty;
    private readonly Label _timer;
    private WorldBossStatusPacket? _status;
    private int _activeCount;

    public WorldBossStatusWindow(Canvas parent) : base(parent, nameof(WorldBossStatusWindow))
    {
        SetSize(486, 304);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _title = Label("WorldBossTitle", 8, 8, 442, 25, 15);
        _health = Label("WorldBossHealth", 8, 43, 442, 20, 10);
        _place = Label("WorldBossPlace", 8, 93, 442, 20, 9);
        _rankings = new ScrollControl(this, "WorldBossRankings") { ShouldDrawBackground = false };
        _ranking = EventPanelText.CreateBody(_rankings, "WorldBossRanking", 10);
        _timer = Label("WorldBossTimer", 8, 211, 442, 19, 9);
        _disclosure = new HudDisclosure(this, _title, "WorldBossHudCollapsed");
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
        _titleText = ClientLanguages.Format("WORLD BOSS - {0}", ClientLanguages.ContentText(packet.Name));
        if (activeCount > 1) _titleText = ClientLanguages.Format("{0} (+{1} active)", _titleText, activeCount - 1);
        _health.Text = ClientLanguages.Format("HP: {0:N0} / {1:N0}", packet.Health, packet.MaxHealth);
        _placeText = ClientLanguages.Format("{0} ({1}, {2}) | {3} participants", ClientLanguages.ContentText(packet.MapName), packet.SpawnX, packet.SpawnY, packet.ParticipantCount);
        var lines = (packet.TopDamagers ?? []).Take(5).Select((entry, i) => $"{i + 1}. {entry.PlayerName} - {entry.Damage:N0}");
        _rankingText = (packet.YourRank > 0 ? ClientLanguages.Format("TOP DAMAGE | YOUR RANK: #{0} ({1:N0})", packet.YourRank, packet.YourDamage) : ClientLanguages.Text("TOP DAMAGE | No contribution yet")) + "\n\n" + string.Join("\n", lines);
        Arrange();
        EventPanelText.SetBody(_rankings, _ranking, _rankingText);
        _rankings.ScrollToTop();
        Show();
        BringToFront();
        Update();
    }

    public void Update()
    {
        IsHidden = _status == null || !_status.Active || Globals.Database?.ShowWorldBossHud == false || Interface.InGameUiOrNull?.SuppressStatusHuds == true;
        if (IsHidden) return;
        Arrange();
        var remaining = DateTimeOffset.FromUnixTimeMilliseconds(
            _status.ExpiresAtUnixMilliseconds) - DateTimeOffset.UtcNow;
        _timer.Text = remaining > TimeSpan.Zero
            ? ClientLanguages.Format("DESPAWNS IN {0:00}:{1:00}", Math.Max(0, (int)remaining.TotalMinutes), remaining.Seconds)
            : ClientLanguages.Text("TIME EXPIRED");
    }

    private void Arrange()
    {
        var width = Math.Max(240, Math.Min(486, (Parent?.Width ?? 502) - 16));
        var availableHeight = Parent?.Height ?? 720;
        var top = (Interface.InGameUiOrNull?.MinimapBottom ?? 10) + 8;
        var height = Math.Min(304, Math.Max(180, availableHeight - top - 8));
        var changed = Width != width || _rankings.Height != height - 160;
        Width = width; _disclosure.Resize(height);
        X = Math.Max(8, (Parent?.Width ?? Width + 16) - Width - 8);
        Y = Math.Max(8, Math.Min(top, availableHeight - Height - 8));
        _title.SetBounds(8, 8, width - 46, 26); _health.SetBounds(8, 43, width - 16, 24);
        _place.SetBounds(8, 92, width - 16, 26);
        _rankings.SetBounds(10, 124, width - 20, height - 160); _timer.SetBounds(8, height - 27, width - 16, 21);
        EventPanelText.Fit(_title, _titleText, 12); EventPanelText.Fit(_place, _placeText, 10);
        if (_status != null) EventPanelText.Fit(_health, ClientLanguages.Format("HP: {0:N0} / {1:N0}", _status.Health, _status.MaxHealth), 11);
        if (changed) EventPanelText.SetBody(_rankings, _ranking, _rankingText);
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

        if (_disclosure.Collapsed) return;

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
