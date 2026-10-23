using Intersect.Framework.Core.Mounts;
namespace Intersect.Client.Entities;
public partial class Player
{
    public MountDefinition? ActiveMount { get; private set; }
    private string _mountJson = "";
    internal void ApplyMountJson(string json)
    {
        if (_mountJson == json) return;
        _mountJson = json;
        try { ActiveMount = string.IsNullOrEmpty(json) ? null : MountConfiguration.FromJson(json).Mounts.SingleOrDefault(); }
        catch { ActiveMount = null; }
    }
    public override float GetMovementTime() => base.GetMovementTime() / (float)(ActiveMount?.SpeedMultiplier ?? 1);
}
