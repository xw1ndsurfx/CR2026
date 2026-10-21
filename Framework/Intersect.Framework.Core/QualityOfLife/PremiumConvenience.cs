using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
namespace Intersect.Framework.Core.QualityOfLife;
public sealed class SavedOutfit
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public CharacterAppearance Appearance {get;set;}=new();
}
public sealed class PremiumConvenienceProfile
{
    public List<SavedOutfit> Outfits {get;set;}=[];
    public HashSet<Guid> FavoriteRecipes {get;set;}=[];
}
public sealed class PetDecorationDefinition
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public string Icon {get;set;}="";
    public int Size {get;set;}=24;
    public int OffsetX {get;set;}
    public int OffsetY {get;set;}
    public bool IsValid=>Id!=Guid.Empty && Name.Length is >0 and <=80 && NavalPresentation.SafeAsset(Icon) && !string.IsNullOrEmpty(Icon) && Size is >=8 and <=96 && OffsetX is >=-128 and <=128 && OffsetY is >=-128 and <=128;
}
public sealed class ConveniencePet
{
    public Guid Id {get;set;}
    public string Name {get;set;}="";
    public Guid DecorationId {get;set;}
}
public sealed class PremiumConvenienceView
{
    public bool Premium {get;set;}
    public PremiumConvenienceProfile Profile {get;set;}=new();
    public PetDecorationDefinition[] Decorations {get;set;}=[];
    public ConveniencePet[] Pets {get;set;}=[];
}
