using Intersect.Localization;
namespace Intersect.Framework.Core.EquipmentLoadouts;
public static class EquipmentLoadoutText
{
    public static GameTextCatalog Catalog { get; } = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(EquipmentLoadoutText).Assembly.GetManifestResourceStream("Intersect.EquipmentLoadouts.Text")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string source, string language) => Catalog.Get(source, language).Replace('\u202f', ' ').Replace('\u00a0', ' ');
}
