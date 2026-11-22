using Intersect.Localization;
namespace Intersect.Framework.Core.RoyalSeasons;
public static class RoyalSeasonText
{
    public static GameTextCatalog Catalog { get; } = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(RoyalSeasonText).Assembly.GetManifestResourceStream("Intersect.RoyalSeasons.Text")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string text, string language) => Catalog.Get(text, language).Replace('\u202f', ' ').Replace('\u00a0', ' ');
}
