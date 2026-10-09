using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Entities.Pathfinding;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;

namespace Intersect.Server.Entities;

public partial class Npc
{
    private long _nextPetLootCheck;

    // Companions may move a little faster to catch their master, without
    // changing the movement speed of ordinary or invasion NPCs.
    public override float GetMovementTime()
    {
        var normalTime = base.GetMovementTime();
        if (PetOwner == null || Target != null)
            return normalTime;

        var gap = GetDistanceTo(PetOwner);
        return gap > 3 && gap < 9999 ? Math.Max(95f, normalTime * 0.65f) : normalTime;
    }

    /// <summary>
    /// Refresh a live companion's name without resetting its HP, MP or stats.
    /// </summary>
    public void RefreshPetDisplayName()
    {
        if (PetOwner != null)
            Name = PetOwner.GetPetDisplayName(Descriptor);
    }

    public void ApplyPetLevel(int petLevel)
    {
        if (PetOwner == null) return;
        Level = Math.Clamp(petLevel, 1, Math.Clamp(Descriptor.PetMaxLevel, 1, 200));
        // Intersect already renders Level in the entity nameplate.
        // Keep the name itself free of level suffixes.
        RefreshPetDisplayName();
        var bonusLevels = Level - 1;
        for (var i = 0; i < BaseStats.Length; i++)
            BaseStats[i] = (int)Math.Min(int.MaxValue,
                (long)Descriptor.Stats[i] + (long)bonusLevels * Math.Max(0, Descriptor.PetStatGrowth));
        for (var i = 0; i < Enum.GetValues<Vital>().Length; i++)
        {
            var growth = i == (int)Vital.Health ? Math.Max(0, Descriptor.PetHealthGrowth) : 0;
            var maximum = Descriptor.MaxVitals[i] + (long)bonusLevels * growth;
            SetMaxVital(i, maximum);
            SetVital(i, maximum);
        }
        PacketSender.SendEntityStats(this);
    }

    // Dedicated companion AI, independent from invasions and normal NPC roaming.
    private void UpdatePet(long timeMs)
    {
        var owner = PetOwner;
        if (owner == null || owner.IsDead || !owner.InGame || owner.Client == null ||
            !PetMapTraversal.CanWalkToOwner(owner, this)) return;

        if (_nextPetLootCheck <= timeMs)
        {
            _nextPetLootCheck = timeMs + 750;
            CollectPetLoot(owner, timeMs);
        }

        var opponent = owner.Target as Npc;
        if (opponent == null || opponent.IsDead || opponent.MapInstanceId != MapInstanceId ||
            !CanTarget(opponent) || GetDistanceTo(opponent) > 8)
        {
            opponent = null;
            if (MapController.TryGetInstanceFromMap(MapId, MapInstanceId, out var mapInstance))
                opponent = mapInstance.GetEntities(true).OfType<Npc>()
                    .Where(npc => npc != this && !npc.IsDead &&
                        npc.MapInstanceId == MapInstanceId &&
                        (npc.Target == owner || npc.Target == this) &&
                        CanTarget(npc) && GetDistanceTo(npc) <= 7)
                    .OrderBy(npc => GetDistanceTo(npc)).FirstOrDefault();
        }

        if (Target != opponent)
        {
            Target = opponent;
            PacketSender.SendNpcAggressionToProximity(this);
        }
        if (opponent != null) TryCastSpells();
        if (MoveTimer >= timeMs || IsCasting || IsStunnedOrSleeping) return;

        Entity follow = opponent ?? (Entity)owner;
        var distance = GetDistanceTo(follow);
        // Never warp for a modest gap or a normal map seam. The shared map-grid
        // pathfinder can navigate naturally between adjacent maps, even when
        // owner.MapId differs from MapId during a boundary crossing.
        // Let the pathfinder attempt a route even if the player is near the
        // far side of a neighbouring map; its own range check is authoritative.
        if (opponent != null && IsOneBlockAway(opponent))
        {
            var direction = DirectionToTarget(opponent);
            if (direction != Dir && direction != Direction.None) ChangeDir(direction);
            else if (CanAttack(opponent, null)) TryAttack(opponent);
            return;
        }
        if (distance <= (opponent == null ? 2 : 1))
        {
            mPathFinder.SetTarget(null);
            return;
        }

        var target = mPathFinder.GetTarget();
        if (target == null || target.TargetMapId != follow.MapId ||
            target.TargetX != follow.X || target.TargetY != follow.Y)
            mPathFinder.SetTarget(new PathfinderTarget(
                follow.MapId, follow.X, follow.Y, follow.Z));

        var move = mPathFinder.Update(timeMs);
        if (move.Type == PathfinderResultType.Success)
        {
            var direction = mPathFinder.GetMove();
            if (direction > Direction.None && CanMoveInDirection(direction))
                Move(direction, null);
        }
        else if (move.Type == PathfinderResultType.Failure)
            mPathFinder.PathFailed(timeMs);
    }

    private void CollectPetLoot(Player owner, long timeMs)
    {
        owner.PetCollection.OwnedPets ??= [];
        if (!owner.PetCollection.OwnedPets.TryGetValue(Descriptor.Id, out var progress) ||
            !progress.AutoLoot || Descriptor.PetLootRadius <= 0 ||
            !MapController.TryGetInstanceFromMap(MapId, MapInstanceId, out var instance)) return;

        var radius = Math.Clamp(Descriptor.PetLootRadius, 0, 8);
        var count = 0;
        foreach (var item in instance.AllMapItems.Values.ToArray())
        {
            if (count >= 8) break;
            if ((!item.VisibleToAll && item.Owner != owner.Id) ||
                (item.Owner != Guid.Empty && item.Owner != owner.Id &&
                 timeMs < item.OwnershipTime) ||
                GetDistanceTo(Map, item.X, item.Y) > radius ||
                !owner.CanGiveItem(item, out _)) continue;

            instance.RemoveItem(item);
            if (!owner.TryGiveItem(item, ItemHandling.Overflow, false, -1, true, item.X, item.Y))
                continue;
            count++;
            if (ItemDescriptor.TryGet(item.ItemId, out var descriptor))
                PacketSender.SendActionMsg(owner, descriptor.Name, CustomColors.Items.Rarities[descriptor.Rarity]);
        }
    }
}
