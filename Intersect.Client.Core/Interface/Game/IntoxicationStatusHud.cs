using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class IntoxicationStatusHud : Base
{
    private readonly Label _title;
    private readonly Label _timer;

    internal IntoxicationStatusHud(Canvas parent) : base(parent, nameof(IntoxicationStatusHud))
    {
        SetSize(220, 60);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        _title = new Label(this, "IntoxicationLevel")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Left,
            TextColorOverride = new Color(a: 255, r: 255, g: 212, b: 130),
        };
        _title.SetBounds(50, 8, 162, 21);
        _timer = new Label(this, "IntoxicationTimer")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Left,
            TextColorOverride = Color.White,
        };
        _timer.SetBounds(50, 32, 162, 18);
        Hide();
    }

    internal void Update(GameInterface gameInterface)
    {
        var seconds = IntoxicationVisuals.RemainingSeconds;
        IsHidden = seconds <= 0 || gameInterface.SuppressStatusHuds;
        if (IsHidden) return;

        var parentWidth = Parent?.Width ?? Width;
        var parentHeight = Parent?.Height ?? Height;
        SetPosition(
            Math.Clamp(parentWidth - Width - 10, 0, Math.Max(0, parentWidth - Width)),
            Math.Clamp(gameInterface.MinimapBottom + 8, 0, Math.Max(0, parentHeight - Height)));
        _title.Text = ClientLanguages.Format("Intoxication - level {0}/5", IntoxicationVisuals.DrunkIntensity + 1);
        _timer.Text = ClientLanguages.Format("Remaining: {0:00}:{1:00}", seconds / 60, seconds % 60);
    }

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;
        var bounds = RenderBounds;
        renderer.DrawColor = new Color(a: 232, r: 34, g: 30, b: 27);
        renderer.DrawFilledRect(bounds);
        renderer.DrawColor = new Color(a: 255, r: 155, g: 114, b: 58);
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2));
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, 2, bounds.Height));
        renderer.DrawFilledRect(new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height));

        var mugX = bounds.X + 15;
        var mugY = bounds.Y + 13;
        renderer.DrawColor = new Color(a: 255, r: 109, g: 65, b: 26);
        renderer.DrawFilledRect(new Rectangle(mugX + 19, mugY + 5, 11, 19));
        renderer.DrawColor = new Color(a: 255, r: 216, g: 158, b: 66);
        renderer.DrawFilledRect(new Rectangle(mugX, mugY + 5, 22, 28));
        renderer.DrawColor = new Color(a: 255, r: 243, g: 230, b: 187);
        renderer.DrawFilledRect(new Rectangle(mugX - 1, mugY + 2, 24, 9));
        renderer.DrawColor = new Color(a: 255, r: 165, g: 93, b: 34);
        renderer.DrawFilledRect(new Rectangle(mugX + 8, mugY + 12, 3, 18));

        for (var i = 0; i < 5; i++)
        {
            renderer.DrawColor = i <= IntoxicationVisuals.DrunkIntensity
                ? new Color(a: 255, r: 225, g: 174, b: 77)
                : new Color(a: 255, r: 88, g: 79, b: 64);
            renderer.DrawFilledRect(new Rectangle(bounds.X + 51 + i * 22, bounds.Bottom - 6, 16, 2));
        }
    }
}
