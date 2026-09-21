using System.ComponentModel;

namespace Intersect.Framework.Core.MiniGames;

public sealed record PokerEffectSettings
{
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CheckAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid CallAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid RaiseAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid FoldAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid AllInAnimationId { get; set; }
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")] public Guid LevelUpAnimationId { get; set; }

    [DefaultValue("")] public string DealSound { get; set; } = "";
    [DefaultValue("")] public string CheckSound { get; set; } = "";
    [DefaultValue("")] public string CallSound { get; set; } = "";
    [DefaultValue("")] public string RaiseSound { get; set; } = "";
    [DefaultValue("")] public string FoldSound { get; set; } = "";
    [DefaultValue("")] public string AllInSound { get; set; } = "";
    [DefaultValue("")] public string WinSound { get; set; } = "";
    [DefaultValue("")] public string LevelUpSound { get; set; } = "";

    public static PokerEffectSettings Empty => new();

    public bool IsValid() =>
        ValidSound(DealSound) && ValidSound(CheckSound) && ValidSound(CallSound) &&
        ValidSound(RaiseSound) && ValidSound(FoldSound) && ValidSound(AllInSound) &&
        ValidSound(WinSound) && ValidSound(LevelUpSound);

    private static bool ValidSound(string? value) =>
        string.IsNullOrEmpty(value) ||
        value.Length <= 128 && !value.Contains('/') && !value.Contains('\\') &&
        !value.Contains(':') && !value.Contains("..", StringComparison.Ordinal);
}
