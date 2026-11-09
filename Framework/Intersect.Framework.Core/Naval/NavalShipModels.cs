namespace Intersect.Framework.Core.Naval;

public sealed record NavalShipModel(string Id,string Stem)
{
    public string Graphics=>Id=="royal-three-mast"?"Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png":Stem+"_Normal_v5_compat.png";
    public string IdleGraphics=>Stem+"_Idle_v5_compat.png";
    public string DamagedGraphics=>Stem+"_Damaged_v5_compat.png";
    public string SinkingGraphics=>Stem+"_Sinking_v5_compat.png";
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
    public static NavalShipModel? Find(string? graphics)=>ThreeMasts.FirstOrDefault(m=>
        string.Equals(m.Graphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.IdleGraphics,graphics,StringComparison.OrdinalIgnoreCase) || string.Equals(m.DamagedGraphics,graphics,StringComparison.OrdinalIgnoreCase));
}
