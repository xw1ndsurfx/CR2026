using Intersect.Framework.Core.Archery;
using Intersect.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Archery;

/// <summary>Server-only registration and score aggregation. Never calls players while holding Gate.</summary>
internal static class ArcheryCompetitions
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Competition> Matches = new();
    private static readonly Dictionary<(Guid Match, Guid Character), ArcheryResult> Pending = new();
    private static bool _flushing;
    private static long _retryAt;
    private sealed class Member(Guid id, string name, long now)
    {
        internal Guid Id = id;
        internal string Name = name;
        internal long Seen = now;
        internal int Score;
        internal bool Done, Forfeited;
    }
    private sealed class Competition(ArcheryRange range, Guid instance, long now)
    {
        internal Guid Id = Guid.NewGuid(), Range = range.Id, Instance = instance;
        internal ArcheryRules Rules = range.Rules.Snapshot();
        internal double Wind = range.Wind;
        internal bool Ranked = range.RankedCompetition;
        internal string Season = range.SeasonKey, RulesKey = RoyalCombatRuntime.ArcheryRulesKey(range.Rules, range.Wind);
        internal int Minimum = range.MinimumPlayers, Maximum = range.MaximumPlayers;
        internal long Created = now, Deadline = now + range.RegistrationSeconds * 1000L, Started;
        internal bool Cancelled;
        internal List<Member> Members = [];
    }
    internal sealed record Update(ArcheryCompetitionView View, long Started, double Wind, ArcheryRules Rules);

    internal static Guid Join(ArcheryRange range, Guid instance, Guid character, string name, long now)
    {
        lock (Gate)
        {
            Prune(now);
            if (!range.CompetitionEnabled || Matches.Values.Any(c => !c.Cancelled && c.Members.Any(m => m.Id == character && !m.Done))) return Guid.Empty;
            var match = Matches.Values.FirstOrDefault(c => c.Range == range.Id && c.Instance == instance &&
                !c.Cancelled && c.Started == 0 && now < c.Deadline && c.Members.Count < c.Maximum);
            if (match == null) { match = new(range, instance, now); Matches.Add(match.Id, match); }
            match.Members.Add(new(character, name, now));
            Advance(match, now); return match.Id;
        }
    }
    internal static Update? Poll(Guid id, Guid character, long now, int score, bool done, bool forfeited)
    {
        lock (Gate)
        {
            Prune(now);
            if (!Matches.TryGetValue(id, out var match) || match.Members.Find(m => m.Id == character) is not { } member) return null;
            member.Seen = now;
            if (match.Started != 0 && !member.Done)
            {
                member.Score = Math.Clamp(score, member.Score, match.Rules.MaximumScore);
                member.Done = done || forfeited; member.Forfeited = forfeited;
                if (member.Done && !member.Forfeited && match.Ranked)
                    Pending.TryAdd((match.Id, member.Id), new(match.Id, member.Id, match.Range, match.Season,
                        match.RulesKey, member.Name, member.Score, match.Rules.MaximumScore, DateTimeOffset.UtcNow));
            }
            Advance(match, now);
            var players = match.Members.OrderBy(m => m.Forfeited).ThenByDescending(m => m.Score).ThenBy(m => m.Id).Select(m => new ArcheryCompetitorView {
                Id = m.Id, Name = m.Name, Score = m.Score, Done = m.Done, Forfeited = m.Forfeited,
                Rank = m.Forfeited ? 0 : 1 + match.Members.Count(other => !other.Forfeited && other.Score > m.Score)
            }).ToArray();
            return new(new() { Id = id, Started = match.Started != 0, Cancelled = match.Cancelled,
                Ranked = match.Ranked, SavePending = Pending.Keys.Any(k => k.Match == match.Id),
                Finished = match.Started != 0 && match.Members.All(m => m.Done), Players = players,
                MinimumPlayers = match.Minimum, MaximumPlayers = match.Maximum,
                RegistrationRemainingMs = (int)Math.Max(0, match.Deadline - now) }, match.Started, match.Wind, match.Rules);
        }
    }
    /// <summary>Bounded retry batch; database I/O happens outside the registration lock.</summary>
    internal static void FlushPending()
    {
        ArcheryResult[] batch;
        lock (Gate)
        {
            if (_flushing || Environment.TickCount64 < _retryAt || Pending.Count == 0) return;
            _flushing = true; batch = Pending.Values.Take(16).ToArray();
        }
        var failed = false;
        try
        {
            foreach (var result in batch)
            {
                RoyalCombatRuntime.SaveArcheryResult(result);
                lock (Gate) Pending.Remove((result.CompetitionId, result.CharacterId));
            }
        }
        catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or InvalidDataException or UnauthorizedAccessException)
        {
            failed = true;
            ApplicationContext.Context.Value?.Logger.LogError(e, "Archery results await persistence; retrying in five seconds");
        }
        finally { lock (Gate) { _flushing = false; _retryAt = Environment.TickCount64 + (failed ? 5000 : 0); } }
    }
    internal static void Leave(Guid id, Guid character, long now)
    {
        lock (Gate)
        {
            if (!Matches.TryGetValue(id, out var match) || match.Members.Find(m => m.Id == character) is not { } member) return;
            if (match.Started == 0) match.Members.Remove(member);
            else if (!member.Done) { member.Done = member.Forfeited = true; member.Seen = now; }
            if (match.Members.Count == 0) Matches.Remove(id);
        }
    }
    private static void Advance(Competition match, long now)
    {
        if (match.Cancelled || match.Started != 0) return;
        if (match.Members.Count == match.Maximum || now >= match.Deadline && match.Members.Count >= match.Minimum) match.Started = now;
        else if (now >= match.Deadline) match.Cancelled = true;
    }
    private static void Prune(long now)
    {
        foreach (var match in Matches.Values.ToArray())
        {
            foreach (var member in match.Members.Where(m => !m.Done && now - m.Seen > 10000).ToArray())
                if (match.Started == 0) match.Members.Remove(member);
                else member.Done = member.Forfeited = true;
            if (match.Members.Count == 0 || now - match.Created > 3600000 ||
                (match.Cancelled || match.Started != 0 && match.Members.All(m => m.Done)) && match.Members.All(m => now - m.Seen > 120000)) Matches.Remove(match.Id);
        }
    }
}
