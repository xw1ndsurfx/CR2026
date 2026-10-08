using System.Security.Cryptography;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Server.Professions;

namespace Intersect.Server.MiniGames.Lockpicking;

internal static class LockpickingRuntime
{
    private readonly record struct CrewLockKey(
        Guid LeaderId,
        Guid LockId,
        Guid MapId,
        Guid MapInstanceId
    );

    private sealed class Session
    {
        public required Player Player;
        public required Client Client;
        public required Guid SessionId;
        public required Guid EventId;
        public required Guid LockId;
        public required Guid MapId;
        public required Guid MapInstanceId;
        public required DateTime? LoginStamp;
        public required int Difficulty;
        public required int MaxMistakes;
        public required int TimeLimitSeconds;
        public required Guid ProfessionId;
        public required long ProfessionBaseExperience;
        public required int SecretAngle;
        public required long StartedAt;
        public long LastRequestId;
        public long Sequence;
        public int Mistakes;
        public int TurnPercent;
        public string Hint = string.Empty;
        public bool Closed;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> Sessions = new();
    private static readonly HashSet<CrewLockKey> CrewUnlocks = [];
    private static readonly System.Threading.Timer SweepTimer =
        new(_ => Sweep(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    internal static bool IsUnlockedForCrew(
        Player player,
        Guid lockId,
        Guid mapId,
        Guid mapInstanceId
    )
    {
        if (!TryGetCrewLockKey(player, lockId, mapId, mapInstanceId, out var key))
            return false;

        lock (Gate)
            return CrewUnlocks.Contains(key);
    }

    internal static bool Join(
        Player player,
        StartMiniGameCommand command,
        Guid eventId,
        Guid lockId
    )
    {
        _ = SweepTimer;

        if (command.Game != MiniGameType.Lockpicking ||
            !command.HasValidSettings() ||
            eventId == Guid.Empty ||
            lockId == Guid.Empty ||
            player.Client is not { IsEditor: false } client ||
            player.IsDead)
        {
            return false;
        }

        lock (Gate)
        {
            if (Sessions.ContainsKey(player.Id))
                return false;

            if (IsUnlockedForCrew(player, lockId, player.MapId, player.MapInstanceId))
                return false;

            var session = new Session
            {
                Player = player,
                Client = client,
                SessionId = Guid.NewGuid(),
                EventId = eventId,
                LockId = lockId,
                MapId = player.MapId,
                MapInstanceId = player.MapInstanceId,
                LoginStamp = player.LoginTime,
                Difficulty = command.LockpickDifficulty,
                MaxMistakes = command.LockpickMaxMistakes,
                TimeLimitSeconds = command.LockpickTimeSeconds,
                ProfessionId = command.LockpickProfessionId,
                ProfessionBaseExperience = command.LockpickProfessionBaseExperience,
                SecretAngle = RandomNumberGenerator.GetInt32(-80, 81),
                StartedAt = Environment.TickCount64,
            };

            Sessions[player.Id] = session;
            Send(session);
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
            {
                return;
            }

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
            {
                return;
            }

            if (RemainingMilliseconds(session) <= 0)
            {
                FinishLocked(session, false, "Timeout");
                return;
            }

            var distance = Math.Abs(packet.Angle - session.SecretAngle);
            var tolerance = Tolerance(session.Difficulty);

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

            if (session.Mistakes >= session.MaxMistakes)
            {
                FinishLocked(session, false, "BrokenPick", packet.RequestId);
                return;
            }

            Send(session, packet.RequestId);
        }
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
        return (int)Math.Clamp(session.TimeLimitSeconds * 1000L - elapsed, 0L, session.TimeLimitSeconds * 1000L);
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

            foreach (var key in CrewUnlocks
                         .Where(key => !CrewStillExists(key))
                         .ToArray())
            {
                CrewUnlocks.Remove(key);
            }
        }

        foreach (var session in stale)
            Finish(session, false, RemainingMilliseconds(session) <= 0 ? "Timeout" : "Interrupted");
    }

    private static void Finish(Session session, bool success, string error)
    {
        lock (Gate)
            FinishLocked(session, success, error);
    }

    private static void FinishLocked(Session session, bool success, string error, long requestId = 0)
    {
        if (session.Closed)
            return;

        session.Closed = true;
        Sessions.Remove(session.Player.Id);
        session.TurnPercent = success ? 100 : session.TurnPercent;

        if (success &&
            TryGetCrewLockKey(
                session.Player,
                session.LockId,
                session.MapId,
                session.MapInstanceId,
                out var crewKey
            ))
        {
            CrewUnlocks.Add(crewKey);
        }

        Send(session, requestId, closed: true, success: success, error: error);

        if (success)
        {
            long professionExperience;
            try
            {
                professionExperience = checked(session.ProfessionBaseExperience * session.Difficulty);
            }
            catch (OverflowException)
            {
                professionExperience = long.MaxValue;
            }

            ProfessionRuntime.AwardActivity(
                session.Player,
                session.ProfessionId,
                professionExperience
            );
        }

        PacketSender.SendChatMsg(
            session.Player,
            success
                ? session.Player.IsInParty
                    ? "[Lockpicking] The lock clicks open for your Crew."
                    : "[Lockpicking] The lock clicks open."
                : error switch
                {
                    "Timeout" => "[Lockpicking] Time ran out. The door remains locked.",
                    "BrokenPick" => "[Lockpicking] The pick slipped too many times. The door remains locked.",
                    "Cancelled" => "[Lockpicking] You stopped picking the lock.",
                    _ => "[Lockpicking] The attempt was interrupted. The door remains locked.",
                },
            success ? ChatMessageType.Local : ChatMessageType.Error,
            Color.White
        );

        session.Player.ResolveLockpickingEvent(session.EventId, success);

        if (success && session.Player.IsInParty)
        {
            foreach (var member in session.Player.Party
                         .Where(member => member != null && member.Id != session.Player.Id)
                         .ToArray())
            {
                PacketSender.SendChatMsg(
                    member,
                    $"[Lockpicking] {session.Player.Name} unlocked the door for the Crew.",
                    ChatMessageType.Local,
                    Color.White
                );
            }
        }
    }

    private static bool TryGetCrewLockKey(
        Player player,
        Guid lockId,
        Guid mapId,
        Guid mapInstanceId,
        out CrewLockKey key
    )
    {
        key = default;

        if (player?.Party == null ||
            player.Party.Count < 2 ||
            player.PartyLeader is not { } leader ||
            lockId == Guid.Empty)
        {
            return false;
        }

        key = new CrewLockKey(leader.Id, lockId, mapId, mapInstanceId);
        return true;
    }

    private static bool CrewStillExists(CrewLockKey key)
    {
        var leader = Player.FindOnline(key.LeaderId);
        return leader != null &&
               leader.Party != null &&
               leader.Party.Count > 1 &&
               leader.PartyLeader?.Id == key.LeaderId;
    }

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
            }
        );
    }
}
