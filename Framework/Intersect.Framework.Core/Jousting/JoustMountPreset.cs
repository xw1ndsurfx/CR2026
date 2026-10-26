using Intersect.Framework.Core.Mounts;
namespace Intersect.Framework.Core.Jousting;
public static class JoustMountPreset
{
    public static readonly Guid WilliamId = new("249701dc-763c-40f0-adb7-6f1d332c398b");
    public static MountDefinition William() => new() {
        Id = WilliamId, Name = "William", Starter = false, AllowIndoors = true, IntegratedRider = true,
        Idle = "jousting/William/Tales_Knight_William_Idle.png", Start = "jousting/William/Tales_Knight_William_Move_1_Start.png",
        Loop = "jousting/William/Tales_Knight_William_Move_2_Loop.png", End = "jousting/William/Tales_Knight_William_Move_3_End.png",
        FrameWidth = 128, FrameHeight = 128, IdleFrames = 4, StartFrames = 6, LoopFrames = 6, EndFrames = 6,
        FrameMilliseconds = 100, SpeedMultiplier = 1.35, GroundX = 64, GroundY = 80
    };
}
