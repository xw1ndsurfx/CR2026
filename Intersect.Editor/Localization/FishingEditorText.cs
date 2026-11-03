using Intersect.Localization;

namespace Intersect.Editor.Localization;

internal static class FishingEditorText
{
    private static readonly Lazy<GameTextCatalog> Catalog = new(() =>
    {
        using var stream = typeof(FishingEditorText).Assembly.GetManifestResourceStream("Intersect.Editor.Fishing.Text")
            ?? throw new InvalidOperationException("Missing fishing editor text catalog.");
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    });
    internal static string Get(string source) => Catalog.Value.Get(source,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
