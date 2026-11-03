#if FISHING_SERVER_TESTS
using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Intersect;
using Intersect.Framework.Core.Fishing;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Fishing;
using Intersect.Server.Networking;
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
        }
        finally
        {
            lock (gate) { Directory.SetCurrentDirectory(oldDirectory); currentField.SetValue(null, oldCurrent); }
            Directory.Delete(directory, recursive: true);
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
        check(fallback is { Editor: true, Draft: true, Saved: false } && fallback.ConfigurationJson == activeJson && !File.Exists(DraftPath),
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
        check(saved is { Editor: true, Draft: true, Saved: true } && saved.ConfigurationJson == File.ReadAllText(DraftPath),
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
        check(activeReply is { Draft: false, Saved: false } && activeReply.ConfigurationJson == activeJson,
            "The active configuration endpoint continues to return the applied data for Event pickers");

        lock (gate) currentField.SetValue(null, null);
        check(FishingRuntime.ReadDraft().ToJson() == draftJson && FishingRuntime.Current.ToJson() == activeJson,
            "Restarting the active cache reloads applied data independently of the saved incomplete draft");
        activeReference = FishingRuntime.Current;
        var rejected = Request(FishingAction.SaveConfiguration, draftJson)!;
        check(rejected is { Editor: true, Draft: false, Saved: false } && rejected.Message.Length > 0 &&
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
        check(tooLarge is { Draft: true, Saved: false } && invalidStructure is { Draft: true, Saved: false } &&
            File.ReadAllText(DraftPath) == draftJson && File.ReadAllBytes(ActivePath).SequenceEqual(activeBytes),
            "Oversized or structurally invalid drafts are refused without overwriting saved work");

        var corrupt = Encoding.UTF8.GetBytes("{\"Zones\": [ invalid saved content");
        File.WriteAllBytes(DraftPath, corrupt);
        var corruptReply = Request(FishingAction.GetDraftConfiguration)!;
        check(corruptReply is { Editor: true, Draft: true, Saved: false } && corruptReply.ConfigurationJson.Length == 0 &&
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
        check(applied is { Editor: true, Draft: false, Saved: true } && applied.ConfigurationJson == File.ReadAllText(ActivePath) &&
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
            check(failed is { Draft: false, Saved: false } && failed.Message.Length > 0 &&
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
            check(failed is { Draft: false, Saved: false } && failed.Message.Length > 0 &&
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
        var response = new FishingStatePacket { Editor = true, Draft = true, Saved = true, ConfigurationJson = "{}" };
        var responseCopy = MessagePackSerializer.Deserialize<FishingStatePacket>(MessagePackSerializer.Serialize(response));
        check(requestCopy.Action == request.Action && requestCopy.ConfigurationJson == "{}" &&
            responseCopy is { Editor: true, Draft: true, Saved: true } && responseCopy.ConfigurationJson == "{}",
            "Native MessagePack preserves the new draft request and acknowledgement discriminator");

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(7);
        writer.Write(""); writer.Write("{}"); writer.Write("");
        writer.Write(true); writer.Write(true); writer.Write(false); writer.Write(""); writer.Flush();
        var oldResponse = MessagePackSerializer.Deserialize<FishingStatePacket>(buffer.WrittenMemory);
        check(oldResponse is { Editor: true, Open: true, Draft: false } && oldResponse.ConfigurationJson == "{}",
            "A historical seven-field response remains an active configuration response");
    }

    private static FishingStatePacket? Request(FishingAction action, string json = "", bool editor = true)
    {
        var queue = new ConcurrentQueue<Tuple<IPacket, TransmissionMode, long>>();
        var client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client));
        client.IsEditor = editor; client.LanguageCode = "en"; client.PacketSendingQueued = true;
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
