using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Localization;

public sealed class GameTextCatalog
{
    public static readonly string[] Languages = ["en", "fr", "es"];
    public Dictionary<string, Dictionary<string, string>> Entries { get; set; } = new(StringComparer.Ordinal);
    public static bool IsDisplayObject(Intersect.Enums.GameObjectType type) => type is
        Intersect.Enums.GameObjectType.Class or Intersect.Enums.GameObjectType.Item or Intersect.Enums.GameObjectType.Npc or
        Intersect.Enums.GameObjectType.Quest or Intersect.Enums.GameObjectType.Resource or Intersect.Enums.GameObjectType.Spell or
        Intersect.Enums.GameObjectType.Event or Intersect.Enums.GameObjectType.Map or Intersect.Enums.GameObjectType.Shop or
        Intersect.Enums.GameObjectType.Crafts or Intersect.Enums.GameObjectType.CraftTables;
    public static string NormalizeLanguage(string? code) => Languages.Contains(code) ? code! : "en";
    public static string NormalizeText(string value) => value.Replace("\r", "");
    public string Get(string? source, string language)
    {
        if (string.IsNullOrEmpty(source)) return source ?? "";
        return Entries.TryGetValue(NormalizeText(source), out var versions) && versions != null && versions.TryGetValue(NormalizeLanguage(language), out var value) &&
            !string.IsNullOrWhiteSpace(value) && GameTextTokens.Compatible(source, value) ? value : source;
    }
    private static readonly Regex FormatArgument = new(@"(?<!\{)\{(?<index>\d+)(?:,[^{}:]+)?(?::[^{}]*)?\}(?!\})", RegexOptions.Compiled);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex> _expandedPatterns = new();
    public string GetExpanded(string source, string language)
    {
        var direct = Get(source, language); if (direct != source) return direct;
        foreach (var (template, versions) in Entries.OrderByDescending(entry => entry.Key.Length))
        {
            if (!versions.TryGetValue(language, out var translated) || translated == template || !FormatArgument.IsMatch(template) ||
                FormatArgument.Replace(template, "").Count(char.IsLetter) < 4) continue;
            if (FormatArgument.Matches(template).Any(argument => !int.TryParse(argument.Groups["index"].Value, out var value) || value > 100)) continue;
            if (!_expandedPatterns.TryGetValue(template, out var pattern))
            {
                var expression = new System.Text.StringBuilder(@"\A"); var last = 0; var used = new HashSet<int>();
                foreach (Match argument in FormatArgument.Matches(template))
                {
                    expression.Append(Regex.Escape(template[last..argument.Index]));
                    var index = int.Parse(argument.Groups["index"].Value);
                    expression.Append(used.Add(index) ? $"(?<p{index}>.*?)" : $@"\k<p{index}>");
                    last = argument.Index + argument.Length;
                }
                expression.Append(Regex.Escape(template[last..])); expression.Append(@"\z");
                _expandedPatterns[template] = pattern = new Regex(expression.ToString(), RegexOptions.Singleline, TimeSpan.FromMilliseconds(20));
            }
            Match match; try { match = pattern.Match(source); } catch (RegexMatchTimeoutException) { continue; }
            if (!match.Success) continue;
            try
            {
                var indices = FormatArgument.Matches(template).Select(argument => int.Parse(argument.Groups["index"].Value)).ToArray();
                if (indices.Max() > 100) continue;
                var values = new object[indices.Max() + 1]; foreach (var index in indices) values[index] = match.Groups["p" + index].Value;
                return string.Format(translated, values);
            }
            catch (FormatException) { }
        }
        return source;
    }
    public void Merge(GameTextCatalog other)
    {
        if (other.Entries == null) return;
        foreach (var (source, versions) in other.Entries)
        {
            if (versions == null) continue;
            var key = NormalizeText(source);
            if (!Entries.TryGetValue(key, out var existing)) Entries[key] = existing = [];
            foreach (var (language, text) in versions)
                if (text != null && Languages.Contains(language) && GameTextTokens.Compatible(source, text)) existing[language] = text;
        }
    }
    public static GameTextCatalog FromJson(string? json)
    {
        var result = new GameTextCatalog();
        if (!string.IsNullOrWhiteSpace(json)) result.Merge(JsonConvert.DeserializeObject<GameTextCatalog>(json) ?? new());
        return result;
    }
    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    // Whitelist display fields only: filenames, expressions, scripts and IDs remain untouched.
    private static readonly HashSet<string> TextFields = new(StringComparer.OrdinalIgnoreCase)
    { "Name", "Label", "TaskText", "Description", "Text", "Prompt", "Message", "Title", "TaskDescription", "StartDescription", "EndDescription", "CompletionDescription", "Options" };
    public static IEnumerable<string> Extract(string json)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        Walk(JToken.Parse(json), (value) => { if (!string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetter)) values.Add(value); return value; });
        return values;
    }
    public string TranslateJson(string json, string language)
    {
        var token = JToken.Parse(json); Walk(token, source => Get(source, language)); return token.ToString(Formatting.None);
    }
    private static void Walk(JToken token, Func<string, string> visit)
    {
        if (token is JObject obj)
            foreach (var property in obj.Properties().ToArray())
            {
                if (TextFields.Contains(property.Name) || property.Name.EndsWith("Description", StringComparison.OrdinalIgnoreCase) || property.Name.EndsWith("Message", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.Type == JTokenType.String) property.Value = visit(property.Value.Value<string>()!);
                    else if (property.Value is JArray array)
                        foreach (var value in array.OfType<JValue>().Where(value => value.Type == JTokenType.String).ToArray()) value.Value = visit((string)value.Value!);
                }
                if (property.Value is JObject or JArray) Walk(property.Value, visit);
            }
        else if (token is JArray array) foreach (var value in array) Walk(value, visit);
    }
}

public static class GameTextTokens
{
    private static readonly Regex Tokens = new(@"\\[a-zA-Z]+(?:\{[^{}\r\n]*\})?|\{\{|\}\}|\{\d+[^{}]*\}|<[^>]+>", RegexOptions.Compiled);
    public static string[] Read(string value) => Tokens.Matches(value).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    public static bool Compatible(string source, string target) => Read(source).SequenceEqual(Read(target));
    public static (string Text, Dictionary<string, string> Tokens) Protect(string source)
    {
        var tokens = new Dictionary<string, string>();
        var text = Tokens.Replace(source, match => { var key = $"ZXQ{tokens.Count:D6}XQZ"; tokens[key] = match.Value; return key; });
        return (text, tokens);
    }
    public static string Restore(string text, Dictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
        {
            var pattern = Regex.Escape(key);
            if (Regex.Matches(text, pattern, RegexOptions.IgnoreCase).Count != 1) throw new InvalidDataException("Translation changed a protected token.");
            text = Regex.Replace(text, pattern, _ => value, RegexOptions.IgnoreCase);
        }
        return text;
    }
}
