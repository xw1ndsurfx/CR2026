namespace Intersect.Client.Interface.Game;

internal static class WeaponSkillHudVisibility
{
    internal static bool Allowed(bool hasPlayer, bool aboard, bool miniGame, bool blockingOverlay) =>
        hasPlayer && !aboard && !miniGame && !blockingOverlay;
}
