using Intersect.Localization;

namespace Intersect.Framework.Core.Exportation;

public static class ExportationText
{
    private static readonly Lazy<GameTextCatalog> Catalog = new(() =>
    {
        using var stream = typeof(ExportationText).Assembly.GetManifestResourceStream("Intersect.Exportation.Text")
            ?? throw new InvalidDataException("Exportation translation resource missing.");
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    });

    public static string Get(string source, string language) => Catalog.Value.Get(source, language);
}
