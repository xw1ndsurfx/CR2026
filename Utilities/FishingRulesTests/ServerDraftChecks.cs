#if FISHING_SERVER_TESTS
using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Intersect;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.Professions;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Fishing;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using MessagePack;

internal static class ServerDraftChecks
{
    private const string ActivePath = "resources/fishing.json";
    private const string DraftPath = "resources/fishing.draft.json";

    internal static void Run(Action<bool, string> check)
    {
        Options.EnsureCreated();
        var currentField = typeof(FishingRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        var gate = typeof(FishingRuntime).GetField("Gate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var oldCurrent = currentField.GetValue(null);
        var oldDirectory = Directory.GetCurrentDirectory();
        var directory = Path.Combine(Path.GetTempPath(), "fishing-draft-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            lock (gate) { Directory.SetCurrentDirectory(directory); currentField.SetValue(null, null); }
            CheckProtocol(check);
            CheckPersistence(check, currentField, gate);
            CheckInvalidActiveReload(check, currentField, gate);
            CheckActivationAndDiagnostics(check, currentField, gate);
        }
        finally
        {
            lock (gate) { Directory.SetCurrentDirectory(oldDirectory); currentField.SetValue(null, oldCurrent); }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckActivationAndDiagnostics(Action<bool, string> check, FieldInfo currentField, object gate)
    {
        var map = new MapController(Guid.NewGuid()) { Name = "Activation fixture map" };
        GC.SuppressFinalize(map);
        var tool = new ItemDescriptor(Guid.NewGuid()) { Name = "Activation fixture net", ItemType = ItemType.Equipment, EquipmentSlot = 0 };
        var bait = new ItemDescriptor(Guid.NewGuid()) { Name = "Activation fixture bait", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
        var normal = new ItemDescriptor(Guid.NewGuid()) { Name = "Activation fixture fish", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
        var legendary = new ItemDescriptor(Guid.NewGuid()) { Name = "Activation fixture legendary", ItemType = ItemType.Currency, MaxInventoryStack = 100 };
        var nonStackable = new ItemDescriptor(Guid.NewGuid()) { Name = "Activation fixture non-stackable", ItemType = ItemType.Consumable, Stackable = false };
        var items = new[] { tool, bait, normal, legendary, nonStackable };
        var animations = Enumerable.Range(0, 3).Select(index => new AnimationDescriptor(Guid.NewGuid()) { Name = "Activation fixture animation " + index }).ToArray();
        var variable = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Activation fixture condition" };
        var profession = new ProfessionDefinition { Name = "Activation fixture profession" };
        var professionField = typeof(ProfessionConfigurationRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        var professionGate = typeof(ProfessionConfigurationRuntime).GetField("Gate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var previousProfessions = professionField.GetValue(null);
        var professions = new ProfessionConfiguration { Professions = [profession] };
        NativeProfessions.Include(professions);
        try
        {
            check(Options.Instance.Equipment.Slots.Count > 0, "The activation fixture has a real configured equipment slot");
            MapController.Lookup.Set(map.Id, map);
            foreach (var item in items) ItemDescriptor.Lookup.Set(item.Id, item);
            foreach (var animation in animations) AnimationDescriptor.Lookup.Set(animation.Id, animation);
            PlayerVariableDescriptor.Lookup.Set(variable.Id, variable);
            lock (professionGate) professionField.SetValue(null, professions);

            var inactive = new FishingConfiguration
            {
                Tools = [new() { ItemId = tool.Id, Type = FishingToolType.Net, BaitItemId = bait.Id,
                    CastAnimationId = animations[0].Id, UseAnimationId = animations[1].Id, CatchAnimationId = animations[2].Id }],
                Zones = [new() { Name = "Activation fixture lagoon", Enabled = false, MapIds = [map.Id], UseTileRadius = false,
                    Catches = [new() { ItemId = normal.Id, ToolItemIds = [tool.Id] }] }],
            };
            FishingRuntime.Save(inactive.ToJson());
            var activeReference = FishingRuntime.Current;
            var activeBytes = File.ReadAllBytes(ActivePath);
            var activation = FishingConfiguration.FromJson(inactive.ToJson());
            activation.ProfessionId = profession.Id;
            activation.Zones[0].Enabled = true;
            activation.Tools[0].WorldProfile = FishingWorldProfile.Net();
            activation.Zones[0].Catches.Add(new() { ItemId = legendary.Id, ToolItemIds = [tool.Id], Legendary = true, RequiredVariableId = variable.Id });
            var toolReference = $"{tool.Name} ({tool.Id})";
            var zoneReference = $"{activation.Zones[0].Name} ({activation.Zones[0].Id})";
            var catchReference = $"#2 ({activation.Zones[0].Catches[1].Id})";
            var unknown = Guid.NewGuid();

            void Reject(Action<FishingConfiguration> change, string source, params object[] arguments)
            {
                var candidate = FishingConfiguration.FromJson(activation.ToJson());
                change(candidate);
                var savedDraft = Request(FishingAction.SaveDraftConfiguration, candidate.ToJson())!;
                check(savedDraft is { Draft: true, Saved: true, ConfigurationAction: FishingAction.SaveDraftConfiguration } && FishingConfiguration.FromJson(savedDraft.ConfigurationJson).Zones[0].Enabled,
                    "The rejected activation remains a saved enabled draft: " + source);
                var draftBytes = File.ReadAllBytes(DraftPath);
                var english = string.Format(CultureInfo.InvariantCulture, source, arguments);
                foreach (var language in new[] { "en", "fr", "es", "unsupported" })
                {
                    var reply = Request(FishingAction.SaveConfiguration, candidate.ToJson(), language: language)!;
                    var expected = string.Format(CultureInfo.InvariantCulture, FishingText.Get(source, language), arguments);
                    check(reply is { Editor: true, Draft: false, Saved: false, ConfigurationAction: FishingAction.SaveConfiguration } && reply.ConfigurationJson.Length == 0 && reply.Message == expected &&
                        (language is not ("fr" or "es") || expected != english),
                        "The real handler returns the exact localized resource diagnostic (" + language + "): " + source);
                    check(ReferenceEquals(activeReference, FishingRuntime.Current) && !FishingRuntime.Current.Zones[0].Enabled &&
                        FishingRuntime.ReadDraft().Zones[0].Enabled && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes) &&
                        File.ReadAllBytes(DraftPath).SequenceEqual(draftBytes),
                        "Rejected activation preserves the disabled active state and enabled draft (" + language + ")");
                }
            }
            void ReplaceTool(FishingConfiguration config, Guid itemId)
            {
                config.Tools[0].ItemId = itemId;
                foreach (var entry in config.Zones[0].Catches) entry.ToolItemIds = [itemId];
            }

            Reject(config => config.ProfessionId = unknown,
                "Fishing profession {0} does not exist on the server. Save it in the Profession Editor or select automatic detection / built-in progression.", unknown);
            Reject(config => config.ProfessionId = NavalProfessions.Sailing,
                "Native profession {0} cannot be used for fishing. Select a custom profession or automatic detection / built-in progression.", NavalProfessions.Sailing);
            Reject(config => ReplaceTool(config, unknown),
                "Fishing tool #{0}: equipment item {1} does not exist on the server. Select an existing equipment item.", 1, unknown);
            Reject(config => ReplaceTool(config, nonStackable.Id),
                "Fishing tool #{0}: item {1} is not equipment. Choose an equipment item.", 1, $"{nonStackable.Name} ({nonStackable.Id})");
            tool.EquipmentSlot = -1;
            try
            {
                Reject(_ => { },
                    "Fishing tool #{0}: item {1} uses equipment slot {2}, but the server defines {3} slots. Correct its slot in the Item Editor.",
                    1, toolReference, -1, Options.Instance.Equipment.Slots.Count);
            }
            finally { tool.EquipmentSlot = 0; }
            Reject(config => config.Tools[0].BaitItemId = unknown,
                "Fishing tool #{0} ({1}): bait item {2} does not exist on the server.", 1, toolReference, unknown);
            Reject(config => config.Tools[0].BaitItemId = nonStackable.Id,
                "Fishing tool #{0} ({1}): bait item {2} must be stackable. Correct the item or choose another bait.",
                1, toolReference, $"{nonStackable.Name} ({nonStackable.Id})");
            Reject(config => config.Tools[0].CastAnimationId = unknown,
                "Fishing tool #{0} ({1}): cast animation {2} does not exist on the server.", 1, toolReference, unknown);
            Reject(config => config.Tools[0].UseAnimationId = unknown,
                "Fishing tool #{0} ({1}): use animation {2} does not exist on the server.", 1, toolReference, unknown);
            Reject(config => config.Tools[0].CatchAnimationId = unknown,
                "Fishing tool #{0} ({1}): catch animation {2} does not exist on the server.", 1, toolReference, unknown);
            Reject(config => config.Zones[0].MapIds.Add(unknown),
                "Fishing zone {0}: map {1} does not exist on the server. Remove or replace that map selection.", zoneReference, unknown);
            Reject(config => { config.Zones[0].UseTileRadius = true; config.Zones[0].X = Options.Instance.Map.MapWidth; },
                "Fishing zone {0}: tile center ({1}, {2}) is outside the server map dimensions ({3} by {4}). Correct the coordinates or use whole maps.",
                zoneReference, Options.Instance.Map.MapWidth, 0, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
            Reject(config => config.Zones[0].Catches[1].ItemId = unknown,
                "Fishing zone {0}, catch {1}: reward item {2} does not exist on the server.", zoneReference, catchReference, unknown);
            Reject(config => config.Zones[0].Catches[1].BonusLoot = [new() { ItemId = unknown }],
                "Fishing zone {0}, catch {1}: bonus loot item {2} is missing or has an invalid inventory stack.",
                zoneReference, catchReference, unknown);
            legendary.MaxInventoryStack = 0;
            try
            {
                Reject(_ => { },
                    "Fishing zone {0}, catch {1}: reward item {2} has maximum inventory stack {3}. Set a value greater than zero.",
                    zoneReference, catchReference, $"{legendary.Name} ({legendary.Id})", 0);
            }
            finally { legendary.MaxInventoryStack = 100; }
            Reject(config => config.Zones[0].Catches[1].RequiredVariableId = unknown,
                "Fishing zone {0}, catch {1}: required player variable {2} does not exist on the server.", zoneReference, catchReference, unknown);

            var ordinaryFish = FishingConfiguration.FromJson(activation.ToJson());
            ordinaryFish.Zones[0].Catches[1].ItemId = nonStackable.Id;
            ordinaryFish.Zones[0].Catches[1].BonusLoot = [new() { ItemId = normal.Id, Chance = 1 }, new() { ItemId = nonStackable.Id, Chance = .05 }];
            check(Request(FishingAction.SaveConfiguration, ordinaryFish.ToJson())!.Saved,
                "Non-stackable fish and rare equipment bonuses can be activated through the server");

            var applied = Request(FishingAction.SaveConfiguration, activation.ToJson())!;
            var appliedConfig = FishingConfiguration.FromJson(applied.ConfigurationJson);
            check(applied is { Editor: true, Draft: false, Saved: true, ConfigurationAction: FishingAction.SaveConfiguration } && appliedConfig.Zones.Single().Enabled &&
                appliedConfig.Zones[0].Id == inactive.Zones[0].Id && appliedConfig.ProfessionId == profession.Id &&
                appliedConfig.Tools[0].WorldProfile.Enabled && appliedConfig.Zones[0].Catches.Count == 2,
                "Real registered resources activate the same zone with a custom profession, net profile and legendary catch");
            check(FishingRuntime.Current.Zones[0].Enabled && FishingRuntime.ReadDraft().Zones[0].Enabled &&
                FishingConfiguration.FromJson(File.ReadAllText(ActivePath)).Zones[0].Enabled &&
                FishingConfiguration.FromJson(File.ReadAllText(DraftPath)).Zones[0].Enabled,
                "A successful activation persists Enabled=true in both files and the active runtime");
            var eventReply = Request(FishingAction.GetConfiguration)!;
            check(eventReply is { Editor: true, Draft: false, Saved: false, ConfigurationAction: FishingAction.GetConfiguration } &&
                FishingConfiguration.FromJson(eventReply.ConfigurationJson).Zones.Single(zone => zone.Id == inactive.Zones[0].Id).Enabled,
                "The Event endpoint returns Enabled=true for the same activated zone identity");
            lock (gate) currentField.SetValue(null, null);
            check(FishingRuntime.Current.Zones[0].Enabled && FishingRuntime.Current.ProfessionId == profession.Id,
                "The activated zone remains enabled after a real active-file reload");
        }
        finally
        {
            lock (professionGate) professionField.SetValue(null, previousProfessions);
            foreach (var item in items) ItemDescriptor.Lookup.DeleteAt(item.Id);
            foreach (var animation in animations) AnimationDescriptor.Lookup.DeleteAt(animation.Id);
            PlayerVariableDescriptor.Lookup.DeleteAt(variable.Id);
            MapController.Lookup.Delete(map);
        }
    }

    private static void CheckInvalidActiveReload(Action<bool, string> check, FieldInfo currentField, object gate)
    {
        var previousBytes = File.ReadAllBytes(ActivePath);
        var previousCache = currentField.GetValue(null);
        var invalid = Encoding.UTF8.GetBytes("{\"Zones\":null}");
        try
        {
            File.WriteAllBytes(ActivePath, invalid);
            lock (gate) currentField.SetValue(null, null);
            var first = FishingRuntime.Current;
            var second = FishingRuntime.Current;
            FishingRules.Validate(first);
            FishingRules.Validate(second);
            check(ReferenceEquals(first, second) && first.Zones.Count == 0 && first.Tools.Count == 0,
                "Two accesses after an invalid active reload return the same validated disabled fallback, never the rejected configuration");
            check(File.ReadAllBytes(ActivePath).SequenceEqual(invalid),
                "Loading a structurally invalid active configuration preserves its original bytes for recovery");
        }
        finally
        {
            File.WriteAllBytes(ActivePath, previousBytes);
            lock (gate) currentField.SetValue(null, previousCache);
        }
    }

    private static void CheckPersistence(Action<bool, string> check, FieldInfo currentField, object gate)
    {
        var active = new FishingConfiguration
        {
            Zones = [new() { Name = "Applied lagoon", Enabled = false, MapIds = [], UseTileRadius = false }],
        };
        var activeJson = FishingRuntime.Save(active.ToJson());
        var activeReference = FishingRuntime.Current;
        var activeBytes = File.ReadAllBytes(ActivePath);
        check(File.ReadAllText(DraftPath) == activeJson, "Applying fishing initializes the matching recoverable draft");
        File.Delete(DraftPath);
        var fallback = Request(FishingAction.GetDraftConfiguration)!;
        check(fallback is { Editor: true, Draft: true, Saved: false, ConfigurationAction: FishingAction.GetDraftConfiguration } && fallback.ConfigurationJson == activeJson && !File.Exists(DraftPath),
            "The first draft request falls back to the active configuration without creating or modifying a file");
        var detached = FishingRuntime.ReadDraft();
        detached.Zones[0].Name = "Local unsaved edit";
        check(ReferenceEquals(activeReference, FishingRuntime.Current) && FishingRuntime.Current.Zones[0].Name == "Applied lagoon",
            "A draft fallback never exposes the mutable active instance");

        var draft = new FishingConfiguration
        {
            ProfessionId = Guid.NewGuid(),
            Tools = [new(), new() { Type = FishingToolType.Net }],
            Zones = [new() { Name = "Unfinished lagoon", Enabled = true, MapIds = [], UseTileRadius = false,
                Catches = [new() { RequiredVariableId = Guid.NewGuid(), SpriteX = 64 }] }],
        };
        var saved = Request(FishingAction.SaveDraftConfiguration, draft.ToJson())!;
        check(saved is { Editor: true, Draft: true, Saved: true, ConfigurationAction: FishingAction.SaveDraftConfiguration } && saved.ConfigurationJson == File.ReadAllText(DraftPath),
            "The draft acknowledgement identifies the exact independently persisted snapshot");
        var draftJson = saved.ConfigurationJson;
        var loaded = FishingRuntime.ReadDraft();
        check(loaded.Tools.Count == 2 && loaded.Tools.All(tool => tool.ItemId == Guid.Empty) &&
            loaded.Zones[0].Enabled && loaded.Zones[0].MapIds.Count == 0 && loaded.Zones[0].Catches[0].ItemId == Guid.Empty &&
            loaded.Zones[0].Catches[0].ToolItemIds.Count == 0 && loaded.Zones[0].Catches[0].SpriteX == 64 &&
            loaded.ProfessionId == draft.ProfessionId && loaded.Zones[0].Catches[0].RequiredVariableId == draft.Zones[0].Catches[0].RequiredVariableId,
            "Incomplete tool, map and catch choices and unresolved authoring references survive a draft reload");
        check(ReferenceEquals(activeReference, FishingRuntime.Current) && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes),
            "Saving a draft changes neither the active cache instance nor the applied file");
        var activeReply = Request(FishingAction.GetConfiguration)!;
        check(activeReply is { Draft: false, Saved: false, ConfigurationAction: FishingAction.GetConfiguration } && activeReply.ConfigurationJson == activeJson,
            "The active configuration endpoint continues to return the applied data for Event pickers");

        lock (gate) currentField.SetValue(null, null);
        check(FishingRuntime.ReadDraft().ToJson() == draftJson && FishingRuntime.Current.ToJson() == activeJson,
            "Restarting the active cache reloads applied data independently of the saved incomplete draft");
        activeReference = FishingRuntime.Current;
        var rejected = Request(FishingAction.SaveConfiguration, draftJson)!;
        check(rejected is { Editor: true, Draft: false, Saved: false, ConfigurationAction: FishingAction.SaveConfiguration } && rejected.Message.Length > 0 &&
            File.ReadAllText(DraftPath) == draftJson && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes) &&
            ReferenceEquals(activeReference, FishingRuntime.Current),
            "Strict application rejects an incomplete draft before writing either file or changing the active cache");

        foreach (var action in new[] { FishingAction.GetConfiguration, FishingAction.SaveConfiguration,
            FishingAction.GetDraftConfiguration, FishingAction.SaveDraftConfiguration })
            check(Request(action, new FishingConfiguration().ToJson(), editor: false) is null,
                "Player clients cannot request or write fishing editor configuration: " + action);
        check(File.ReadAllText(DraftPath) == draftJson && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes) &&
            ReferenceEquals(activeReference, FishingRuntime.Current), "Rejected player requests preserve both stores and the active cache");

        var oversizedUtf8 = "{\"ignored\":\"" + new string('é', FishingRuntime.MaximumConfigurationBytes / 2) + "\"}";
        check(oversizedUtf8.Length < FishingRuntime.MaximumConfigurationBytes, "The size fixture exercises UTF-8 bytes rather than character count");
        var tooLarge = Request(FishingAction.SaveDraftConfiguration, oversizedUtf8)!;
        var invalidStructure = Request(FishingAction.SaveDraftConfiguration, "{\"Zones\":null}")!;
        check(tooLarge is { Draft: true, Saved: false, ConfigurationAction: FishingAction.SaveDraftConfiguration } &&
            invalidStructure is { Draft: true, Saved: false, ConfigurationAction: FishingAction.SaveDraftConfiguration } &&
            File.ReadAllText(DraftPath) == draftJson && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes),
            "Oversized or structurally invalid drafts are refused without overwriting saved work");

        var corrupt = Encoding.UTF8.GetBytes("{\"Zones\": [ invalid saved content");
        File.WriteAllBytes(DraftPath, corrupt);
        var corruptReply = Request(FishingAction.GetDraftConfiguration)!;
        check(corruptReply is { Editor: true, Draft: true, Saved: false, ConfigurationAction: FishingAction.GetDraftConfiguration } && corruptReply.ConfigurationJson.Length == 0 &&
            corruptReply.Message == FishingText.Get(FishingRuntime.InvalidDraftMessage, "en"),
            "A corrupt draft is explicitly reported as a draft read failure, never as a successful fallback");
        check(File.ReadAllBytes(DraftPath).SequenceEqual(corrupt) && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes) &&
            ReferenceEquals(activeReference, FishingRuntime.Current), "Reading a corrupt draft preserves its bytes and all applied state");
        check(Request(FishingAction.GetConfiguration)!.ConfigurationJson == activeJson,
            "A corrupt authoring draft cannot prevent the active configuration endpoint from serving Event data");
        FishingRuntime.SaveDraft(draftJson);

        var complete = new FishingConfiguration
        {
            Zones = [new() { Id = draft.Zones[0].Id, Name = "Completed lagoon", Enabled = false, MapIds = [], UseTileRadius = false }],
        };
        var applied = Request(FishingAction.SaveConfiguration, complete.ToJson())!;
        check(applied is { Editor: true, Draft: false, Saved: true, ConfigurationAction: FishingAction.SaveConfiguration } && applied.ConfigurationJson == File.ReadAllText(ActivePath) &&
            applied.ConfigurationJson == File.ReadAllText(DraftPath) && FishingRuntime.Current.ToJson() == applied.ConfigurationJson,
            "A successful strict application synchronizes active data and the recoverable draft before acknowledging success");
        CheckWriteFailures(check, complete);
    }

    private static void CheckWriteFailures(Action<bool, string> check, FishingConfiguration complete)
    {
        var prior = FishingRuntime.Current;
        var activeBytes = File.ReadAllBytes(ActivePath);
        var draftBytes = File.ReadAllBytes(DraftPath);
        var next = FishingConfiguration.FromJson(complete.ToJson());
        next.Zones[0].Name = "Next applied lagoon";
        File.Delete(DraftPath);
        Directory.CreateDirectory(DraftPath);
        try
        {
            var failed = Request(FishingAction.SaveConfiguration, next.ToJson())!;
            check(failed is { Draft: false, Saved: false, ConfigurationAction: FishingAction.SaveConfiguration } && failed.Message.Length > 0 &&
                File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes) && ReferenceEquals(prior, FishingRuntime.Current),
                "A draft write failure prevents the active write and never acknowledges an application");
        }
        finally { Directory.Delete(DraftPath); File.WriteAllBytes(DraftPath, draftBytes); }

        // A directory at the destination reliably blocks rename on Windows and Linux.
        // Keep the previous active file separately so its bytes can also be checked.
        var backup = ActivePath + ".previous";
        File.Move(ActivePath, backup);
        Directory.CreateDirectory(ActivePath);
        try
        {
            var failed = Request(FishingAction.SaveConfiguration, next.ToJson())!;
            check(failed is { Draft: false, Saved: false, ConfigurationAction: FishingAction.SaveConfiguration } && failed.Message.Length > 0 &&
                ReferenceEquals(prior, FishingRuntime.Current) && File.ReadAllBytes(backup).SequenceEqual(activeBytes),
                "An active write failure preserves the previously loaded active configuration and cannot produce Saved=true");
            check(FishingRuntime.ReadDraft().ToJson() == next.ToJson(),
                "The draft is durably written first and remains recoverable when applying to the active destination fails");
            check(Directory.GetFiles("resources", "*.tmp").Length == 0, "Failed atomic writes remove their temporary files");
        }
        finally { Directory.Delete(ActivePath); File.Move(backup, ActivePath); }
        check(File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes), "The failed application did not alter the previous active file");
    }

    private static void CheckProtocol(Action<bool, string> check)
    {
        var historical = new[] { FishingAction.GetConfiguration, FishingAction.SaveConfiguration, FishingAction.Cast,
            FishingAction.SetMode, FishingAction.SetReeling, FishingAction.AcceptLegendary, FishingAction.ChallengeInput,
            FishingAction.Leave, FishingAction.UseSkill };
        check(historical.Select(action => (int)action).SequenceEqual(Enumerable.Range(0, historical.Length)) &&
            (int)FishingAction.GetDraftConfiguration == 9 && (int)FishingAction.SaveDraftConfiguration == 10,
            "Draft actions are appended without changing any historical action value");
        var request = new FishingRequestPacket { Action = FishingAction.SaveDraftConfiguration, ConfigurationJson = "{}" };
        var requestCopy = MessagePackSerializer.Deserialize<FishingRequestPacket>(MessagePackSerializer.Serialize(request));
        var response = new FishingStatePacket { Editor = true, Draft = true, Saved = true, ConfigurationJson = "{}", ConfigurationAction = request.Action };
        var responseCopy = MessagePackSerializer.Deserialize<FishingStatePacket>(MessagePackSerializer.Serialize(response));
        check(requestCopy.Action == request.Action && requestCopy.ConfigurationJson == "{}" &&
            responseCopy is { Editor: true, Draft: true, Saved: true, ConfigurationAction: FishingAction.SaveDraftConfiguration } && responseCopy.ConfigurationJson == "{}",
            "Native MessagePack preserves the draft request, acknowledgement discriminator and echoed action");
        foreach (var action in new[] { FishingAction.GetConfiguration, FishingAction.SaveConfiguration,
            FishingAction.GetDraftConfiguration, FishingAction.SaveDraftConfiguration })
        {
            var error = new FishingStatePacket { Editor = true, ConfigurationAction = action, Message = "Fixture configuration failure" };
            var copy = MessagePackSerializer.Deserialize<FishingStatePacket>(MessagePackSerializer.Serialize(error));
            check(copy.ConfigurationAction == action && !copy.Saved && copy.Message == error.Message,
                "An error reply preserves its exact configuration action, including zero-valued GetConfiguration: " + action);
        }

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(7);
        writer.Write(""); writer.Write("{}"); writer.Write("");
        writer.Write(true); writer.Write(true); writer.Write(false); writer.Write(""); writer.Flush();
        var oldResponse = MessagePackSerializer.Deserialize<FishingStatePacket>(buffer.WrittenMemory);
        check(oldResponse is { Editor: true, Open: true, Draft: false, ConfigurationAction: null } && oldResponse.ConfigurationJson == "{}",
            "A historical seven-field response remains active with an absent configuration action");

        var previousBuffer = new ArrayBufferWriter<byte>();
        var previousWriter = new MessagePackWriter(previousBuffer);
        previousWriter.WriteArrayHeader(8);
        previousWriter.Write("legacy-state"); previousWriter.Write("{}"); previousWriter.Write("legacy-message");
        previousWriter.Write(true); previousWriter.Write(true); previousWriter.Write(false);
        previousWriter.Write("legacy-sound"); previousWriter.Write(true); previousWriter.Flush();
        var previousResponse = MessagePackSerializer.Deserialize<FishingStatePacket>(previousBuffer.WrittenMemory);
        check(previousResponse is { StateJson: "legacy-state", ConfigurationJson: "{}", Message: "legacy-message", Open: true,
                Editor: true, Saved: false, NoticeSoundJson: "legacy-sound", Draft: true, ConfigurationAction: null },
            "An eight-field response preserves keys zero through seven and defaults the absent action to null");
    }

    private static FishingStatePacket? Request(FishingAction action, string json = "", bool editor = true, string language = "en")
    {
        var queue = new ConcurrentQueue<Tuple<IPacket, TransmissionMode, long>>();
        var client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client));
        client.IsEditor = editor; client.LanguageCode = language; client.PacketSendingQueued = true;
        typeof(Client).GetField("mSendPacketQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, queue);
        typeof(Client).GetProperty(nameof(Client.Connection))!.SetValue(client, new TestConnection());
        var handler = (Intersect.Server.Networking.PacketHandler)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Server.Networking.PacketHandler));
        handler.HandlePacket(client, new FishingRequestPacket { Action = action, ConfigurationJson = json });
        return queue.Count == 0 ? null : (FishingStatePacket)queue.Single().Item1;
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
