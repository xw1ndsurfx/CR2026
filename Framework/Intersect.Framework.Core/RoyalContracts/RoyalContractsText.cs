using Intersect.Localization;

namespace Intersect.Framework.Core.RoyalContracts;

public static class RoyalContractsText
{
    private static readonly Lazy<GameTextCatalog> Catalog = new(() =>
    {
        using var stream = typeof(RoyalContractsText).Assembly.GetManifestResourceStream("Intersect.RoyalContracts.Text")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    });
    public static string Get(string source, string language) => Catalog.Value.Get(source, language);
}
