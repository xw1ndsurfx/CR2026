using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Mounts;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Maps;
using Intersect.Server.Mounts;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public sealed class MountCollection
{
    public HashSet<Guid> Owned { get; set; } = [];
    public Guid Favorite { get; set; }
}
public partial class Player
{
    [NotMapped, JsonIgnore] public Guid ActiveMountId { get; private set; }
    private long _lastMountAction;
    private long _lastMountList;
    internal MountDefinition? ActiveMount => ActiveMountId == Guid.Empty ? null : MountRuntime.Find(ActiveMountId);
    public override float GetMovementTime() => base.GetMovementTime() / (float)(ActiveMount?.SpeedMultiplier ?? 1);
    internal bool CanRide(MountDefinition definition) => !IsDead && !HasPendingDeath && !IsCasting && !IsBlocking &&
        !CachedStatuses.Any(s => s.Type == Intersect.Enums.SpellEffect.Transform) &&
        CombatTimer <= Timing.Global.Milliseconds && CaptainedShipId == Guid.Empty && BoardedShipId == Guid.Empty &&
        MapInstanceId == MapInstance.OverworldInstanceId && MapController.Get(MapId) is { } map && (!map.IsIndoors || definition.AllowIndoors);
    internal void Dismount()
    {
        if (ActiveMountId == Guid.Empty) return;
        ActiveMountId = Guid.Empty; PacketSender.SendEntityDataToProximity(this); SendMountState();
    }
    internal void UpdateMount()
    {
        if (ActiveMountId != Guid.Empty && (ActiveMount is not { } mount || !MountRuntime.Owns(this, mount) || !CanRide(mount))) Dismount();
    }
    internal void SendMountState(bool open = false, string message = "") => SendPacket(new MountStatePacket
    {
        ConfigurationJson = MountRuntime.Current.ToJson(), Owned = MountRuntime.Current.Mounts.Where(m => MountRuntime.Owns(this, m)).Select(m => m.Id).ToArray(),
        Active = ActiveMountId, Favorite = PetCollection.Mounts.Favorite, OpenWindow = open, Message = message
    });
    internal void HandleMountAction(MountRequestPacket packet)
    {
        lock (EntityLock)
        {
            if (packet.Action == MountAction.List)
            {
                var requested = Timing.Global.Milliseconds;
                if (_lastMountList > 0 && requested - _lastMountList < 300) return;
                _lastMountList = requested; SendMountState(true); return;
            }
            if (packet.Action is not (MountAction.Toggle or MountAction.Favorite)) return;
            var now = Timing.Global.Milliseconds;
            if (_lastMountAction > 0 && now - _lastMountAction < 700) return;
            _lastMountAction = now;
            if (packet.Action == MountAction.Toggle && ActiveMountId != Guid.Empty &&
                (packet.MountId == Guid.Empty || packet.MountId == ActiveMountId)) { Dismount(); return; }
            var id = packet.MountId == Guid.Empty ? PetCollection.Mounts.Favorite : packet.MountId;
            if (id == Guid.Empty) { SendMountState(message: "Choose a favorite mount first."); return; }
            var mount = MountRuntime.Find(id);
            if (mount == null || !MountRuntime.Owns(this, mount)) { SendMountState(message: "This mount is not unlocked."); return; }
            if (packet.Action == MountAction.Favorite)
            {
                var previous = PetCollection.Mounts.Favorite; PetCollection.Mounts.Favorite = id;
                if (!PersistMountChange())
                { PetCollection.Mounts.Favorite = previous; SendMountState(message: "Changes could not be saved."); return; }
                SendMountState(); return;
            }
            if (!CanRide(mount)) { SendMountState(message: "You cannot ride here or during combat."); return; }
            ActiveMountId = id; PacketSender.SendEntityDataToProximity(this); SendMountState(message: "Mount summoned.");
        }
    }
    internal bool TryActivateMountItem(Guid itemId, Func<bool>? persist = null)
    {
        lock (EntityLock)
        {
            if (itemId == Guid.Empty || !MountRuntime.Current.Enabled) return false;
            var mounts = MountRuntime.Current.Mounts.Where(m => m.UnlockItemId == itemId).ToArray();
            if (mounts.Length == 0) return false;
            var added = mounts.Where(m => PetCollection.Mounts.Owned.Add(m.Id)).Select(m => m.Id).ToArray();
            if (added.Length > 0 && !PersistMountChange(persist))
            {
                foreach (var id in added) PetCollection.Mounts.Owned.Remove(id);
                SendMountState(message: "Changes could not be saved."); return true;
            }
            SendMountState(true, "Mount unlocked."); return true;
        }
    }
    private bool PersistMountChange(Func<bool>? persist = null)
    {
        try { return persist?.Invoke() ?? (User?.Save(force: true) == UserSaveResult.Completed); }
        catch (Exception) { return false; }
    }
}
