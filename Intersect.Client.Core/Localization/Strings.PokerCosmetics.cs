using Intersect.Localization;

namespace Intersect.Client.Localization;

public static partial class Strings
{
    public static class PokerCosmetics
    {
        public static LocalizedString ChangeBack = "Card back: {00} (change)";
        public static LocalizedString NextHand = "Applies to your next hand.";
        public static LocalizedString Selected = "Visible to the other players.";
        public static LocalizedString MissingArt = "Artwork missing: showing the classic back.";
        public static LocalizedString NetWin = "Your net gain: +{00}";
        public static Dictionary<int, LocalizedString> Backs = new()
        {
            [0] = "Classic", [1] = "Royal", [2] = "Pirate", [3] = "Halloween",
        };
    }
}
