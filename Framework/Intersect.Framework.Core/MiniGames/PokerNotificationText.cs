namespace Intersect.Framework.Core.MiniGames;

public static class PokerNotificationText
{
    public const int MaximumQueuedMessages = 24;

    public static string NpcAction(string? name, string? action, long amount, string? currency, bool automatic)
    {
        name = Clean(name, "NPC");
        action = Clean(action, "acts").ToLowerInvariant();
        currency = Clean(currency, "chips");
        var suffix = automatic ? " (auto)" : "";
        return action switch
        {
            "deal" => $"[Poker] {name}: deals.{suffix}",
            "fold" => $"[Poker] {name}: folds.{suffix}",
            "check" => $"[Poker] {name}: checks.{suffix}",
            "call" => amount > 0 ? $"[Poker] {name}: calls {amount} {currency}.{suffix}" : $"[Poker] {name}: calls.{suffix}",
            "raise" => $"[Poker] {name}: raises to {amount} {currency}.{suffix}",
            "leave" => $"[Poker] {name}: leaves the table.{suffix}",
            "wins" => $"[Poker] {name} wins {amount} {currency}.",
            _ => $"[Poker] {name}: {action}.{suffix}",
        };
    }

    public static string LevelUp(int level) => $"[Poker] Level up! You reached Poker level {Math.Max(1, level)}.";

    public static string Reward(int level, long quantity, string? itemName) =>
        $"[Poker] Level {Math.Max(1, level)} reward: {Math.Max(0, quantity):N0} x {Clean(itemName, "item")}.";

    public static string GlobalWin(string? name, long amount, string? currency) =>
        $"[Poker] {Clean(name, "Player")} wins {Math.Max(0, amount)} {Clean(currency, "chips")} (net gain).";

    private static string Clean(string? value, string fallback)
    {
        var clean = new string((value ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }
}
