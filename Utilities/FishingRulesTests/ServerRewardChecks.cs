#if FISHING_SERVER_TESTS
using Intersect;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Fishing;

internal static class ServerRewardChecks
{
    internal static void Run(Action<bool, string> check)
    {
        Options.EnsureCreated();
        var fish = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Currency, MaxInventoryStack = 10 };
        var bait = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Currency, MaxInventoryStack = 100 };
        ItemDescriptor.Lookup.Set(fish.Id, fish); ItemDescriptor.Lookup.Set(bait.Id, bait);
        Player Player()
        {
            var player = new Player();
            GC.SuppressFinalize(player);
            Array.Fill(player.Equipment, -1);
            return player;
        }
        FishingProgressAward Award(long before = 0) => new()
        {
            VariableId = FishingRules.FallbackExperienceId, Before = before, After = before + 25,
            PreviousLevel = 1, NewLevel = 1,
        };
        long Quantity(Player p) => p.Items.Where(i => i.ItemId == fish.Id).Sum(i => (long)i.Quantity);
        try
        {
            var p = Player(); var receipt = Guid.NewGuid();
            p.Items[0].Set(new Item(fish.Id, 8));
            p.HandleFishingRequest(new Intersect.Network.Packets.Client.FishingRequestPacket
            { Action = Intersect.Network.Packets.Client.FishingAction.Cast, SessionId = Guid.NewGuid(), Sequence = 1 });
            check(!p.IsFishingActive && Quantity(p) == 8 && p.GetVariable(FishingRules.LastRewardId) == null,
                "A forged cast without a server Event session cannot start fishing or grant fish");
            var called = 0;
            var result = FishingRewards.Commit(p, receipt, fish.Id, 15, Award(), Guid.Empty, 0, () =>
            {
                called++;
                check(Quantity(p) == 23, "All fish are staged in the same native save");
                check(p.GetVariable(FishingRules.FallbackExperienceId).Value.Integer == 25, "XP is staged with fish");
                check(p.GetVariable(FishingRules.LastRewardId).Value.String == receipt.ToString("N"), "Receipt is staged with fish");
                return UserSaveResult.Completed;
            });
            check(result == FishingRewardResult.Success && called == 1, "Successful reward commits once");
            check(p.Items.Where(i => i.ItemId == fish.Id).Select(i => i.Quantity).Order().SequenceEqual(new[] { 3, 10, 10 }), "Existing pile fills before creating bounded stacks");
            check(FishingRewards.Commit(p, receipt, fish.Id, 15, Award(), Guid.Empty, 0, () => throw new Exception("Replay saved again")) == FishingRewardResult.AlreadyCommitted,
                "A persisted receipt rejects reward replay before saving");
            check(Quantity(p) == 23 && p.GetVariable(FishingRules.FallbackExperienceId).Value.Integer == 25, "Replay cannot duplicate fish or XP");
            var reloaded = Player();
            var durableReceipt = new PlayerVariable(FishingRules.LastRewardId); durableReceipt.Value.String = p.GetVariable(FishingRules.LastRewardId).Value.String;
            reloaded.Variables.Add(durableReceipt);
            check(FishingRewards.Commit(reloaded, receipt, fish.Id, 15, Award(), Guid.Empty, 0, () => throw new Exception()) == FishingRewardResult.AlreadyCommitted,
                "Restored player receipt still rejects replay");

            foreach (var failure in new[] { UserSaveResult.Failed, UserSaveResult.DatabaseFailure, UserSaveResult.SkippedCouldNotTakeLock })
            {
                var before = p.Items.Select(i => i.Clone()).ToArray();
                var oldReceipt = p.GetVariable(FishingRules.LastRewardId).Value.String;
                var failed = FishingRewards.Commit(p, Guid.NewGuid(), fish.Id, 3, Award(25), Guid.Empty, 0, () => failure);
                check(failed == FishingRewardResult.SaveFailed, "Native save failure is surfaced");
                check(p.Items.Select(i => (i.ItemId, i.Quantity)).SequenceEqual(before.Select(i => (i.ItemId, i.Quantity))), "Failed save restores every original pile");
                check(p.GetVariable(FishingRules.FallbackExperienceId).Value.Integer == 25 && p.GetVariable(FishingRules.LastRewardId).Value.String == oldReceipt,
                    "Failed save restores XP and previous receipt");
            }
            var fresh = Player();
            check(FishingRewards.Commit(fresh, Guid.NewGuid(), fish.Id, 2, Award(), Guid.Empty, 0, () => throw new IOException("injected")) == FishingRewardResult.SaveFailed,
                "Throwing checkpoint rolls back");
            check(Quantity(fresh) == 0 && fresh.GetVariable(FishingRules.LastRewardId) == null && fresh.GetVariable(FishingRules.FallbackExperienceId) == null,
                "First failed reward removes newly created XP and receipt variables");

            var full = Player();
            _ = full.FindOpenInventorySlots();
            foreach (var slot in full.Items) slot.Set(new Item(fish.Id, 10));
            full.Items[0].Quantity = 9;
            var snapshot = Quantity(full); called = 0;
            check(FishingRewards.Commit(full, Guid.NewGuid(), fish.Id, 2, Award(), Guid.Empty, 0, () => { called++; return UserSaveResult.Completed; }) == FishingRewardResult.InventoryFull,
                "Partial space cannot grant a partial catch");
            check(called == 0 && Quantity(full) == snapshot && full.GetVariable(FishingRules.LastRewardId) == null,
                "Full inventory consumes neither receipt nor XP and never saves");

            var baitPlayer = Player(); baitPlayer.Items[0].Set(new Item(bait.Id, 5)); baitPlayer.Equipment[0] = 0;
            check(FishingRewards.Commit(baitPlayer, Guid.Empty, Guid.Empty, 0, null, bait.Id, 1, () => throw new Exception()) == FishingRewardResult.MissingBait,
                "Equipped item is never consumed as bait");
            check(baitPlayer.Items[0].Quantity == 5, "Rejected bait debit preserves item");
            baitPlayer.Equipment[0] = -1;
            check(FishingRewards.Commit(baitPlayer, Guid.Empty, Guid.Empty, 0, null, bait.Id, 2, () => UserSaveResult.DatabaseFailure) == FishingRewardResult.SaveFailed && baitPlayer.Items[0].Quantity == 5,
                "Failed bait save restores exact quantity");
            check(FishingRewards.Commit(baitPlayer, Guid.Empty, Guid.Empty, 0, null, bait.Id, 2, () => UserSaveResult.Completed) == FishingRewardResult.Success && baitPlayer.Items[0].Quantity == 3,
                "Successful cast consumes exact bait once");

            var profession = new ProfessionDefinition { Id = Guid.NewGuid(), LevelRewards = [new(2, bait.Id, 2, Guid.Empty)] };
            var professionAward = new FishingProgressAward { Profession = profession, VariableId = profession.Id, Before = 90, After = 110, PreviousLevel = 1, NewLevel = 2 };
            var professional = Player();
            check(FishingRewards.Commit(professional, Guid.NewGuid(), fish.Id, 1, professionAward, Guid.Empty, 0, () =>
            {
                check(professional.GetVariable(profession.Id).Value.Integer == 111, "Profession stores cumulative XP plus learned marker");
                check(professional.Items.Where(i => i.ItemId == bait.Id).Sum(i => i.Quantity) == 2, "Profession level items share the catch transaction");
                return UserSaveResult.Completed;
            }) == FishingRewardResult.Success, "Existing profession is awarded atomically");

            check(FishingText.Get("Fishing", "fr") == "Pêche" && FishingText.Get("Fishing", "es") == "Pesca", "Server localization resolves French and Spanish");
            check(FishingText.Get("Fishing", "unknown") == "Fishing", "Server catalog has English fallback");
            var packet = new Intersect.Network.Packets.Client.FishingRequestPacket
            { Action = Intersect.Network.Packets.Client.FishingAction.UseSkill, SessionId = Guid.NewGuid(), Sequence = 19, Input = 3 };
            var copy = MessagePack.MessagePackSerializer.Deserialize<Intersect.Network.Packets.Client.FishingRequestPacket>(MessagePack.MessagePackSerializer.Serialize(packet));
            check(copy.SessionId == packet.SessionId && copy.Sequence == 19 && copy.Input == 3 && copy.Action == packet.Action, "Native protocol preserves authoritative request fields");
            check((int)new Intersect.Framework.Core.GameObjects.Events.Commands.OpenFishingCommand().Type == 1100, "Fishing Event ID is stable and distinct");
        }
        finally { ItemDescriptor.Lookup.DeleteAt(fish.Id); ItemDescriptor.Lookup.DeleteAt(bait.Id); }
    }
}
#endif
