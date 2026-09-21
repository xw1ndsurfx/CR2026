namespace Intersect.Client.MiniGames;

public readonly record struct PokerSceneRect(int X, int Y, int Width, int Height);

/// <summary>Renderer-independent coordinates shared by the scene, cards and layout tests.</summary>
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
    public PokerSceneRect Rect(int x, int y, int width, int height) => new(
        OffsetX + (int)(x * Scale), OffsetY + (int)(y * Scale),
        Math.Max(1, (int)(width * Scale)), Math.Max(1, (int)(height * Scale)));
    public PokerSceneRect LocalRect(int x, int y, int width, int height) => new(
        (int)(x * Scale), (int)(y * Scale), Math.Max(1, (int)(width * Scale)), Math.Max(1, (int)(height * Scale)));
    public int FontSize(int desired) => Math.Max(desired >= 18 ? 12 : 10, (int)Math.Round(desired * Math.Min(1f, Scale)));
    public static int Slot(int seat, int localSeat) => (seat - localSeat + 6) % 6;
    public static Point Center(int slot) => slot switch
    {
        0 => new(500, 480), 1 => new(150, 394), 2 => new(170, 132),
        3 => new(500, 76), 4 => new(830, 132), 5 => new(850, 394),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
    public static Point Cards(int slot) => slot switch
    {
        0 => new(445, 420), 1 => new(250, 393), 2 => new(260, 190),
        3 => new(445, 177), 4 => new(634, 190), 5 => new(634, 393),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
}
