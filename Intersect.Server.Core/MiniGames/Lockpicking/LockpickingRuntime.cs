using System.Security.Cryptography;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Lockpicking;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using Intersect.Server.WorldEvents.Invasions;

namespace Intersect.Server.MiniGames.Lockpicking;

internal static class LockpickingRuntime
{
    private readonly record struct UnlockKey(
        LockpickUnlockScope Scope,
        Guid OwnerId,
        Guid LockId,
        Guid MapId,
        Guid MapInstanceId
    );

    private readonly record struct CooldownKey(Guid PlayerId, Guid LockId, Guid MapInstanceId);

    private sealed class Session
    {
        public required Player Player;
        public required Client Client;
        public required StartMiniGameCommand Command;
        public required Guid SessionId;
        public required Guid EventId;
        public required Guid LockId;
        public required Guid MapId;
        public required Guid MapInstanceId;
        public required DateTime? LoginStamp;
        public required int Difficulty;
        public required int ProfessionLevel;
        public required int MaxMistakes;
        public required int TimeLimitSeconds;
        public required Guid ProfessionId;
        public required long ProfessionBaseExperience;
        public required Guid ToolItemId;
        public required LockpickToolQuality ToolQuality;
        public required string ToolName;
        public required int SecretAngle;
        public required long StartedAt;
        public long LastRequestId;
        public long Sequence;
        public int Mistakes;
        public int TurnPercent;
        public string Hint = string.Empty;
        public bool Closed;
        public bool PickBroken;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> Sessions = new();
    private static readonly HashSet<UnlockKey> Unlocks = [];
    private static readonly Dictionary<CooldownKey, long> FailureCooldowns = [];
    private static readonly System.Threading.Timer SweepTimer =
        new(_ => Sweep(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    internal static bool IsUnlockedFor(
        Player player,
        StartMiniGameCommand command,
        Guid lockId,
        Guid mapId,
        Guid mapInstanceId
    )
    {
        var key = MakeUnlockKey(player, command.LockpickUnlockScope, lockId, mapId, mapInstanceId);
        lock (Gate)
            return Unlocks.Contains(key);
    }

    internal static bool TryUnlockWithKey(
        Player player,
        StartMiniGameCommand command,
        Guid lockId,
        out string message
    )
    {
        message = string.Empty;
        if (command.LockpickKeyItemId == Guid.Empty ||
            !player.CanTakeItem(command.LockpickKeyItemId, 1))
            return false;

        if (command.LockpickConsumeKey &&
            !player.TryTakeItem(command.LockpickKeyItemId, 1))
        {
            message = "[Lockpicking] The configured key could not be consumed.";
            return false;
        }

        RegisterUnlock(player, command, lockId, player.MapId, player.MapInstanceId);
        var keyName = ItemDescriptor.GetName(command.LockpickKeyItemId);
        message = $"[Lockpicking] {keyName} opens the lock.";
        return true;
    }

    internal static bool Join(
        Player player,
        StartMiniGameCommand command,
        Guid eventId,
        Guid lockId,
        out string error
    )
    {
        _ = SweepTimer;
        error = string.Empty;

        if (command.Game != MiniGameType.Lockpicking ||
            !command.HasValidSettings() ||
            eventId == Guid.Empty ||
            lockId == Guid.Empty ||
            player.Client is not { IsEditor: false } client ||
            player.IsDead)
        {
            error = "Unable to start this lock.";
            return false;
        }

        var professionLevel = ProfessionRuntime.GetLevel(player, command.LockpickProfessionId);
        if (professionLevel < command.LockpickRequiredProfessionLevel)
        {
            error =
                $"{ProfessionName(command.LockpickProfessionId)} level {command.LockpickRequiredProfessionLevel} is required. " +
                $"Your level: {professionLevel}.";
            return false;
        }

        var cooldownKey = new CooldownKey(player.Id, lockId, player.MapInstanceId);
        lock (Gate)
        {
            if (FailureCooldowns.TryGetValue(cooldownKey, out var until))
            {
                var remaining = until - Environment.TickCount64;
                if (remaining > 0)
                {
                    error = $"The lock is jammed for {Math.Ceiling(remaining / 1000d):0} more second(s).";
                    return false;
                }

                FailureCooldowns.Remove(cooldownKey);
            }

            if (Sessions.ContainsKey(player.Id))
            {
                error = "A lockpicking session is already active.";
                return false;
            }

            if (IsUnlockedFor(player, command, lockId, player.MapId, player.MapInstanceId))
            {
                error = "AlreadyUnlocked";
                return false;
            }

            if (!TrySelectTool(player, command, out var toolItemId, out var toolQuality, out var toolName))
            {
                error = command.LockpickMinimumToolQuality == LockpickToolQuality.None
                    ? "No configured lockpick is available."
                    : $"{command.LockpickMinimumToolQuality}+ lockpick required.";
                return false;
            }

            var difficulty = command.LockpickDifficulty;
            if (command.LockpickInvasionDifficultyBonus > 0 &&
                InvasionRuntime.IsActiveOnMap(player.MapId))
            {
                difficulty = Math.Clamp(
                    difficulty + command.LockpickInvasionDifficultyBonus,
                    1,
                    5
                );
            }

            var session = new Session
            {
                Player = player,
                Client = client,
                Command = command,
                SessionId = Guid.NewGuid(),
                EventId = eventId,
                LockId = lockId,
                MapId = player.MapId,
                MapInstanceId = player.MapInstanceId,
                LoginStamp = player.LoginTime,
                Difficulty = difficulty,
                ProfessionLevel = professionLevel,
                MaxMistakes = command.LockpickMaxMistakes,
                TimeLimitSeconds = command.LockpickTimeSeconds,
                ProfessionId = command.LockpickProfessionId,
                ProfessionBaseExperience = command.LockpickProfessionBaseExperience,
                ToolItemId = toolItemId,
                ToolQuality = toolQuality,
                ToolName = toolName,
                SecretAngle = RandomNumberGenerator.GetInt32(-80, 81),
                StartedAt = Environment.TickCount64,
            };

            Sessions[player.Id] = session;
            Send(session);
            AnnounceBestCrewLocksmith(player, command.LockpickProfessionId);
            return true;
        }
    }

    internal static bool Leave(Player player)
    {
        Session? session;
        lock (Gate)
        {
            if (!Sessions.TryGetValue(player.Id, out session))
                return false;
        }

        Finish(session, false, "Cancelled");
        return true;
    }

    internal static void Handle(Client client, LockpickingRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player || !packet.IsValid)
            return;

        Session? session;
        lock (Gate)
        {
            if (!Sessions.TryGetValue(player.Id, out session) ||
                session.Closed ||
                !ReferenceEquals(session.Client, client) ||
                session.SessionId != packet.SessionId ||
                packet.RequestId <= session.LastRequestId)
                return;

            session.LastRequestId = packet.RequestId;
        }

        if (packet.Kind == LockpickingRequestKind.Cancel)
        {
            Finish(session, false, "Cancelled");
            return;
        }

        if (!StillValid(session))
        {
            Finish(session, false, "Interrupted");
            return;
        }

        Attempt(session, packet);
    }

    private static void Attempt(Session session, LockpickingRequestPacket packet)
    {
        lock (Gate)
        {
            if (session.Closed ||
                !Sessions.TryGetValue(session.Player.Id, out var current) ||
                !ReferenceEquals(current, session))
                return;

            if (RemainingMilliseconds(session) <= 0)
            {
                FinishLocked(session, false, "Timeout");
                return;
            }

            var distance = Math.Abs(packet.Angle - session.SecretAngle);
            var tolerance = Tolerance(session.Difficulty);
            if (session.Command.LockpickUseProfessionSkillBonus)
                tolerance += Math.Clamp(session.ProfessionLevel / 10, 0, 10);

            if (distance <= tolerance)
            {
                session.TurnPercent = 100;
                session.Hint = "Unlocked";
                FinishLocked(session, true, string.Empty, packet.RequestId);
                return;
            }

            session.Mistakes++;
            session.TurnPercent = Math.Clamp(100 - distance, 0, 95);
            session.Hint = distance <= tolerance * 2
                ? "Very hot"
                : distance <= tolerance * 4
                    ? "Warm"
                    : "Cold";

            TriggerCommonEvent(session.Player, session.Command.LockpickMistakeCommonEventId);

            if (TryBreakPick(session))
            {
                session.PickBroken = true;
                LockpickingStatsRuntime.RecordBrokenPick(session.Player, session.ProfessionId);
                FinishLocked(session, false, "ToolBroken", packet.RequestId);
                return;
            }

            if (session.Mistakes >= session.MaxMistakes)
            {
                FinishLocked(session, false, "BrokenPick", packet.RequestId);
                return;
            }

            Send(session, packet.RequestId);
        }
    }

    private static bool TryBreakPick(Session session)
    {
        if (session.ToolItemId == Guid.Empty || session.ToolQuality == LockpickToolQuality.None)
            return false;

        var baseChance = session.ToolQuality switch
        {
            LockpickToolQuality.Basic => session.Command.LockpickBasicBreakChancePercent,
            LockpickToolQuality.Reinforced => session.Command.LockpickReinforcedBreakChancePercent,
            LockpickToolQuality.Royal => session.Command.LockpickRoyalBreakChancePercent,
            LockpickToolQuality.Master => session.Command.LockpickMasterBreakChancePercent,
            _ => 0,
        };

        if (baseChance <= 0)
            return false;

        var levelReductionPercent = Math.Clamp(session.ProfessionLevel, 0, 80);
        var effectiveChance = (int)Math.Round(
            baseChance * (100 - levelReductionPercent) / 100d,
            MidpointRounding.AwayFromZero
        );

        if (RandomNumberGenerator.GetInt32(0, 100) >= effectiveChance)
            return false;

        return session.Player.TryTakeItem(session.ToolItemId, 1);
    }

    private static int Tolerance(int difficulty) => difficulty switch
    {
        1 => 24,
        2 => 18,
        3 => 13,
        4 => 9,
        _ => 6,
    };

    private static int RemainingMilliseconds(Session session)
    {
        var elapsed = Math.Max(0L, Environment.TickCount64 - session.StartedAt);
        return (int)Math.Clamp(
            session.TimeLimitSeconds * 1000L - elapsed,
            0L,
            session.TimeLimitSeconds * 1000L
        );
    }

    private static bool StillValid(Session session) =>
        ReferenceEquals(session.Player.Client, session.Client) &&
        session.Player.LoginTime == session.LoginStamp &&
        session.Player.MapId == session.MapId &&
        session.Player.MapInstanceId == session.MapInstanceId &&
        !session.Player.IsDead;

    private static void Sweep()
    {
        Session[] stale;
        lock (Gate)
        {
            stale = Sessions.Values
                .Where(session => !session.Closed &&
                    (!StillValid(session) || RemainingMilliseconds(session) <= 0))
                .ToArray();

            foreach (var key in Unlocks
                         .Where(key =>
                             key.Scope == LockpickUnlockScope.Crew &&
                             !CrewStillExists(key.OwnerId))
                         .ToArray())
            {
                Unlocks.Remove(key);
            }

            foreach (var key in FailureCooldowns
                         .Where(pair => pair.Value <= Environment.TickCount64)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                FailureCooldowns.Remove(key);
            }
        }

        foreach (var session in stale)
            Finish(
                session,
                false,
                RemainingMilliseconds(session) <= 0 ? "Timeout" : "Interrupted"
            );
    }

    private static void Finish(Session session, bool success, string error)
    {
        lock (Gate)
            FinishLocked(session, success, error);
    }

    private static void FinishLocked(
        Session session,
        bool success,
        string error,
        long requestId = 0
    )
    {
        if (session.Closed)
            return;

        session.Closed = true;
        Sessions.Remove(session.Player.Id);
        session.TurnPercent = success ? 100 : session.TurnPercent;

        var elapsed = Math.Max(1L, Environment.TickCount64 - session.StartedAt);
        var perfect = success && session.Mistakes == 0;
        var fast = success &&
                   elapsed <= session.Command.LockpickFastThresholdSeconds * 1000L;

        if (success)
            RegisterUnlock(
                session.Player,
                session.Command,
                session.LockId,
                session.MapId,
                session.MapInstanceId
            );
        else if (error is not ("Cancelled" or "Interrupted") &&
                 session.Command.LockpickFailureCooldownSeconds > 0)
            FailureCooldowns[new CooldownKey(
                session.Player.Id,
                session.LockId,
                session.MapInstanceId
            )] = Environment.TickCount64 +
                 session.Command.LockpickFailureCooldownSeconds * 1000L;

        Send(session, requestId, closed: true, success: success, error: error);

        if (success)
        {
            var professionExperience = ExperienceAward(session, perfect, fast);
            ProfessionRuntime.AwardActivity(
                session.Player,
                session.ProfessionId,
                professionExperience
            );

            LockpickingStatsRuntime.RecordSuccess(
                session.Player,
                session.ProfessionId,
                session.Difficulty,
                elapsed,
                perfect,
                fast,
                session.PickBroken
            );

            if (perfect && session.Command.VictoryAnimationId != Guid.Empty)
            {
                PacketSender.SendAnimationToProximity(
                    session.Command.VictoryAnimationId,
                    1,
                    session.Player.Id,
                    session.Player.MapId,
                    0,
                    0,
                    session.Player.Dir,
                    session.Player.MapInstanceId
                );
            }

            var lockName = session.Player.EventLookup.Values
                .FirstOrDefault(evt => evt.Descriptor?.Id == session.LockId)
                ?.Descriptor?.Name ?? "Lock";

            session.Player.UpdateLockpickingQuestTasks(
                session.LockId,
                lockName,
                session.Difficulty,
                perfect,
                session.PickBroken
            );
        }
        else if (error is not ("Cancelled" or "Interrupted"))
        {
            TriggerCommonEvent(
                session.Player,
                session.Command.LockpickFailureCommonEventId
            );
        }

        PacketSender.SendChatMsg(
            session.Player,
            ResultMessage(session, success, error, perfect, fast),
            success ? ChatMessageType.Local : ChatMessageType.Error,
            Color.White
        );

        session.Player.ResolveLockpickingEvent(session.EventId, success);

        if (success && session.Command.LockpickUnlockScope == LockpickUnlockScope.Crew &&
            session.Player.IsInParty)
        {
            foreach (var member in session.Player.Party
                         .Where(member => member != null && member.Id != session.Player.Id)
                         .ToArray())
            {
                PacketSender.SendChatMsg(
                    member,
                    $"[Lockpicking] {session.Player.Name} unlocked the lock for the Crew.",
                    ChatMessageType.Local,
                    Color.White
                );
            }
        }
    }

    private static long ExperienceAward(Session session, bool perfect, bool fast)
    {
        decimal value = session.ProfessionBaseExperience * session.Difficulty;
        if (perfect)
            value *= 1m + session.Command.LockpickPerfectExperienceBonusPercent / 100m;
        if (fast)
            value *= 1m + session.Command.LockpickFastExperienceBonusPercent / 100m;

        return value >= long.MaxValue
            ? long.MaxValue
            : Math.Max(1L, (long)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    private static string ResultMessage(
        Session session,
        bool success,
        string error,
        bool perfect,
        bool fast
    )
    {
        if (success)
        {
            var suffix = perfect
                ? " PERFECT!"
                : fast
                    ? " Fast pick!"
                    : string.Empty;

            return session.Command.LockpickUnlockScope == LockpickUnlockScope.Crew &&
                   session.Player.IsInParty
                ? $"[Lockpicking] The lock clicks open for your Crew.{suffix}"
                : $"[Lockpicking] The lock clicks open.{suffix}";
        }

        return error switch
        {
            "Timeout" => "[Lockpicking] Time ran out. The lock remains closed.",
            "BrokenPick" => "[Lockpicking] Too many mistakes. The lock remains closed.",
            "ToolBroken" => $"[Lockpicking] Your {session.ToolName} broke.",
            "Cancelled" => "[Lockpicking] You stopped picking the lock.",
            _ => "[Lockpicking] The attempt was interrupted. The lock remains closed.",
        };
    }

    private static void RegisterUnlock(
        Player player,
        StartMiniGameCommand command,
        Guid lockId,
        Guid mapId,
        Guid mapInstanceId
    )
    {
        Unlocks.Add(MakeUnlockKey(
            player,
            command.LockpickUnlockScope,
            lockId,
            mapId,
            mapInstanceId
        ));
    }

    private static UnlockKey MakeUnlockKey(
        Player player,
        LockpickUnlockScope scope,
        Guid lockId,
        Guid mapId,
        Guid mapInstanceId
    )
    {
        var owner = scope switch
        {
            LockpickUnlockScope.Crew when player.IsInParty =>
                player.PartyLeader?.Id ?? player.Id,
            LockpickUnlockScope.Instance =>
                mapInstanceId != Guid.Empty ? mapInstanceId : mapId,
            _ => player.Id,
        };

        return new UnlockKey(scope, owner, lockId, mapId, mapInstanceId);
    }

    private static bool CrewStillExists(Guid leaderId)
    {
        var leader = Player.FindOnline(leaderId);
        return leader != null &&
               leader.Party != null &&
               leader.Party.Count > 1 &&
               leader.PartyLeader?.Id == leaderId;
    }

    private static bool TrySelectTool(
        Player player,
        StartMiniGameCommand command,
        out Guid itemId,
        out LockpickToolQuality quality,
        out string name
    )
    {
        var options = new[]
        {
            (LockpickToolQuality.Master, command.LockpickMasterToolItemId),
            (LockpickToolQuality.Royal, command.LockpickRoyalToolItemId),
            (LockpickToolQuality.Reinforced, command.LockpickReinforcedToolItemId),
            (LockpickToolQuality.Basic, command.LockpickBasicToolItemId),
        };

        foreach (var candidate in options)
        {
            if (candidate.Item2 == Guid.Empty ||
                candidate.Item1 < command.LockpickMinimumToolQuality ||
                !player.CanTakeItem(candidate.Item2, 1))
                continue;

            itemId = candidate.Item2;
            quality = candidate.Item1;
            name = ItemDescriptor.GetName(candidate.Item2);
            return true;
        }

        if (command.LockpickMinimumToolQuality == LockpickToolQuality.None &&
            options.All(candidate => candidate.Item2 == Guid.Empty))
        {
            itemId = Guid.Empty;
            quality = LockpickToolQuality.None;
            name = "lockpick";
            return true;
        }

        itemId = Guid.Empty;
        quality = LockpickToolQuality.None;
        name = "lockpick";
        return false;
    }

    private static void TriggerCommonEvent(Player player, Guid eventId)
    {
        if (eventId == Guid.Empty)
            return;

        if (EventDescriptor.Get(eventId) is { CommonEvent: true } evt)
            player.EnqueueStartCommonEvent(evt);
    }

    private static void AnnounceBestCrewLocksmith(Player player, Guid professionId)
    {
        if (!player.IsInParty || player.Party == null || player.Party.Count < 2)
            return;

        var best = player.Party
            .Where(member => member != null && !member.IsDisposed)
            .Select(member => new
            {
                Player = member,
                Level = ProfessionRuntime.GetLevel(member, professionId),
            })
            .OrderByDescending(entry => entry.Level)
            .ThenBy(entry => entry.Player.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (best == null)
            return;

        foreach (var member in player.Party.Where(member => member != null))
        {
            PacketSender.SendChatMsg(
                member,
                $"[Crew] Best Locksmith: {best.Player.Name} - Level {best.Level}.",
                ChatMessageType.Party,
                Color.White
            );
        }
    }

    private static string ProfessionName(Guid professionId) =>
        ProfessionConfigurationRuntime.Current.Find(professionId)?.Name ?? "Profession";

    private static void Send(
        Session session,
        long requestId = 0,
        bool closed = false,
        bool success = false,
        string error = ""
    )
    {
        if (!ReferenceEquals(session.Client.Entity, session.Player))
            return;

        session.Sequence++;
        session.Client.Send(
            new LockpickingStatePacket
            {
                SessionId = session.SessionId,
                EventId = session.EventId,
                Sequence = session.Sequence,
                RequestId = requestId,
                Closed = closed,
                Success = success,
                Difficulty = session.Difficulty,
                MaxMistakes = session.MaxMistakes,
                Mistakes = session.Mistakes,
                TimeLimitSeconds = session.TimeLimitSeconds,
                RemainingMilliseconds = RemainingMilliseconds(session),
                TurnPercent = session.TurnPercent,
                Hint = session.Hint,
                ErrorCode = error,
                LockType = $"{session.Command.LockpickTargetKind} / {session.Command.LockpickType}",
                ToolName = session.ToolName,
                ProfessionLevel = session.ProfessionLevel,
                RequiredProfessionLevel = session.Command.LockpickRequiredProfessionLevel,
                PerfectEligible = session.Mistakes == 0,
            }
        );
    }
}
