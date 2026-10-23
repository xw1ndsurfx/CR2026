using System.Collections;
using System.Reflection;
using Intersect.Client.General;
using Intersect.Localization;

namespace Intersect.Client.Localization;

public static class ClientLanguages
{
    private static readonly Dictionary<FieldInfo, object> EnglishFields = [];
    private static readonly object ContentGate = new();
    private static readonly Lazy<GameTextCatalog> BuiltIn = new(() =>
    {
        using var stream = typeof(ClientLanguages).Assembly.GetManifestResourceStream("Intersect.Client.Localization.Locales.ui.json")!;
        using var reader = new StreamReader(stream); return GameTextCatalog.FromJson(reader.ReadToEnd());
    });
    public static string Current { get; private set; } = "en";
    public static GameTextCatalog Content { get; private set; } = new();
    public static int ContentRevision { get; private set; }
    public static string Text(string source)
    {
        var value = BuiltIn.Value.Get(source, Current);
        return value != source ? value : Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source, Current);
    }
    public static string Interpolated(FormattableString source) => string.Format(Text(source.Format), source.GetArguments());
    public static string Format(string source, params object[] values) => string.Format(Text(source), values);
    public static string Expanded(string source)
    {
        var value = BuiltIn.Value.GetExpanded(source, Current);
        lock (ContentGate) return value != source ? value : Content.GetExpanded(source, Current);
    }
    public static string ContentText(string source) { lock (ContentGate) return Content.Get(source, Current); }
    public static void MergeContent(string json, bool replace = false) { lock (ContentGate) { if (replace) Content = new(); Content.Merge(GameTextCatalog.FromJson(json)); ContentRevision++; } }
    public static string ContentJson(string json)
    {
        lock (ContentGate)
        {
            if (string.IsNullOrWhiteSpace(json) || Content.Entries.Count == 0) return json;
            try { return Content.TranslateJson(json, Current); } catch { return json; }
        }
    }
    internal static void RestoreEnglish()
    {
        foreach (var (field, value) in EnglishFields)
        {
            if (value is Dictionary<object, string> entries && field.GetValue(null) is IDictionary dictionary)
                foreach (var (key, text) in entries) dictionary[key] = new LocalizedString(text);
            else field.SetValue(null, new LocalizedString((string)value));
        }
        EnglishFields.Clear();
    }
    internal static void Apply()
    {
        Current = GameTextCatalog.NormalizeLanguage(Globals.Database?.LanguageCode);
        foreach (var group in typeof(Strings).GetNestedTypes(BindingFlags.Public))
            foreach (var field in group.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.GetValue(null) is LocalizedString text)
                {
                    EnglishFields[field] = text.ToString(); field.SetValue(null, new LocalizedString(Text(text)));
                }
                else if (field.GetValue(null) is IDictionary dictionary)
                {
                    var entries = new Dictionary<object, string>();
                    foreach (DictionaryEntry entry in dictionary)
                        if (entry.Value is LocalizedString value) entries[entry.Key] = value.ToString();
                    EnglishFields[field] = entries;
                    foreach (var (key, value) in entries) dictionary[key] = new LocalizedString(Text(value));
                }
            }
    }
}
