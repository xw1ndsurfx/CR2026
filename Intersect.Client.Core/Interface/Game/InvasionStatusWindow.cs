using Intersect.Client.Framework.File_Management;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Utilities;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class InvasionStatusWindow : Base
{
    private readonly HudDisclosure _disclosure;
    private readonly Label _title;
    private readonly Label _wave;
    private readonly Label _objective;
    private InvasionStatusPacket? _state;
    private bool _reminderMode;
    private long _reminderHideAt;

    public InvasionStatusWindow(Canvas parent) : base(parent, nameof(InvasionStatusWindow))
    {
        SetSize(440, 118);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;

        _title = new Label(this, "InvasionTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 16,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 238, g: 205, b: 125),
        };
        _title.SetBounds(12, 8, 416, 26);

        _wave = new Label(this, "InvasionWave")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _wave.SetBounds(12, 36, 416, 20);

        _objective = new Label(this, "InvasionObjective")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 220, g: 205, b: 185),
        };
        _objective.SetBounds(12, 84, 416, 20);

        _disclosure = new HudDisclosure(this, _title, "InvasionHudCollapsed");
        Hide();
    }

    public void Update()
    {
        IsHidden = (!_reminderMode && _state?.Active != true) || Globals.Database?.ShowInvasionHud == false || Interface.InGameUiOrNull?.SuppressStatusHuds == true;
        PositionAtTop();
        if (!_reminderMode || _reminderHideAt <= 0 || Timing.Global.Milliseconds <= _reminderHideAt)
            return;

        _reminderMode = false;
        _reminderHideAt = 0;
        Hide();
    }

    public void ShowReminder(string announcementText, long displayTime)
    {
        _state = null;
        _reminderMode = true;
        _reminderHideAt = Timing.Global.Milliseconds + Math.Max(1_000L, displayTime);

        var lines = (announcementText ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _title.Text = lines.Length > 0 ? lines[0] : "INVASION APPROACHING";
        _wave.Text = lines.Length > 1 ? string.Join(" ", lines.Skip(1)) : "Prepare for battle.";
        _wave.FontSize = 9;
        _objective.Text = Intersect.Client.Localization.ClientLanguages.Text("Prepare your defenses.");

        EventPanelText.Fit(_title, _title.Text, 12);
        EventPanelText.Fit(_wave, _wave.Text, 10);
        EventPanelText.Fit(_objective, _objective.Text, 10);
        PositionAtTop();
        Show();
        BringToFront();
    }

    public void Apply(InvasionStatusPacket state)
    {
        _reminderMode = false;
        _reminderHideAt = 0;
        _wave.FontSize = 10;
        _state = state;
        if (!state.Active)
        {
            Hide();
            return;
        }

        _title.Text = ClientLanguages.Format(state.BossWave ? "BOSS INVASION - {0}" : "INVASION - {0}", ClientLanguages.ContentText(state.Name));
        _wave.Text = state.Wave <= 0
            ? ClientLanguages.Format("Preparing - {0} wave(s)", state.WaveCount)
            : ClientLanguages.Format("Wave {0}/{1}", state.Wave, state.WaveCount) +
              (string.IsNullOrWhiteSpace(state.Message) ? string.Empty : " - " + ClientLanguages.ContentText(state.Message));
        _objective.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"Defense target: {state.ObjectiveHealth:N0} / {state.ObjectiveMaxHealth:N0} HP");

        EventPanelText.Fit(_title, _title.Text, 12);
        EventPanelText.Fit(_wave, _wave.Text, 10);
        EventPanelText.Fit(_objective, _objective.Text, 10);
        PositionAtTop();
        Show();
        BringToFront();
    }

    private void PositionAtTop()
    {
        X = Math.Max(8, (Parent?.Width ?? Width) / 2 - Width / 2);
        Y = 16;
    }

    protected override void Render(SkinBase skin)
    {
        if (!_reminderMode && (_state == null || !_state.Active)) return;

        var renderer = skin.Renderer;
        var bounds = RenderBounds;
        renderer.DrawColor = new Color(a: 235, r: 37, g: 24, b: 20);
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = new Color(a: 255, r: 139, g: 92, b: 54);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));

        if (_disclosure.Collapsed) return;

        if (_reminderMode)
        {
            renderer.DrawColor = new Color(a: 255, r: 238, g: 205, b: 125);
            renderer.DrawFilledRect(new Rectangle(bounds.X + 28, bounds.Y + 76, bounds.Width - 56, 2));
            return;
        }

        var bar = new Rectangle(bounds.X + 28, bounds.Y + 62, bounds.Width - 56, 15);
        renderer.DrawColor = new Color(a: 255, r: 28, g: 18, b: 17);
        renderer.DrawFilledRect(bar);

        var fraction = _state.ObjectiveMaxHealth <= 0
            ? 0d
            : Math.Clamp(_state.ObjectiveHealth / (double)_state.ObjectiveMaxHealth, 0d, 1d);
        var fill = (int)Math.Round(bar.Width * fraction);
        if (fill > 0)
        {
            renderer.DrawColor = fraction > 0.5
                ? new Color(a: 255, r: 95, g: 180, b: 85)
                : fraction > 0.25
                    ? new Color(a: 255, r: 215, g: 160, b: 65)
                    : new Color(a: 255, r: 205, g: 70, b: 60);
            renderer.DrawFilledRect(new Rectangle(bar.X, bar.Y, fill, bar.Height));
        }
    }
}
