using Intersect.Enums;

namespace Intersect.Framework.Core.GameObjects.Combat;

/// <summary>
/// Conservative gate: only true hostile single-target native spells without splash
/// can be avoided by leaving their locked tile. Native AoE/projectiles keep their
/// existing behavior until a separate, fully authoritative geometry integration.
/// </summary>
public static class AdvancedSpellTelegraphRule
{
    public const int MaximumNaturalCastMs = 5_000;

    public static bool CanWarn(
        bool featureEnabled,
        bool hasPetOwner,
        SpellType spellType,
        SpellTargetType targetType,
        bool friendly,
        int hitRadius,
        int naturalCastMs) =>
        featureEnabled &&
        !hasPetOwner &&
        spellType == SpellType.CombatSpell &&
        targetType == SpellTargetType.Single &&
        !friendly &&
        hitRadius == 0 &&
        naturalCastMs is >= 0 and <= MaximumNaturalCastMs;

    public static int EffectiveCastDurationMs(int originalCastMs, int telegraphMs) =>
        Math.Max(
            Math.Clamp(originalCastMs, 0, MaximumNaturalCastMs),
            Math.Clamp(telegraphMs, 250, 2_000));

    public static bool RemainsOnWarnedTile(
        Guid originalTargetId, Guid targetId,
        Guid originalMapId, Guid currentMapId,
        Guid originalInstanceId, Guid currentInstanceId,
        int originalX, int originalY, int currentX, int currentY,
        bool targetAlive) =>
        targetAlive &&
        targetId == originalTargetId &&
        currentMapId == originalMapId &&
        currentInstanceId == originalInstanceId &&
        currentX == originalX &&
        currentY == originalY;
}
