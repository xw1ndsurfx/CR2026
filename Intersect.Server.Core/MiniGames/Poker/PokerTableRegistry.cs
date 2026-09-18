#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

// Supplied by the server adapter, never trusted from a client packet.
public readonly record struct PokerSession(Guid PlayerId, Guid ConnectionId);
public readonly record struct PokerPresence(PokerSession Session, Guid MapId, Guid MapInstanceId);
public readonly record struct PokerTableKey(Guid MapId, Guid MapInstanceId, string Name);
public enum PokerRegistryError
{
    None, InvalidPresence, InvalidTableName, InvalidRules, NotSeated, SessionChanged,
    WrongLocation, WrongTable, AlreadyAtAnotherTable, Leaving, RulesConflict, Capacity, PokerRejected,
}
public sealed record PokerRegistryResult(
    PokerRegistryError Error, Guid TableInstanceId = default,
    PokerSnapshot? Snapshot = null, PokerError Detail = PokerError.None);
public sealed record PokerDelivery(PokerSession Recipient, Guid TableInstanceId, PokerSnapshot Snapshot);

/// <summary>
/// Process-local registry for test-chip tables. Serializes membership changes with table actions.
/// Tables are keyed by map + map instance + case-sensitive name. Each newly created table also
/// gets a fresh ID, preventing delayed actions from targeting a replacement table.
/// No external callbacks or network sends run while the registry lock is held.
/// </summary>
public sealed class PokerTableRegistry
{
    private sealed class Entry
    {
        public Guid Id = Guid.NewGuid();
        public required PokerTableKey Key;
        public required PokerRules Rules;
        public required PokerTable Table;
        public readonly HashSet<Guid> Players = new();
        public long PublishedRevision = -1;
    }
    private sealed class Membership
    {
        public required PokerPresence Presence;
        public required Entry Entry;
        public bool Leaving;
    }
    private readonly object _gate = new();
    private readonly int _maximumTables;
    private readonly Dictionary<PokerTableKey, Entry> _tables = new();
    private readonly Dictionary<Guid, Membership> _members = new();

    public PokerTableRegistry(int maximumTables = 1024)
    {
        if (maximumTables < 1 || maximumTables > 100_000)
            throw new ArgumentOutOfRangeException(nameof(maximumTables));
        _maximumTables = maximumTables;
    }
    public int TableCount { get { lock (_gate) return _tables.Count; } }
    public int MemberCount { get { lock (_gate) return _members.Count; } }

    public static bool IsValidTableName(string? name) => !string.IsNullOrEmpty(name) &&
        name.Length <= 64 && name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '-' or '_');

    public PokerRegistryResult Join(PokerPresence caller, string tableName, string playerName, PokerRules rules)
    {
        if (caller.Session.PlayerId == Guid.Empty || caller.Session.ConnectionId == Guid.Empty || caller.MapId == Guid.Empty)
            return new(PokerRegistryError.InvalidPresence);
        if (!IsValidTableName(tableName)) return new(PokerRegistryError.InvalidTableName);
        PokerTable candidate;
        try { candidate = new PokerTable(rules); }
        catch (ArgumentException) { return new(PokerRegistryError.InvalidRules); }
        var key = new PokerTableKey(caller.MapId, caller.MapInstanceId, tableName);
        lock (_gate)
        {
            if (_members.TryGetValue(caller.Session.PlayerId, out var current))
            {
                if (current.Presence.Session != caller.Session) return new(PokerRegistryError.SessionChanged);
                if (current.Leaving) return new(PokerRegistryError.Leaving);
                if (current.Entry.Key != key) return new(PokerRegistryError.AlreadyAtAnotherTable);
                if (current.Entry.Rules != rules) return new(PokerRegistryError.RulesConflict);
                return View(current); // No second seat or extra starting chips.
            }
            var exists = _tables.TryGetValue(key, out var entry);
            if (exists && entry!.Rules != rules) return new(PokerRegistryError.RulesConflict);
            if (!exists && _tables.Count >= _maximumTables) return new(PokerRegistryError.Capacity);
            entry ??= new Entry { Key = key, Rules = rules, Table = candidate };
            var error = entry.Table.Join(caller.Session.PlayerId, playerName);
            if (error != PokerError.None) return new(PokerRegistryError.PokerRejected, Detail: error);
            if (!exists) _tables.Add(key, entry);
            var member = new Membership { Presence = caller, Entry = entry };
            _members.Add(caller.Session.PlayerId, member);
            entry.Players.Add(caller.Session.PlayerId);
            return View(member);
        }
    }

    public PokerRegistryResult Snapshot(PokerPresence caller, Guid tableInstanceId)
    {
        lock (_gate)
        {
            var error = Find(caller, tableInstanceId, out var member);
            return error == PokerRegistryError.None ? View(member!) : new(error);
        }
    }

    public PokerRegistryResult StartHand(PokerPresence caller, Guid tableInstanceId, long revision, DateTimeOffset now)
    {
        lock (_gate)
        {
            var error = Find(caller, tableInstanceId, out var member);
            if (error != PokerRegistryError.None) return new(error);
            var entry = member!.Entry;
            if (entry.Table.Snapshot(caller.Session.PlayerId).Revision != revision)
                return Rejected(member, PokerError.StaleState);
            var result = entry.Table.StartHand(caller.Session.PlayerId, now);
            Cleanup(entry, now);
            return result == PokerError.None ? View(member) : Rejected(member, result);
        }
    }

    public PokerRegistryResult Act(PokerPresence caller, Guid tableInstanceId, long handId, long revision,
        PokerAction action, long raiseTo, DateTimeOffset now)
    {
        lock (_gate)
        {
            var error = Find(caller, tableInstanceId, out var member);
            if (error != PokerRegistryError.None) return new(error);
            var entry = member!.Entry;
            var result = entry.Table.Act(caller.Session.PlayerId, handId, revision, action, raiseTo, now);
            Cleanup(entry, now);
            return result == PokerError.None ? View(member) : Rejected(member, result);
        }
    }

    public PokerRegistryResult Leave(PokerSession caller, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_members.TryGetValue(caller.PlayerId, out var member)) return new(PokerRegistryError.NotSeated);
            if (member.Presence.Session != caller) return new(PokerRegistryError.SessionChanged);
            var id = member.Entry.Id;
            Leave(member, now);
            return new(PokerRegistryError.None, id);
        }
    }

    /// <summary>
    /// Checks presence OUTSIDE the registry lock, then applies observations only to the same
    /// membership object. A join/leave racing with this sweep cannot invalidate a new membership.
    /// The callback may read player locks; it must identify the current authenticated session,
    /// map and instance. Missing sessions leave once, retaining active-hand stakes until settled.
    /// </summary>
    public void Tick(DateTimeOffset now, Func<PokerPresence, bool> isPresent)
    {
        ArgumentNullException.ThrowIfNull(isPresent);
        Membership[] observed;
        lock (_gate) observed = _members.Values.Where(m => !m.Leaving).ToArray();
        var absent = observed.Where(m => !isPresent(m.Presence)).ToArray();
        lock (_gate)
        {
            foreach (var member in absent)
            {
                if (_members.TryGetValue(member.Presence.Session.PlayerId, out var current) &&
                    ReferenceEquals(current, member)) Leave(member, now);
            }
            foreach (var entry in _tables.Values.ToArray())
            {
                entry.Table.Tick(now);
                Cleanup(entry, now);
            }
        }
    }

    public PokerPresence[] Memberships()
    {
        lock (_gate) return _members.Values.Select(m => m.Presence).ToArray();
    }

    /// <summary>Detached per-recipient views, emitted only after a revision changes.</summary>
    public PokerDelivery[] CollectUpdates()
    {
        lock (_gate)
        {
            var updates = new List<PokerDelivery>();
            foreach (var entry in _tables.Values)
            {
                var revision = entry.Table.Snapshot(entry.Players.First()).Revision;
                if (revision == entry.PublishedRevision) continue;
                foreach (var id in entry.Players)
                {
                    var member = _members[id];
                    if (!member.Leaving)
                        updates.Add(new(member.Presence.Session, entry.Id, entry.Table.Snapshot(id)));
                }
                entry.PublishedRevision = revision;
            }
            return updates.ToArray();
        }
    }

    private PokerRegistryError Find(PokerPresence caller, Guid tableId, out Membership? member)
    {
        if (!_members.TryGetValue(caller.Session.PlayerId, out member)) return PokerRegistryError.NotSeated;
        if (member.Presence.Session != caller.Session) return PokerRegistryError.SessionChanged;
        if (member.Leaving) return PokerRegistryError.Leaving;
        if (member.Presence.MapId != caller.MapId || member.Presence.MapInstanceId != caller.MapInstanceId)
            return PokerRegistryError.WrongLocation;
        return member.Entry.Id == tableId ? PokerRegistryError.None : PokerRegistryError.WrongTable;
    }
    private static PokerRegistryResult View(Membership member) => new(PokerRegistryError.None,
        member.Entry.Id, member.Entry.Table.Snapshot(member.Presence.Session.PlayerId));
    private static PokerRegistryResult Rejected(Membership member, PokerError error) =>
        View(member) with { Error = PokerRegistryError.PokerRejected, Detail = error };
    private static bool Playing(PokerPhase phase) => phase is >= PokerPhase.PreFlop and <= PokerPhase.River;

    private void Leave(Membership member, DateTimeOffset now)
    {
        if (member.Leaving) return;
        var entry = member.Entry;
        var id = member.Presence.Session.PlayerId;
        var before = entry.Table.Snapshot(id);
        var retained = Playing(before.Phase) && before.Seats.Single(s => s.PlayerId == id).InHand;
        member.Leaving = true;
        entry.Table.Leave(id, now);
        if (!retained) Remove(member);
        Cleanup(entry, now);
    }
    private void Cleanup(Entry entry, DateTimeOffset now)
    {
        if (entry.Players.Count == 0) return;
        if (Playing(entry.Table.Snapshot(entry.Players.First()).Phase)) return;
        foreach (var id in entry.Players.ToArray())
        {
            var member = _members[id];
            if (!member.Leaving) continue;
            entry.Table.Leave(id, now);
            Remove(member);
        }
    }
    private void Remove(Membership member)
    {
        _members.Remove(member.Presence.Session.PlayerId);
        member.Entry.Players.Remove(member.Presence.Session.PlayerId);
        if (member.Entry.Players.Count == 0) _tables.Remove(member.Entry.Key);
    }
}
