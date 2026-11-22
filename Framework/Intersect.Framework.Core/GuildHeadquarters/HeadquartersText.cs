using Intersect.Localization;
namespace Intersect.Framework.Core.GuildHeadquarters;
public static class HeadquartersText
{
    public static GameTextCatalog Catalog { get; } = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(HeadquartersText).Assembly.GetManifestResourceStream("Intersect.GuildHeadquarters.Text")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string source, string language) => Catalog.Get(source, language).Replace('\u202f', ' ').Replace('\u00a0', ' ');
}
