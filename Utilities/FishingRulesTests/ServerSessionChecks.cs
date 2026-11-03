#if FISHING_SERVER_TESTS
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using Intersect.Server.Fishing;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using MessagePack;
using GameEvent = Intersect.Server.Entities.Events.Event;

/// <summary>
/// Exercises native event activation and dialogue acknowledgement, rather than calling the
/// fishing rule helpers directly. A queued connection captures the actual packets sent to a client.
/// </summary>
internal static class ServerSessionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly FieldInfo Current = typeof(FishingRuntime).GetField("_current", PrivateStatic)!;
    private static readonly object Gate = typeof(FishingRuntime).GetField("Gate", PrivateStatic)!.GetValue(null)!;
    private static readonly MethodInfo UpdateFishing = typeof(Player).GetMethod("UpdateFishing", PrivateInstance)!;

    internal static void Run(Action<bool, string> check)
    {
        Options.EnsureCreated();
        var directory = Path.Combine(Path.GetTempPath(), "fishing-session-tests-" + Guid.NewGuid().ToString("N"));
        var oldDirectory = Directory.GetCurrentDirectory();
        var oldCurrent = Current.GetValue(null);
        Directory.CreateDirectory(directory);
        try
        {
            Directory.SetCurrentDirectory(directory);
            CheckOpening(check, withDialogue: false, holdPlayer: false, worldProfile: false);
            CheckOpening(check, withDialogue: true, holdPlayer: false, worldProfile: false);
            CheckOpening(check, withDialogue: true, holdPlayer: true, worldProfile: true);
            CheckRejections(check);
            CheckMovementInterruption(check);
        }
        finally
        {
            lock (Gate) Current.SetValue(null, oldCurrent);
            Directory.SetCurrentDirectory(oldDirectory);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckOpening(Action<bool, string> check, bool withDialogue, bool holdPlayer, bool worldProfile)
    {
        using var fixture = new Fixture(withDialogue, worldProfile);
        fixture.Event.HoldingPlayer = holdPlayer;
        fixture.RunEvent(check, withDialogue);
        var opening = fixture.Packets.OfType<FishingStatePacket>().Where(packet => packet.Open).ToArray();
        check(opening.Length == 1 && fixture.Player.IsFishingActive,
            $"A native fishing Event opens exactly once (dialogue={withDialogue}, own hold={holdPlayer}, net profile={worldProfile})");
        var packet = MessagePackSerializer.Deserialize<FishingStatePacket>(MessagePackSerializer.Serialize(opening.Single()));
        var state = FishingState.FromJson(packet.StateJson)!;
        check(!packet.Editor && state.Phase == FishingPhase.Ready && state.SessionId != Guid.Empty,
            "The serialized opening packet is a client Ready session");
        check(state.ZoneId == fixture.Configuration.Zones.Single().Id && state.ToolItemId == fixture.Tool.Id &&
            FishingConfiguration.FromJson(packet.ConfigurationJson).Zones.Single().Enabled,
            "The opening packet carries the selected active zone and actually equipped tool");
        check(state.RemainingBait == 0 && fixture.Player.IsFishingActive,
            "An empty bait stack does not suppress the fishing window before casting");
        check(fixture.Event.CallStack.Count == 0,
            "The native event completes after opening the fishing session");

        fixture.ClearPackets();
        fixture.TickFishing();
        check(fixture.Player.IsFishingActive && fixture.Packets.OfType<FishingStatePacket>().Any(p =>
                !p.Open && FishingState.FromJson(p.StateJson)?.Phase == FishingPhase.Ready),
            "Event completion and its own player hold do not close the Ready session on the next fishing tick");
        check(fixture.Packets.OfType<FishingStatePacket>().All(p => FishingState.FromJson(p.StateJson)?.Phase != FishingPhase.Closed),
            "A valid stationary player receives no immediate close after the opening packet");
    }

    private static void CheckRejections(Action<bool, string> check)
    {
        using (var fixture = new Fixture(withDialogue: true, worldProfile: false))
        {
            fixture.Player.Equipment[0] = -1;
            fixture.RunEvent(check, withDialogue: true);
            var replies = fixture.Packets.OfType<FishingStatePacket>().ToArray();
            check(!fixture.Player.IsFishingActive && replies.Length == 1 && !replies[0].Open &&
                    replies[0].Message == FishingText.Get("Equip a compatible fishing tool.", "fr"),
                "A reached Event without an equipped net returns an explicit localized error instead of disappearing");
        }
        using (var fixture = new Fixture(withDialogue: true, worldProfile: false))
        {
            fixture.Configuration.Zones[0].Enabled = false;
            fixture.RunEvent(check, withDialogue: true);
            var replies = fixture.Packets.OfType<FishingStatePacket>().ToArray();
            check(!fixture.Player.IsFishingActive && replies.Length == 1 && !replies[0].Open &&
                    replies[0].Message == FishingText.Get("Fishing is unavailable here.", "fr"),
                "A reached Event for a disabled zone returns its localized rejection");
        }
    }

    private static void CheckMovementInterruption(Action<bool, string> check)
    {
        using var fixture = new Fixture(withDialogue: true, worldProfile: false);
        fixture.RunEvent(check, withDialogue: true);
        check(fixture.Player.IsFishingActive, "The movement case starts from a real open session");
        fixture.ClearPackets();
        fixture.Player.X++;
        fixture.TickFishing();
        var reply = fixture.Packets.OfType<FishingStatePacket>().Single();
        var state = FishingState.FromJson(reply.StateJson)!;
        check(!fixture.Player.IsFishingActive && state.Phase == FishingPhase.Closed &&
                state.Message == FishingText.Get("Fishing stopped because your position or equipment changed.", "fr"),
            "Movement after the opening packet closes the session with a localized state message");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConcurrentQueue<Tuple<IPacket, TransmissionMode, long>> _queue = new();
        private readonly PacketHandler _handler;
        private readonly ItemDescriptor _bait, _fish;
        private readonly List<MapGrid> _grids;
        private readonly MapGrid _grid;
        internal readonly MapController Map;
        internal readonly ItemDescriptor Tool;
        internal readonly Player Player;
        internal readonly Client Client;
        internal readonly GameEvent Event;
        internal readonly FishingConfiguration Configuration;
        internal IPacket[] Packets => _queue.Select(entry => entry.Item1).ToArray();

        internal Fixture(bool withDialogue, bool worldProfile)
        {
            Map = new MapController(Guid.NewGuid()) { Name = "Fishing Event fixture", SurroundingMapIds = [], SurroundingMaps = [] };
            GC.SuppressFinalize(Map);
            MapController.Lookup.Set(Map.Id, Map);
            // Turning an activated event towards its player uses the real world grid. Append
            // an isolated grid and remove it on disposal without rebuilding other maps.
            _grids = (List<MapGrid>)typeof(DbInterface).GetField("mapGrids", PrivateStatic)!.GetValue(null)!;
            lock (_grids)
            {
                _grid = new MapGrid(Map.Id, _grids.Count);
                _grids.Add(_grid);
            }
            Tool = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing Event fixture net", ItemType = ItemType.Equipment, EquipmentSlot = 0 };
            _bait = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing Event fixture bait", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
            _fish = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing Event fixture fish", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
            foreach (var item in new[] { Tool, _bait, _fish }) ItemDescriptor.Lookup.Set(item.Id, item);
            var configuration = new FishingConfiguration
            {
                Tools = [new() { ItemId = Tool.Id, Type = FishingToolType.Net, BaitItemId = _bait.Id,
                    WorldProfile = worldProfile ? FishingWorldProfile.Net() : new() }],
                Zones = [new() { Name = "Fishing Event fixture zone", Enabled = true, MapIds = [Map.Id], UseTileRadius = false,
                    Catches = [new() { ItemId = _fish.Id, ToolItemIds = [Tool.Id] }] }],
            };
            FishingRuntime.Save(configuration.ToJson());
            Configuration = FishingRuntime.Current;

            Player = new Player { Name = "Fishing Event fixture player", InGame = true, MapId = Map.Id, X = 2, Y = 3 };
            GC.SuppressFinalize(Player);
            Array.Fill(Player.Equipment, -1);
            Player.Items[0].Set(new Item(Tool.Id, 1));
            Player.Equipment[0] = 0;
            Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client));
            Client.LanguageCode = "fr";
            Client.Entity = Player;
            Client.PacketSendingQueued = true;
            typeof(Client).GetField("mSendPacketQueue", PrivateInstance)!.SetValue(Client, _queue);
            typeof(Client).GetProperty(nameof(Client.Connection))!.SetValue(Client, new TestConnection());
            Player.Client = Client;
            _handler = (PacketHandler)RuntimeHelpers.GetUninitializedObject(typeof(PacketHandler));

            var descriptor = new EventDescriptor(Guid.NewGuid(), Map.Id, 2, 4) { Name = "Fishing Event fixture spot" };
            var commands = descriptor.Pages[0].CommandLists.Values.First();
            if (withDialogue) commands.Add(new ShowTextCommand { Text = "Fishing Event fixture dialogue" });
            commands.Add(new OpenFishingCommand { ZoneId = Configuration.Zones[0].Id });
            // Exercise the same polymorphic JSON round-trip used when the authored map is saved.
            descriptor = new EventDescriptor(descriptor.Id, descriptor.JsonData);
            Event = new GameEvent(Guid.NewGuid(), Map, Player, descriptor) { WaitTimer = -1 };
            Event.PageInstance = new EventPageInstance(descriptor, descriptor.Pages[0], Map.Id, Player.MapInstanceId, Event, Player, sendInitial: false);
            GC.SuppressFinalize(Event.PageInstance);
            Player.EventLookup[Event.Id] = Event;
        }

        internal void RunEvent(Action<bool, string> check, bool withDialogue)
        {
            _handler.HandlePacket(Client, new ActivateEventPacket(Event.PageInstance.Id));
            check(Event.CallStack.Count == 1, "The native activation packet starts the authored Action Button Event");
            Event.Update(Timing.Global.Milliseconds, Map);
            if (!withDialogue) return;

            var dialogue = Packets.OfType<EventDialogPacket>().Single();
            check(Event.CallStack.Peek().WaitingForResponse == CommandInstance.EventResponse.Dialogue && !Player.IsFishingActive,
                "Show Text pauses the Event before the fishing command");
            _handler.HandlePacket(Client, new EventResponsePacket(dialogue.EventId, 0) { DialogueToken = dialogue.DialogueToken });
            Event.Update(Timing.Global.Milliseconds, Map);
        }

        internal void TickFishing() => UpdateFishing.Invoke(Player, [(long?)(Environment.TickCount64 + 250)]);
        internal void ClearPackets() { while (_queue.TryDequeue(out _)) { } }

        public void Dispose()
        {
            Player.LeaveFishingFromEvent();
            Player.EventLookup.Clear();
            foreach (var item in new[] { Tool, _bait, _fish }) ItemDescriptor.Lookup.DeleteAt(item.Id);
            lock (_grids) _grids.Remove(_grid);
            MapController.Lookup.DeleteAt(Map.Id);
        }
    }

    private sealed class TestConnection : IConnection
    {
        public Guid Guid { get; } = System.Guid.NewGuid();
        public bool IsConnected => true;
        public string Ip => "127.0.0.1";
        public int Port => 0;
        public ConnectionStatistics Statistics => null!;
        public bool Send(IPacket packet, TransmissionMode mode = TransmissionMode.All) => true;
        public void HandleConnected() { }
        public void HandleApproved() { }
        public void HandleDisconnected() { }
        public void Disconnect(string? message = null) { }
        public void Dispose() { }
    }
}
#endif
