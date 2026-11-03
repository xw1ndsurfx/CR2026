using Intersect.Localization;
namespace Intersect.Framework.Core.Fishing;

public static class FishingText
{
    private static readonly Lazy<GameTextCatalog> Catalog = new(() =>
    {
        using var stream = typeof(FishingText).Assembly.GetManifestResourceStream("Intersect.Fishing.Text")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    });
    public static string Get(string source, string language) => Catalog.Value.Get(source, language);
}
