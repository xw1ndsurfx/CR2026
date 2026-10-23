using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Mounts;

public enum MountPhase { Idle, Start, Loop, End }

public sealed class MountSeat
{
    public double X { get; set; } = 64;
    public double Y { get; set; } = 56;
    public double ForegroundY { get; set; } = 60;
    public double ClipY { get; set; } = 65;
}

public sealed class MountFrameOffset
{
    public MountPhase Phase { get; set; }
    public int Direction { get; set; }
    public int Frame { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class MountDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New mount";
    public string Description { get; set; } = "";
    public bool Starter { get; set; }
    public Guid UnlockItemId { get; set; }
    public bool AllowIndoors { get; set; }
    public string Idle { get; set; } = "";
    public string Start { get; set; } = "";
    public string Loop { get; set; } = "";
    public string End { get; set; } = "";
    public int FrameWidth { get; set; } = 128;
    public int FrameHeight { get; set; } = 128;
    public int IdleFrames { get; set; } = 4;
    public int StartFrames { get; set; } = 6;
    public int LoopFrames { get; set; } = 6;
    public int EndFrames { get; set; } = 6;
    public int FrameMilliseconds { get; set; } = 100;
    public double SpeedMultiplier { get; set; } = 1.35;
    public double Scale { get; set; } = 1;
    public double GroundX { get; set; } = 64;
    public double GroundY { get; set; } = 80;
    public double RiderScale { get; set; } = 1;
    public double HipRatio { get; set; } = .65;
    public double Bob { get; set; } = 1.5;
    public bool ClipLegs { get; set; } = true;
    public MountSeat[] Seats { get; set; } = [new() { Y = 54, ForegroundY = 40, ClipY = 62 }, new(), new(), new() { Y = 52, ForegroundY = 64 }];
    public MountFrameOffset[] FrameOffsets { get; set; } = [];
    public string Sheet(MountPhase phase) => phase switch { MountPhase.Start => Start, MountPhase.Loop => Loop, MountPhase.End => End, _ => Idle };
    public int Frames(MountPhase phase) => phase switch { MountPhase.Start => StartFrames, MountPhase.Loop => LoopFrames, MountPhase.End => EndFrames, _ => IdleFrames };
    public MountFrameOffset? Offset(MountPhase phase, int row, int frame) => FrameOffsets.FirstOrDefault(p => p.Phase == phase && p.Direction == row && p.Frame == frame);
    public static bool SafeAsset(string value) => value.Length <= 255 && !Path.IsPathRooted(value) && !value.Contains(':') &&
        !value.Contains('\\') && !value.Split('/').Any(p => p is ".." or ".") && (value.Length == 0 || value.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    private static bool Between(double value, double low, double high) => double.IsFinite(value) && value >= low && value <= high;
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 128 && Description != null && Description.Length <= 2000 &&
        new[] { Idle, Start, Loop, End }.All(p => p != null && SafeAsset(p)) && Idle.Length > 0 && Loop.Length > 0 &&
        FrameWidth is >= 8 and <= 1024 && FrameHeight is >= 8 and <= 1024 &&
        new[] { IdleFrames, StartFrames, LoopFrames, EndFrames }.All(n => n is >= 1 and <= 64) && FrameMilliseconds is >= 30 and <= 1000 &&
        Between(SpeedMultiplier, 1, 2) && Between(Scale, .25, 4) && Between(GroundX, 0, FrameWidth) && Between(GroundY, 0, FrameHeight) &&
        Between(RiderScale, .25, 3) && Between(HipRatio, .2, 1) && Between(Bob, 0, 12) &&
        Seats is { Length: 4 } && Seats.All(p => p != null && Between(p.X, -1024, 2048) && Between(p.Y, -1024, 2048) &&
            Between(p.ForegroundY, 0, FrameHeight) && Between(p.ClipY, 0, FrameHeight)) &&
        FrameOffsets is { Length: <= 1024 } && FrameOffsets.All(p => p != null && Enum.IsDefined(p.Phase) && p.Direction is >= 0 and < 4 &&
            p.Frame >= 0 && p.Frame < Frames(p.Phase) && Between(p.X, -128, 128) && Between(p.Y, -128, 128)) &&
        FrameOffsets.Select(p => (p.Phase, p.Direction, p.Frame)).Distinct().Count() == FrameOffsets.Length;
}

public sealed class MountConfiguration
{
    public bool Enabled { get; set; } = true;
    public MountDefinition[] Mounts { get; set; } = [];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    [JsonIgnore] public bool IsValid => Mounts is { Length: <= 256 } && Mounts.All(m => m is { IsValid: true }) && Mounts.Select(m => m.Id).Distinct().Count() == Mounts.Length;
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static MountConfiguration FromJson(string json)
    {
        if (json.Length > 2_000_000) throw new InvalidDataException("Mount configuration is too large.");
        var config = JsonSerializer.Deserialize<MountConfiguration>(json, JsonOptions) ?? throw new InvalidDataException("Invalid mount configuration.");
        if (!config.IsValid) throw new InvalidDataException("Invalid mount configuration.");
        return config;
    }
    public static MountConfiguration Prototype() => new() { Mounts = [new()
    {
        Id = Guid.Parse("67ad317d-cc3f-4aa6-9a61-8d62425d9fa4"), Name = "Farm horse", Starter = true,
        Idle = "mounts/prototype_horse/Farm_Horse_Adult_Idle.png",
        Start = "mounts/prototype_horse/Farm_Horse_Adult_Move_1_Start.png",
        Loop = "mounts/prototype_horse/Farm_Horse_Adult_Move_2_Loop.png",
        End = "mounts/prototype_horse/Farm_Horse_Adult_Move_3_End.png"
    }] };
}

/// <summary>Per-entity clock: start/end play once, movement and idle loop.</summary>
public sealed class MountAnimationClock
{
    private bool _moving;
    private long _since;
    private MountPhase _phase;
    public (MountPhase Phase, int Frame) Sample(MountDefinition mount, bool moving, long now)
    {
        if (now < _since) _since = now;
        if (moving != _moving)
        {
            _moving = moving; _since = now;
            _phase = moving ? MountPhase.Start : MountPhase.End;
        }
        var frames = mount.Frames(_phase);
        var elapsed = Math.Max(0, now - _since);
        if (_phase is MountPhase.Start or MountPhase.End && elapsed >= frames * mount.FrameMilliseconds)
        {
            _since += frames * mount.FrameMilliseconds;
            _phase = moving ? MountPhase.Loop : MountPhase.Idle;
            elapsed = Math.Max(0, now - _since); frames = mount.Frames(_phase);
        }
        return (_phase, (int)(elapsed / mount.FrameMilliseconds % frames));
    }
}

public readonly record struct MountRiderTransform(double Scale, double OffsetX, double OffsetY, double ClipY)
{
    public static MountRiderTransform AtSeat(MountDefinition mount, int row, MountPhase phase, int frame,
        double mountX, double mountY, double bodyX, double bodyY, double visibleX, double visibleY, double visibleWidth, double visibleHeight)
    {
        var seat = mount.Seats[row]; var correction = mount.Offset(phase, row, frame);
        var bob = phase is MountPhase.Idle ? 0 : Math.Sin(frame * Math.PI * 2 / mount.Frames(phase)) * mount.Bob;
        var x = mountX + (seat.X + (correction?.X ?? 0)) * mount.Scale;
        var y = mountY + (seat.Y + (correction?.Y ?? 0) + bob) * mount.Scale;
        return new(mount.RiderScale, x - (bodyX + visibleX + visibleWidth / 2) * mount.RiderScale,
            y - (bodyY + visibleY + visibleHeight * mount.HipRatio) * mount.RiderScale,
            mount.ClipLegs ? mountY + seat.ClipY * mount.Scale : double.PositiveInfinity);
    }
}
