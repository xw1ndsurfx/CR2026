using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Utilities;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.Systems.Deaths;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Server.Systems.Deaths;

namespace Intersect.Server.Entities;
public partial class Player
{
    private sealed class PendingDeath
    {
        internal required DeathRule Rule;
        internal Guid EventInstanceId;
        internal long Deadline;
        internal bool Animating;
        internal bool WasHidden;
        internal Guid DeathMapId;
        internal Guid DeathInstanceId;
        internal int X;
        internal int Y;
        internal Direction Direction;
    }
    private PendingDeath? _pendingDeath;
    [NotMapped, JsonIgnore] public bool HasPendingDeath => _pendingDeath != null;

    private bool TryBeginConfiguredDeath()
    {
        var rule = DeathConfigurationRuntime.Current.Find(MapId);
        if (rule == null || MapController.Get(rule.RespawnMapId) == null) return false;
        var pending = new PendingDeath
        {
            Rule = rule, Deadline = Timing.Global.Milliseconds + rule.EventTimeoutSeconds * 1000L,
            WasHidden = HideEntity, DeathMapId = MapId, DeathInstanceId = MapInstanceId,
            X = (int)X, Y = (int)Y, Direction = Dir,
        };
        _pendingDeath = pending;
        PacketSender.SendEntityDataToProximity(this);
        var commonEvent = EventDescriptor.Get(rule.CommonEventId);
        if (commonEvent is { CommonEvent: true } && EventBaseIdLookup.TryGetValue(commonEvent.Id, out var previous))
            CancelDeathEvent(previous.Id);
        if (commonEvent is { CommonEvent: true } && UnsafeStartCommonEvent(commonEvent) &&
            EventBaseIdLookup.TryGetValue(commonEvent.Id, out var instance))
            pending.EventInstanceId = instance.Id;
        AdvanceConfiguredDeath();
        return true;
    }

    private void AdvanceConfiguredDeath()
    {
        if (_pendingDeath is not { } pending) return;
        var now = Timing.Global.Milliseconds;
        if (!pending.Animating)
        {
            if (pending.EventInstanceId != Guid.Empty && EventLookup.TryGetValue(pending.EventInstanceId, out var instance) && instance.CallStack.Count > 0)
            {
                if (now < pending.Deadline) return;
                // A dialogue/wait must never leave a character permanently dead.
                CancelDeathEvent(pending.EventInstanceId);
            }
            HideEntity = true;
            PacketSender.SendEntityDataToProximity(this);
            var animation = AnimationDescriptor.Get(pending.Rule.AnimationId);
            var duration = DeathConfiguration.AnimationDuration(animation);
            if (duration > 60000) { animation = null; duration = 0; } // Asset changed after this rule was saved.
            if (animation != null)
                PacketSender.SendAnimationToProximity(animation.Id, -1, Guid.Empty, pending.DeathMapId,
                    pending.X, pending.Y, pending.Direction, pending.DeathInstanceId);
            pending.Animating = true;
            pending.Deadline = now + duration;
        }
        if (now >= pending.Deadline) CompleteConfiguredDeath();
    }

    private void CancelDeathEvent(Guid id)
    {
        if (EventLookup.TryGetValue(id, out var instance) && instance.HoldingPlayer)
        {
            instance.HoldingPlayer = false;
            PacketSender.SendReleasePlayer(this, id);
        }
        RemoveEvent(id);
    }

    private void CompleteConfiguredDeath()
    {
        if (_pendingDeath is not { } pending) return;
        _pendingDeath = null;
        if (pending.EventInstanceId != Guid.Empty) CancelDeathEvent(pending.EventInstanceId);
        HideEntity = pending.WasHidden;
        if (MapController.Get(pending.Rule.RespawnMapId) != null)
            RespawnFromDungeon(pending.Rule.RespawnMapId, (byte)pending.Rule.X, (byte)pending.Rule.Y, pending.Rule.Direction);
        else Respawn();
    }
}
