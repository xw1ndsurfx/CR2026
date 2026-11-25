using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Framework.Core.Matchmaking;

public enum MatchmakingActivity { Dungeon, Expedition }
public enum MatchmakingAction { Open, Poll, Enqueue, Cancel, Ready, Decline }
public enum MatchmakingStatus { Idle, Queued, Confirming, Launching, Departed, Cancelled }

public sealed class MatchmakingEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; }
    public MatchmakingActivity Activity { get; set; }
    public Guid ContentId { get; set; }
    public int TargetPlayers { get; set; } = 4;
    public int MaximumLevelDifference { get; set; }
    public Guid EntryMapId { get; set; }
    public int EntryX { get; set; }
    public int EntryY { get; set; }
    public WarpDirection EntryDirection { get; set; } = WarpDirection.Down;
    public string Image { get; set; } = "";

    public void Validate()
    {
        if (Id == Guid.Empty || !Enum.IsDefined(Activity) || !Enum.IsDefined(EntryDirection) ||
            TargetPlayers is < 1 or > 100 || MaximumLevelDifference is < 0 or > 1000 ||
            EntryX is < 0 or > 255 || EntryY is < 0 or > 255 || !MatchmakingConfiguration.AssetName(Image) ||
            Enabled && (ContentId == Guid.Empty || Activity == MatchmakingActivity.Dungeon && EntryMapId == Guid.Empty))
            throw new InvalidDataException("Invalid matchmaking entry.");
    }
}

public sealed class MatchmakingConfiguration
{
    public bool Enabled { get; set; } = true;
    public int ReadyTimeoutSeconds { get; set; } = 45;
    public int QueueTimeoutSeconds { get; set; } = 900;
    public int MaximumQueuedPlayers { get; set; } = 256;
    public List<MatchmakingEntry> Entries { get; set; } = [];
    public string BackgroundImage { get; set; } = "matchmaking_background_v1.png";
    public string IconsImage { get; set; } = "matchmaking_icons_v1.png";
    public string OpenSound { get; set; } = "matchmaking/open_v1.wav";
    public string QueuedSound { get; set; } = "matchmaking/queued_v1.wav";
    public string FoundSound { get; set; } = "matchmaking/found_v1.wav";
    public string ReadySound { get; set; } = "matchmaking/ready_v1.wav";
    public string CancelledSound { get; set; } = "matchmaking/cancelled_v1.wav";
    public string DepartSound { get; set; } = "matchmaking/depart_v1.wav";
    public string ErrorSound { get; set; } = "matchmaking/error_v1.wav";

    public void Validate()
    {
        if (ReadyTimeoutSeconds is < 15 or > 180 || QueueTimeoutSeconds is < 60 or > 3600 ||
            MaximumQueuedPlayers is < 2 or > 1024 || Entries == null || Entries.Count > 256 ||
            Entries.Any(e => e == null) || Entries.Select(e => e.Id).Distinct().Count() != Entries.Count ||
            Entries.Select(e => (e.Activity, e.ContentId)).Where(e => e.ContentId != Guid.Empty).Distinct().Count() != Entries.Count(e => e.ContentId != Guid.Empty) ||
            new[] { BackgroundImage, IconsImage, OpenSound, QueuedSound, FoundSound, ReadySound, CancelledSound, DepartSound, ErrorSound }.Any(s => !AssetName(s)))
            throw new InvalidDataException("Invalid matchmaking settings.");
        foreach (var entry in Entries) entry.Validate();
    }

    public static bool AssetName(string? value) => value is { Length: <= 160 } &&
        !value.StartsWith('/') && !value.StartsWith('\\') && !value.Contains(':') &&
        !value.Contains("..") && !value.Any(char.IsControl);
}

public sealed class MatchmakingChoice
{
    public Guid Id { get; set; }
    public MatchmakingActivity Activity { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int TargetPlayers { get; set; }
    public int MinimumLevel { get; set; }
    public int MaximumLevel { get; set; }
    public int MinimumShips { get; set; }
    public int MaximumShips { get; set; }
    public int WaitingPlayers { get; set; }
    public bool CanQueue { get; set; }
    public string Reason { get; set; } = "";
    public string Image { get; set; } = "";
}

public sealed class MatchmakingMemberView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Ready { get; set; }
    public bool Captain { get; set; }
    public string ShipName { get; set; } = "";
}

public sealed class MatchmakingView
{
    public MatchmakingStatus Status { get; set; }
    public string Message { get; set; } = "";
    public bool Error { get; set; }
    public MatchmakingChoice[] Choices { get; set; } = [];
    public Guid EntryId { get; set; }
    public Guid ConfirmationId { get; set; }
    public string ActivityName { get; set; } = "";
    public string PortName { get; set; } = "";
    public long DeadlineUtcMilliseconds { get; set; }
    public int ConfirmationSecondsRemaining { get; set; }
    public int WaitingSeconds { get; set; }
    public int TargetPlayers { get; set; }
    public MatchmakingMemberView[] Members { get; set; } = [];
    public MatchmakingConfiguration Presentation { get; set; } = new();
}
