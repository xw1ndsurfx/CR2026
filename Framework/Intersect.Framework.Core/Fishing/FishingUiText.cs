using Intersect.Localization;

namespace Intersect.Framework.Core.Fishing;

public static class FishingUiText
{
    private static readonly Lazy<GameTextCatalog> Catalog = new(() =>
    {
        using var stream = typeof(FishingUiText).Assembly.GetManifestResourceStream("Intersect.Fishing.UiText")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    });
    public static string Get(string source, string language) => Catalog.Value.Get(source, language);
}
