using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Systems.Deaths;
using Intersect.Network;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using MessagePack;

internal static class DeathSystemTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        var before = Options.Instance;
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            var map = Guid.NewGuid(); var destination = Guid.NewGuid();
            var config = new DeathConfiguration { Rules = [new() { MapIds = [map], RespawnMapId = destination, X = 4, Y = 5, AnimationId = Guid.NewGuid(), CommonEventId = Guid.NewGuid(), Direction = Direction.Left }] };
            var restored = DeathConfiguration.FromJson(config.ToJson());
            Check(restored.Find(map)?.RespawnMapId == destination && restored.Find(Guid.NewGuid()) == null, "Death rules use explicit maps, with ordinary maps unaffected");
            Check(restored.Rules[0].CommonEventId == config.Rules[0].CommonEventId && restored.Rules[0].AnimationId == config.Rules[0].AnimationId, "Event and animation choices survive persistence");
            config.Rules.Add(new() { MapIds = [map], RespawnMapId = destination }); Reject(() => DeathConfiguration.FromJson(config.ToJson())); config.Rules.RemoveAt(1);
            config.Rules[0].X = Options.Instance.Map.MapWidth; Reject(() => DeathConfiguration.FromJson(config.ToJson())); config.Rules[0].X = 0;
            config.Rules[0].EventTimeoutSeconds = 0; Reject(() => DeathConfiguration.FromJson(config.ToJson())); config.Rules[0].EventTimeoutSeconds = 120;
            Reject(() => DeathConfiguration.FromJson("{\"Rules\":null}")); Reject(() => DeathConfiguration.FromJson("{\"Rules\":[null]}"));
            var animation = new AnimationDescriptor(Guid.NewGuid()) { Lower = new() { Sprite = "death", FrameCount = 8, XFrames = 4, YFrames = 2, FrameSpeed = 100, LoopCount = 1 }, Upper = new() { Sprite = "", FrameCount = 1000, FrameSpeed = 1000 } };
            Check(DeathConfiguration.AnimationDuration(animation) == 1600, "Death delay includes the initial animation and all repeats, ignoring empty layers");
            animation.Upper = new() { Sprite = "effect", FrameCount = 6, XFrames = 3, YFrames = 2, FrameSpeed = 500 };
            Check(DeathConfiguration.AnimationDuration(animation) == 3000 && DeathConfiguration.AnimationDuration(null) == 0, "Respawn waits for the longest visible layer and skips missing animations");
            animation.Upper.FrameCount = animation.Upper.FrameSpeed = animation.Upper.LoopCount = int.MaxValue;
            Check(DeathConfiguration.AnimationDuration(animation) > 60000, "Malformed animation timing is detectable for rejection without overflowing");
            var item = new ItemDescriptor(Guid.NewGuid()) { Price = 251 }; var currency = new ItemDescriptor(Guid.NewGuid()) { Price = 10 };
            Check(ShopPriceSuggestion.Suggest(item, currency, false) == 26 && ShopPriceSuggestion.Suggest(item, currency, true) == 12, "Shop suggestions convert currency value and retain a buyback margin");
            currency.Price = 0; Check(ShopPriceSuggestion.Suggest(item, currency, false) == 251 && ShopPriceSuggestion.Suggest(item, currency, true) == 125, "Unvalued currency falls back to the editable base item price");
            currency.Price = 1; item.Price = int.MaxValue;
            Check(ShopPriceSuggestion.Suggest(item, currency, false) == int.MaxValue && ShopPriceSuggestion.Suggest(item, item, true) == null, "Price overflow and self-currency are handled");
            var pendingPlayer = (Intersect.Server.Entities.Player)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Server.Entities.Player)); GC.SuppressFinalize(pendingPlayer);
            var nested = pendingPlayer.GetType().GetNestedType("PendingDeath", BindingFlags.NonPublic)!;
            var pending = RuntimeHelpers.GetUninitializedObject(nested);
            nested.GetField("Animating", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pending, true);
            nested.GetField("Deadline", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pending, long.MaxValue);
            pendingPlayer.GetType().GetField("_pendingDeath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pendingPlayer, pending);
            pendingPlayer.GetType().GetMethod("AdvanceConfiguredDeath", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pendingPlayer, null);
            Check(pendingPlayer.HasPendingDeath && !pendingPlayer.CanMoveInDirection(Direction.Up, out _, out _), "A running death animation blocks authoritative movement until its deadline");
            pendingPlayer.Die(); Check(pendingPlayer.HasPendingDeath, "Repeated death calls cannot restart a pending sequence or drop items twice");
            var eventPacket = new EventEntityPacket { AnimationOffsetX = -128, AnimationOffsetY = 64, RenderLayer = 0 };
            var eventCopy = MessagePackSerializer.Deserialize<EventEntityPacket>(MessagePackSerializer.Serialize(eventPacket));
            Check(eventCopy.AnimationOffsetX == -128 && eventCopy.AnimationOffsetY == 64 && eventCopy.RenderLayer == 0, "Offsets and layer travel together in event data");
            var playerPacket = new PlayerEntityPacket { PendingDeath = true };
            Check(MessagePackSerializer.Deserialize<PlayerEntityPacket>(MessagePackSerializer.Serialize(playerPacket)).PendingDeath, "Death input lock travels with entity data");
            var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
            Check(registry.TryRegisterBuiltIn(), "New death packet types register");
            if (PackedIntersectPacket.KnownTypes.Count == 0) PackedIntersectPacket.AddKnownTypes(registry.Types);
            foreach (var packet in new IntersectPacket[] { new DeathConfigurationRequestPacket { ConfigurationJson = restored.ToJson() }, new DeathConfigurationPacket { ConfigurationJson = restored.ToJson(), Saved = true } })
                Check(MessagePacker.Instance.Deserialize(packet.Data)?.GetType() == packet.GetType(), "Death editor packets survive the real network envelope");
            // A damaged optional config must never crash the next ordinary player death.
            var runtime = typeof(Intersect.Server.Entities.Player).Assembly.GetType("Intersect.Server.Systems.Deaths.DeathConfigurationRuntime", true)!;
            var cache = runtime.GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
            var original = cache.GetValue(null); var cwd = Environment.CurrentDirectory;
            var temporary = Path.Combine(Path.GetTempPath(), "cr-deaths-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(temporary, "resources"));
            try
            {
                Environment.CurrentDirectory = temporary; File.WriteAllText(Path.Combine("resources", "deaths.json"), "broken json"); cache.SetValue(null, null);
                var fallback = (DeathConfiguration)runtime.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                Check(fallback.Rules.Count == 0 && File.ReadAllText(Path.Combine("resources", "deaths.json")) == "broken json", "Corrupt config falls back without destroying the recoverable file");
            }
            finally { Environment.CurrentDirectory = cwd; cache.SetValue(null, original); Directory.Delete(temporary, true); }
            System.Console.WriteLine("PASS death configuration, timing, packet envelopes, corrupt-config fallback and shop currency suggestions");
        }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, before); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid death configuration accepted"); }
}
