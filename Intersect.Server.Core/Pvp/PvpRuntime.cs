using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Pvp;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Pvp;
using Intersect.Server.Database;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Server.WorldEvents;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
namespace Intersect.Server.Pvp;

internal static class PvpRuntime
{
    internal static PvpConfiguration Configuration => WorldActivitiesRuntime.Current.Pvp;
    private static readonly Guid PointsId = Key("points"), WinsId = Key("wins"), LossesId = Key("losses"),ReturnPendingId=Key("return-pending");
    private static Guid Key(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("CR2026/PvP/" + key)).AsSpan(0, 16));
    internal static long Points(Player p) => Math.Max(0, p.GetVariable(PointsId)?.Value.Integer ?? 0);
    internal static string Grade(Player p) => Configuration.Enabled ? Configuration.Grade(Points(p)) : "";
    private static readonly object RankingGate = new(), Gate = new();
    private static long _rankingUntil, _nextTick;
    private static PvpRankEntry[] _ranking = [];
    private sealed record Waiting(Player Player, Guid Arena);
    private sealed class Member
    {
        public required Player Player; public required Match Match; public int Team;
        public Guid ReturnMap; public float ReturnX, ReturnY; public long[] Vitals = []; public bool WasHidden;
        public long RespawnAt, ProtectedUntil;
    }
    private sealed class Match
    {
        public Guid Id = Guid.NewGuid(); public required CaptureChestRound Round;
        public readonly List<Member> Members = []; public readonly List<Member> Roster = []; public long LastState;
    }
    private static readonly Dictionary<Guid, Waiting> Queue = [];
    private static readonly Dictionary<Guid, Match> Matches = [];
    private static readonly ConcurrentDictionary<Guid, Member> Members = new();
    private static readonly Dictionary<Guid, long> LastRequests = [];
    internal static bool InMatch(Player p) => Members.ContainsKey(p.Id);
    internal static Guid MatchId(Player p) => Members.TryGetValue(p.Id, out var m) ? m.Match.Id : Guid.Empty;
    internal static int Team(Player p) => Members.TryGetValue(p.Id, out var m) ? m.Team : -1;
    internal static bool Respawning(Player p) => Members.TryGetValue(p.Id, out var m) && m.RespawnAt > 0;
    internal static bool Protected(Player p) => Members.TryGetValue(p.Id, out var m) && (m.RespawnAt > 0 || m.ProtectedUntil > Timing.Global.Milliseconds);
    internal static bool? Allies(Player a, Player b)
    {
        if (!Members.TryGetValue(a.Id, out var x) && !Members.TryGetValue(b.Id, out _))
            return null;
        return Members.TryGetValue(a.Id, out x) && Members.TryGetValue(b.Id, out var y) && x.Match == y.Match && x.Team == y.Team;
    }
    private static bool EligibleOpponent(Player a, Player b, long now)
    {
        var config = Configuration;
        return a.Id != b.Id && (a.UserId == Guid.Empty || a.UserId != b.UserId) &&
            (!config.BlockSameIp || a.Client == null || b.Client == null || a.Client.Ip != b.Client.Ip) &&
            (config.MaximumLevelDifference == 0 || Math.Abs(a.Level - b.Level) <= config.MaximumLevelDifference) &&
            now - (a.GetVariable(Key("opponent/" + b.Id))?.Value.Integer ?? 0) >= config.RepeatOpponentSeconds * 1000L;
    }
    private static void Adjust(Player p, long delta, bool won)
    {
        p.SetVariableValue(PointsId, (long)Math.Clamp((decimal)Points(p) + delta, 0, int.MaxValue));
        var key = won ? WinsId : LossesId;
        p.SetVariableValue(key, Math.Min(int.MaxValue, (p.GetVariable(key)?.Value.Integer ?? 0) + 1));
        _rankingUntil = 0;
    }
    internal static void RecordKill(Player winner, Player loser)
    {
        var config = Configuration;
        if (!config.Enabled || InMatch(winner) || InMatch(loser) || winner.MapId != loser.MapId || winner.MapInstanceId != loser.MapInstanceId ||
            !config.RankedMaps.Contains(winner.MapId) || winner.IsAllyOf(loser))
            return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (RankingGate)
        {
            if (!EligibleOpponent(winner, loser, now))
                return;
            winner.SetVariableValue(Key("opponent/" + loser.Id), now);
            Adjust(winner, config.KillPoints, true);
            Adjust(loser, -config.DeathPenalty, false);
        }
        PacketSender.SendEntityDataToProximity(winner);
        PacketSender.SendEntityDataToProximity(loser);
    }
    internal static bool HandleDeath(Player player, Entity? killer)
    {
        if (!Members.TryGetValue(player.Id, out var member))
            return false;
        // Combat already holds EntityLock. Never acquire Gate here: match travel takes
        // Gate before Warp's EntityLock. The next round tick drops this carrier's chest.
        var respawn = Timing.Global.Milliseconds + 5000;
        if (Interlocked.CompareExchange(ref member.RespawnAt, respawn, 0) != 0)
            return true;
        player.HideEntity = true;
        player.CastTime = 0;
        player.CastTarget = null;
        player.SetVital(Vital.Health, Math.Max(1, player.GetMaxVital(Vital.Health)));
        PacketSender.SendEntityDataToProximity(player);
        return true;
    }
    internal static bool RecoverLogin(Player p)
    {
        if(Members.TryGetValue(p.Id,out var session) && ReferenceEquals(session.Player,p))return false;
        var pending=(p.GetVariable(ReturnPendingId)?.Value.Integer??0)>0;
        if(!pending && (p.MapInstanceId==Guid.Empty || !Configuration.Arenas.Any(a=>a.MapId==p.MapId)))return false;
        p.SetVariableValue(ReturnPendingId,0L);
        p.HideEntity = false;
        p.WarpToLastOverworldLocation(true);
        return true;
    }
    private static bool CanQueue(Player p) => p.InGame && ReferenceEquals(Player.FindOnline(p.Id), p) && p.MapInstanceId == Guid.Empty && p.CanUseLobbyTravel && !DungeonRunRuntime.IsPlayerInActiveRun(p);
    internal static PvpStatePacket Handle(Player player, PvpRequestPacket request)
    {
        lock (Gate)
        {
            var now = Timing.Global.Milliseconds;
            if (LastRequests.TryGetValue(player.Id, out var last) && now - last < 500)
                return State(player, "Please wait before another PvP request.", false);
            LastRequests[player.Id] = now;
            if (!Enum.IsDefined(request.Action))
                return State(player, "PvP request is unavailable.", false);
            var message = "";
            if (request.Action == PvpAction.Leave)
            {
                Queue.Remove(player.Id);
                if (Members.TryGetValue(player.Id, out var member))
                    Remove(member, true);
                message = "You left the PvP queue or match.";
            }
            if (request.Action == PvpAction.Queue)
            {
                var arena = Configuration.Arenas.FirstOrDefault(a => a.Id == request.ArenaId && a.Enabled);
                if (!Configuration.Enabled || arena == null)
                    message = "Choose an available Capture the Chest arena.";
                else if (!CanQueue(player) || player.Level < arena.MinimumLevel)
                    message = "Return to the open world and finish your current activity before joining PvP.";
                else
                {
                    Queue[player.Id] = new(player, arena.Id);
                    message = "Queued for Capture the Chest. The match starts when both teams are full.";
                }
            }
            return State(player, message, true);
        }
    }
    private static PvpRankEntry[] Rankings()
    {
        if (Timing.Global.Milliseconds < _rankingUntil)
            return _ranking;
        var rows = new Dictionary<Guid, PvpRankEntry>();
        try
        {
            using var context = DbInterface.CreatePlayerContext();
            var variables = context.Player_Variables.AsNoTracking().Where(v => v.VariableId == PointsId).Select(v => new { v.PlayerId, v.Player.Name, v.Json }).ToArray();
            foreach (var v in variables)
            {
                var points = Math.Max(0, JObject.Parse(v.Json)["Value"]?.Value<long>() ?? 0);
                rows[v.PlayerId] = new()
                {
                    Name = v.Name,
                    Points = points,
                    Grade = Configuration.Grade(points)
                };
            }
        }
        catch { /* Keep the online ranking available if the database is temporarily unavailable. */ }
        foreach (var p in Player.OnlinePlayersSnapshot())
            rows[p.Id] = new()
            {
                Name = p.Name,
                Points = Points(p),
                Grade = Grade(p)
            };
        _ranking = rows.Values.OrderByDescending(r => r.Points).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
        _rankingUntil = Timing.Global.Milliseconds + 30000;
        return _ranking;
    }
    private static PvpStatePacket State(Player p, string message = "", bool ranking = false)
    {
        Members.TryGetValue(p.Id, out var member);
        return new PvpStatePacket
        {
            Points = Points(p),
            Grade = Grade(p),
            Message = message,
            Rankings = ranking ? Rankings() : null,
            QueuedArenaId = Queue.TryGetValue(p.Id, out var wait) ? wait.Arena : Guid.Empty,
            Arenas = Configuration.Arenas.Where(a => a.Enabled).Select(a => new PvpArenaEntry { Id = a.Id, Name = a.Name, Queued = Queue.Values.Count(w => w.Arena == a.Id), Players = a.PlayersPerTeam * 2, Minutes = a.Minutes, MinimumLevel = a.MinimumLevel }).ToArray(),
            Match = member == null ? null : new CaptureChestState { Id = member.Match.Id, MapId = member.Match.Round.Arena.MapId, Team = member.Team, RedScore = member.Match.Round.Scores[0], BlueScore = member.Match.Round.Scores[1], RemainingSeconds = (int)Math.Max(0, (member.Match.Round.EndsAt - Timing.Global.Milliseconds + 999) / 1000), RespawnSeconds = (int)Math.Max(0, (member.RespawnAt - Timing.Global.Milliseconds + 999) / 1000), ScoreToWin = member.Match.Round.Arena.ScoreToWin, ChestItemId = member.Match.Round.Arena.ChestItemId, Chests = member.Match.Round.Chests.Select(c => new PvpChest { X = c.X, Y = c.Y, Carrier = c.Carrier, Home = c.Home }).ToArray() },
        };
    }
    private static void Restore(Member m)
    {
        var p = m.Player;
        Members.TryRemove(p.Id, out _);
        p.HideEntity = m.WasHidden;
        p.IsDead = false;
        if (p.InGame)
        {
            for (var i = 0; i < m.Vitals.Length; i++)
                p.SetVital((Vital)i, m.Vitals[i]);
            p.SetVariableValue(ReturnPendingId,0L);
            p.AdminWarp(m.ReturnMap, m.ReturnX, m.ReturnY, Guid.Empty, MapInstanceType.Overworld, true);
            PacketSender.SendEntityDataToProximity(p);
        }
    }
    private static void Remove(Member m, bool leave)
    {
        m.Match.Round.Drop(m.Player.Id, Timing.Global.Milliseconds);
        m.Match.Members.Remove(m);
        if (leave && Timing.Global.Milliseconds - m.Match.Round.StartedAt >= Configuration.MinimumMatchSeconds * 1000L)
            lock (RankingGate)
                Adjust(m.Player, -Configuration.MatchLossPenalty, false);
        Restore(m);
        if (m.Match.Members.Count(x => x.Team == m.Team) == 0)
            m.Match.Round.Forfeit(m.Team);
    }
    private static void Start(CaptureChestArena arena, Player[] players, long now)
    {
        var match = new Match { Round = new CaptureChestRound(arena, now) };
        Matches.Add(match.Id, match);
        var ordered = players.OrderByDescending(p => p.Level).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var p = ordered[i];
            Queue.Remove(p.Id);
            var m = new Member { Player = p, Match = match, Team = i % 2, ReturnMap = p.MapId, ReturnX = p.X, ReturnY = p.Y, Vitals = p.GetVitals(), WasHidden = p.HideEntity, ProtectedUntil = now + 3000 };
            Members[p.Id] = m;
            match.Members.Add(m);
            match.Roster.Add(m);
            p.SetVariableValue(ReturnPendingId,1L);
            p.SetVital(Vital.Health, p.GetMaxVital(Vital.Health));
            p.SetVital(Vital.Mana, p.GetMaxVital(Vital.Mana));
            p.AdminWarp(arena.MapId, m.Team == 0 ? arena.RedX : arena.BlueX, m.Team == 0 ? arena.RedY : arena.BlueY, match.Id, MapInstanceType.Shared, true);
        }
        if (match.Members.Any(m => m.Player.MapId != arena.MapId || m.Player.MapInstanceId != match.Id))
            End(match, false, "The PvP match could not start.");
        else
        foreach (var m in match.Members)
            m.Player.SendPacket(State(m.Player, "Capture the Chest has begun!"));
    }
    private static void End(Match match, bool rewards, string message = "")
    {
        var winner = match.Round.Winner;
        if (rewards && winner >= 0 && Timing.Global.Milliseconds - match.Round.StartedAt >= Configuration.MinimumMatchSeconds * 1000L)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rewarded = false;
            lock (RankingGate)
            {
                foreach (var m in match.Members.Where(m => m.Team == winner))
                {
                    var opponents = match.Roster.Where(o => o.Team != winner && EligibleOpponent(m.Player, o.Player, now)).ToArray();
                    if (opponents.Length == 0)
                        continue;
                    foreach (var opponent in opponents)
                        m.Player.SetVariableValue(Key("opponent/" + opponent.Player.Id), now);
                    Adjust(m.Player, Configuration.MatchWinPoints, true);
                    rewarded = true;
                }
                if (rewarded)
                foreach (var m in match.Members.Where(m => m.Team != winner))
                    Adjust(m.Player, -Configuration.MatchLossPenalty, false);
            }
        }
        foreach (var m in match.Members.ToArray())
        {
            Restore(m);
            if (m.Player.InGame)
                m.Player.SendPacket(State(m.Player, message.Length > 0 ? message : winner < 0 ? "Capture the Chest ended in a draw." : winner == m.Team ? "Victory! Your team captured the chests." : "Defeat. The other team won Capture the Chest."));
        }
        match.Members.Clear();
        Matches.Remove(match.Id);
    }
    internal static void Update(long now)
    {
        if (now < _nextTick)
            return;
        _nextTick = now + 200;
        lock (Gate)
        {
            foreach (var wait in Queue.Values.ToArray())
                if (!Configuration.Enabled || !CanQueue(wait.Player) || !Configuration.Arenas.Any(a => a.Enabled && a.Id == wait.Arena))
                    Queue.Remove(wait.Player.Id);
            foreach (var id in LastRequests.Keys.Where(id => Player.FindOnline(id) == null).ToArray())
                LastRequests.Remove(id);
            foreach (var arena in Configuration.Arenas.Where(a => a.Enabled))
            {
                var ready = Queue.Values.Where(w => w.Arena == arena.Id).Take(arena.PlayersPerTeam * 2).Select(w => w.Player).ToArray();
                if (ready.Length == arena.PlayersPerTeam * 2)
                    Start(arena, ready, now);
            }
            foreach (var match in Matches.Values.ToArray())
            {
                if (!Configuration.Enabled || MapController.Get(match.Round.Arena.MapId)?.ZoneType != MapZone.Arena)
                {
                    End(match, false, "The PvP match was cancelled.");
                    continue;
                }
                foreach (var m in match.Members.ToArray())
                {
                    var p = m.Player;
                    if (!p.InGame || p.Client==null || Player.FindOnline(p.Id) != p || p.MapInstanceId != match.Id || p.MapId != match.Round.Arena.MapId)
                    {
                        Remove(m, true);
                        continue;
                    }
                    if (m.RespawnAt > 0 && now >= m.RespawnAt)
                    {
                        m.RespawnAt = 0;
                        m.ProtectedUntil = now + 3000;
                        p.HideEntity = false;
                        p.SetVital(Vital.Health, p.GetMaxVital(Vital.Health));
                        p.SetVital(Vital.Mana, p.GetMaxVital(Vital.Mana));
                        p.AdminWarp(match.Round.Arena.MapId, m.Team == 0 ? match.Round.Arena.RedX : match.Round.Arena.BlueX, m.Team == 0 ? match.Round.Arena.RedY : match.Round.Arena.BlueY, match.Id, MapInstanceType.Shared, true);
                        PacketSender.SendEntityDataToProximity(p);
                    }
                }
                match.Round.Tick(now, match.Members.Select(m => new ChestActor(m.Player.Id, m.Team, m.Player.X, m.Player.Y, m.RespawnAt == 0 && !m.Player.IsDead)));
                if (match.Round.Finished)
                {
                    End(match, true);
                    continue;
                }
                if (now >= match.LastState + 1000)
                {
                    match.LastState = now;
                    foreach (var m in match.Members)
                        m.Player.SendPacket(State(m.Player));
                }
            }
        }
    }
}
