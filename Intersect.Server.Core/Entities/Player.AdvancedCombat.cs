using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Server.Entities.Combat;
using Intersect.Server.Maps;
using Intersect.Utilities;

namespace Intersect.Server.Entities;

public partial class Player
{
    private long _advancedNextDodgeAt;
    private long _advancedDodgeProtectionUntil;
    private long _advancedParryUntil;
    private long _advancedCounterUntil;
    private Guid _advancedCounterTarget;

    public void RegisterAdvancedParry()
    {
        if (!Options.Instance.Combat.EnableAdvancedCombat || !IsBlocking)
            return;
        _advancedParryUntil = Timing.Global.Milliseconds +
            Math.Clamp(Options.Instance.Combat.AdvancedParryWindowMs, 50, 500);
    }

    public bool IsAdvancedDodging(long now) =>
        Options.Instance.Combat.EnableAdvancedCombat && now < _advancedDodgeProtectionUntil;

    public bool TryAdvancedPerfectParry(Npc attacker, long now)
    {
        if (!IsBlocking || now > _advancedParryUntil || _advancedParryUntil == 0 ||
            !TryGetEquippedItem(Options.Instance.Equipment.ShieldSlot, out _))
            return false;

        _advancedParryUntil = 0;
        _advancedCounterTarget = attacker.Id;
        _advancedCounterUntil = now + Math.Clamp(Options.Instance.Combat.AdvancedCounterWindowMs, 250, 3000);
        return true;
    }

    public bool ConsumeAdvancedCounter(Guid npcId)
    {
        if (npcId == Guid.Empty || _advancedCounterTarget != npcId ||
            Timing.Global.Milliseconds >= _advancedCounterUntil)
            return false;
        _advancedCounterTarget = Guid.Empty;
        _advancedCounterUntil = 0;
        return true;
    }

    public void TryAdvancedDodge(Direction direction)
    {
        if (!Options.Instance.Combat.EnableAdvancedCombat || !InGame || IsDead || HasPendingDeath ||
            IsAttacking || IsCasting || IsBlocking || MoveRoute != null ||
            direction <= Direction.None || Timing.Global.Milliseconds < _advancedNextDodgeAt ||
            Timing.Global.Milliseconds < MoveTimer ||
            CachedStatuses.Any(s => s.Type is SpellEffect.Stun or SpellEffect.Sleep or SpellEffect.Snare))
            return;

        if (!CanMoveInDirection(direction, out var blocker, out _) && blocker != MovementBlockerType.Slide)
            return;

        var dash = new Dash(this, 1, direction);
        if (dash.Range < 1)
            return;

        var now = Timing.Global.Milliseconds;
        _advancedDodgeProtectionUntil = now + Math.Clamp(Options.Instance.Combat.AdvancedDodgeInvulnerabilityMs, 0, 300);
        _advancedNextDodgeAt = now + Math.Clamp(Options.Instance.Combat.AdvancedDodgeCooldownMs, 500, 10000);
        // Keep our own client in sync: ordinary entity movement packets suppress local-player movement.
        if (Client != null)
            Intersect.Server.Networking.PacketSender.SendEntityPositionTo(Client, this);
    }

    public void TryAdvancedCounter()
    {
        if (!Options.Instance.Combat.EnableAdvancedCombat || !InGame || IsDead || HasPendingDeath ||
            IsAttacking || IsCasting || Timing.Global.Milliseconds >= _advancedCounterUntil ||
            _advancedCounterTarget == Guid.Empty)
            return;

        foreach (var instance in MapController.GetSurroundingMapInstances(MapId, MapInstanceId, true))
        {
            foreach (var entity in instance.GetEntities())
            {
                if (entity is Npc npc && npc.Id == _advancedCounterTarget &&
                    !npc.IsDead && !npc.IsDisposed && npc.MapInstanceId == MapInstanceId &&
                    IsOneBlockAway(npc) && CanAttack(npc, null))
                {
                    // Consume only when the normal weapon calculation processes this target.
                    TryAttack(npc);
                    return;
                }
            }
        }
    }
}
