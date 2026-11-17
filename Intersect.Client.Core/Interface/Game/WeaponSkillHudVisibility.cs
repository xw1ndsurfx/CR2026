namespace Intersect.Client.Interface.Game;

internal static class WeaponSkillHudVisibility
{
    internal static bool HasOpenWindow(Framework.Gwen.Control.Canvas canvas) =>
        canvas.Children.OfType<Framework.Gwen.Control.WindowControl>().Any(window => window.IsVisibleInTree);

    internal static bool Allowed(bool hasPlayer, bool aboard, bool miniGame, bool blockingOverlay) =>
        hasPlayer && !aboard && !miniGame && !blockingOverlay;
}
