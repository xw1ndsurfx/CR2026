using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities.Combat;
using Intersect.Server.Maps;
using Intersect.Utilities;

namespace Intersect.Server.Entities;

public partial class Player
{
    private readonly AdvancedCombatStamina _advancedStamina = new();
    private long _advancedNextDodgeAt;
    private long _advancedDodgeProtectionUntil;
    private long _advancedParryUntil;
    private long _advancedCounterUntil;
    private Guid _advancedCounterTarget;

    private bool _advancedStateSent;
    private bool _advancedWasEnabled;
    private int _advancedLastSentStamina = -1;
    private long _advancedLastStateAt;
    private long _advancedLastExhaustedAt;

    private void AdvanceAdvancedStamina(long now)
    {
        var options = Options.Instance.Combat;
        _advancedStamina.Advance(now, options.AdvancedStaminaMaximum,
            options.AdvancedStaminaRegenPerSecond, options.AdvancedStaminaRegenDelayMs);
    }

    private bool TrySpendAdvancedStamina(int amount, long now)
    {
        AdvanceAdvancedStamina(now);
        if (_advancedStamina.TrySpend(amount, now))
            return true;

        if (now - _advancedLastExhaustedAt > 900)
        {
            _advancedLastExhaustedAt = now;
            SendAdvancedCombatState(AdvancedCombatFeedback.Exhausted);
        }
        return false;
    }

    private void SendAdvancedCombatState(
        AdvancedCombatFeedback feedback = AdvancedCombatFeedback.None)
    {
        if (!InGame || Client == null)
            return;

        var enabled = Options.Instance.Combat.EnableAdvancedCombat;
        var now = Timing.Global.Milliseconds;
        var cooldown = enabled ? (int)Math.Clamp(_advancedNextDodgeAt - now, 0L, 20_000L) : 0;
        SendPacket(new AdvancedCombatStatePacket(
            Id, enabled, _advancedStamina.Current, _advancedStamina.Maximum, cooldown, feedback));
        _advancedStateSent = true;
        _advancedWasEnabled = enabled;
        _advancedLastSentStamina = _advancedStamina.Current;
        _advancedLastStateAt = now;
    }

    private void UpdateAdvancedCombatStamina(long now)
    {
        var enabled = Options.Instance.Combat.EnableAdvancedCombat;
        if (!enabled)
        {
            if (_advancedWasEnabled)
                SendAdvancedCombatState();
            return;
        }

        AdvanceAdvancedStamina(now);
        var current = _advancedStamina.Current;
        if (!_advancedStateSent || !_advancedWasEnabled ||
            (current != _advancedLastSentStamina && now - _advancedLastStateAt >= 180) ||
            (_advancedNextDodgeAt > now && now - _advancedLastStateAt >= 450))
            SendAdvancedCombatState();
    }

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
        if (!Options.Instance.Combat.EnableAdvancedCombat ||
            !IsBlocking || now > _advancedParryUntil || _advancedParryUntil == 0 ||
            !TryGetEquippedItem(Options.Instance.Equipment.ShieldSlot, out _))
            return false;

        if (!TrySpendAdvancedStamina(Options.Instance.Combat.AdvancedPerfectParryStaminaCost, now))
            return false;

        _advancedParryUntil = 0;
        _advancedCounterTarget = attacker.Id;
        _advancedCounterUntil = now + Math.Clamp(Options.Instance.Combat.AdvancedCounterWindowMs, 250, 3000);
        SendAdvancedCombatState(AdvancedCombatFeedback.PerfectParry);
        return true;
    }

    public bool ConsumeAdvancedCounter(Guid npcId)
    {
        var now = Timing.Global.Milliseconds;
        if (!Options.Instance.Combat.EnableAdvancedCombat ||
            npcId == Guid.Empty || _advancedCounterTarget != npcId ||
            now >= _advancedCounterUntil || 
            !TrySpendAdvancedStamina(Options.Instance.Combat.AdvancedCounterStaminaCost, now))
            return false;

        _advancedCounterTarget = Guid.Empty;
        _advancedCounterUntil = 0;
        SendAdvancedCombatState(AdvancedCombatFeedback.CounterAttack);
        return true;
    }

    public void TryAdvancedDodge(Direction direction)
    {
        var now = Timing.Global.Milliseconds;
        if (!Options.Instance.Combat.EnableAdvancedCombat || !InGame || IsDead || HasPendingDeath ||
            IsAttacking || IsCasting || IsBlocking || MoveRoute != null ||
            direction <= Direction.None || now < _advancedNextDodgeAt ||
            now < MoveTimer ||
            CachedStatuses.Any(s => s.Type is SpellEffect.Stun or SpellEffect.Sleep or SpellEffect.Snare))
            return;

        AdvanceAdvancedStamina(now);
        var cost = Options.Instance.Combat.AdvancedDodgeStaminaCost;
        if (!_advancedStamina.CanSpend(cost))
        {
            TrySpendAdvancedStamina(cost, now);
            return;
        }

        if (!CanMoveInDirection(direction, out var blocker, out _) && blocker != MovementBlockerType.Slide)
            return;

        var dash = new Dash(this, 1, direction);
        if (dash.Range < 1)
            return;

        if (!TrySpendAdvancedStamina(cost, now))
            return;

        _advancedDodgeProtectionUntil = now +
            Math.Clamp(Options.Instance.Combat.AdvancedDodgeInvulnerabilityMs, 0, 300);
        _advancedNextDodgeAt = now +
            Math.Clamp(Options.Instance.Combat.AdvancedDodgeCooldownMs, 500, 10000);
        if (Client != null)
            Intersect.Server.Networking.PacketSender.SendEntityPositionTo(Client, this);
        SendAdvancedCombatState(AdvancedCombatFeedback.Dodge);
    }

    public void TryAdvancedCounter()
    {
        var now = Timing.Global.Milliseconds;
        if (!Options.Instance.Combat.EnableAdvancedCombat || !InGame || IsDead || HasPendingDeath ||
            IsAttacking || IsCasting || now >= _advancedCounterUntil ||
            _advancedCounterTarget == Guid.Empty)
            return;

        AdvanceAdvancedStamina(now);
        if (!_advancedStamina.CanSpend(Options.Instance.Combat.AdvancedCounterStaminaCost))
        {
            TrySpendAdvancedStamina(Options.Instance.Combat.AdvancedCounterStaminaCost, now);
            return;
        }

        foreach (var instance in MapController.GetSurroundingMapInstances(MapId, MapInstanceId, true))
        {
            foreach (var entity in instance.GetEntities())
            {
                if (entity is Npc npc && npc.Id == _advancedCounterTarget &&
                    !npc.IsDead && !npc.IsDisposed && npc.MapInstanceId == MapInstanceId &&
                    IsOneBlockAway(npc) && CanAttack(npc, null))
                {
                    TryAttack(npc);
                    return;
                }
            }
        }
    }
}
