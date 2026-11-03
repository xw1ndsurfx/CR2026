#if FISHING_SERVER_TESTS
using System.Reflection;
using System.Text.Json;
using Intersect;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Fishing;
using Intersect.Server.Maps;

internal static class ServerRewardChecks
{
    internal static void Run(Action<bool, string> check)
    {
        Options.EnsureCreated();
        CheckZonePersistence(check);
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

    private static void CheckZonePersistence(Action<bool, string> check)
    {
        var maps = new[] { new MapController(Guid.NewGuid()), new MapController(Guid.NewGuid()) };
        foreach (var map in maps) GC.SuppressFinalize(map);
        var cache = typeof(FishingRuntime).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previousConfiguration = cache.GetValue(null);
        var previousDirectory = Environment.CurrentDirectory;
        var temporary = Path.Combine(Path.GetTempPath(), "cr-fishing-maps-" + Guid.NewGuid().ToString("N"));
        void Reject(Action action, string message)
        {
            try { action(); }
            catch (InvalidDataException) { check(true, message); return; }
            check(false, message);
        }
        try
        {
            // The server registry forwards additions to the descriptor registry, not the reverse.
            foreach (var map in maps) MapController.Lookup.Set(map.Id, map);
            check(maps.All(map => ReferenceEquals(MapController.Get(map.Id), map) &&
                ReferenceEquals(MapDescriptor.Lookup.Get(map.Id), map)),
                "Fixture maps are registered in both native server and descriptor lookups");
            Directory.CreateDirectory(temporary); Environment.CurrentDirectory = temporary;
            var configuration = new FishingConfiguration
            {
                Zones = [new() { Name = "Map persistence", Enabled = false, MapIds = maps.Select(m => m.Id).ToList() }],
            };
            FishingRuntime.Save(configuration.ToJson());
            var activeFile = Path.Combine("resources", "fishing.json");
            var persisted = File.ReadAllText(activeFile);
            cache.SetValue(null, null);
            var reloaded = FishingRuntime.Current;
            check(reloaded.Zones[0].MapIds.SequenceEqual(maps.Select(m => m.Id)) && !reloaded.Zones[0].UseTileRadius,
                "The native server persists and reloads every selected map in whole-map mode");

            configuration.Zones[0].MapIds[1] = Guid.NewGuid();
            Reject(() => FishingRuntime.Save(configuration.ToJson()),
                "Native validation checks the second selected map even on a disabled zone");
            check(File.ReadAllText(activeFile) == persisted && ReferenceEquals(FishingRuntime.Current, reloaded),
                "A missing map reference cannot change the active file or in-memory configuration");

            configuration.Zones[0].MapIds = [maps[1].Id];
            FishingRuntime.Save(configuration.ToJson()); cache.SetValue(null, null);
            check(FishingRuntime.Current.Zones[0].MapIds.SequenceEqual([maps[1].Id]),
                "Unchecking a map remains effective after a native save and reload");

            configuration.Zones[0].X = Options.Instance.Map.MapWidth;
            FishingRuntime.Save(configuration.ToJson());
            check(!FishingRuntime.Current.Zones[0].UseTileRadius,
                "Inactive tile coordinates do not restrict whole-map zones");
            configuration.Zones[0].UseTileRadius = true;
            Reject(() => FishingRuntime.Save(configuration.ToJson()),
                "Explicit tile-radius mode still rejects a center outside the actual map dimensions");
            configuration.Zones[0].X = 0; configuration.Zones[0].Y = 0; configuration.Zones[0].Radius = 2;
            FishingRuntime.Save(configuration.ToJson()); cache.SetValue(null, null);
            check(FishingRuntime.Current.Zones[0].UseTileRadius && FishingRuntime.Current.Zones[0].Radius == 2,
                "Explicit radius mode survives native persistence");

            var legacy = JsonSerializer.Serialize(new
            {
                Version = 1,
                Zones = new[] { new { Name = "Legacy server area", Enabled = false, MapId = maps[0].Id, X = 0, Y = 0, Radius = 2 } },
            });
            File.WriteAllText(activeFile, legacy); cache.SetValue(null, null);
            var migrated = FishingRuntime.Current;
            check(migrated.Zones[0].MapIds.SequenceEqual([maps[0].Id]) && migrated.Zones[0].UseTileRadius && migrated.Zones[0].Radius == 2,
                "Loading a real legacy server file preserves its original map and restricted area");
            FishingRuntime.Save(migrated.ToJson()); cache.SetValue(null, null);
            check(FishingRuntime.Current.Zones[0].MapIds.SequenceEqual([maps[0].Id]) && FishingRuntime.Current.Zones[0].UseTileRadius,
                "Applying and reloading migrated server data never widens its area");
            using var canonical = JsonDocument.Parse(File.ReadAllText(activeFile));
            check(!canonical.RootElement.GetProperty("Zones")[0].TryGetProperty("MapId", out _),
                "Native persistence writes only the canonical map selection");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            cache.SetValue(null, previousConfiguration);
            foreach (var map in maps) MapController.Lookup.Delete(map);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }
}
#endif
