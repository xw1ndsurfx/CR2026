namespace Intersect.GameObjects;
public sealed class ShopServiceOffer
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public Guid SpellId {get;set;}
    public string Music {get;set;}="";
    public string Icon {get;set;}="";
    public Guid CurrencyItemId {get;set;}
    public int Price {get;set;}
    public int RequiredLevel {get;set;}=1;
    public int DurationSeconds {get;set;}=180;
    public bool IsValid=>Id!=Guid.Empty && Name is {Length:>=1 and <=128} && Music is {Length:<=255} && Icon is {Length:<=255} &&
        !Music.Contains("..") && !Music.Contains('/') && !Music.Contains('\\') && !Icon.Contains("..") &&
        Price is >=0 and <=1000000000 && RequiredLevel is >=1 and <=1000000 && DurationSeconds is >=10 and <=1800;
}
