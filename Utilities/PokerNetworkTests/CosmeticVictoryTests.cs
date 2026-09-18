using System.Runtime.CompilerServices;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Poker;
using MessagePack;
using Newtonsoft.Json;

internal static class CosmeticVictoryTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [ModuleInitializer]
    internal static void Run()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("Catalog IDs serialize as intent and do not control winnings", () =>
            {
                var request = new PokerRequestPacket { TableInstanceId = Guid.NewGuid(), ViewId = Guid.NewGuid(),
                    RequestId = 1, Kind = PokerRequestKind.SelectCardBack, CardBackId = 2 };
                var copy = MessagePackSerializer.Deserialize<PokerRequestPacket>(MessagePackSerializer.Serialize(request));
                Check(copy.IsValid && copy.CardBackId == 2 && copy.RaiseTo == 0, "Choice roundtrip");
                copy.CardBackId = 500; Check(!copy.IsValid, "Invalid catalog ID accepted");
                Check(typeof(PokerRequestPacket).GetProperty("NetWin") == null && typeof(PokerRequestPacket).GetProperty("VictoryAnimationId") == null, "Client controls win effect");
            }),
            ("Event JSON retains independent dealing and winner animation settings", () =>
            {
                var old = JsonConvert.DeserializeObject<StartMiniGameCommand>("{}")!;
                Check(!old.AnnounceWins && old.VictoryAnimationId == Guid.Empty && old.NpcCardBackId == 0, "Old event defaults changed");
                var command = new StartMiniGameCommand { DealAnimationId = Guid.NewGuid(), VictoryAnimationId = Guid.NewGuid(), AnnounceWins = true, NpcCardBackId = 2 };
                var restored = JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(command))!;
                Check(restored.HasValidSettings() && restored.VictoryAnimationId == command.VictoryAnimationId &&
                    restored.DealAnimationId == command.DealAnimationId && restored.AnnounceWins && restored.NpcCardBackId == 2, "Command roundtrip");
            }),
            ("Winner projection roundtrips, with no opponent private cards", () =>
            {
                var map = Guid.NewGuid(); var a = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
                var b = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
                var registry = new PokerTableRegistry(); var options = new PokerTableOptions(VictoryAnimationId: Guid.NewGuid());
                var id = registry.Join(a, "wire", "Alice", new PokerRules(2), options).TableInstanceId;
                registry.Join(b, "wire", "Bob", new PokerRules(2), options);
                registry.SelectCardBack(a, id, 1);
                var start = registry.StartHand(a, id, registry.Snapshot(a, id).Snapshot!.Revision, DateTimeOffset.UtcNow).Snapshot!;
                var live = PokerPresentationTransport.Project(start, registry.Presentation(a, id));
                Check(live.Seats.All(s => s.RevealedCards.Length == 0), "Cosmetic leaked cards");
                registry.Act(a, id, start.HandId, start.Revision, PokerAction.Fold, 0, DateTimeOffset.UtcNow);
                var win = PokerPresentationTransport.Project(registry.Snapshot(b, id).Snapshot!, registry.Presentation(b, id));
                var copy = MessagePackSerializer.Deserialize<PokerTableState>(MessagePackSerializer.Serialize(win));
                Check(copy.HasValidShape() && copy.NetWin == 5 && copy.VictoryAnimationId == options.VictoryAnimationId &&
                    copy.Seats.Single(s => s.PlayerId == a.Session.PlayerId).CardBackId == 1, "Winner/back wire data wrong");
                var loser = PokerPresentationTransport.Project(registry.Snapshot(a, id).Snapshot!, registry.Presentation(a, id));
                Check(loser.NetWin == 0 && loser.VictoryAnimationId == Guid.Empty, "Winner effect broadcast to loser");
            }),
            ("Victory tracker handles refresh, reopen, late join, loss and new hands", () =>
            {
                var tracker = new PokerVictoryTracker(); var table = Guid.NewGuid(); var player = Guid.NewGuid();
                Check(!tracker.Observe(table, player, 1, false, 0), "Initial live view celebrated");
                Check(tracker.Observe(table, player, 1, true, 10), "New victory ignored");
                Check(!tracker.Observe(table, player, 1, true, 10), "Refresh/reopen replayed victory");
                Check(!tracker.Observe(table, player, 2, true, 0), "Loss celebrated");
                Check(tracker.Observe(table, player, 3, true, 20), "Next victory ignored");
                Check(!tracker.Observe(table, player, 1, true, 10), "Old hand replayed");
                Check(!tracker.Observe(Guid.NewGuid(), player, 8, true, 50), "Late finished view celebrated");
            }),
            ("Back filenames are a fixed safe catalog with a classic fallback", () =>
            {
                Check(PokerCardAssets.BackFileName(0) == "back.png" && PokerCardAssets.BackFileName(1) == "back_royal.png" &&
                    PokerCardAssets.BackFileName(2) == "back_pirate.png" && PokerCardAssets.BackFileName(3) == "back_halloween.png", "Catalog mismatch");
                Check(PokerCardAssets.BackFileName(-1) == "back.png" && PokerCardAssets.BackFileName(int.MaxValue) == "back.png", "Unsafe fallback");
            }),
        };
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); System.Console.WriteLine("PASS COSMETIC WIRE: " + name); }
            catch (Exception ex) { ++failures; System.Console.Error.WriteLine("FAIL COSMETIC WIRE: " + name + "\n" + ex); }
        }
        System.Console.WriteLine($"{tests.Length - failures}/{tests.Length} cosmetic/victory protocol groups passed.");
        if (failures != 0) throw new InvalidOperationException("Cosmetic/victory protocol regression failed.");
    }
}
