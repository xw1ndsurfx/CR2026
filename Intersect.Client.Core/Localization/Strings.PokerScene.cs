using Intersect.Localization;

namespace Intersect.Client.Localization;

public static partial class Strings
{
    public static class PokerScene
    {
        public static LocalizedString Title = "POKER  /  {00}  /  Hand {01}";
        public static LocalizedString Pot = "POT  {00}";
        public static LocalizedString Dealer = "{00} - Dealer";
        public static LocalizedString You = "{00} - You";
        public static LocalizedString Progress = "Poker level {00}  |  {01} / {02} XP";
        public static LocalizedString Mastered = "Poker level {00} - Mastered";
        public static LocalizedString LevelUp = "POKER LEVEL {00}!";
        public static LocalizedString Back = "Card backs - B{00}";
        public static LocalizedString BackLevel = "B{00} - Lv. {01}";
        public static LocalizedString Locked = "This card back requires poker level {00}.";
        public static LocalizedString Saving = "Saving poker experience. The next hand will wait.";
        public static LocalizedString ProgressError = "Poker progression is unavailable. Please try again later.";
        public static LocalizedString TestProgress = "Test chips and test progression only. No Aureons are taken or paid.";
        public static LocalizedString Net = "+{00} net";
        public static LocalizedString Wins = "{00} wins";
        public static Dictionary<string, LocalizedString> Actions = new()
        {
            ["deal"] = "Deals the cards", ["fold"] = "Folds", ["check"] = "Checks",
            ["call"] = "Calls {00}", ["raise"] = "Raises to {00}", ["wins"] = "Wins +{00} net",
            ["leave"] = "Leaves the table",
        };
    }
}
