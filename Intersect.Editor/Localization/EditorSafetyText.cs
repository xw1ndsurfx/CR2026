using System.Globalization;
using Intersect.Localization;

namespace Intersect.Editor.Localization;

internal static class EditorSafetyText
{
    internal static readonly GameTextCatalog Catalog = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(EditorSafetyText).Assembly.GetManifestResourceStream("Intersect.Editor.SafetyText")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    internal static string Get(string text, params object[] args)
    {
        var translated = Catalog.Get(text, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return args.Length == 0 ? translated : string.Format(CultureInfo.CurrentCulture, translated, args);
    }
}
