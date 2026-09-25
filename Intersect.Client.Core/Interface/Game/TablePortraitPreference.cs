using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Session-local avatar choice shared by the Poker and Blackjack table scenes.
/// The selector intentionally reuses the existing tavern portrait artwork and does
/// not change game rules, progression, balances, or network identity.
/// </summary>
internal static class TablePortraitPreference
{
    public const int Count = 6;

    private static int _selectedId;

    public static int SelectedId
    {
        get => _selectedId;
        set => _selectedId = Math.Clamp(value, 0, Count - 1);
    }

    public static string FileName(int id) => id switch
    {
        0 => "poker_player.png",
        1 => "poker_npc_1.png",
        2 => "poker_npc_2.png",
        3 => "poker_npc_3.png",
        4 => "poker_npc_4.png",
        5 => "poker_npc_5.png",
        _ => "poker_player.png",
    };

    public static IGameTexture? Texture(int id) =>
        GameContentManager.Current?.GetTexture(TextureType.Misc, FileName(id));

    public static IGameTexture? SelectedTexture() =>
        Texture(SelectedId) ?? Texture(0);
}
