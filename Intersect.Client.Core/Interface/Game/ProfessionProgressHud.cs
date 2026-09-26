using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Server;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class ProfessionProgressHud : Base
{
    private const int HudWidth = 460;
    private const int HudHeight = 92;
    private const int DisplayDurationMs = 5_000;
    private const int BarX = 18;
    private const int BarY = 57;
    private const int BarWidth = HudWidth - 36;
    private const int BarHeight = 20;

    private readonly Label _title;
    private readonly Label _details;
    private readonly Label _barText;

    private long _hideAtMs;
    private float _progress;

    public ProfessionProgressHud(Base parent) : base(parent, "ProfessionProgressHud")
    {
        SetSize(HudWidth, HudHeight);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;

        var titleFont = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
        var normalFont = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;

        _title = new Label(this, "ProfessionProgressTitle")
        {
            AutoSizeToContents = false,
            Font = titleFont,
            FontSize = 13,
            TextColorOverride = Color.White,
            TextAlign = Pos.Center,
            MouseInputEnabled = false,
        };
        _title.SetBounds(14, 8, HudWidth - 28, 23);

        _details = new Label(this, "ProfessionProgressDetails")
        {
            AutoSizeToContents = false,
            Font = normalFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 231, g: 220, b: 195),
            TextAlign = Pos.Center,
            MouseInputEnabled = false,
        };
        _details.SetBounds(14, 31, HudWidth - 28, 20);

        _barText = new Label(this, "ProfessionProgressBarText")
        {
            AutoSizeToContents = false,
            Font = titleFont,
            FontSize = 9,
            TextColorOverride = Color.White,
            TextAlign = Pos.Center,
            MouseInputEnabled = false,
        };
        _barText.SetBounds(BarX, BarY, BarWidth, BarHeight);

        Hide();
    }

    public void ShowProgress(ProfessionProgressPacket packet)
    {
        _progress = Math.Clamp(packet.Percentage / 100f, 0f, 1f);

        var levelUpSuffix = packet.LeveledUp ? "  •  LEVEL UP!" : string.Empty;
        _title.Text =
            $"{packet.ProfessionName}  •  Level {packet.Level}/{packet.MaximumLevel}{levelUpSuffix}";

        if (packet.MaximumLevelReached)
        {
            _details.Text =
                packet.ExperienceGained > 0
                    ? $"+{packet.ExperienceGained:N0} XP  •  Maximum level reached"
                    : "Maximum level reached";
            _barText.Text = "MAX LEVEL";
            _progress = 1f;
        }
        else
        {
            _details.Text =
                $"+{packet.ExperienceGained:N0} XP  •  " +
                $"{packet.ExperienceToNextLevel:N0} XP remaining";

            _barText.Text =
                $"{packet.Percentage}%   " +
                $"({packet.ExperienceIntoLevel:N0}/{packet.ExperienceRequiredForLevel:N0} XP)";
        }

        _hideAtMs = Timing.Global.Milliseconds + DisplayDurationMs;
        Show();
        BringToFront();
        Update();
    }

    public void Update()
    {
        if (Parent is { } parent)
        {
            SetPosition(
                Math.Max(8, (parent.Width - Width) / 2),
                Math.Max(8, parent.Height - Height - 120)
            );
        }

        if (!IsHidden && _hideAtMs > 0 && Timing.Global.Milliseconds >= _hideAtMs)
            Hide();
    }

    protected override void Render(SkinBase skin)
    {
        if (IsHidden)
            return;

        var renderer = skin.Renderer;

        // Dark brown / bronze styling to match CR UI and explicitly avoid purple.
        Fill(renderer, new Color(a: 232, r: 24, g: 18, b: 16), 0, 0, Width, Height);
        Outline(renderer, new Color(a: 255, r: 116, g: 74, b: 45), 0, 0, Width, Height, 2);

        Fill(renderer, new Color(a: 255, r: 33, g: 28, b: 24), BarX, BarY, BarWidth, BarHeight);

        var fillWidth = (int)Math.Round((BarWidth - 4) * _progress, MidpointRounding.AwayFromZero);
        if (fillWidth > 0)
        {
            Fill(
                renderer,
                new Color(a: 255, r: 169, g: 122, b: 58),
                BarX + 2,
                BarY + 2,
                fillWidth,
                BarHeight - 4
            );
        }

        Outline(
            renderer,
            new Color(a: 255, r: 224, g: 188, b: 111),
            BarX,
            BarY,
            BarWidth,
            BarHeight,
            1
        );

        base.Render(skin);
    }

    private static void Outline(
        RendererBase renderer,
        Color color,
        int x,
        int y,
        int width,
        int height,
        int thickness
    )
    {
        Fill(renderer, color, x, y, width, thickness);
        Fill(renderer, color, x, y + height - thickness, width, thickness);
        Fill(renderer, color, x, y, thickness, height);
        Fill(renderer, color, x + width - thickness, y, thickness, height);
    }

    private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(x, y, width, height));
    }
}
