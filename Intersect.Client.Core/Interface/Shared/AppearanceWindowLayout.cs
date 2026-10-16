using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
namespace Intersect.Client.Interface.Shared;
internal static class AppearanceWindowLayout
{
    public const int Width = 860;
    public const int Height = 640;
    public static void Apply(WindowControl window)
    {
        window.MinimumSize = new Point(Width, Height);
        window.SetSize(Width, Height);
        window.InnerPanelPadding = new Padding(0);
        window.IsResizable = false;
    }
}
