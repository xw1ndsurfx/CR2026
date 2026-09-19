using Intersect.Localization;

namespace Intersect.Client.Localization;

public static partial class Strings
{
    public static class PokerCurrency
    {
        public static LocalizedString Funded = "Currency: {00} | At table: {01} | Balance returned after leaving and settling the hand.";
        public static LocalizedString Stack = "Balance: {00}  Bet: {01}";
        public static LocalizedString Net = "+{00} {01} net";
        public static LocalizedString Pending = "Saving table balances. Please wait.";
    }
}
