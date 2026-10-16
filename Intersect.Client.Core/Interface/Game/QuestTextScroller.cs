using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
namespace Intersect.Client.Interface.Game;

internal static class QuestTextScroller
{
    internal static void Configure(ScrollControl area, RichLabel text)
    {
        area.MouseInputEnabled = true; area.OverflowX = OverflowBehavior.Hidden; area.OverflowY = OverflowBehavior.Scroll; area.AutoHideBars = false;
        text.Dock = Pos.Top; text.MouseInputEnabled = false;
        text.Rebuilt += (_, _) => area.UpdateScrollBars();
    }
}
