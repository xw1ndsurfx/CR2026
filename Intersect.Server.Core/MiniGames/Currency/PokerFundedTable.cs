#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;

namespace Intersect.Server.MiniGames.Currency;

internal sealed record FundedWin(Guid Player, string Name, long Amount);
internal sealed record FundedLevelReward(Guid Player, int Level, PokerLevelReward[] Rewards);
internal sealed record FundedLevelNotice(Guid Player, int Level);
internal sealed record FundedQuestUpdate(Guid Player, PokerQuestUpdate Update);

/// <summary>The tested hold'em engine owns the rules; this adapter owns funded hand checkpoints.</summary>
internal sealed class PokerFundedTable
{
    private sealed class Member(MoneySeat escrow, string name)
    {
        public MoneySeat Escrow = escrow;
        public string Name = name;
        public bool Leaving;
        public int HandBack;
        public MiniGameProgress Profile = new();
    }
    public Guid Id { get; } = Guid.NewGuid();
    public PokerTableKey Key { get; }
    public Guid Currency { get; }
    public string House { get; }
    public PokerRules Rules { get; }
    public PokerTableOptions Options { get; }
    public long Reserve { get; }
    public string CurrencyName { get; }
    public bool Pending { get; private set; }
    public long Version { get; private set; }
    private readonly PokerMoneyLedger _money;
    private readonly PokerTable _table;
    private readonly Dictionary<Guid, Member> _members = new();
    private readonly List<PokerPublicDecision> _decisions = new();
    private readonly Queue<FundedWin> _wins = new();
    private readonly Queue<FundedLevelReward> _levelRewards = new();
    private readonly Queue<FundedLevelNotice> _levels = new();
    private readonly Queue<FundedQuestUpdate> _questUpdates = new();
    private readonly Queue<string> _npcChat = new();
    private readonly Dictionary<Guid, long> _net = new();
    private long _decisionId, _settledHand, _npcRevision = -1;
    private Guid _dealer;
    private DateTimeOffset? _nextHand;
    private DateTimeOffset _npcDue;
    public DateTimeOffset RetryAt { get; private set; }
    public Guid[] Humans => _members.Where(p => !p.Value.Escrow.Npc).Select(p => p.Key).ToArray();
    public bool IsEmpty => _members.Count == 0;
    private PokerSnapshot Current => _table.Snapshot(_members.Keys.First());
    private static bool Playing(PokerPhase phase) => phase is >= PokerPhase.PreFlop and <= PokerPhase.River;
    public PokerFundedTable(PokerMoneyLedger money, PokerTableKey key, Guid currency, PokerRules rules, PokerTableOptions options, long reserve, string? currencyName = null)
    {
        _money = money; Key = key; Currency = currency; Rules = rules; Options = options; Reserve = reserve;
        CurrencyName = string.IsNullOrWhiteSpace(currencyName) ? "currency" : currencyName;
        _table = new PokerTable(rules);
        House = key.MapId.ToString("N") + ":" + key.Name + ":" + currency.ToString("N");
    }
    public bool Contains(Guid player) => _members.ContainsKey(player);
    public bool Leaving(Guid player) => _members.TryGetValue(player, out var m) && m.Leaving;
    public bool CanJoin()
    {
        if (Pending) return false;
        // Keep the configured dealer chair reserved even when its house is temporarily empty.
        if (Options.DealerPlays && Humans.Length >= Rules.MaxPlayers - 1) return false;
        if (_members.Count < Rules.MaxPlayers) return true;
        if (Playing(Current.Phase)) return false;
        var npc = _members.FirstOrDefault(p => p.Value.Escrow.Npc && p.Key != _dealer);
        if (npc.Value == null) return false;
        Remove(npc.Key, DateTimeOffset.UtcNow); return true;
    }
    public void Join(MoneySeat escrow, string name)
    {
        if (escrow.Npc || escrow.Table != Id || escrow.Currency != Currency || escrow.Status != 0)
            throw new MoneyRuleException("Invalid funded admission.");
        var profile = _money.Profile(escrow.Character);
        var result = _table.Join(escrow.Character, name, escrow.Amount);
        if (result != PokerError.None) throw new MoneyRuleException("Unable to take a funded seat: " + result);
        _members.Add(escrow.Character, new Member(escrow, name) { Profile = profile, HandBack = profile.SelectedBack }); ++Version;
    }
    public PokerSnapshot Snapshot(Guid player) => _table.Snapshot(player);
    public PokerPresentation Presentation(Guid player)
    {
        var state = Snapshot(player); var profile = _members[player].Profile;
        var net = state.Phase == PokerPhase.Finished && state.HandId == _settledHand ? _net.GetValueOrDefault(player) : 0;
        return new(_members.Where(p => p.Value.Escrow.Npc).Select(p => p.Key).ToArray(), _dealer, Options.AutoStart, Options.DealAnimationId)
        {
            CardBacks = state.Seats.Select(s =>
            {
                var selected = Back(s.PlayerId);
                return new PokerSeatBack(s.PlayerId, selected, selected);
            }).ToArray(),
            NetWin = net, VictoryAnimationId = net > 0 ? Options.VictoryAnimationId : Guid.Empty,
            Experience = profile.Experience, Wins = profile.Wins, ProgressPending = Pending, Decisions = _decisions.ToArray(),
            Sounds = Options.EffectiveSounds,
            Animations = Options.EffectiveAnimations,
        };
    }
    private int Back(Guid player) => _members[player].Escrow.Npc ? Options.NpcCardBackId : _members[player].Profile.SelectedBack;
    public void SelectBack(Guid player, int back)
    {
        if (Pending || Leaving(player)) throw new MoneyRuleException("FundingPending");
        _members[player].Profile = _money.SelectBack(player, back);
        _members[player].HandBack = back;
        ++Version;
    }
    public PokerError Start(Guid player, long revision, DateTimeOffset now)
    {
        if (Pending) return PokerError.IllegalAction;
        if (Current.Revision != revision) return PokerError.StaleState;
        if (Playing(Current.Phase)) return PokerError.HandInProgress;
        Complete(); Cleanup(now); PrepareNpcs(now); return StartReady(player, now);
    }
    private PokerError StartReady(Guid player, DateTimeOffset now)
    {
        var result = _table.StartHand(player, now); if (result != PokerError.None) return result;
        _decisions.Clear(); _net.Clear();
        foreach (var p in _members) p.Value.HandBack = Back(p.Key);
        var state = Current;
        var dealer = state.Seats.FirstOrDefault(s => s.PlayerId == _dealer) ?? state.Seats.First(s => s.Seat == state.DealerSeat);
        Decision(dealer.PlayerId, "deal"); ++Version; Complete(); return result;
    }
    public PokerError Act(Guid player, long hand, long revision, PokerAction action, long amount, DateTimeOffset now)
    {
        if (Pending) return PokerError.IllegalAction;
        var before = Snapshot(player); var result = _table.Act(player, hand, revision, action, amount, now);
        if (Current.Revision != before.Revision)
        {
            var actor = before.Seats.FirstOrDefault(s => s.Seat == before.ActingSeat);
            if (actor != null)
            {
                if (result == PokerError.None) Decision(actor.PlayerId,
                    action switch { PokerAction.Fold => "fold", PokerAction.Check => "check", PokerAction.Call => "call", _ => "raise" },
                    action == PokerAction.RaiseTo ? amount : action == PokerAction.Call ? before.ToCall : 0);
                else if (now >= before.Deadline) Decision(actor.PlayerId, actor.StreetBet >= before.CurrentBet ? "check" : "fold", automatic: true);
            }
            AdditionalFolds(before, actor?.PlayerId ?? Guid.Empty); ++Version;
        }
        Complete(); return result;
    }
    private void AdditionalFolds(PokerSnapshot before, Guid primary)
    {
        foreach (var s in Current.Seats.Where(s => s.PlayerId != primary && s.Folded && before.Seats.Any(b => b.PlayerId == s.PlayerId && !b.Folded && b.InHand)))
            Decision(s.PlayerId, "fold", automatic: true);
    }
    public void Leave(Guid player, DateTimeOffset now)
    {
        if (!_members.TryGetValue(player, out var m)) return;
        m.Leaving = true; ++Version; if (Pending) return;
        var before = Current; var seat = before.Seats.Single(s => s.PlayerId == player);
        if (Playing(before.Phase) && seat.InHand)
        { _table.Leave(player, now); Decision(player, "leave"); AdditionalFolds(before, Guid.Empty); Complete(); }
        else { Complete(); Remove(player, now); }
        Cleanup(now);
    }
    public void Suspend(DateTimeOffset now) { Pending = true; RetryAt = now.AddSeconds(2); ++Version; }
    public void Tick(DateTimeOffset now)
    {
        if (now < RetryAt || IsEmpty) return;
        if (Pending) { Pending = false; ++Version; }
        Complete(); Cleanup(now); if (IsEmpty) return;
        foreach (var p in _members.Where(p => p.Value.Leaving).ToArray())
        {
            var s = Current.Seats.FirstOrDefault(s => s.PlayerId == p.Key);
            if (s != null && Playing(Current.Phase) && s.InHand && !s.Leaving) _table.Leave(p.Key, now);
        }
        var before = Current;
        if (_table.Tick(now))
        {
            var actor = before.Seats.FirstOrDefault(s => s.Seat == before.ActingSeat);
            if (actor != null) Decision(actor.PlayerId, actor.StreetBet >= before.CurrentBet ? "check" : "fold", automatic: true);
            AdditionalFolds(before, actor?.PlayerId ?? Guid.Empty); ++Version;
        }
        Complete(); Cleanup(now); if (IsEmpty) return;
        var state = Current;
        if (!Playing(state.Phase))
        {
            PrepareNpcs(now); state = Current;
            var human = state.Seats.FirstOrDefault(s => !_members[s.PlayerId].Escrow.Npc && !_members[s.PlayerId].Leaving && s.Chips > 0);
            if (!Options.AutoStart || human == null || state.Seats.Count(s => !s.Leaving && s.Chips > 0) < 2) { _nextHand = null; return; }
            _nextHand ??= now.AddSeconds(5);
            if (now >= _nextHand.Value) { _nextHand = null; StartReady(human.PlayerId, now); } return;
        }
        _nextHand = null;
        var next = state.Seats.FirstOrDefault(s => s.Seat == state.ActingSeat);
        if (next == null || !_members[next.PlayerId].Escrow.Npc) return;
        if (_npcRevision != state.Revision) { _npcRevision = state.Revision; _npcDue = now.AddMilliseconds(1200); return; }
        if (now < _npcDue) return;
        var view = Snapshot(next.PlayerId); var choice = PokerNpcPolicy.Choose(view, next.PlayerId, Rules.BigBlind);
        var result = Act(next.PlayerId, view.HandId, view.Revision, choice.Action, choice.Amount, now);
        if (result is PokerError.IllegalAction or PokerError.InvalidAmount)
        { view = Snapshot(next.PlayerId); Act(next.PlayerId, view.HandId, view.Revision, view.ToCall == 0 ? PokerAction.Check : PokerAction.Fold, 0, now); }
        Cleanup(now);
    }
    private void Complete()
    {
        if (IsEmpty) return; var state = Current;
        if (state.Phase != PokerPhase.Finished || state.HandId <= _settledHand) return;
        var previousProfiles = Humans.ToDictionary(id => id, id => _members[id].Profile);
        _money.Settle(Id, state.HandId, state.Seats.ToDictionary(s => _members[s.PlayerId].Escrow.Id, s => s.Chips));
        var profiles = Humans.ToDictionary(id => id, id => _money.Profile(id));
        foreach (var seat in state.Seats)
        {
            var member = _members[seat.PlayerId]; var net = seat.Chips - member.Escrow.Amount;
            member.Escrow = member.Escrow with { Amount = seat.Chips };
            if (!member.Escrow.Npc) member.Profile = profiles[seat.PlayerId];
            if (!member.Escrow.Npc)
            {
                var beforeProfile = previousProfiles[seat.PlayerId];
                var afterProfile = profiles[seat.PlayerId];
                if (afterProfile.Level > beforeProfile.Level)
                {
                    _levels.Enqueue(new(seat.PlayerId, afterProfile.Level));
                    var rewards = Options.EffectiveLevelRewards
                        .Where(r => r.Level > beforeProfile.Level && r.Level <= afterProfile.Level).ToArray();
                    if (rewards.Length > 0) _levelRewards.Enqueue(new(seat.PlayerId, afterProfile.Level, rewards));
                }
                _questUpdates.Enqueue(new(seat.PlayerId,
                    new PokerQuestUpdate(true, Math.Max(0, net), afterProfile.Level)));
            }
            if (net <= 0) continue;
            _net[seat.PlayerId] = net; Decision(seat.PlayerId, "wins", net);
            if (!member.Escrow.Npc) _wins.Enqueue(new(seat.PlayerId, seat.Name, net));
        }
        _settledHand = state.HandId; ++Version;
    }
    private void Cleanup(DateTimeOffset now)
    {
        if (IsEmpty || Playing(Current.Phase)) return;
        foreach (var p in _members.Where(p => p.Value.Leaving).ToArray()) Remove(p.Key, now);
        if (Humans.Length == 0) foreach (var p in _members.Keys.ToArray()) Remove(p, now);
    }
    private void Remove(Guid actor, DateTimeOffset now)
    {
        var member = _members[actor]; _money.Release(member.Escrow.Id);
        _table.Leave(actor, now); _members.Remove(actor); if (_dealer == actor) _dealer = Guid.Empty; ++Version;
    }
    private void PrepareNpcs(DateTimeOffset now)
    {
        if (IsEmpty || Playing(Current.Phase) || Humans.Length == 0) return;
        foreach (var s in Current.Seats.Where(s => _members[s.PlayerId].Escrow.Npc && s.Chips == 0).ToArray()) Remove(s.PlayerId, now);
        var desired = Math.Max(0, Math.Min(Options.NpcPlayers, Rules.MaxPlayers - Humans.Length - (Options.DealerPlays ? 1 : 0)));
        while (_members.Count(p => p.Value.Escrow.Npc && p.Key != _dealer) > desired)
            Remove(_members.First(p => p.Value.Escrow.Npc && p.Key != _dealer).Key, now);
        if (Options.DealerPlays && _dealer == Guid.Empty && _members.Count < Rules.MaxPlayers) AddNpc(PokerTableTheme.DealerName, true);
        while (_members.Count(p => p.Value.Escrow.Npc && p.Key != _dealer) < desired && _members.Count < Rules.MaxPlayers)
        {
            var name = Enumerable.Range(1, 5).Select(PokerTableTheme.GuestName).First(n => _members.Values.All(m => m.Name != n));
            if (!AddNpc(name, false)) break;
        }
    }
    private bool AddNpc(string name, bool dealer)
    {
        var escrow = _money.OpenNpc(Guid.NewGuid(), Id, Currency, House, Reserve, Rules.StartingChips, Options.UnlimitedNpcBankroll);
        if (escrow == null) return false;
        var error = _table.Join(escrow.Id, name, escrow.Amount);
        if (error != PokerError.None) { _money.Release(escrow.Id); throw new MoneyRuleException("NPC admission failed: " + error); }
        _members.Add(escrow.Id, new Member(escrow, name) { HandBack = Options.NpcCardBackId });
        if (dealer) _dealer = escrow.Id; ++Version; return true;
    }
    private void Decision(Guid player, string action, long amount = 0, bool automatic = false)
    {
        var member = _members[player];
        _decisions.Add(new(++_decisionId, player, member.Name, action, amount, automatic));
        if (_decisions.Count > 12) _decisions.RemoveAt(0);
        if (member.Escrow.Npc)
        {
            _npcChat.Enqueue(PokerNotificationText.NpcAction(member.Name, action, amount, CurrencyName, automatic));
            while (_npcChat.Count > PokerNotificationText.MaximumQueuedMessages) _npcChat.Dequeue();
        }
        ++Version;
    }
    public string[] CollectNpcChat()
    {
        var messages = _npcChat.ToArray();
        _npcChat.Clear();
        return messages;
    }
    public FundedWin[] CollectWins() { var wins = _wins.ToArray(); _wins.Clear(); return wins; }
    public FundedLevelReward[] CollectLevelRewards() { var rewards = _levelRewards.ToArray(); _levelRewards.Clear(); return rewards; }
    public FundedLevelNotice[] CollectLevels() { var levels = _levels.ToArray(); _levels.Clear(); return levels; }
    public FundedQuestUpdate[] CollectQuestUpdates() { var updates = _questUpdates.ToArray(); _questUpdates.Clear(); return updates; }
}
