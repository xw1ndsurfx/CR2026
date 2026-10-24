using System.Globalization;
using System.Text.Json;
namespace Intersect.Editor.Forms.Cinematics;

internal static class CinematicText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Catalog = Load();
    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using var stream = typeof(CinematicText).Assembly.GetManifestResourceStream("Cinematics.Texts.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
    }
    internal static string T(string key) => Catalog.TryGetValue(key, out var entry)
        ? entry.GetValueOrDefault(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? entry["en"] : key;
}
