using System.Runtime.CompilerServices;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Poker;
using MessagePack;
using Newtonsoft.Json;

internal static class PresentationTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    [ModuleInitializer]
    internal static void Run()
    {
        var files = Enumerable.Range(0, 52).Select(PokerCardAssets.FileNameFor).ToArray();
        Check(files.Distinct(StringComparer.Ordinal).Count() == 52 && files.All(f => f is { Length: 6 } && f.EndsWith(".png", StringComparison.Ordinal)), "Card filename contract");
        Check(PokerCardAssets.FileNameFor(12) == "AC.png" && PokerCardAssets.FileNameFor(34) == "TH.png" &&
            PokerCardAssets.FileNameFor(-1) == null && PokerCardAssets.FileNameFor(52) == null && PokerCardAssets.Back == "back.png", "Card identity mapping");
        Console.WriteLine("PASS PRESENTATION: 52 fixed Misc basenames and one common back");

        var tracker = new PokerDealTracker(); var id = Guid.NewGuid();
        Check(!tracker.Observe(id, 0, 0) && tracker.Observe(id, 1, 0) && !tracker.Observe(id, 1, 0), "Initial distribution deduplication");
        Check(tracker.Observe(id, 1, 3) && !tracker.Observe(id, 1, 3) && tracker.Observe(id, 1, 4), "Board distribution deduplication");
        Check(!tracker.Observe(id, 1, 3) && !tracker.Observe(id, 0, 0) && tracker.Observe(id, 1, 5), "Stale deal replay");
        Check(tracker.Observe(id, 2, 0) && !new PokerDealTracker().Observe(id, 2, 0) &&
            !tracker.Observe(Guid.NewGuid(), 3, 5), "Reopen or other-table replay");
        Console.WriteLine("PASS PRESENTATION: no replay on refresh/reopen/stale packets");

        var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate };
        var legacy = JsonConvert.DeserializeObject<StartMiniGameCommand>("{}", settings)!;
        Check(legacy.HasValidSettings() && !legacy.DealerPlays && legacy.NpcPlayers == 0 && !legacy.AutoStart && legacy.DealAnimationId == Guid.Empty, "Legacy command defaults");
        var animation = Guid.NewGuid();
        var command = new StartMiniGameCommand { DealerPlays = true, NpcPlayers = 2, AutoStart = true, DealAnimationId = animation };
        var copy = JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(command, settings), settings)!;
        Check(copy.HasValidSettings() && copy.DealerPlays && copy.NpcPlayers == 2 && copy.AutoStart && copy.DealAnimationId == animation, "Editor settings did not round-trip");
        Console.WriteLine("PASS PRESENTATION: legacy and new event serialization");

        var registry = new PokerTableRegistry();
        var person = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), Guid.Empty);
        var joined = registry.Join(person, "wire", "Human", new(), new(true, 1, true, animation));
        registry.StartHand(person, joined.TableInstanceId, joined.Snapshot!.Revision, DateTimeOffset.UtcNow);
        var snapshot = registry.Snapshot(person, joined.TableInstanceId).Snapshot!;
        var meta = registry.Presentation(person, joined.TableInstanceId);
        var state = PokerPresentationTransport.Project(snapshot, meta);
        var wire = MessagePackSerializer.Deserialize<PokerTableState>(MessagePackSerializer.Serialize(state));
        Check(wire.HasValidShape() && wire.DealerNpcId == meta.DealerNpcId && wire.NpcIds.Length == 2 &&
            wire.AutoStart && wire.DealAnimationId == animation && wire.MyCards.Length == 2 &&
            wire.Seats.All(s => s.RevealedCards.Length == 0), "Presentation round-trip/privacy");
        state.NpcIds[0] = Guid.Empty;
        Check(meta.NpcIds.All(x => x != Guid.Empty) && wire.NpcIds.All(x => x != Guid.Empty), "NPC metadata aliases source");
        Console.WriteLine("PASS PRESENTATION: MessagePack metadata, isolation and NPC privacy");

        var valid = wire.NpcIds.ToArray();
        wire.NpcIds = [Guid.NewGuid()]; Check(!wire.HasValidShape(), "Unknown NPC accepted");
        wire.NpcIds = [valid[0], valid[0]]; Check(!wire.HasValidShape(), "Duplicate NPC accepted");
        wire.NpcIds = valid; wire.DealerNpcId = person.Session.PlayerId;
        Check(!wire.HasValidShape(), "Human accepted as NPC croupier");
        Console.WriteLine("PASS PRESENTATION: malformed NPC metadata rejected");
        Console.WriteLine("5/5 poker presentation test groups passed.");
    }
}
