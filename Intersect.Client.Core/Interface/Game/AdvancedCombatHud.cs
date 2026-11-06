using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Compact, movable bronze/gold endurance HUD. It never assumes a client-predicted
/// stamina value: every update is the authoritative value sent by the server.
/// </summary>
internal sealed class AdvancedCombatHud : Base
{
    private readonly Label _title;
    private readonly Label _amount;
    private readonly Label _controls;
    private readonly Label _feedback;

    private int _current = 100;
    private int _maximum = 100;
    private long _dodgeReadyAt;
    private long _feedbackUntil;
    private AdvancedCombatFeedback _recentFeedback;

    internal AdvancedCombatHud(Canvas parent) : base(parent, nameof(AdvancedCombatHud))
    {
        SetSize(276, 91);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;

        var titleFont = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
        var regular = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;
        _title = new Label(this, "AdvancedCombatEnduranceLabel")
        {
            AutoSizeToContents = false, Font = titleFont, FontSize = 12,
            TextColorOverride = new Color(a: 255, r: 236, g: 201, b: 130),
            TextAlign = Pos.Left,
        };
        _title.SetBounds(13, 5, 140, 22);
        _title.Text = ClientLanguages.Text("Endurance");

        _amount = new Label(this, "AdvancedCombatEnduranceAmount")
        {
            AutoSizeToContents = false, Font = regular, FontSize = 10,
            TextColorOverride = Color.White, TextAlign = Pos.Right,
        };
        _amount.SetBounds(156, 6, 107, 21);

        _controls = new Label(this, "AdvancedCombatControlHints")
        {
            AutoSizeToContents = false, Font = regular, FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 212, g: 200, b: 167),
            TextAlign = Pos.Left,
        };
        _controls.SetBounds(13, 55, 250, 15);

        _feedback = new Label(this, "AdvancedCombatFeedback")
        {
            AutoSizeToContents = false, Font = titleFont, FontSize = 10,
            TextColorOverride = new Color(a: 255, r: 246, g: 209, b: 123),
            TextAlign = Pos.Center,
        };
        _feedback.SetBounds(12, 71, 252, 17);
        Hide();
    }

    internal void Apply(AdvancedCombatStatePacket packet)
    {
        _maximum = Math.Max(1, packet.Maximum);
        _current = Math.Clamp(packet.Current, 0, _maximum);
        _dodgeReadyAt = Timing.Global.Milliseconds + Math.Clamp(packet.DodgeCooldownMs, 0, 20_000);
        _amount.Text = $"{_current}/{_maximum}";
        if (packet.Feedback != AdvancedCombatFeedback.None)
        {
            _recentFeedback = packet.Feedback;
            _feedbackUntil = Timing.Global.Milliseconds + 1_100;
            _feedback.Text = packet.Feedback switch
            {
                AdvancedCombatFeedback.Dodge => ClientLanguages.Text("DODGE!"),
                AdvancedCombatFeedback.PerfectParry => ClientLanguages.Text("PERFECT PARRY!"),
                AdvancedCombatFeedback.CounterAttack => ClientLanguages.Text("COUNTERATTACK!"),
                AdvancedCombatFeedback.Exhausted => ClientLanguages.Text("NOT ENOUGH ENDURANCE"),
                _ => string.Empty,
            };
        }
    }

    internal void Update(GameInterface gameInterface)
    {
        if (IsHidden)
            return;

        var w = Parent?.Width ?? Width;
        var h = Parent?.Height ?? Height;
        SetPosition(Math.Max(0, (w - Width) / 2), Math.Max(0, h - Height - 115));
        var cooldown = Math.Max(0L, _dodgeReadyAt - Timing.Global.Milliseconds);
        _controls.Text = cooldown > 0
            ? ClientLanguages.Format("Dodge in {0:0.0}s", cooldown / 1000d)
            : ClientLanguages.Text("Dodge ready");
        if (Timing.Global.Milliseconds >= _feedbackUntil)
            _feedback.Text = string.Empty;
    }

    protected override void Render(SkinBase skin)
    {
        if (IsHidden)
            return;

        var renderer = skin.Renderer;
        var now = Timing.Global.Milliseconds;
        Fill(renderer, new Color(a: 234, r: 26, g: 23, b: 23), 0, 0, Width, Height);
        Fill(renderer, new Color(a: 255, r: 125, g: 89, b: 46), 0, 0, Width, 2);
        Fill(renderer, new Color(a: 255, r: 125, g: 89, b: 46), 0, Height - 2, Width, 2);
        Fill(renderer, new Color(a: 255, r: 125, g: 89, b: 46), 0, 0, 2, Height);
        Fill(renderer, new Color(a: 255, r: 125, g: 89, b: 46), Width - 2, 0, 2, Height);

        Fill(renderer, new Color(a: 255, r: 55, g: 47, b: 38), 12, 32, Width - 24, 15);
        var fraction = _current / (double)Math.Max(1, _maximum);
        var filled = (int)Math.Round((Width - 28) * fraction);
        var color = fraction <= 0.22
            ? new Color(a: 255, r: 193, g: 72, b: 43)
            : new Color(a: 255, r: 212, g: 169, b: 77);
        Fill(renderer, color, 14, 34, filled, 11);

        if (now < _feedbackUntil)
        {
            var flash = _recentFeedback switch
            {
                AdvancedCombatFeedback.Dodge => new Color(a: 180, r: 65, g: 146, b: 191),
                AdvancedCombatFeedback.PerfectParry => new Color(a: 180, r: 245, g: 202, b: 99),
                AdvancedCombatFeedback.CounterAttack => new Color(a: 180, r: 212, g: 151, b: 54),
                _ => new Color(a: 150, r: 202, g: 72, b: 47),
            };
            Fill(renderer, flash, 9, 50, Width - 18, 2);
        }
        base.Render(skin);
    }

    private static void Fill(
        Intersect.Client.Framework.Gwen.Renderer.Base renderer,
        Color color, int x, int y, int width, int height)
    {
        if (width < 1 || height < 1)
            return;
        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(x, y, width, height));
    }
}
