using Intersect.Localization;
namespace Intersect.Framework.Core.QualityOfLife;
public static class QualityOfLifeText
{
    public static GameTextCatalog Catalog {get;}=Load();
    private static GameTextCatalog Load()
    {
        using var stream=typeof(QualityOfLifeText).Assembly.GetManifestResourceStream("Intersect.QualityOfLife.Text")!;
        using var reader=new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string source,string language)=>Catalog.Get(source,language).Replace('\u202f',' ').Replace('\u00a0',' ').Replace("’","'");
}
