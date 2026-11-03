namespace Intersect.Framework.Core.Fishing;

public enum FishingWorldAction { Stop, Idle, Throw, Fishing, Catch, Move }

public sealed class FishingWorldClip
{
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";
    public int Frames { get; set; } = 4;
    public bool Loop { get; set; } = true;
    public string BodyAnimation { get; set; } = "Normal";
}

/// <summary>Equipment-only layers drawn around the player's existing body and appearance.</summary>
public sealed class FishingWorldProfile
{
    public bool Enabled { get; set; }
    public int FrameWidth { get; set; } = 128;
    public int FrameHeight { get; set; } = 128;
    public int FramesPerSecond { get; set; } = 8;
    public double Scale { get; set; } = 1;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int[] DirectionRows { get; set; } = [0, 1, 2, 3];
    public Dictionary<FishingWorldAction, FishingWorldClip> Clips { get; set; } = [];

    public static FishingWorldProfile Net() => new()
    {
        Enabled = true,
        Clips = new()
        {
            [FishingWorldAction.Idle] = new() { Front = "fishing/paperdolls/F_Idle_1H_Tools_Fishing_Net.png" },
            [FishingWorldAction.Throw] = new() { Front = "fishing/paperdolls/F_Throw_1H_Tools_Fishing_Net.png",
                Back = "fishing/paperdolls/B_Throw_1H_Tools_Fishing_Net.png", Frames = 6, Loop = false, BodyAnimation = "Cast" },
            [FishingWorldAction.Fishing] = new() { Front = "fishing/paperdolls/F_Fishing_1H_Tools_Fishing_Net.png",
                Back = "fishing/paperdolls/B_Fishing_1H_Tools_Fishing_Net.png", BodyAnimation = "Weapon" },
            [FishingWorldAction.Catch] = new() { Front = "fishing/paperdolls/F_Catch_1H_Tools_Fishing_Net.png",
                Back = "fishing/paperdolls/B_Catch_1H_Tools_Fishing_Net-sheet.png", Frames = 6, Loop = false, BodyAnimation = "Cast" },
            [FishingWorldAction.Move] = new() { Front = "fishing/paperdolls/F_Move_1H_Tools_Fishing_Net.png",
                Back = "fishing/paperdolls/B_Move_1H_Tools_Fishing_Net.png", Frames = 6 },
        },
    };

    public void Validate()
    {
        if (FrameWidth is < 16 or > 1024 || FrameHeight is < 16 or > 1024 || FramesPerSecond is < 1 or > 60 ||
            !double.IsFinite(Scale) || Scale is < .1 or > 4 || OffsetX is < -512 or > 512 || OffsetY is < -512 or > 512 ||
            DirectionRows is not { Length: 4 } || DirectionRows.Any(r => r is < 0 or > 7) || Clips == null || Clips.Count > 6 ||
            Clips.Any(p => !Enum.IsDefined(p.Key) || p.Value == null || p.Value.Frames is < 1 or > 32 ||
                !FishingRules.SafeAssetPath(p.Value.Front, ".png") || !FishingRules.SafeAssetPath(p.Value.Back, ".png") ||
                p.Value.BodyAnimation is not ("Normal" or "Idle" or "Attack" or "Shoot" or "Cast" or "Weapon")))
            throw new InvalidDataException("Invalid fishing world animation profile.");
    }
}
