namespace Intersect.Framework.Core.Naval;

/// <summary>Explicit source rectangles and waterline anchors. Directions: N, NE, E, SE, S, SW, W, NW.</summary>
public sealed class ShipSpriteAtlas
{
    public int Version { get; set; } = 1;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double PixelsPerTile { get; set; } = 64;
    public bool Loop { get; set; } = true;
    public string ForegroundGraphics { get; set; } = "";
    public string CannonGraphics { get; set; } = "";
    public string CannonOcclusionGraphics { get; set; } = "";
    public ShipSpriteFrame[][] Directions { get; set; } = [];

    public bool IsValid(int textureWidth, int textureHeight, int frames) =>
        NavalPresentation.SafeAsset(ForegroundGraphics) && NavalPresentation.SafeAsset(CannonGraphics) &&
        NavalPresentation.SafeAsset(CannonOcclusionGraphics) && Version is 1 or 2 && ImageWidth == textureWidth && ImageHeight == textureHeight &&
        textureWidth is > 0 and <= 16384 && textureHeight is > 0 and <= 16384 &&
        double.IsFinite(PixelsPerTile) && PixelsPerTile is >= 8 and <= 1024 &&
        frames is >= 1 and <= 16 && Directions is { Length: 8 } &&
        Directions.All(row => row != null && row.Length == frames && row.All(frame =>
            frame != null && frame.CrewAnchors != null && frame.CrewAnchors.Length <= 16 &&
            frame.DeckMasts!=null && (frame.DeckMasts.Length==0 || frame.DeckMasts.Length==3) &&
            frame.DeckMasts.All(p=>double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X>=0 && p.Y>=0 && p.X<=frame.Width && p.Y<=frame.Height) &&
            (!frame.DeckBow.HasValue || double.IsFinite(frame.DeckBow.Value.X) && double.IsFinite(frame.DeckBow.Value.Y) &&
                frame.DeckBow.Value.X>=0 && frame.DeckBow.Value.X<=frame.Width && frame.DeckBow.Value.Y>=0 && frame.DeckBow.Value.Y<=frame.Height) &&
            new[]{frame.DeckPort,frame.DeckStarboard}.All(points=>points!=null && (points.Length==0 || points.Length==3) &&
                points.All(p=>double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X>=0 && p.Y>=0 && p.X<=frame.Width && p.Y<=frame.Height)) &&
            frame.CannonAnchors != null && frame.CannonAnchors.Length<=16 &&
            (frame.CannonAnchors.Length==0 || !string.IsNullOrEmpty(CannonGraphics)) &&
            frame.CannonAnchors.All(a=>a!=null && a.IsValid(frame.Width,frame.Height)) &&
            frame.CannonAnchors.Select(a=>(a.Starboard,a.Index)).Distinct().Count()==frame.CannonAnchors.Length &&
            frame.CrewAnchors.All(a => a != null && a.IsValid(frame.Width, frame.Height)) &&
            frame.CrewAnchors.Select(a => (a.Role,a.Index)).Distinct().Count() == frame.CrewAnchors.Length && double.IsFinite(frame.RotationDegrees) && Math.Abs(frame.RotationDegrees)<=360 && frame.X >= 0 && frame.Y >= 0 && frame.Width > 0 && frame.Height > 0 &&
            frame.Width <= textureWidth && frame.Height <= textureHeight &&
            frame.X <= textureWidth - frame.Width && frame.Y <= textureHeight - frame.Height &&
            double.IsFinite(frame.AnchorX) && double.IsFinite(frame.AnchorY) &&
            frame.AnchorX >= 0 && frame.AnchorX <= frame.Width && frame.AnchorY >= 0 && frame.AnchorY <= frame.Height &&
            (frame.Slices==null || frame.Slices.Length<=512 && frame.Slices.All(r=>r is {Length:4} && r[0]>=0 && r[1]>=0 && r[2]>0 && r[3]>0 && r[0]<=frame.Width-r[2] && r[1]<=frame.Height-r[3]))));

    public bool UpgradeLegacySoutheast(string name)
    {
        var shipped = name.ToLowerInvariant() is "ship_corpsroyaux_002.png" or "ship_corpsroyaux_005.png" or
            "ship_corpsroyaux_001_sink.png" or "ship_corpsroyaux_002_sink.png" or "ship_corpsroyaux_003_sink.png" or
            "ship_corpsroyaux_004_sink.png" or "ship_corpsroyaux_005_sink.png" or "ship_corpsroyaux_006_sink.png" or
            "ship_herbonia_001_sink.png" or "ship_herbonia_002_sink.png";
        if (!shipped || ImageWidth != 1448 || ImageHeight != 1086 || Version != 1 || !IsValid(ImageWidth, ImageHeight, 4) ||
            Directions[3].Any(f => f.FlipHorizontal)) return false;
        Directions[3] = Directions[5].Select(f => new ShipSpriteFrame { X = f.X, Y = f.Y, Width = f.Width,
            Height = f.Height, AnchorX = f.AnchorX, AnchorY = f.AnchorY, FlipHorizontal = !f.FlipHorizontal }).ToArray();
        Version = 2;
        return true;
    }

    // Call after validation; both the client and the preview use these same coordinates.
    public ShipSpriteFrame Frame(double heading, long milliseconds, int frameMilliseconds) =>
        Directions[ShipMovement.DirectionRow(heading)][FrameIndex(milliseconds, frameMilliseconds)];

    public int FrameIndex(long milliseconds, int frameMilliseconds)
    {
        var position = Math.Max(0, milliseconds) / Math.Max(50, frameMilliseconds);
        return (int)(Loop ? position % Directions[0].Length : Math.Min(position, Directions[0].Length - 1));
    }

    public bool IsFinished(long milliseconds, int frameMilliseconds) =>
        !Loop && Math.Max(0, milliseconds) / Math.Max(50, frameMilliseconds) >= Directions[0].Length;
}

public sealed class ShipSpriteFrame
{
    public DeckPoint[] DeckMasts {get;set;}=[];
    public DeckPoint[] DeckPort {get;set;}=[];
    public DeckPoint[] DeckStarboard {get;set;}=[];
    public DeckPoint? DeckBow {get;set;}
    public ShipCannonAnchor[] CannonAnchors { get; set; } = [];
    public ShipCrewAnchor[] CrewAnchors { get; set; } = [];
    public double RotationDegrees {get;set;}
    public int[][]? Slices {get;set;}
    public bool FlipHorizontal { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
}

/// <summary>Independent cannon cells: eight columns of 64 px, pivot at each cell's centre.</summary>
public sealed class ShipCannonAnchor
{
    public const int CellSize=64, Columns=8;
    public bool Starboard { get; set; }
    public int Index { get; set; }
    public int Sprite { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool BehindCrew { get; set; }
    public bool IsValid(int width,int height)=>Index is >=0 and <8 && Sprite is >=0 and <256 &&
        double.IsFinite(X) && double.IsFinite(Y) && X>=0 && Y>=0 && X<=width && Y<=height;
}

public enum ShipCrewRole { Helm, PortCannon, StarboardCannon, Passenger }
public sealed class ShipCrewAnchor
{
    public ShipCrewRole Role { get; set; }
    public int Index { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public Intersect.Enums.Direction Facing { get; set; }
    public bool IsValid(int width,int height) => Enum.IsDefined(Role) && Index is >=0 and <8 &&
        double.IsFinite(X) && double.IsFinite(Y) && X>=0 && Y>=0 && X<=width && Y<=height &&
        Facing is Intersect.Enums.Direction.Up or Intersect.Enums.Direction.Down or Intersect.Enums.Direction.Left or Intersect.Enums.Direction.Right;
}
