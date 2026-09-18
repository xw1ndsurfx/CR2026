using System.Runtime.CompilerServices;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using MessagePack;
using Newtonsoft.Json;

internal static class CosmeticVictoryTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [ModuleInitializer]
    internal static void Run()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("All six back IDs roundtrip as intent, never winnings or XP", () =>
            {
                for (var id = 0; id < 6; ++id)
                {
                    var request = new PokerRequestPacket { TableInstanceId = Guid.NewGuid(), ViewId = Guid.NewGuid(), RequestId = 1, Kind = PokerRequestKind.SelectCardBack, CardBackId = id };
                    var copy = MessagePackSerializer.Deserialize<PokerRequestPacket>(MessagePackSerializer.Serialize(request));
                    Check(copy.IsValid && copy.CardBackId == id && copy.RaiseTo == 0, "Choice roundtrip");
                    copy.CardBackId = 6; Check(!copy.IsValid, "Invalid ID accepted");
                }
                Check(typeof(PokerRequestPacket).GetProperty("NetWin") == null && typeof(PokerRequestPacket).GetProperty("Experience") == null &&
                    typeof(PokerRequestPacket).GetProperty("VictoryAnimationId") == null, "Client controls rewards");
            }),
            ("Event JSON preserves independent animations and B6 NPC option", () =>
            {
                var old = JsonConvert.DeserializeObject<StartMiniGameCommand>("{}")!;
                Check(!old.AnnounceWins && old.VictoryAnimationId == Guid.Empty && old.NpcCardBackId == 0, "Old defaults changed");
                var command = new StartMiniGameCommand { DealAnimationId = Guid.NewGuid(), VictoryAnimationId = Guid.NewGuid(), AnnounceWins = true, NpcCardBackId = 5 };
                var copy = JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(command))!;
                Check(copy.HasValidSettings() && copy.VictoryAnimationId == command.VictoryAnimationId && copy.DealAnimationId == command.DealAnimationId && copy.AnnounceWins && copy.NpcCardBackId == 5, "Command roundtrip");
            }),
            ("Winner XP and public decisions roundtrip without opponent private cards", () =>
            {
                var map = Guid.NewGuid(); var a = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
                var b = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
                var store = new MemoryMiniGameProgressStore();
                for (var h = 1; h <= 40; ++h) store.AwardWin(a.Session.PlayerId, "poker", map, h);
                var registry = new PokerTableRegistry(store); var options = new PokerTableOptions(VictoryAnimationId: Guid.NewGuid());
                var id = registry.Join(a, "wire", "Alice", new PokerRules(2), options).TableInstanceId;
                registry.Join(b, "wire", "Bob", new PokerRules(2), options);
                Check(registry.SelectCardBack(a, id, 1).Error == PokerRegistryError.None, "Unlocked back rejected");
                var start = registry.StartHand(a, id, registry.Snapshot(a, id).Snapshot!.Revision, DateTimeOffset.UtcNow).Snapshot!;
                var live = PokerPresentationTransport.Project(start, registry.Presentation(a, id));
                Check(live.Seats.All(s => s.RevealedCards.Length == 0), "Cosmetic leaked cards");
                registry.Act(a, id, start.HandId, start.Revision, PokerAction.Fold, 0, DateTimeOffset.UtcNow);
                var win = PokerPresentationTransport.Project(registry.Snapshot(b, id).Snapshot!, registry.Presentation(b, id));
                var copy = MessagePackSerializer.Deserialize<PokerTableState>(MessagePackSerializer.Serialize(win));
                Check(copy.HasValidShape() && copy.NetWin == 5 && copy.Experience == 25 && copy.Wins == 1 &&
                    copy.VictoryAnimationId == options.VictoryAnimationId && copy.Seats.Single(s => s.PlayerId == a.Session.PlayerId).CardBackId == 1, "Winner wire data wrong");
                Check(copy.Decisions.Any(d => d.Name == "Alice" && d.Action == "fold") && copy.Decisions.Any(d => d.Name == "Bob" && d.Action == "wins"), "Public feed missing");
                var loser = PokerPresentationTransport.Project(registry.Snapshot(a, id).Snapshot!, registry.Presentation(a, id));
                Check(loser.NetWin == 0 && loser.VictoryAnimationId == Guid.Empty && loser.Experience == 1000, "Recipient reward isolation broken");
                copy.Experience = -1; Check(!copy.HasValidShape(), "Negative XP accepted"); copy.Experience = 25;
                copy.Decisions[0].Action = "secret-cards"; Check(!copy.HasValidShape(), "Unknown decision accepted");
            }),
            ("Victory replay protection handles refresh, reopen and late joins", () =>
            {
                var t = new PokerVictoryTracker(); var table = Guid.NewGuid(); var player = Guid.NewGuid();
                Check(!t.Observe(table, player, 1, false, 0) && t.Observe(table, player, 1, true, 10), "First victory wrong");
                Check(!t.Observe(table, player, 1, true, 10) && !t.Observe(table, player, 2, true, 0), "Replay/loss celebrated");
                Check(t.Observe(table, player, 3, true, 20) && !t.Observe(table, player, 1, true, 10), "Hand sequence wrong");
                Check(!t.Observe(Guid.NewGuid(), player, 8, true, 50), "Historical win replayed");
            }),
            ("B1-B6 are fixed names with B1 fallback", () =>
            {
                for (var id = 0; id < 6; ++id) Check(PokerCardAssets.BackFileName(id) == $"B{id + 1}.png", "Artist filename mismatch");
                Check(PokerCardAssets.BackFileName(-1) == "B1.png" && PokerCardAssets.BackFileName(int.MaxValue) == "B1.png", "Unsafe fallback");
            }),
            ("Relative seating and reference rectangles stay bounded", () =>
            {
                for (var local = 0; local < 6; ++local)
                {
                    Check(PokerSceneLayout.Slot(local, local) == 0, "Local seat not at bottom");
                    Check(Enumerable.Range(0, 6).Select(s => PokerSceneLayout.Slot(s, local)).Distinct().Count() == 6, "Seat positions collided");
                }
                foreach (var (width, height) in new[] { (640,480), (858,658), (1280,720), (1920,1080) })
                {
                    var all = new PokerSceneLayout(width, height).Rect(0, 0, 1000, 780);
                    Check(all.X >= 0 && all.Y >= 0 && all.X + all.Width <= width && all.Y + all.Height <= height, "Scene outside canvas");
                }
            }),
        };
        var failed = 0;
        foreach (var (name, test) in tests)
        { try { test(); System.Console.WriteLine("PASS COSMETIC WIRE: " + name); } catch (Exception ex) { ++failed; System.Console.Error.WriteLine("FAIL COSMETIC WIRE: " + name + "\n" + ex); } }
        System.Console.WriteLine($"{tests.Length - failed}/{tests.Length} cosmetic/victory protocol groups passed.");
        if (failed > 0) throw new InvalidOperationException("Cosmetic/victory protocol regression failed.");
    }
}
