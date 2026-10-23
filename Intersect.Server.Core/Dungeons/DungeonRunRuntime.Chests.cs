using Intersect.Enums;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Networking;
using Intersect.Network.Packets.Server;
namespace Intersect.Server.Dungeons;
internal static partial class DungeonRunRuntime
{
    private static void Complete(Run run)
    {
        lock (Gate)
        {
            if (run.Finished || run.CompletionStarted) return;
            run.CompletionStarted = true;
            run.CompletedAtUnixMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (run.ChestMap == Guid.Empty) { run.ChestMap = run.EntryMapId; run.ChestX = run.EntryX; run.ChestY = run.EntryY; }
            run.ChestUntil = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + run.Dungeon.Chest.CollectionSeconds * 1000L;
            foreach (var id in run.Participants.Keys)
                run.ChestLoot[id] = run.Dungeon.Chest.Drops.Select(drop => (drop.ItemId, Quantity: drop.Roll(Random.Shared)))
                    .Where(drop => drop.Quantity > 0).ToArray();
        }
        if (AnimationDescriptor.Get(run.Dungeon.Chest.CompletionAnimationId) != null)
            PacketSender.SendAnimationToProximity(run.Dungeon.Chest.CompletionAnimationId, -1, Guid.Empty,
                run.ChestMap, run.ChestX, run.ChestY, Direction.Down, run.MapInstanceId);
        if (!run.Dungeon.Chest.Enabled) { FinishComplete(run); return; }
        foreach(var player in GetParticipants(run))
        {
            var text = QualityOfLifeText.Get("Dungeon cleared! Open the completion chest before leaving.", player.Client?.LanguageCode ?? "en");
            player.SendPacket(CreatePacket(run, DungeonRunStatus.Active, text)); SendChest(run, player);
        }
    }

    internal static bool ClaimChest(Player player, Guid runId, bool timeout = false, Func<bool>? persist = null)
    {
        lock (player.EntityLock)
        lock (Gate)
        {
            if (!RunsByInstance.TryGetValue(runId, out var run) || run.Finished || !run.CompletionStarted ||
                !run.Dungeon.Chest.Enabled || !run.Participants.ContainsKey(player.Id) ||
                run.ChestClaimed.Contains(player.Id) || player.MapInstanceId != run.MapInstanceId || player.IsDead ||
                (!timeout && (player.MapId != run.ChestMap || Math.Abs(player.X-run.ChestX)>2 || Math.Abs(player.Y-run.ChestY)>2))) return false;
            if (run.NavalEncounterId != Guid.Empty && !Naval.NavalSystem.CanReceiveBoardingReward(player, run.NavalEncounterId)) return false;
            if(timeout)
            {
                var now=Environment.TickCount64;
                if(run.ChestRetryAt.GetValueOrDefault(player.Id)>now)return false;
                run.ChestRetryAt[player.Id]=now+5000;
            }
            var inventory = Naval.NavalSystem.SnapshotCargoInventory(player);
            var bank = player.Bank.Select(item => new Item(item)).ToArray();
            var committed = false;
            try
            {
                var plannedInventory = inventory.Select(item=>new Item(item)).ToArray();
                var plannedBank = bank.Select(item=>new Item(item)).ToArray();
                if (!PlanChestLoot(plannedInventory, plannedBank, run.ChestLoot.GetValueOrDefault(player.Id) ?? [])) return false;
                for(var i=0;i<plannedInventory.Length;i++) player.Items[i].Set(plannedInventory[i]);
                for(var i=0;i<plannedBank.Length;i++) player.Bank[i].Set(plannedBank[i]);
                committed = persist?.Invoke() ?? player.User?.Save(force:true) == UserSaveResult.Completed;
                if (!committed) return false;
                run.ChestClaimed.Add(player.Id);
                SendChest(run, player);
                return true;
            }
            catch(Exception error)
            {
                ApplicationContext.Context.Value?.Logger.LogError(error,"Could not collect dungeon chest for {PlayerId}",player.Id);
                return false;
            }
            finally
            {
                if (!committed)
                {
                    for (var i=0;i<inventory.Length;i++) player.Items[i].Set(inventory[i]);
                    for (var i=0;i<bank.Length;i++) player.Bank[i].Set(bank[i]);
                }
                PacketSender.SendInventory(player);
                if (committed) player.RefreshNavalCargoQuests((run.ChestLoot.GetValueOrDefault(player.Id) ?? []).Select(drop=>drop.ItemId));
                if (!committed) PacketSender.SendChatMsg(player,
                    QualityOfLifeText.Get("The chest rewards could not be collected. Check inventory and bank space, then try again.", player.Client?.LanguageCode ?? "en"), ChatMessageType.Error);
            }
        }
    }

    internal static bool PlanChestLoot(Item[] inventory, Item[] bank, IEnumerable<(Guid ItemId,int Quantity)> loot)
    {
        foreach(var drop in loot)
        {
            if(ItemDescriptor.Get(drop.ItemId) is not {} descriptor || drop.Quantity is <1 or >1000000) return false;
            var remaining=drop.Quantity;
            foreach(var (slots,maximum) in new[]{(inventory,descriptor.IsStackable?Math.Max(1,descriptor.MaxInventoryStack):1),
                (bank,descriptor.IsStackable?Math.Max(1,descriptor.MaxBankStack):1)})
            {
                if(descriptor.IsStackable)
                    foreach(var slot in slots.Where(item=>item.ItemId==drop.ItemId && item.Quantity<maximum))
                    {var amount=Math.Min(remaining,maximum-slot.Quantity);slot.Quantity+=amount;remaining-=amount;if(remaining==0)break;}
                foreach(var slot in slots.Where(item=>item.ItemId==Guid.Empty))
                {if(remaining==0)break;var amount=Math.Min(remaining,maximum);slot.Set(new Item(drop.ItemId,amount));remaining-=amount;}
                if(remaining==0)break;
            }
            if(remaining>0)return false;
        }
        return true;
    }

    private static void UpdateChests()
    {
        Run[] pending;
        lock (Gate) pending = RunsByInstance.Values.Where(run => !run.Finished && run.CompletionStarted && run.Dungeon.Chest.Enabled).ToArray();
        foreach (var run in pending)
        {
            var players = GetParticipants(run).ToArray();
            var expired = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= run.ChestUntil;
            foreach (var player in players)
            {
                if (expired && !run.ChestClaimed.Contains(player.Id)) ClaimChest(player, run.MapInstanceId, timeout:true);
                SendChest(run, player);
            }
            // Failed deliveries remain retryable; no forced warp can destroy their rolled rewards.
            bool collected,allParticipantsCollected;
            lock(Gate)
            {
                collected=players.All(player => run.ChestClaimed.Contains(player.Id));
                allParticipantsCollected=run.Participants.Keys.All(id=>run.ChestClaimed.Contains(id));
            }
            if (allParticipantsCollected || (expired && collected)) FinishComplete(run);
        }
    }
    private static void SendChest(Run run, Player player) => player.SendPacket(new DungeonChestPacket
    {
        RunId=run.MapInstanceId, MapId=run.ChestMap, X=run.ChestX, Y=run.ChestY, AnimationId=run.Dungeon.Chest.AnimationId,
        Visible=!run.Finished, Claimed=run.ChestClaimed.Contains(player.Id),
        SecondsRemaining=(int)Math.Max(0,(run.ChestUntil-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())/1000),
    });
    private static void SendChestClosed(Run run)
    { foreach(var player in GetParticipants(run)) player.SendPacket(new DungeonChestPacket { RunId=run.MapInstanceId }); }
}
