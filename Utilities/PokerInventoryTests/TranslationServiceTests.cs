using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Intersect.Localization;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Networking;
using Intersect.Server.Localization;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class TranslationServiceTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunAsync().GetAwaiter().GetResult(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static async Task RunAsync()
    {
        var registry = new Intersect.Network.PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(Intersect.Network.IntersectPacket).Assembly);
        Check(registry.TryRegisterBuiltIn(), "Packet registry initializes");
        if (Intersect.Network.PackedIntersectPacket.KnownTypes.Count == 0) Intersect.Network.PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach (var envelopePacket in new Intersect.Network.IntersectPacket[]
        {
            new ContentTranslationsPacket { CatalogJson = "{\"Entries\":{}}", Replace = true },
            new TranslationSettingsPacket { Status = "Ready" },
            new Intersect.Network.Packets.Editor.TranslationSettingsRequestPacket { Action = "get" }
        })
            Check(Intersect.Network.MessagePacker.Instance.Deserialize(envelopePacket.Data)?.GetType() == envelopePacket.GetType(), "Translation packets survive the actual network envelope");
        var fake = new TranslationHandler(); using var http = new HttpClient(fake);
        var settings = new TranslationServiceSettings { Enabled = true, Endpoint = "https://translation.invalid/translate", ApiKey = "test-secret", SourceLanguage = "en" };
        const string source = @"Hello \pn, collect {00} apples. \c{#ff0000}Quest\c{}";
        var versions = await MachineTextTranslator.TranslateAsync(source, settings, http, default);
        Check(versions.Count == 3 && versions["en"] == source && versions["fr"].StartsWith("fr:"), "Provider creates all three languages");
        Check(GameTextTokens.Compatible(source, versions["es"]) && fake.Sent.All(value => !value.Contains(@"\pn") && !value.Contains("{00}")),
            "Remote translation protects variables, formatting and markup");
        fake.BreakTokens = true;
        try { await MachineTextTranslator.TranslateAsync(source, settings, http, default); throw new Exception("Broken placeholders accepted"); }
        catch (InvalidDataException) { }
        fake.BreakTokens = false; fake.Fail = true;
        try { await MachineTextTranslator.TranslateAsync(source, settings, http, default); throw new Exception("Service error ignored"); }
        catch (HttpRequestException exception) { Check(!exception.Message.Contains(settings.ApiKey), "Failure logs never contain credentials"); }
        fake.Fail = false; settings.Enabled = false;
        var before = fake.Sent.Count;
        try { await MachineTextTranslator.TranslateAsync(source, settings, http, default); throw new Exception("Disabled service used"); }
        catch (InvalidOperationException) { }
        Check(fake.Sent.Count == before, "Disabled service makes no requests");
        var login = MessagePackSerializer.Deserialize<LoginPacket>(MessagePackSerializer.Serialize(new LoginPacket("test", "test") { LanguageCode = "es" }));
        Check(login.LanguageCode == "es" && new SteamLoginPacket().LanguageCode == "en", "Session language survives packets; older logins default to English");
        var packet = new ContentTranslationsPacket { CatalogJson = new GameTextCatalog { Entries = new() { [source] = versions } }.ToJson(), Replace = true };
        var publicJson = Encoding.UTF8.GetString(MessagePackSerializer.Serialize(packet));
        Check(!publicJson.Contains(settings.ApiKey), "Public translation payload contains no service credentials");
        var client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)); GC.SuppressFinalize(client);
        ContentTranslationRuntime.HandleEditor(client, new Intersect.Network.Packets.Editor.TranslationSettingsRequestPacket { Action = "save", SettingsJson = "invalid" });
        System.Console.WriteLine("PASS translation service: fake endpoint, three locales, protected tokens, failure/disable behavior, credentials isolation, session packets and editor-only configuration");
    }
    private sealed class TranslationHandler : HttpMessageHandler
    {
        public List<string> Sent = []; public bool BreakTokens; public bool Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var payload = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
            var q = payload.Value<string>("q")!; Sent.Add(q);
            if (Fail) return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            var translated = BreakTokens ? "broken" : payload.Value<string>("target") + ": " + q;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(new { translatedText = translated }), Encoding.UTF8, "application/json") };
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
