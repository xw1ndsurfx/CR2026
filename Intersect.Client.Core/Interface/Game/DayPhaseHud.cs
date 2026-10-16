using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.GameObjects;
namespace Intersect.Client.Interface.Game;
internal sealed class DayPhaseHud : Label
{
    public DayPhaseHud(Base parent) : base(parent, "CurrentDayPhase")
    {
        FontSize = 10;
        TextColorOverride = Color.White;
        MouseInputEnabled = KeyboardInputEnabled = false;
        SetBounds(8, 4, 180, 20);
        IsHidden = true;
    }
    public void UpdatePhase(DayPhase phase, bool available)
    {
        IsHidden = !available;
        if (available) Text = ClientLanguages.Text(phase.ToString());
    }
}
