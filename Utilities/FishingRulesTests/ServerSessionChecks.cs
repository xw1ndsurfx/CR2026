#if FISHING_SERVER_TESTS
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Professions;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using Intersect.Server.Fishing;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using MessagePack;
using GameEvent = Intersect.Server.Entities.Events.Event;
using ChatMsgPacket = Intersect.Network.Packets.Server.ChatMsgPacket;

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
            CheckConfiguredNet(check);
            CheckAutomaticFailure(check);
            CheckPendingLoot(check);
            CheckRejections(check);
            CheckMovementInterruption(check);
            ServerConnectionChecks.Run(check);
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

    private static void CheckConfiguredNet(Action<bool, string> check)
    {
        // Match the reported setup without using any live game IDs: an ordinary weapon
        // configured as a fishing net, currency bait/reward, and a custom profession.
        using var fixture = new Fixture(withDialogue: true, worldProfile: true, customProfession: true);
        check(fixture.Player.TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var equipped) &&
                equipped.ItemId == fixture.Tool.Id && fixture.Player.Equipment[Options.Instance.Equipment.WeaponSlot] == 9,
            "The configured net is equipped through the real equipment API from a nonzero inventory slot");
        fixture.RunEvent(check, withDialogue: true);
        var opening = fixture.Packets.OfType<FishingStatePacket>().Single(packet => packet.Open);
        var state = FishingState.FromJson(opening.StateJson)!;
        check(fixture.Player.IsFishingActive && state.Phase == FishingPhase.Ready && state.ToolType == FishingToolType.Net &&
                state.ToolItemId == fixture.Tool.Id && state.RemainingBait == 17,
            "A weapon named Knife Wood configured as a net opens with currency bait and a currency reward");
        check(state.Level == 1 && state.Experience == 0 && state.MaximumLevel == 12 && state.ExperienceRequired == 100,
            "A custom fishing profession opens at level one before it has a stored player variable");
        check(FishingConfiguration.FromJson(opening.ConfigurationJson).ProfessionId == fixture.Configuration.ProfessionId,
            "The opening packet retains the explicitly selected custom profession");

        var variable = new PlayerVariable(fixture.Configuration.ProfessionId);
        variable.Value.Integer = 151; // Custom professions persist total experience + the learned marker.
        fixture.Player.Variables.Add(variable);
        fixture.ClearPackets();
        fixture.TickFishing();
        state = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Single().StateJson)!;
        check(fixture.Player.IsFishingActive && state.Phase == FishingPhase.Ready && state.Level == 2 &&
                state.Experience == 150 && state.ExperienceIntoLevel == 50 && state.ExperienceRequired == 100,
            "The following Ready update uses the registered custom profession and its stored progression");
    }

    private static void CheckPendingLoot(Action<bool, string> check)
    {
        using var fixture = new Fixture(withDialogue: true, worldProfile: false, customProfession: true);
        fixture.Configuration.Tools.Single().BaitItemId = Guid.Empty;
        fixture.Configuration.Zones.Single().AutomaticFailureChance = 0;
        fixture.RunEvent(check, withDialogue: true);
        var ready = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Single(p => p.Open).StateJson)!;
        void Request(FishingAction action, long sequence, int input = 0) =>
            fixture.Player.HandleFishingRequest(new FishingRequestPacket
            { Action = action, SessionId = ready.SessionId, Sequence = sequence, Input = input });
        Request(FishingAction.Cast, 1);
        fixture.TickFishing(120_000);
        // Finish the real fight without relying on random reel resistance.
        typeof(Player).GetMethod("CompleteFishing", PrivateInstance)!.Invoke(fixture.Player, [Environment.TickCount64 + 120_000]);
        fixture.TickFishing(120_250);
        var retrieving = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(retrieving.Phase == FishingPhase.Retrieving,
            "A successful capture retrieves the hook before presenting loot");
        fixture.TickFishing(121_300);
        var loot = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(loot.Phase == FishingPhase.Loot && fixture.Player.GetVariable(FishingRules.LastRewardId) == null,
            "Manual capture waits for a loot decision without granting items or XP");
        Request(FishingAction.SetLootDestination, 2, 1);
        var denied = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(!denied.BankLoot && !denied.CanBankLoot, "Forged bank routing is denied without premium");
        Request(FishingAction.Cast, 3);
        var blocked = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(blocked.Phase == FishingPhase.Loot, "Casting cannot bypass unclaimed loot");
        Request(FishingAction.TakeLoot, 4); // This fixture deliberately has no persistable account.
        var retry = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(retry.Phase == FishingPhase.Loot && fixture.Player.GetVariable(FishingRules.LastRewardId) == null,
            "A failed checkpoint retains all pending loot for retry or discard");
        Request(FishingAction.DiscardLoot, 5);
        var discarded = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(discarded.Phase == FishingPhase.Ready && discarded.CatchItemId == Guid.Empty &&
            fixture.Player.GetVariable(FishingRules.LastRewardId) == null,
            "Discarding all loot grants no fish or XP and permits a new cast");
        Request(FishingAction.TakeLoot, 6);
        check(fixture.Player.GetVariable(FishingRules.LastRewardId) == null, "Discarded loot cannot be claimed later");
    }

    private static void CheckAutomaticFailure(Action<bool, string> check)
    {
        // Exercise the real Event and state machine with a guaranteed idle failure.
        // The lightweight Event fixture has no persisted User, so keep this no-bait:
        // bait persistence is exercised by the separate native reward tests.
        using var fixture = new Fixture(withDialogue: true, worldProfile: false, customProfession: true);
        fixture.Configuration.Tools.Single().BaitItemId = Guid.Empty;
        var zone = fixture.Configuration.Zones.Single();
        zone.AutomaticFailureChance = 1;
        zone.AutomaticExperienceMultiplier = .20;
        fixture.RunEvent(check, withDialogue: true);
        var ready = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Single(p => p.Open).StateJson)!;
        fixture.ClearPackets();
        fixture.Player.HandleFishingRequest(new FishingRequestPacket
        {
            Action = FishingAction.SetMode, SessionId = ready.SessionId, Automatic = true, Sequence = 1,
        });
        var waiting = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(waiting.Phase == FishingPhase.Waiting && waiting.Automatic,
            "Enabling automatic fishing casts from the native Event session");
        check(waiting.RemainingBait == 0,
            "The native Event test can enter automatic fishing without a bait item");
        fixture.TickFishing(120_000);
        var fighting = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(fighting.Phase == FishingPhase.Fighting && fighting.Automatic,
            "The automatic attempt reaches the fishing phase before the failure roll");
        fixture.ClearPackets();
        fixture.TickFishing(150_000);
        var failed = FishingState.FromJson(fixture.Packets.OfType<FishingStatePacket>().Last().StateJson)!;
        check(failed.Phase == FishingPhase.Failed && failed.Automatic && fixture.Player.IsFishingActive,
            "An automatic miss does not end the session or disable idle fishing");
        check(failed.ExperienceGained == 0 && failed.CatchesThisSession == 0 &&
            fixture.Player.Items.All(item => item.ItemId != fixture.RewardItemId || item.Quantity == 0) &&
            (fixture.Player.GetVariable(fixture.Configuration.ProfessionId)?.Value.Integer ?? 0) == 0,
            "A failed automatic attempt gives neither fish nor profession experience");
    }

    private static void CheckRejections(Action<bool, string> check)
    {
        foreach (var language in new[] { "en", "fr", "es", "unsupported" })
        {
            using var fixture = new Fixture(withDialogue: true, worldProfile: true, customProfession: true);
            fixture.Client.LanguageCode = language;
            fixture.Player.UnequipItem(fixture.Tool.EquipmentSlot);
            fixture.ClearPackets();
            fixture.RunEvent(check, withDialogue: true);
            var replies = fixture.Packets.OfType<FishingStatePacket>().ToArray();
            var messages = fixture.Packets.OfType<ChatMsgPacket>().Where(packet => packet.Type == ChatMessageType.Error).ToArray();
            var expected = string.Format(FishingText.Get(
                "Equip a configured fishing tool: {0}. Keeping it in your inventory is not enough.", language), fixture.Tool.Name);
            check(!fixture.Player.IsFishingActive && replies.Length == 1 && !replies[0].Open &&
                    messages.Length == 1 && messages[0].Message == expected && replies[0].Message.Length == 0,
                "An inventory-only tool returns one normal localized chat error naming Knife Wood (" + language + ")");
            check(fixture.Player.Items.Any(item => item.ItemId == fixture.Tool.Id && item.Quantity == 1),
                "Owning the configured net without equipping it cannot open the fishing session");
            var notice = JsonSerializer.Deserialize<FishingSoundCue>(replies[0].NoticeSoundJson, FishingConfiguration.JsonOptions);
            check(notice?.File == "fishing/fishing_ui_error.wav" && replies[0].StateJson.Length == 0,
                "The optional error sound packet carries no duplicate refusal message (" + language + ")");
        }
        using (var fixture = new Fixture(withDialogue: true, worldProfile: false))
        {
            fixture.Configuration.Zones[0].Enabled = false;
            fixture.RunEvent(check, withDialogue: true);
            var replies = fixture.Packets.OfType<FishingStatePacket>().ToArray();
            var messages = fixture.Packets.OfType<ChatMsgPacket>().Where(packet => packet.Type == ChatMessageType.Error).ToArray();
            check(!fixture.Player.IsFishingActive && replies.Length == 1 && !replies[0].Open &&
                    replies[0].Message.Length == 0 && messages.Length == 1 &&
                    messages[0].Message == FishingText.Get("Fishing is unavailable here.", "fr"),
                "A disabled zone returns its localized rejection through normal chat independently of fishing UI");
        }
        using (var fixture = new Fixture(withDialogue: true, worldProfile: false))
        {
            fixture.Player.OpenCraftingTableId = Guid.NewGuid();
            fixture.RunEvent(check, withDialogue: true);
            var replies = fixture.Packets.OfType<FishingStatePacket>().ToArray();
            var messages = fixture.Packets.OfType<ChatMsgPacket>().Where(packet => packet.Type == ChatMessageType.Error).ToArray();
            check(!fixture.Player.IsFishingActive && replies.Length == 1 && !replies[0].Open &&
                    replies[0].Message.Length == 0 && messages.Length == 1 &&
                    messages[0].Message == FishingText.Get("Finish your current activity before fishing.", "fr"),
                "A busy player receives a normal chat explanation without starting a fishing session");
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

    internal sealed class Fixture : IDisposable
    {
        private readonly ConcurrentQueue<Tuple<IPacket, TransmissionMode, long>> _queue = new();
        private readonly PacketHandler _handler;
        private readonly ItemDescriptor _bait, _fish;
        private readonly List<MapGrid> _grids;
        private readonly MapGrid _grid;
        private readonly object? _previousProfessions;
        private readonly string? _previousSharedProfessions;
        internal readonly MapController Map;
        internal readonly ItemDescriptor Tool;
        internal readonly Player Player;
        internal readonly Client Client;
        internal readonly GameEvent Event;
        internal readonly FishingConfiguration Configuration;
        internal IPacket[] Packets => _queue.Select(entry => entry.Item1).ToArray();
        internal Guid BaitItemId => _bait.Id;
        internal Guid RewardItemId => _fish.Id;

        internal Fixture(bool withDialogue, bool worldProfile, bool customProfession = false, IConnection? connection = null)
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
            Tool = new ItemDescriptor(Guid.NewGuid()) { Name = customProfession ? "Knife Wood" : "Fishing Event fixture net",
                ItemType = ItemType.Equipment, EquipmentSlot = customProfession ? Options.Instance.Equipment.WeaponSlot : 0 };
            _bait = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing Event fixture bait", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
            _fish = new ItemDescriptor(Guid.NewGuid()) { Name = "Fishing Event fixture fish", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
            foreach (var item in new[] { Tool, _bait, _fish }) ItemDescriptor.Lookup.Set(item.Id, item);
            var professionId = Guid.Empty;
            if (customProfession)
            {
                _previousProfessions = typeof(ProfessionConfigurationRuntime).GetField("_current", PrivateStatic)!.GetValue(null);
                _previousSharedProfessions = ProfessionConfiguration.Instance.ToJson();
                var profession = new ProfessionDefinition { Name = "Fishing", MaximumLevel = 12, BaseExperience = 100, ExperienceGrowth = 1 };
                ProfessionConfigurationRuntime.Save(new ProfessionConfiguration { Professions = [profession] }.ToJson());
                professionId = profession.Id;
            }
            var configuration = new FishingConfiguration
            {
                ProfessionId = professionId,
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
            Player.Items[customProfession ? 9 : 0].Set(new Item(Tool.Id, 1));
            if (!customProfession) Player.Equipment[0] = 0;
            Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client));
            Client.LanguageCode = "fr";
            Client.PacketSendingQueued = true;
            typeof(Client).GetField("mSendPacketQueue", PrivateInstance)!.SetValue(Client, _queue);
            typeof(Client).GetProperty(nameof(Client.Connection))!.SetValue(Client, connection ?? new TestConnection());
            Client.LoadCharacter(Player);
            _handler = (PacketHandler)RuntimeHelpers.GetUninitializedObject(typeof(PacketHandler));
            if (customProfession)
            {
                Player.Items[4].Set(new Item(_bait.Id, 17));
                // Joining the game sends the complete inventory and initializes its legal slots.
                PacketSender.SendInventory(Player);
                Player.EquipItem(Tool, 9);
                ClearPackets();
            }

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

        internal void TickFishing() => TickFishing(250);
        internal void TickFishing(long offsetMs) => UpdateFishing.Invoke(Player, [(long?)(Environment.TickCount64 + offsetMs)]);
        internal void ClearPackets() { while (_queue.TryDequeue(out _)) { } }

        public void Dispose()
        {
            Player.LeaveFishingFromEvent();
            Player.EventLookup.Clear();
            foreach (var item in new[] { Tool, _bait, _fish }) ItemDescriptor.Lookup.DeleteAt(item.Id);
            lock (_grids) _grids.Remove(_grid);
            MapController.Lookup.DeleteAt(Map.Id);
            if (_previousSharedProfessions != null)
            {
                var professionGate = typeof(ProfessionConfigurationRuntime).GetField("Gate", PrivateStatic)!.GetValue(null)!;
                lock (professionGate)
                    typeof(ProfessionConfigurationRuntime).GetField("_current", PrivateStatic)!.SetValue(null, _previousProfessions);
                ProfessionConfiguration.Load(_previousSharedProfessions);
            }
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
