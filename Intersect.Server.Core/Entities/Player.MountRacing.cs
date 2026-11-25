using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.MountRacing;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Maps;
using Intersect.Server.MountRacing;
using Intersect.Server.Mounts;
using Intersect.Server.Networking;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;

public sealed class RaceReturnLocation
{
    public Guid MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; }
    public Guid MountId { get; set; }
}
public sealed class RacingProfile
{
    public RaceReturnLocation? Return { get; set; }
    public HashSet<Guid> Receipts { get; set; } = [];
}

public partial class Player
{
    [NotMapped, JsonIgnore] internal MountRaceRound? RaceRound { get; private set; }
    [NotMapped, JsonIgnore] internal bool IsMountRacing => RaceRound != null || PetCollection?.Racing?.Return != null;
    internal bool IsActivityCombatProtected => IsJoustProtected || IsMountRacing;
    private bool _raceTravel;
    internal bool RaceBlocksMovement => IsMountRacing && (RaceRound == null || !RaceRound.CanMove(Id, Timing.Global.Milliseconds));
    internal bool RaceDirectionAllowed(Direction direction) => !IsMountRacing || direction is Direction.Up or Direction.Down or Direction.Left or Direction.Right;
    private bool RaceSave() => User?.Save(force: true) == UserSaveResult.Completed;
    internal bool EnterMountRace(MountRaceRound round, Func<bool>? persist = null)
    {
        if (User == null && persist == null || PetCollection.Racing.Return != null || round.Phase != MountRacePhase.Ready ||
            !round.Riders.Any(r => r.Id == Id && r.Ready && !r.Withdrawn)) return false;
        lock (User?.PokerSaveGate ?? EntityLock)
        {
            PetCollection.Racing.Return = new() { MapId = MapId, X = X, Y = Y, Direction = Dir, MountId = ActiveMountId };
            try { if (!(persist ?? RaceSave)()) { PetCollection.Racing.Return = null; return false; } }
            catch { PetCollection.Racing.Return = null; throw; }
        }
        RaceRound = round; _raceTravel = true;
        try
        {
            DismissPetRuntime();
            AdminWarp(round.Course.MapId, round.Course.StartX, round.Course.StartY, round.Id, MapInstanceType.Shared, true);
            Dir = round.Course.Direction;
            ActiveMountId = round.Course.MountId;
            MoveTimer = ClientMoveTimer = Timing.Global.Milliseconds;
            var arrived = MapId == round.Course.MapId && MapInstanceId == round.Id;
            if (arrived && InGame) { PacketSender.SendEntityDataToProximity(this); SendMountState(); }
            return arrived;
        }
        finally { _raceTravel = false; }
    }
    internal bool ReturnFromMountRace(bool login = false, Func<bool>? persist = null)
    {
        RaceRound = null;
        if (PetCollection.Racing.Return is not {} origin) return true;
        _raceTravel = true;
        try
        {
            ActiveMountId = Guid.Empty;
            Warp(origin.MapId, origin.X, origin.Y, origin.Direction, mapInstanceType: MapInstanceType.Overworld, fromLogin: login);
            if (MapInstanceId != Guid.Empty) return false;
            if (MountRuntime.Find(origin.MountId) is {} mount && MountRuntime.Owns(this, mount) && CanRide(mount)) ActiveMountId = mount.Id;
            if (User == null && persist == null) return false;
            lock (User?.PokerSaveGate ?? EntityLock)
            {
                PetCollection.Racing.Return = null;
                try { if (!(persist ?? RaceSave)()) { PetCollection.Racing.Return = origin; return false; } }
                catch { PetCollection.Racing.Return = origin; throw; }
            }
            if (InGame) { PacketSender.SendEntityDataToProximity(this); SendMountState(); }
            return true;
        }
        finally { _raceTravel = false; }
    }
    internal bool RecoverRaceLogin()
    {
        if (PetCollection.Racing.Return == null) return false;
        try { ReturnFromMountRace(true); }
        catch (Exception e) { Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(e, "Race login recovery will be retried"); }
        return true;
    }
    internal bool PrepareRaceDisconnect()
    {
        if (!IsMountRacing) return false;
        RaceRound?.Withdraw(Id);
        // Logout must remove party membership even though in-race manual party changes are blocked.
        // Keep the saved return location; login recovery owns the eventual warp and save.
        lock (PartyMembershipLock) LeavePartyCore(preserveDungeon: true);
        return true;
    }
    private bool RaceTileAllowed(int x, int y) => RaceRound is {} race && MapId == race.Course.MapId &&
        MapInstanceId == race.Id && MountRaceStore.TrackTile(MapController.Get(MapId), x, y);
    internal string ClaimRacePrize(Guid recordId, RaceJournal journal, Func<bool>? persist = null)
    {
        if (IsMountRacing || User == null && persist == null) return "Finish your race before claiming a prize.";
        var record = journal.Snapshot().FirstOrDefault(r => r.Id == recordId && r.PlayerId == Id && !r.Claimed && r.Quantity > 0 && !r.Practice);
        if (record == null) return "This race prize is no longer available.";
        lock (User?.PokerSaveGate ?? EntityLock)
        {
            var profile = PetCollection.Racing;
            if (profile.Receipts.Contains(recordId)) { journal.Acknowledge(recordId, Id); return "Race prize already claimed."; }
            if (ItemDescriptor.Get(record.ItemId) is not { IsStackable: true, MaxInventoryStack: > 0 } item) return "This race prize is currently unavailable.";
            var plan = new List<(InventorySlot Slot, Item Before, Item After)>(); var left = record.Quantity;
            foreach (var slot in Items.OrderBy(s => s.ItemId == item.Id ? 0 : 1))
            {
                if (left == 0) break;
                if (slot.BagId.GetValueOrDefault() != Guid.Empty || slot.Quantity < 0 || slot.ItemId != Guid.Empty && slot.ItemId != item.Id) continue;
                var count = Math.Min(left, item.MaxInventoryStack - (slot.ItemId == Guid.Empty ? 0 : slot.Quantity));
                if (count <= 0) continue;
                var after = slot.ItemId == Guid.Empty ? new Item(item.Id, count) : slot.Clone();
                if (slot.ItemId != Guid.Empty) after.Quantity += count;
                plan.Add((slot, slot.Clone(), after)); left -= count;
            }
            if (left > 0) return "Make room in your inventory to claim this race prize.";
            // Remove only receipts whose journal rows are durably settled or already compacted.
            var pending = journal.Snapshot().Where(r => r.PlayerId == Id && !r.Claimed).Select(r => r.Id).ToHashSet();
            var beforeReceipts = profile.Receipts.ToHashSet();
            profile.Receipts.IntersectWith(pending); profile.Receipts.Add(recordId);
            foreach (var row in plan) row.Slot.Set(row.After);
            var saved = false;
            try { saved = (persist ?? RaceSave)(); }
            finally
            {
                if (!saved) { profile.Receipts = beforeReceipts; foreach (var row in plan) row.Slot.Set(row.Before); }
            }
            if (!saved) return "Your race prize could not be saved. Please try again.";
            // A crash or journal error here is harmless: the durable character receipt prevents a second grant.
            if (Client != null) { PacketSender.SendInventory(this); UpdateGatherItemQuests(item.Id); }
            journal.Acknowledge(recordId, Id);
            return "Race prize claimed!";
        }
    }
}
