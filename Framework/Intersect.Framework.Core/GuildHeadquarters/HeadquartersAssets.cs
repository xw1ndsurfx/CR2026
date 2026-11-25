namespace Intersect.Framework.Core.GuildHeadquarters;

public sealed class HeadquartersAssets
{
    public string Background { get; set; } = "guild_hq_hall_v1.png";
    public string BankIcon { get; set; } = "guild_hq_bank_v1.png";
    public string InvitationIcon { get; set; } = "guild_hq_invitation_v1.png";
    public string ThemeMusic { get; set; } = "guild_hq_theme_v1.ogg";
    public string AmbienceSound { get; set; } = "guild_hq_hearth_v1.wav";
    public string OpenSound { get; set; } = "guild_hq_open_v1.wav";
    public string EnterSound { get; set; } = "guild_hq_enter_v1.wav";
    public string LeaveSound { get; set; } = "guild_hq_leave_v1.wav";
    public string BankSound { get; set; } = "guild_hq_bank_v1.wav";
    public string InvitationSound { get; set; } = "guild_hq_invitation_v1.wav";
    public string UpgradeSound { get; set; } = "guild_hq_upgrade_v1.wav";
    public string ErrorSound { get; set; } = "guild_hq_error_v1.wav";
    public int EffectVolume { get; set; } = 65;
    public int AmbienceVolume { get; set; } = 25;
    public bool IsValid => new[] { Background, BankIcon, InvitationIcon }.All(f => File(f, ".png")) && File(ThemeMusic, ".ogg") &&
        new[] { AmbienceSound, OpenSound, EnterSound, LeaveSound, BankSound, InvitationSound, UpgradeSound, ErrorSound }.All(f => File(f, ".wav")) &&
        EffectVolume is >= 0 and <= 100 && AmbienceVolume is >= 0 and <= 100;
    public static bool File(string? file, string extension) => file != null && (file.Length == 0 || file.Length <= 128 &&
        !file.Contains("..") && file.IndexOfAny(['/', '\\', ':', '\0']) < 0 && !file.Any(char.IsControl) && file.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
