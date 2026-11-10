namespace Intersect.Framework.Core.Naval;

public enum ShipDeckKind { ThreeMast, Sloop, Raft, ShipOfLine }
public sealed record NavalShipModel(string Id,string Stem,ShipDeckKind DeckKind=ShipDeckKind.ThreeMast)
{
    public ShipDeckActivity Activity {get;init;}
    public int CrewCapacity=>DeckKind==ShipDeckKind.Sloop?3:DeckKind==ShipDeckKind.Raft?2:DeckKind==ShipDeckKind.ShipOfLine?13:7;
    public int CannonCount(bool starboard)=>DeckKind==ShipDeckKind.Raft?(starboard?0:1):DeckKind==ShipDeckKind.Sloop?1:DeckKind==ShipDeckKind.ShipOfLine?6:3;
    public string Caption=>Id switch
    {
        "royal-three-mast"=>"Royal three-mast", "herbonia-three-mast"=>"Herbonia three-mast",
        "majestya-three-mast"=>"Majestya three-mast", "skeleton-three-mast"=>"Skeleton three-mast",
        "ghost-three-mast"=>"Ghost three-mast", "pirate-three-mast"=>"Pirate three-mast",
        "royal-sloop"=>"Royal sloop", "adventurer-three-mast"=>"Adventurer three-mast",
        "adventurer-sloop"=>"Adventurer sloop", "adventurer-raft"=>"Adventurer raft", "adventurer-fishing"=>"Adventurer fishing boat",
        _=>NavalShipModels.FormatCaption(Id)
    };
    public void ApplyTo(ShipDefinition ship)
    {
        var previousKind=ShipDeck.Kind(ship.Graphics);
        ship.Graphics=Graphics;ship.IdleGraphics=IdleGraphics;ship.DamagedGraphics=DamagedGraphics;ship.SinkingGraphics=SinkingGraphics;
        ship.Frames=4;ship.FrameMilliseconds=180;ship.CrewCapacity=CrewCapacity;
        ship.DeckActivity=Activity;
        if(previousKind!=DeckKind){ship.Width=DeckKind==ShipDeckKind.ThreeMast?2:DeckKind==ShipDeckKind.ShipOfLine?2.5:1.5;ship.Length=DeckKind==ShipDeckKind.Sloop?3:DeckKind==ShipDeckKind.Raft?2.5:DeckKind==ShipDeckKind.ShipOfLine?5:4;}
    }
    public string Graphics=>Id=="royal-three-mast"?"Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png":Stem+"_Normal_v5_compat.png";
    public string IdleGraphics=>Stem+"_Idle_v5_compat.png";
    public string DamagedGraphics=>Stem+"_Damaged_v5_compat.png";
    public string SinkingGraphics=>Stem+"_Sinking_v5_compat.png";
    public string PreviewGraphics=>Stem+"_Preview_v5.png";
}
public static class NavalShipModels
{
    public static readonly NavalShipModel[] ThreeMasts=
    [
        new("royal-three-mast","Ship_CorpsRoyaux_ThreeMast"),
        new("herbonia-three-mast","Ship_Herbonia_ThreeMast"),
        new("majestya-three-mast","Ship_Majestya_ThreeMast"),
        new("skeleton-three-mast","Ship_Skeleton_ThreeMast"),
        new("ghost-three-mast","Ship_Ghost_ThreeMast"),
        new("pirate-three-mast","Ship_Pirate_ThreeMast"),
    ];
    public static readonly NavalShipModel[] SmallShips=
    [
        new("royal-sloop","Ship_CorpsRoyaux_Sloop",ShipDeckKind.Sloop),
    ];
    public static readonly NavalShipModel[] Adventurers=
    [
        new("adventurer-three-mast","Ship_Adventurer_ThreeMast"),
        new("adventurer-sloop","Ship_Adventurer_Sloop",ShipDeckKind.Sloop),
        new("adventurer-raft","Ship_Adventurer_Raft",ShipDeckKind.Raft),
        new("adventurer-fishing","Ship_Adventurer_Fishing"){Activity=ShipDeckActivity.Fishing},
        new("adventurer-line","Ship_Adventurer_ShipOfLine",ShipDeckKind.ShipOfLine),
    ];
    public static readonly (string Id,string Stem,string Caption)[] Factions=
    [("royal","CorpsRoyaux","Royal"),("herbonia","Herbonia","Herbonia"),("majestya","Majestya","Majestya"),
     ("skeleton","Skeleton","Skeleton"),("ghost","Ghost","Ghost"),("pirate","Pirate","Pirate")];
    public static readonly NavalShipModel[] OtherFormats=Factions.SelectMany(f=>new[]
    {
        new NavalShipModel(f.Id+"-sloop","Ship_"+f.Stem+"_Sloop",ShipDeckKind.Sloop),
        new NavalShipModel(f.Id+"-raft","Ship_"+f.Stem+"_Raft",ShipDeckKind.Raft),
        new NavalShipModel(f.Id+"-fishing","Ship_"+f.Stem+"_Fishing"){Activity=ShipDeckActivity.Fishing},
        new NavalShipModel(f.Id+"-line","Ship_"+f.Stem+"_ShipOfLine",ShipDeckKind.ShipOfLine),
    }).Where(m=>m.Id!="royal-sloop").ToArray();
    internal static string FormatCaption(string id)
    {
        var split=id.IndexOf('-');if(split<0)return id;
        var faction=id[..split];var type=id[(split+1)..];
        var title=faction=="adventurer"?"Adventurer":Factions.FirstOrDefault(f=>f.Id==faction).Caption;
        return string.IsNullOrEmpty(title)?id:title+(type switch {"line"=>" ship of the line","sloop"=>" sloop","raft"=>" raft","fishing"=>" fishing boat",_=>" "+type});
    }
    public static NavalShipModel PlayerDefault=>Adventurers[0];
    public static readonly NavalShipModel[] All=[..Adventurers,..ThreeMasts,..SmallShips,..OtherFormats];
    public static NavalShipModel? Find(string? graphics)=>All.FirstOrDefault(m=>
        string.Equals(m.Graphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.IdleGraphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.DamagedGraphics,graphics,StringComparison.OrdinalIgnoreCase));
}
