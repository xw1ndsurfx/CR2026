using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Localization;
using Intersect.GameObjects;
namespace Intersect.Client.Interface.Game;
internal sealed class DayPhaseHud : Label
{
    public DayPhaseHud(Base parent) : base(parent, "CurrentDayPhase")
    {
        AutoSizeToContents = false;
        Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
        FontSize = 10;
        TextColorOverride = Color.White;
        MouseInputEnabled = KeyboardInputEnabled = false;
        SetBounds(8, 4, 180, 20);
        IsHidden = true;
    }
    protected override void RenderUnder(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        skin.Renderer.DrawColor = new Color(180, 28, 22, 18);
        skin.Renderer.DrawFilledRect(RenderBounds);
    }
    public void UpdatePhase(DayPhase phase, bool available)
    {
        IsHidden = !available;
        if (available) { Text = ClientLanguages.Text(phase.ToString()); SetBounds(8, 4, 180, 20); BringToFront(); }
    }
}
