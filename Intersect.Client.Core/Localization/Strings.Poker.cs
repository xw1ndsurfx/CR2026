using Intersect.Localization;

namespace Intersect.Client.Localization;

public static partial class Strings
{
    public static class Poker
    {
        public static LocalizedString Title = "Poker - test chips";
        public static LocalizedString TableInfo = "Table: {00}   |   Hand: {01}   |   Pot: {02}";
        public static LocalizedString Start = "Start hand";
        public static LocalizedString Fold = "Fold";
        public static LocalizedString Check = "Check";
        public static LocalizedString Call = "Call {00}";
        public static LocalizedString Raise = "Raise / Bet";
        public static LocalizedString AllIn = "All-in";
        public static LocalizedString Refresh = "Refresh";
        public static LocalizedString Leave = "Leave table";
        public static LocalizedString Minimum = "Minimum";
        public static LocalizedString RaiseTotal = "Total street wager:";
        public static LocalizedString OwnCards = "Your cards: {00}";
        public static LocalizedString EmptySeat = "Seat {00}: empty";
        public static LocalizedString Stack = "Chips: {00}   Bet: {01}";
        public static LocalizedString Waiting = "Waiting for next hand";
        public static LocalizedString Folded = "Folded";
        public static LocalizedString Leaving = "Leaving after this hand";
        public static LocalizedString Playing = "In hand";
        public static LocalizedString YourTurn = "Your turn - {00}s";
        public static LocalizedString OtherTurn = "Waiting for another player - {00}s";
        public static LocalizedString NeedPlayers = "At least two funded players are needed. Press Start hand when ready.";
        public static LocalizedString Ready = "Press Start hand when ready.";
        public static LocalizedString AutomaticNext = "The next hand starts automatically after a short pause.";
        public static LocalizedString NoChips = "No test chips left. Leave and rejoin to test again.";
        public static LocalizedString DealerName = "Dealer";
        public static LocalizedString NpcSuffix = " [NPC]";
        public static LocalizedString ArtLegend = "C = clubs, D = diamonds, H = hearts, S = spades. (B) = rotating betting button.";
        public static LocalizedString Pending = "Waiting for the server...";
        public static LocalizedString TestOnly = "Temporary test chips only. No game currency, items or rewards.";
        public static LocalizedString Legend = "Cards: C = clubs, D = diamonds, H = hearts, S = spades. D beside a name = dealer.";
        public static LocalizedString Paid = "Winnings: {00}";
        public static LocalizedString InvalidAmount = "Enter a valid total wager. Use Minimum or All-in for a short stack.";
        public static LocalizedString Rejected = "Action refused: {00}";
        public static Dictionary<int, LocalizedString> Stages = new()
        {
            [0] = "Waiting", [1] = "Pre-flop", [2] = "Flop", [3] = "Turn", [4] = "River", [5] = "Hand finished",
        };
        public static Dictionary<string, LocalizedString> Errors = new()
        {
            ["StaleState"] = "The table changed. Review the new state before acting again.",
            ["NotYourTurn"] = "It is not your turn.",
            ["NotEnoughPlayers"] = "At least two players with chips are required.",
            ["HandInProgress"] = "A hand is already in progress.",
            ["IllegalAction"] = "That action is not available now.",
            ["InvalidAmount"] = "That wager is not allowed.",
            ["NoActiveHand"] = "No hand is in progress.",
            ["NotSeated"] = "You are no longer seated.",
            ["TableClosed"] = "You left the table or are no longer on the same map.",
        };
    }
}
