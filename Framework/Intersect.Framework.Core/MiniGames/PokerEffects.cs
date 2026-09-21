namespace Intersect.Framework.Core.MiniGames;

public enum PokerEffectKind
{
    Deal = 0,
    Check = 1,
    Call = 2,
    Raise = 3,
    Fold = 4,
    AllIn = 5,
    Win = 6,
    Lose = 7,
    LevelUp = 8,
}

public sealed record PokerEffect(Guid AnimationId = default, string Sound = "")
{
    public static readonly PokerEffect None = new();
    public bool IsValid => Sound != null && Sound.Length <= 128 &&
        !Sound.Contains('/') && !Sound.Contains('\\') && !Sound.Contains("..", StringComparison.Ordinal);
}

public sealed record PokerEffects(
    PokerEffect? Deal = null,
    PokerEffect? Check = null,
    PokerEffect? Call = null,
    PokerEffect? Raise = null,
    PokerEffect? Fold = null,
    PokerEffect? AllIn = null,
    PokerEffect? Win = null,
    PokerEffect? Lose = null,
    PokerEffect? LevelUp = null)
{
    public static readonly PokerEffects Empty = new();

    public PokerEffect Get(PokerEffectKind kind) => kind switch
    {
        PokerEffectKind.Deal => Deal ?? PokerEffect.None,
        PokerEffectKind.Check => Check ?? PokerEffect.None,
        PokerEffectKind.Call => Call ?? PokerEffect.None,
        PokerEffectKind.Raise => Raise ?? PokerEffect.None,
        PokerEffectKind.Fold => Fold ?? PokerEffect.None,
        PokerEffectKind.AllIn => AllIn ?? PokerEffect.None,
        PokerEffectKind.Win => Win ?? PokerEffect.None,
        PokerEffectKind.Lose => Lose ?? PokerEffect.None,
        PokerEffectKind.LevelUp => LevelUp ?? PokerEffect.None,
        _ => PokerEffect.None,
    };

    public bool IsValid => Enum.GetValues<PokerEffectKind>().All(kind => Get(kind).IsValid);
}
