using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Server.Networking;
using Intersect.Utilities;

namespace Intersect.Server.Entities;

/// <summary>
/// Native, hostile single-target spell warning. The cast is still performed by
/// Entity.CastSpell so spell effects, mana use, statistics and normal cooldowns
/// remain identical unless the warned player actually avoids the targeted tile.
/// </summary>
public partial class Npc
{
    private bool _advancedSpellWarningActive;
    private Guid _advancedSpellWarningSpellId;
    private Guid _advancedSpellWarningTargetId;
    private Guid _advancedSpellWarningMapId;
    private Guid _advancedSpellWarningInstanceId;
    private int _advancedSpellWarningX;
    private int _advancedSpellWarningY;

    private long BeginAdvancedSpellTelegraph(
        SpellDescriptor spell, Entity castTarget, long nativeCastEnd)
    {
        // A canceled native cast can leave this transient hint behind. Never
        // carry it over into the next cast.
        _advancedSpellWarningActive = false;

        if (spell == null || spell.Combat == null ||
            castTarget is not Player player ||
            !AdvancedSpellTelegraphRule.CanWarn(
                Options.Instance.Combat.EnableAdvancedCombat &&
                Descriptor.AdvancedSpellTelegraphEnabled,
                PetOwner != null,
                spell.SpellType,
                spell.Combat.TargetType,
                spell.Combat.Friendly,
                spell.Combat.HitRadius,
                spell.CastDuration) ||
            player.IsDisposed || !player.InGame || player.IsDead || player.HasPendingDeath ||
            player.IsJoustProtected || player.MapId != MapId || player.MapInstanceId != MapInstanceId)
            return nativeCastEnd;

        var now = Timing.Global.Milliseconds;
        var durationMs = AdvancedSpellTelegraphRule.EffectiveCastDurationMs(
            spell.CastDuration, Descriptor.AdvancedSpellTelegraphWindupMs);
        _advancedSpellWarningActive = true;
        _advancedSpellWarningSpellId = spell.Id;
        _advancedSpellWarningTargetId = player.Id;
        _advancedSpellWarningMapId = MapId;
        _advancedSpellWarningInstanceId = MapInstanceId;
        _advancedSpellWarningX = player.X;
        _advancedSpellWarningY = player.Y;

        var packet = new CombatTelegraphPacket(
            MapId, _advancedSpellWarningX, _advancedSpellWarningY, durationMs)
        {
            IsSpell = true,
        };
        PacketSender.SendDataToProximityOnMapInstance(MapId, MapInstanceId, packet);
        return now + durationMs;
    }

    public override void CastSpell(Guid spellId, int spellSlot = -1)
    {
        if (!_advancedSpellWarningActive || spellId != _advancedSpellWarningSpellId)
        {
            base.CastSpell(spellId, spellSlot);
            return;
        }

        // Retain a local snapshot before clearing, so the next cast cannot
        // accidentally inherit the previous warning's position or player.
        _advancedSpellWarningActive = false;
        var player = CastTarget as Player;
        var canResolve =
            Options.Instance.Combat.EnableAdvancedCombat &&
            player != null && player.InGame && !player.IsDead &&
            !player.HasPendingDeath && !player.IsDisposed && !player.IsJoustProtected &&
            !IsDead && !IsDisposed && 
            !CachedStatuses.Any(status => status.Type is SpellEffect.Stun or SpellEffect.Sleep or SpellEffect.Silence) &&
            MapId == _advancedSpellWarningMapId &&
            MapInstanceId == _advancedSpellWarningInstanceId &&
            AdvancedSpellTelegraphRule.RemainsOnWarnedTile(
                _advancedSpellWarningTargetId, player.Id,
                _advancedSpellWarningMapId, player.MapId,
                _advancedSpellWarningInstanceId, player.MapInstanceId,
                _advancedSpellWarningX, _advancedSpellWarningY,
                player.X, player.Y, true);

        if (!canResolve)
        {
            // A dodged spell is still an attempted cast. Apply its normal
            // cooldown so the boss cannot spam invisible follow-up spells.
            if (SpellDescriptor.TryGet(spellId, out var interrupted))
                UpdateSpellCooldown(interrupted, spellSlot);
            return;
        }

        // Use native execution only when the original target is still on the
        // exact tile it was warned about. No replacement damage calculations.
        base.CastSpell(spellId, spellSlot);
    }
}
