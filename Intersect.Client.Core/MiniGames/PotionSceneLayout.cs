namespace Intersect.Client.MiniGames;

/// <summary>Alchemy uses the entire viewport instead of the poker table's letterboxed scene.</summary>
public sealed class PotionSceneLayout
{
    private readonly float _x;
    private readonly float _y;
    public PotionSceneLayout(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        _x = width / 1000f;
        _y = height / 780f;
    }
    public PokerSceneRect Rect(int x, int y, int width, int height) => LocalRect(x, y, width, height);
    public PokerSceneRect LocalRect(int x, int y, int width, int height) => new(
        (int)(x * _x), (int)(y * _y), Math.Max(1, (int)(width * _x)), Math.Max(1, (int)(height * _y)));
    public int FontSize(int desired) => Math.Max(desired >= 18 ? 12 : 10,
        (int)Math.Round(desired * Math.Min(1.35f, Math.Min(_x, _y))));
}
