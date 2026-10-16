using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Extensions;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.Localization;

public static class ContentTranslationRuntime
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine("resources", "languages");
    private static readonly string SettingsFile = Path.Combine("config", "translation-service.private.json");
    private static readonly string CatalogFile = Path.Combine(Folder, "content.json");
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly ConcurrentQueue<string> Pending = new();
    private static readonly ConcurrentDictionary<string, byte> Queued = new(StringComparer.Ordinal);
    private static int _running;
    private static bool _serviceFailed;
    private static readonly Lazy<bool> Loaded = new(() =>
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (File.Exists(SettingsFile)) Settings = JsonConvert.DeserializeObject<Intersect.Localization.TranslationServiceSettings>(File.ReadAllText(SettingsFile)) ?? new();
            if (File.Exists(CatalogFile)) Catalog = Intersect.Localization.GameTextCatalog.FromJson(File.ReadAllText(CatalogFile));
        }
        catch { _status = "Translation files could not be loaded. Source text is preserved."; }
        return true;
    });
    private static Intersect.Localization.TranslationServiceSettings Settings = new();
    private static Intersect.Localization.GameTextCatalog Catalog = new();
    private static string _status = "Not configured. Source text is used until translations are available.";
    public static string Translate(string text, string language)
    { _ = Loaded.Value; lock (Gate) return Catalog.Get(text, language); }
    public static string Snapshot()
    { _ = Loaded.Value; lock (Gate) return Catalog.ToJson(); }
    public static void SendTo(Client client) => client.Send(new ContentTranslationsPacket { CatalogJson = Snapshot(), Replace = true });
    public static void QueueJson(string json)
    {
        _ = Loaded.Value;
        lock (Gate) if (!Settings.Enabled || _serviceFailed) return;
        foreach (var text in Intersect.Localization.GameTextCatalog.Extract(json))
        {
            var key = Intersect.Localization.GameTextCatalog.NormalizeText(text);
            lock (Gate)
                if (Catalog.Entries.TryGetValue(key, out var entry) && Intersect.Localization.GameTextCatalog.Languages.All(entry.ContainsKey)) continue;
            if (Queued.TryAdd(key, 0)) Pending.Enqueue(key);
        }
        StartWorker();
    }
    private static void StartWorker()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        _ = Task.Run(async () =>
        {
            var changes = new Intersect.Localization.GameTextCatalog();
            try
            {
                while (Pending.TryDequeue(out var source))
                {
                    try
                    {
                        Intersect.Localization.TranslationServiceSettings settings;
                        lock (Gate) settings = JsonConvert.DeserializeObject<Intersect.Localization.TranslationServiceSettings>(JsonConvert.SerializeObject(Settings))!;
                        if (!settings.Enabled) continue;
                        var versions = await Intersect.Localization.MachineTextTranslator.TranslateAsync(source, settings, Http, CancellationToken.None);
                        lock (Gate)
                        {
                            Catalog.Entries[source] = versions; changes.Entries[source] = versions;
                            Save(CatalogFile, Catalog.ToJson()); _status = $"Translated {Catalog.Entries.Count} text(s).";
                        }
                    }
                    catch (Exception exception)
                    {
                        lock (Gate) { _serviceFailed = true; _status = "Translation unavailable; source text preserved. Check the service and retry Translate existing content. " + exception.GetType().Name; }
                        // Stop a failing batch instead of hammering an unavailable or rate-limited provider.
                        while (Pending.TryDequeue(out var abandoned)) Queued.TryRemove(abandoned, out _);
                        break;
                    }
                    finally { Queued.TryRemove(source, out _); }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
                if (changes.Entries.Count > 0)
                    foreach (var player in Player.OnlinePlayersSnapshot()) player?.SendPacket(new ContentTranslationsPacket { CatalogJson = changes.ToJson() });
                if (!Pending.IsEmpty) StartWorker();
            }
        });
    }
    public static void HandleEditor(Client client, Intersect.Network.Packets.Editor.TranslationSettingsRequestPacket packet)
    {
        if (!client.IsEditor) return;
        _ = Loaded.Value;
        try
        {
            if (packet.Action == "queue") { QueueJson(packet.CatalogJson); return; }
            if (packet.Action == "save")
            {
                var settings = JsonConvert.DeserializeObject<Intersect.Localization.TranslationServiceSettings>(packet.SettingsJson) ?? throw new InvalidDataException();
                settings.Validate();
                lock (Gate) { Settings = settings; _serviceFailed = false; Save(SettingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented)); _status = "Configuration saved."; }
            }
            else if (packet.Action == "save-texts")
            {
                var changes = Intersect.Localization.GameTextCatalog.FromJson(packet.CatalogJson);
                lock (Gate) { Catalog.Merge(changes); Save(CatalogFile, Catalog.ToJson()); _status = "Corrections saved."; }
                foreach (var player in Player.OnlinePlayersSnapshot()) player?.SendPacket(new ContentTranslationsPacket { CatalogJson = changes.ToJson() });
            }
            else if (packet.Action == "translate-all")
            {
                lock (Gate)
                {
                    if (!Settings.Enabled)
                    {
                        client.Send(new TranslationSettingsPacket { SettingsJson = JsonConvert.SerializeObject(Settings), CatalogJson = Catalog.ToJson(), Status = "Enable and save a translation service before translating existing content." });
                        return;
                    }
                }
                lock (Gate) _serviceFailed = false;
                foreach (var property in typeof(Strings).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    var group = property.GetValue(null); if (group == null) continue;
                    foreach (var field in group.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        if (field.GetValue(group) is Intersect.Localization.LocalizedString text)
                            QueueJson(JsonConvert.SerializeObject(new { Text = text.ToString() }));
                }
                foreach (var type in Enum.GetValues<GameObjectType>().Where(Intersect.Localization.GameTextCatalog.IsDisplayObject))
                    foreach (var value in type.GetLookup().Values) QueueJson(value.JsonData);
                lock (Gate) if (!_serviceFailed) _status = $"Queued {Queued.Count} text(s). Refresh to see progress.";
            }
        }
        catch (Exception exception) { lock (Gate) _status = "Could not update translations: " + exception.GetType().Name; }
        lock (Gate) client.Send(new TranslationSettingsPacket { SettingsJson = JsonConvert.SerializeObject(Settings), CatalogJson = Catalog.ToJson(), Status = _status });
    }
    private static void Save(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json); File.Move(temporary, path, true);
    }
}
