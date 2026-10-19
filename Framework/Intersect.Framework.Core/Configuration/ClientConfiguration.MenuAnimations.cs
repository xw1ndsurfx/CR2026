using Intersect.Framework.Core.GameObjects.Animations;
namespace Intersect.Configuration;

public sealed class MenuAnimationDefinition
{
    public Guid AnimationId
    {
        get; set;
    }
    public int XPercent { get; set; } = 50;
    public int YPercent { get; set; } = 50;
    public int ScalePercent { get; set; } = 100;
    public int TransparencyPercent
    {
        get; set;
    }
    public bool Loop { get; set; } = true;
    public bool Foreground
    {
        get; set;
    }
    // Snapshots let the login menu animate before any game-data/login packets.
    public AnimationLayer? Lower
    {
        get; set;
    }
    public AnimationLayer? Upper
    {
        get; set;
    }
}
public sealed partial class ClientConfiguration
{
    public List<MenuAnimationDefinition> MenuAnimations { get; set; } = [];
}
