using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Localization;
using Newtonsoft.Json;

internal static class LanguageTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        using (var resource = typeof(ClientLanguages).Assembly.GetManifestResourceStream("Intersect.Client.Localization.Locales.ui.json")!)
        using (var reader = new StreamReader(resource))
        {
            var shipped = GameTextCatalog.FromJson(reader.ReadToEnd());
            foreach (var (source, versions) in shipped.Entries)
                foreach (var language in GameTextCatalog.Languages)
                    Check(versions.TryGetValue(language, out var value) && GameTextTokens.Compatible(source, value), "Locale coverage / tokens: " + source + " / " + language);
        }
        var oldDatabase = Globals.Database;
        var preferences = new InterfaceRefreshTests.MemoryPreferences();
        Globals.Database = preferences;
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish", BindingFlags.Static | BindingFlags.NonPublic)!;
        var apply = typeof(ClientLanguages).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!;
        restore.Invoke(null, null);
        var fields = new List<string>();
        foreach (var group in typeof(Strings).GetNestedTypes(BindingFlags.Public).Where(group => group.Name is not ("Keys" or "Regex")))
            foreach (var field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.GetValue(null) is LocalizedString text) fields.Add(text);
                else if (field.GetValue(null) is IDictionary dictionary)
                    foreach (DictionaryEntry entry in dictionary) if (entry.Value is LocalizedString value) fields.Add(value);
        using (var resource = typeof(ClientLanguages).Assembly.GetManifestResourceStream("Intersect.Client.Localization.Locales.ui.json")!)
        using (var reader = new StreamReader(resource))
        {
            var shipped = GameTextCatalog.FromJson(reader.ReadToEnd());
            foreach (var source in fields.Where(source => !string.IsNullOrWhiteSpace(source)))
                Check(shipped.Entries.ContainsKey(GameTextCatalog.NormalizeText(source)), "UI source has all three locales: " + source);
        }
        var export = Environment.GetEnvironmentVariable("CR2026_LANGUAGE_SOURCES");
        if (!string.IsNullOrWhiteSpace(export)) File.WriteAllText(export, JsonConvert.SerializeObject(fields.Distinct().Order().ToArray(), Formatting.Indented));
        try
        {
            foreach (var code in new[] { "en", "fr", "es", "en" })
            {
                restore.Invoke(null, null); preferences.LanguageCode = code; preferences.SavePreferences();
                var loaded = new InterfaceRefreshTests.MemoryPreferences(preferences.Values); loaded.LoadPreferences();
                Check(loaded.LanguageCode == code, "Language preference persists");
                Globals.Database = loaded; apply.Invoke(null, null);
                Check((string)Strings.Settings.Language == (code == "fr" ? "Langue" : code == "es" ? "Idioma" : "Language"), "UI language switches without leaking a previous locale");
                Check(ClientLanguages.Text("Unknown custom text") == "Unknown custom text", "Missing translations preserve source");
            }
            var tokens = GameTextTokens.Protect(@"Hello \pn, {00}! \c{#ff0000}Reward\c{}");
            Check(GameTextTokens.Restore(tokens.Text, tokens.Tokens) == @"Hello \pn, {00}! \c{#ff0000}Reward\c{}", "Event variables and placeholders survive protection");
            Check(!GameTextTokens.Compatible("{00} items", "objets"), "Translations cannot drop placeholders");
            var formatted = new GameTextCatalog();
            formatted.Entries["You received {00} from {01}."] = new() { ["fr"] = "Tu as reÃƒÂ§u {00} de {01}." };
            Check(formatted.GetExpanded("You received 50 from Nicolas.", "fr") == "Tu as reÃƒÂ§u 50 de Nicolas.", "System messages preserve inserted player names and amounts");
            Check(formatted.GetExpanded("Nicolas: hello!", "fr") == "Nicolas: hello!", "Unrelated text stays unchanged");
            formatted.Entries["Value {999999999999999999999} items"] = new() { ["fr"] = "Valeur {999999999999999999999} objets" };
            Check(formatted.GetExpanded("Value 50 items", "fr") == "Value 50 items", "Invalid huge format indices safely fall back");
            Check(GameTextCatalog.FromJson("{\"Entries\":null}").Entries.Count == 0, "Malformed empty catalog safely falls back");
            Check(!GameTextCatalog.IsDisplayObject(Intersect.Enums.GameObjectType.Tileset) && !GameTextCatalog.IsDisplayObject(Intersect.Enums.GameObjectType.Animation) &&
                !GameTextCatalog.IsDisplayObject(Intersect.Enums.GameObjectType.ServerVariable), "Technical asset and variable names are excluded from localization");
            var catalog = new GameTextCatalog(); catalog.Entries["Village"] = new() { ["fr"] = "Village traduit", ["es"] = "Pueblo" };
            var json = catalog.TranslateJson("{\"Name\":\"Village\",\"Sprite\":\"Village\",\"Expression\":\"Village\",\"Options\":[\"Village\"]}", "es");
            Check(json.Contains("\"Name\":\"Pueblo\"") && json.Contains("\"Sprite\":\"Village\"") && json.Contains("\"Expression\":\"Village\""), "Only authored display fields are translated");
            System.Console.WriteLine("PASS languages: en/fr/es preference, UI reload isolation, source fallback, content field isolation and protected event tokens");
        }
        finally { restore.Invoke(null, null); Globals.Database = oldDatabase; apply.Invoke(null, null); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
