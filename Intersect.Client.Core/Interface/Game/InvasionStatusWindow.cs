using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.WorldEvents;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class InvasionStatusWindow : Base
{
    private readonly Label _title;
    private readonly Label _wave;
    private readonly Label _objective;
    private InvasionStatusPacket? _state;

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

        Hide();
    }

    public void Apply(InvasionStatusPacket state)
    {
        _state = state;
        if (!state.Active)
        {
            Hide();
            return;
        }

        _title.Text = state.BossWave ? $"BOSS INVASION • {state.Name}" : $"INVASION • {state.Name}";
        _wave.Text = state.Wave <= 0
            ? $"Preparing • {state.WaveCount} wave(s)"
            : $"Wave {state.Wave}/{state.WaveCount}" +
              (string.IsNullOrWhiteSpace(state.Message) ? string.Empty : $" • {state.Message}");
        _objective.Text = $"Defense target: {state.ObjectiveHealth:N0} / {state.ObjectiveMaxHealth:N0} HP";

        X = Math.Max(8, (Parent?.Width ?? Width) / 2 - Width / 2);
        Y = 16;
        Show();
        BringToFront();
    }

    protected override void Render(SkinBase skin)
    {
        if (_state == null || !_state.Active) return;

        var renderer = skin.Renderer;
        var bounds = RenderBounds;
        renderer.DrawColor = new Color(a: 235, r: 37, g: 24, b: 20);
        renderer.DrawFilledRect(bounds);

        renderer.DrawColor = new Color(a: 255, r: 139, g: 92, b: 54);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));

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
