namespace Intersect.Framework.Core.Naval;

public enum ShipDeckKind { ThreeMast, Sloop, Raft }
public sealed record NavalShipModel(string Id,string Stem,ShipDeckKind DeckKind=ShipDeckKind.ThreeMast)
{
    public ShipDeckActivity Activity {get;init;}
    public int CrewCapacity=>DeckKind==ShipDeckKind.Sloop?3:DeckKind==ShipDeckKind.Raft?2:7;
    public int CannonCount(bool starboard)=>DeckKind==ShipDeckKind.Raft?(starboard?0:1):DeckKind==ShipDeckKind.Sloop?1:3;
    public string Caption=>Id switch
    {
        "royal-three-mast"=>"Royal three-mast", "herbonia-three-mast"=>"Herbonia three-mast",
        "majestya-three-mast"=>"Majestya three-mast", "skeleton-three-mast"=>"Skeleton three-mast",
        "ghost-three-mast"=>"Ghost three-mast", "pirate-three-mast"=>"Pirate three-mast",
        "royal-sloop"=>"Royal sloop", "adventurer-three-mast"=>"Adventurer three-mast",
        "adventurer-sloop"=>"Adventurer sloop", "adventurer-raft"=>"Adventurer raft", "adventurer-fishing"=>"Adventurer fishing boat",_=>Id
    };
    public void ApplyTo(ShipDefinition ship)
    {
        var previousKind=ShipDeck.Kind(ship.Graphics);
        ship.Graphics=Graphics;ship.IdleGraphics=IdleGraphics;ship.DamagedGraphics=DamagedGraphics;ship.SinkingGraphics=SinkingGraphics;
        ship.Frames=4;ship.FrameMilliseconds=180;ship.CrewCapacity=CrewCapacity;
        ship.DeckActivity=Activity;
        if(previousKind!=DeckKind){ship.Width=DeckKind==ShipDeckKind.ThreeMast?2:1.5;ship.Length=DeckKind==ShipDeckKind.Sloop?3:DeckKind==ShipDeckKind.Raft?2.5:4;}
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
    ];
    public static NavalShipModel PlayerDefault=>Adventurers[0];
    public static readonly NavalShipModel[] All=[..Adventurers,..ThreeMasts,..SmallShips];
    public static NavalShipModel? Find(string? graphics)=>All.FirstOrDefault(m=>
        string.Equals(m.Graphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.IdleGraphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.DamagedGraphics,graphics,StringComparison.OrdinalIgnoreCase));
}
