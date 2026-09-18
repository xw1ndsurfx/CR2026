namespace Intersect.Client.MiniGames;

/// <summary>One coordinate system for the scene, hitboxes, cards and portraits.</summary>
public sealed class PokerSceneLayout
{
    public const int DesignWidth = 1000;
    public const int DesignHeight = 780;
    public float Scale { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
    public PokerSceneLayout(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        Scale = Math.Min(width / (float)DesignWidth, height / (float)DesignHeight);
        OffsetX = (width - (int)(DesignWidth * Scale)) / 2;
        OffsetY = (height - (int)(DesignHeight * Scale)) / 2;
    }
    public Rectangle Rect(int x, int y, int width, int height) => new(
        OffsetX + (int)(x * Scale), OffsetY + (int)(y * Scale),
        Math.Max(1, (int)(width * Scale)), Math.Max(1, (int)(height * Scale)));
    public Rectangle LocalRect(int x, int y, int width, int height) => new(
        (int)(x * Scale), (int)(y * Scale), Math.Max(1, (int)(width * Scale)), Math.Max(1, (int)(height * Scale)));
    public int FontSize(int desired) => Math.Max(desired >= 18 ? 12 : 10, (int)Math.Round(desired * Math.Min(1f, Scale)));
    // Relative seat order is preserved clockwise; every client's own seat is at the bottom.
    public static int Slot(int seat, int localSeat) => (seat - localSeat + 6) % 6;
    public static Point Center(int slot) => slot switch
    {
        0 => new(500, 498), 1 => new(150, 394), 2 => new(170, 132),
        3 => new(500, 76), 4 => new(830, 132), 5 => new(850, 394),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
    public static Point Cards(int slot) => slot switch
    {
        0 => new(445, 420), 1 => new(250, 393), 2 => new(260, 190),
        3 => new(445, 187), 4 => new(634, 190), 5 => new(634, 393),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
}
