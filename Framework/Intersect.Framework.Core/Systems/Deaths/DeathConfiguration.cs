using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.Systems.Deaths;

public sealed class DeathConfiguration
{
    public List<DeathRule> Rules { get; set; } = [];
    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);
    public static DeathConfiguration FromJson(string json)
    {
        var value = JsonConvert.DeserializeObject<DeathConfiguration>(json) ?? throw new InvalidDataException("Invalid death configuration.");
        if (value.Rules == null || value.Rules.Count > 1000) throw new InvalidDataException("Invalid death rules.");
        var maps = new HashSet<Guid>();
        foreach (var rule in value.Rules)
        {
            if (rule == null || rule.MapIds == null || rule.MapIds.Count == 0 || rule.RespawnMapId == Guid.Empty ||
                rule.MapIds.Any(id => id == Guid.Empty || !maps.Add(id)) ||
                rule.X < 0 || rule.Y < 0 || rule.X >= Options.Instance.Map.MapWidth || rule.Y >= Options.Instance.Map.MapHeight ||
                !Enum.IsDefined(rule.Direction) || rule.EventTimeoutSeconds is < 1 or > 600)
                throw new InvalidDataException("Choose unique maps, a valid respawn location and a common-event timeout between 1 and 600 seconds.");
        }
        return value;
    }
    public DeathRule? Find(Guid mapId) => Rules.FirstOrDefault(rule => rule.MapIds.Contains(mapId));
    public static long AnimationDuration(AnimationDescriptor? descriptor)
    {
        static long Duration(AnimationLayer layer) => string.IsNullOrWhiteSpace(layer.Sprite) ? 0 :
            (long)Math.Clamp((decimal)Math.Max(1, Math.Min(layer.FrameCount, (long)layer.XFrames * layer.YFrames)) * Math.Max(1, layer.FrameSpeed) * (Math.Max(0L, layer.LoopCount) + 1), 1, long.MaxValue);
        return descriptor == null ? 0 : Math.Max(Duration(descriptor.Lower), Duration(descriptor.Upper));
    }
}

public sealed class DeathRule
{
    public string Name { get; set; } = "Death rule";
    public List<Guid> MapIds { get; set; } = [];
    public Guid CommonEventId { get; set; }
    public Guid AnimationId { get; set; }
    public Guid RespawnMapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; } = Direction.Down;
    public int EventTimeoutSeconds { get; set; } = 120;
}
