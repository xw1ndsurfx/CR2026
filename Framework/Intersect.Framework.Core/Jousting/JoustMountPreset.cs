using Intersect.Framework.Core.Mounts;
namespace Intersect.Framework.Core.Jousting;
public static class JoustMountPreset
{
    public static readonly Guid NightId = new("77c1ca15-1226-4ab6-a479-7e2db33e77ef");
    public static MountDefinition Opponent(JoustAiOpponent kind)
    {
        var mount = William();
        if (kind == JoustAiOpponent.Night)
        {
            mount.Id = NightId; mount.Name = "Tale Knight Night";
            mount.Idle = "jousting/Tales-Knight-Night/Tales_Knight_Night_Idle.png";
            mount.Start = "jousting/Tales-Knight-Night/Tales_Knight_Night_Move_1_Start.png";
            mount.Loop = "jousting/Tales-Knight-Night/Tales_Knight_Night_Move_2_Loop.png";
            mount.End = "jousting/Tales-Knight-Night/Tales_Knight_Night_Move_3_End.png";
        }
        return mount;
    }
    public static readonly Guid WilliamId = new("249701dc-763c-40f0-adb7-6f1d332c398b");
    public static MountDefinition William() => new() {
        Id = WilliamId, Name = "William", Starter = false, AllowIndoors = true, IntegratedRider = true,
        Idle = "jousting/William/Tales_Knight_William_Idle.png", Start = "jousting/William/Tales_Knight_William_Move_1_Start.png",
        Loop = "jousting/William/Tales_Knight_William_Move_2_Loop.png", End = "jousting/William/Tales_Knight_William_Move_3_End.png",
        FrameWidth = 128, FrameHeight = 128, IdleFrames = 4, StartFrames = 6, LoopFrames = 6, EndFrames = 6,
        FrameMilliseconds = 100, SpeedMultiplier = 1.35, GroundX = 64, GroundY = 80
    };
}
