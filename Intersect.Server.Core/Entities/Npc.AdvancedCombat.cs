using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Network.Packets.Server;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Stat = Intersect.Enums.Stat;

namespace Intersect.Server.Entities;

public partial class Npc
{
    private readonly HashSet<CombatTelegraphTile> _advancedTelegraphTiles = [];
    private Guid _advancedTelegraphMap;
    private Guid _advancedTelegraphInstance;
    private int _advancedTelegraphAimX;
    private int _advancedTelegraphAimY;
    private Direction _advancedTelegraphFacing;
    private long _advancedTelegraphEnd;
    private bool _advancedBossEnraged;
    private bool _advancedStrikeEnraged;
    private Guid _advancedComboTargetId;
    private int _advancedComboStep;
    private int _advancedComboTotalSteps;

    private void ResetAdvancedCombo()
    {
        _advancedComboTargetId = Guid.Empty;
        _advancedComboStep = 0;
        _advancedComboTotalSteps = 0;
        _advancedTelegraphEnd = 0;
        _advancedTelegraphTiles.Clear();
    }

    private void BeginAdvancedTelegraph(Player target)
    {
        if (_advancedTelegraphEnd != 0 || target.IsDead || target.HasPendingDeath)
            return;

        // First attack is the ordinary descriptor pattern. Subsequent steps
        // are chosen only for opted-in bosses and reacquire the target each time.
        if (_advancedComboTotalSteps == 0)
        {
            _advancedComboStep = 0;
            _advancedComboTargetId = target.Id;
            _advancedComboTotalSteps = AdvancedBossComboPlan.StepCount(
                Descriptor.IsBoss, Descriptor.AdvancedBossComboEnabled,
                Descriptor.AdvancedBossComboCount);
        }

        var shape = AdvancedBossComboPlan.ShapeForStep(
            _advancedComboStep, (CombatTelegraphShape)Descriptor.AdvancedTelegraphShape,
            Descriptor.AdvancedBossComboSecondShape,
            Descriptor.AdvancedBossComboThirdShape);
        var tiles = CombatTelegraphGeometry.GetTiles(
            shape, X, Y, target.X, target.Y, Dir, Descriptor.AdvancedTelegraphRange);

        // Clip both the advertised zone and the actual damage to this map.
        _advancedTelegraphTiles.Clear();
        foreach (var tile in tiles)
        {
            if (tile.X >= 0 && tile.Y >= 0 &&
                tile.X < Options.Instance.Map.MapWidth && tile.Y < Options.Instance.Map.MapHeight)
                _advancedTelegraphTiles.Add(tile);
        }
        if (_advancedTelegraphTiles.Count == 0)
        {
            ResetAdvancedCombo();
            return;
        }

        var enrage = AdvancedCombatBossPhase.IsEnraged(
            Descriptor.IsBoss, Descriptor.AdvancedBossEnrageEnabled,
            GetVital(Vital.Health), GetMaxVital(Vital.Health),
            Options.Instance.Combat.AdvancedBossEnrageHealthPercent);
        var enteredEnrage = enrage && !_advancedBossEnraged;
        _advancedBossEnraged = enrage;
        _advancedStrikeEnraged = enrage;

        var firstWindup = Descriptor.AdvancedTelegraphWindupMs > 0
            ? Descriptor.AdvancedTelegraphWindupMs
            : Options.Instance.Combat.BossTelegraphDurationMs;
        var warnedMs = AdvancedBossComboPlan.WarningMilliseconds(
            firstWindup, Descriptor.AdvancedBossComboFollowupWindupMs, _advancedComboStep);
        var windup = AdvancedCombatBossPhase.WindupMilliseconds(
            warnedMs, enrage,
            Options.Instance.Combat.AdvancedBossEnrageWindupReductionPercent);
        _advancedTelegraphMap = target.MapId;
        _advancedTelegraphInstance = target.MapInstanceId;
        _advancedTelegraphAimX = target.X;
        _advancedTelegraphAimY = target.Y;
        _advancedTelegraphFacing = Dir;
        _advancedTelegraphEnd = Timing.Global.Milliseconds + windup;

        var packet = new CombatTelegraphPacket(
            _advancedTelegraphMap, _advancedTelegraphAimX, _advancedTelegraphAimY, windup)
        {
            Tiles = _advancedTelegraphTiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X)
                .SelectMany(tile => new[] { tile.X, tile.Y }).ToArray(),
            Enraged = enrage,
            EnrageStarted = enteredEnrage,
            ComboStep = _advancedComboTotalSteps > 1 ? _advancedComboStep + 1 : 0,
            ComboTotal = _advancedComboTotalSteps > 1 ? _advancedComboTotalSteps : 0,
        };
        // Broadcast to the correct map instance so nearby players see the attack.
        PacketSender.SendDataToProximityOnMapInstance(MapId, MapInstanceId, packet);
    }

    /// <returns>True if a pending windup consumed this AI update.</returns>
    private bool ProcessAdvancedTelegraph(long timeMs)
    {
        if (_advancedTelegraphEnd == 0)
            return false;

        if (!Options.Instance.Combat.EnableAdvancedCombat)
        {
            ResetAdvancedCombo();
            return false;
        }

        // A stun during the windup interrupts the charged attack immediately.
        if (CachedStatuses.Any(status => status.Type is SpellEffect.Stun or SpellEffect.Sleep))
        {
            ResetAdvancedCombo();
            return true;
        }

        if (timeMs < _advancedTelegraphEnd)
            return true;

        var impactMap = _advancedTelegraphMap;
        var impactInstance = _advancedTelegraphInstance;
        var impactX = _advancedTelegraphAimX;
        var impactY = _advancedTelegraphAimY;
        var impactFacing = _advancedTelegraphFacing;
        _advancedTelegraphEnd = 0;

        // Invalidate on death, teleport, stun or instance change.
        if (IsDead || IsDisposed || MapId != impactMap || MapInstanceId != impactInstance ||
            CachedStatuses.Any(status => status.Type is SpellEffect.Stun or SpellEffect.Sleep) ||
            !MapController.TryGetInstanceFromMap(impactMap, impactInstance, out var mapInstance))
        {
            ResetAdvancedCombo();
            return true;
        }

        if (Descriptor.AttackAnimation != null)
            PacketSender.SendAnimationToProximity(
                Descriptor.AttackAnimationId, -1, Guid.Empty,
                impactMap, impactX, impactY, impactFacing, impactInstance);

        // The footprint was fixed when the warning began: no tracking or double hits.
        foreach (var entity in mapInstance.GetEntities().ToArray())
        {
            if (entity is not Player player || !player.InGame || player.IsDead ||
                player.HasPendingDeath || player.IsDisposed || player.IsJoustProtected ||
                player.MapId != impactMap || player.MapInstanceId != impactInstance ||
                !_advancedTelegraphTiles.Contains(new CombatTelegraphTile(player.X, player.Y)) ||
                !CanAttack(player, null))
                continue;

            Attack(player, AdvancedCombatBossPhase.Damage(
                    Descriptor.Damage, _advancedStrikeEnraged,
                    Options.Instance.Combat.AdvancedBossEnrageDamageBonusPercent),
                0, (DamageType)Descriptor.DamageType,
                (Stat)Descriptor.ScalingStat, Descriptor.Scaling, Descriptor.CritChance,
                Descriptor.CritMultiplier, isAutoAttack: true);
        }

        _advancedTelegraphTiles.Clear();
        _advancedStrikeEnraged = false;

        // A single completed strike may lead into one or two more announced
        // attacks. The next footprint is calculated at the START of the next
        // warning, not silently retargeted while it is resolving.
        if (_advancedComboStep + 1 < _advancedComboTotalSteps && !IsDead && !IsDisposed)
        {
            var followup = mapInstance.GetEntities().OfType<Player>().FirstOrDefault(player =>
                player.Id == _advancedComboTargetId &&
                player.InGame && !player.IsDead && !player.HasPendingDeath &&
                !player.IsDisposed && player.MapId == MapId &&
                player.MapInstanceId == MapInstanceId &&
                InRangeOf(player, Math.Max(2, Descriptor.AdvancedTelegraphRange + 2)) &&
                CanAttack(player, null));

            if (followup != null)
            {
                _advancedComboStep++;
                BeginAdvancedTelegraph(followup);
                if (_advancedTelegraphEnd != 0)
                    return true;
            }
        }

        ResetAdvancedCombo();
        // A missed zone still costs one attack cooldown.
        AttackTimer = Timing.Global.Milliseconds + CalculateAttackTime();
        PacketSender.SendEntityAttack(this, CalculateAttackTime());
        return true;
    }
}
