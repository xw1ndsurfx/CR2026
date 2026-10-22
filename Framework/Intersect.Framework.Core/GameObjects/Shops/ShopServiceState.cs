namespace Intersect.GameObjects;
public sealed class ShopServiceState
{
    public string Name {get;set;}="";
    public bool Music {get;set;}
    public bool Premium {get;set;}
    public string Message {get;set;}="";
    public ShopServiceRow[] Rows {get;set;}=[];
}
public sealed class ShopServiceRow
{
    public ShopServiceOffer Offer {get;set;}=new();
    public string Currency {get;set;}="";
    public long Balance {get;set;}
    public bool Owned {get;set;}
    public bool Available {get;set;}
}
