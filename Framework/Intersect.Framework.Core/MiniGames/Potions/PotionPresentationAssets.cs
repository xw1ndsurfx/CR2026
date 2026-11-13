using MessagePack;

namespace Intersect.Framework.Core.MiniGames.Potions;

/// <summary>Presentation only; empty filenames disable an asset. Atlas rows are families, columns are tiers.</summary>
[MessagePackObject]
public sealed record PotionPresentationAssets
{
    [Key(0)] public string Background { get; init; } = "royal_alchemist_background_v1.png";
    [Key(1)] public string PieceAtlas { get; init; } = "royal_alchemist_pieces_v2.png";
    [Key(2)] public string RotateSound { get; init; } = "royal_alchemist_rotate_v1.wav";
    [Key(3)] public string DropSound { get; init; } = "royal_alchemist_drop_v1.wav";
    [Key(4)] public string MergeSound { get; init; } = "royal_alchemist_merge_v1.wav";
    [Key(5)] public string CompleteSound { get; init; } = "royal_alchemist_complete_v1.wav";
    [Key(6)] public string GameOverSound { get; init; } = "royal_alchemist_game_over_v1.wav";

    [IgnoreMember]
    public bool IsValid => IsFile(Background, ".png") && IsFile(PieceAtlas, ".png") &&
        new[] { RotateSound, DropSound, MergeSound, CompleteSound, GameOverSound }.All(s => IsFile(s, ".wav"));

    private static bool IsFile(string? file, string extension) => file != null && (file.Length == 0 ||
        file.Length <= 128 && !file.Contains("..") && file.IndexOfAny(['/', '\\', ':', '\0']) < 0 &&
        !file.Any(char.IsControl) && file.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
