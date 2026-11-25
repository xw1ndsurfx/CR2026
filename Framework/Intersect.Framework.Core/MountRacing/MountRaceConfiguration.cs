using System.Text.Json;
using Intersect.Enums;

namespace Intersect.Framework.Core.MountRacing;

public sealed class RacePoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Radius { get; set; }
    public bool Contains(int x, int y) => Math.Abs(X - x) <= Radius && Math.Abs(Y - y) <= Radius;
    public bool IsValid => X is >= 0 and <= 255 && Y is >= 0 and <= 255 && Radius is >= 0 and <= 2;
}

public sealed class RacePrize
{
    public int Place { get; set; } = 1;
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsValid => Place is >= 1 and <= 16 && ItemId != Guid.Empty && Quantity is >= 1 and <= 100000;
}

public sealed class MountRaceCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; }
    public string Name { get; set; } = "New race circuit";
    public string Description { get; set; } = "";
    public Guid RegistrationMapId { get; set; }
    public int RegistrationX { get; set; }
    public int RegistrationY { get; set; }
    public Guid MapId { get; set; }
    public int StartX { get; set; } = 4;
    public int StartY { get; set; } = 4;
    public Direction Direction { get; set; } = Direction.Right;
    // The provided mount is temporary; it never unlocks or replaces an owned mount.
    public Guid MountId { get; set; } = Guid.Parse("67ad317d-cc3f-4aa6-9a61-8d62425d9fa4");
    public int Laps { get; set; } = 3;
    public int MinimumPlayers { get; set; } = 2;
    public int MaximumPlayers { get; set; } = 8;
    public int MovementMilliseconds { get; set; } = 180;
    public int ReadySeconds { get; set; } = 45;
    public int CountdownSeconds { get; set; } = 5;
    public int TimeLimitSeconds { get; set; } = 300;
    public int QueueSeconds { get; set; } = 20;
    public int RewardCooldownSeconds { get; set; } = 300;
    public bool AllowPractice { get; set; } = true;
    // Last checkpoint is the finish line. After it, the next lap starts at index zero.
    public RacePoint[] Checkpoints { get; set; } = [new() { X = 15, Y = 4 }, new() { X = 15, Y = 15 }, new() { X = 4, Y = 15 }, new() { X = 4, Y = 4 }];
    public RacePrize[] Prizes { get; set; } = [];
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 128 && Description is { Length: <= 2000 } &&
        (!Enabled || RegistrationMapId != Guid.Empty && MapId != Guid.Empty && MountId != Guid.Empty) &&
        new[] { StartX, StartY, RegistrationX, RegistrationY }.All(n => n is >= 0 and <= 255) && Enum.IsDefined(Direction) && Direction != Direction.None &&
        Laps is >= 1 and <= 20 && MinimumPlayers is >= 2 and <= 16 && MaximumPlayers >= MinimumPlayers && MaximumPlayers <= 16 &&
        MovementMilliseconds is >= 100 and <= 500 && ReadySeconds is >= 15 and <= 120 && CountdownSeconds is >= 3 and <= 10 &&
        TimeLimitSeconds is >= 30 and <= 1800 && QueueSeconds is >= 5 and <= 120 && RewardCooldownSeconds is >= 0 and <= 86400 &&
        Checkpoints is { Length: >= 3 and <= 64 } && Checkpoints.All(p => p is { IsValid: true }) &&
        Checkpoints.Select(p => (p.X, p.Y)).Distinct().Count() == Checkpoints.Length &&
        Checkpoints[^1].X == StartX && Checkpoints[^1].Y == StartY &&
        Checkpoints.Select((p, i) => (p, next: Checkpoints[(i + 1) % Checkpoints.Length])).All(v =>
            Math.Max(Math.Abs(v.p.X - v.next.X), Math.Abs(v.p.Y - v.next.Y)) > v.p.Radius + v.next.Radius) &&
        Prizes is { Length: <= 16 } && Prizes.All(p => p is { IsValid: true } && p.Place <= MaximumPlayers) && Prizes.Select(p => p.Place).Distinct().Count() == Prizes.Length;
}

public sealed class MountRacePresentation
{
    public string Background { get; set; } = "racing_background_v1.png";
    public string Icons { get; set; } = "racing_icons_v1.png";
    public string OpenSound { get; set; } = "racing/open_v1.wav";
    public string ReadySound { get; set; } = "racing/ready_v1.wav";
    public string TickSound { get; set; } = "racing/countdown_v1.wav";
    public string StartSound { get; set; } = "racing/start_v1.wav";
    public string CheckpointSound { get; set; } = "racing/checkpoint_v1.wav";
    public string LapSound { get; set; } = "racing/lap_v1.wav";
    public string FinishSound { get; set; } = "racing/finish_v1.wav";
    public string RewardSound { get; set; } = "racing/reward_v1.wav";
    public string CancelSound { get; set; } = "racing/cancel_v1.wav";
    public bool IsValid => new[] { Background, Icons, OpenSound, ReadySound, TickSound, StartSound, CheckpointSound, LapSound, FinishSound, RewardSound, CancelSound }
        .All(Matchmaking.MatchmakingConfiguration.AssetName);
}

public sealed class MountRaceConfiguration
{
    public bool Enabled { get; set; } = true;
    public MountRaceCourse[] Courses { get; set; } = [];
    public MountRacePresentation Presentation { get; set; } = new();
    public bool IsValid => Courses is { Length: <= 128 } && Courses.All(c => c is { IsValid: true }) &&
        Courses.Select(c => c.Id).Distinct().Count() == Courses.Length && Presentation is { IsValid: true };
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public string ToJson() => JsonSerializer.Serialize(this, Options);
    public static MountRaceConfiguration FromJson(string json)
    {
        if (json.Length > 524288) throw new InvalidDataException("Invalid race configuration.");
        var config = JsonSerializer.Deserialize<MountRaceConfiguration>(json, Options);
        if (config is not { IsValid: true }) throw new InvalidDataException("Invalid race configuration.");
        return config;
    }
}
