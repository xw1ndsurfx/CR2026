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

    private void BeginAdvancedTelegraph(Player target)
    {
        if (_advancedTelegraphEnd != 0 || target.IsDead || target.HasPendingDeath)
            return;

        var shape = Enum.IsDefined(typeof(CombatTelegraphShape), Descriptor.AdvancedTelegraphShape)
            ? (CombatTelegraphShape)Descriptor.AdvancedTelegraphShape
            : CombatTelegraphShape.TargetTile;
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
            return;

        var enrage = AdvancedCombatBossPhase.IsEnraged(
            Descriptor.IsBoss, Descriptor.AdvancedBossEnrageEnabled,
            GetVital(Vital.Health), GetMaxVital(Vital.Health),
            Options.Instance.Combat.AdvancedBossEnrageHealthPercent);
        var enteredEnrage = enrage && !_advancedBossEnraged;
        _advancedBossEnraged = enrage;
        _advancedStrikeEnraged = enrage;

        var windup = AdvancedCombatBossPhase.WindupMilliseconds(
            Descriptor.AdvancedTelegraphWindupMs > 0
                ? Descriptor.AdvancedTelegraphWindupMs
                : Options.Instance.Combat.BossTelegraphDurationMs,
            enrage, Options.Instance.Combat.AdvancedBossEnrageWindupReductionPercent);
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
            _advancedTelegraphEnd = 0;
            _advancedTelegraphTiles.Clear();
            return false;
        }

        // A stun during the windup interrupts the charged attack immediately.
        if (CachedStatuses.Any(status => status.Type is SpellEffect.Stun or SpellEffect.Sleep))
        {
            _advancedTelegraphEnd = 0;
            _advancedTelegraphTiles.Clear();
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
            _advancedTelegraphTiles.Clear();
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
        // A missed zone still costs one attack cooldown.
        AttackTimer = Timing.Global.Milliseconds + CalculateAttackTime();
        PacketSender.SendEntityAttack(this, CalculateAttackTime());
        return true;
    }
}
