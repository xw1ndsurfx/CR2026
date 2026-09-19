using System;

namespace Intersect.Framework.Core.MiniGames;

/// <summary>Original tavern characters. Optional portraits live beside the card PNGs.</summary>
public static class PokerTableTheme
{
    public const string DealerName = "Marlow";
    public static string GuestName(int index) => index switch
    { 1 => "Nora", 2 => "Silas", 3 => "Iris", 4 => "Bastien", 5 => "Oren", _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static int Portrait(string name, bool dealer)
    {
        if (dealer) return 0;
        for (var index = 1; index <= 5; ++index) if (GuestName(index) == name) return index;
        return 1;
    }
    public static string PortraitFile(int index) => index == 0 ? "poker_dealer.png" : "poker_npc_" + index + ".png";
}
