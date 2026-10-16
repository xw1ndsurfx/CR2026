using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Localization;

public sealed class TranslationServiceSettings
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SourceLanguage { get; set; } = "auto";
    public void Validate()
    {
        if (SourceLanguage != "auto" && !GameTextCatalog.Languages.Contains(SourceLanguage)) throw new InvalidDataException("Invalid source language.");
        if (Enabled && (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new InvalidDataException("Configure an HTTP(S) LibreTranslate-compatible /translate endpoint first.");
    }
}

public static class MachineTextTranslator
{
    public static async Task<Dictionary<string, string>> TranslateAsync(string source, TranslationServiceSettings settings, HttpClient client, CancellationToken token)
    {
        settings.Validate();
        if (!settings.Enabled) throw new InvalidOperationException("Automatic translation is disabled.");
        var (protectedText, tokens) = GameTextTokens.Protect(source);
        var result = new Dictionary<string, string>();
        foreach (var language in GameTextCatalog.Languages)
        {
            if (language == settings.SourceLanguage) { result[language] = source; continue; }
            var payload = JsonConvert.SerializeObject(new { q = protectedText, source = settings.SourceLanguage, target = language, format = "text", api_key = settings.ApiKey });
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Translation service returned HTTP {(int)response.StatusCode}.");
            var body = JObject.Parse(await response.Content.ReadAsStringAsync(token));
            var text = body.Value<string>("translatedText") ?? throw new InvalidDataException("Translation response contains no text.");
            text = GameTextTokens.Restore(text, tokens);
            if (string.IsNullOrWhiteSpace(text) || !GameTextTokens.Compatible(source, text)) throw new InvalidDataException("Invalid translated text.");
            result[language] = text;
        }
        return result;
    }
}
