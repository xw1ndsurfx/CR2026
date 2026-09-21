#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Progression;

namespace Intersect.Server.MiniGames.Poker;

public sealed partial class PokerTableRegistry
{
    private sealed record PendingExperience(Guid Player, Guid Table, long Hand);
    private readonly IMiniGameProgressStore _progression = new MemoryMiniGameProgressStore();
    private readonly Dictionary<PendingExperience, PendingExperience> _pendingExperience = new();
    private DateTimeOffset _retryExperienceAt;
    public Exception? ProgressionFailure { get; private set; }

    public PokerTableRegistry(IMiniGameProgressStore progression, int maximumTables = 1024) : this(maximumTables)
    { _progression = progression ?? throw new ArgumentNullException(nameof(progression)); }

    private bool TryLoadProgress(Guid player, out MiniGameProgress progress)
    {
        try
        {
            progress = _progression.Load(player, MiniGameProgression.Poker);
            if (!progress.IsValid) throw new InvalidOperationException("Invalid mini-game profile.");
            return true;
        }
        catch (Exception exception)
        { ProgressionFailure = exception; progress = new(); return false; }
    }
    private bool HasPendingExperience(Entry entry) => _pendingExperience.Keys.Any(p => p.Table == entry.Id);

    private void QueueExperience(Entry entry, Guid player, long hand)
    {
        var award = new PendingExperience(player, entry.Id, hand);
        _pendingExperience.TryAdd(award, award);
    }

    // Called under the registry gate. No player/network callback. Failed awards keep their
    // original receipt and retry with backoff. A table cannot start another hand while pending.
    private void ProcessExperience(DateTimeOffset now)
    {
        if (_pendingExperience.Count == 0 || now < _retryExperienceAt) return;
        foreach (var award in _pendingExperience.Keys.Take(16).ToArray())
        {
            try
            {
                _members.TryGetValue(award.Player, out var member);
                var before = member?.Entry.Extras.Profiles.GetValueOrDefault(award.Player) ?? _progression.Load(award.Player, MiniGameProgression.Poker);
                var profile = _progression.AwardWin(award.Player, MiniGameProgression.Poker, award.Table, award.Hand);
                if (!profile.IsValid) throw new InvalidOperationException("Invalid mini-game award result.");
                if (member != null)
                {
                    member.Entry.Extras.Profiles[award.Player] = profile;
                    member.Entry.PublishedRevision = -1;
                    if (profile.Level > before.Level)
                    {
                        var rewards = member.Entry.Options.EffectiveLevelRewards
                            .Where(r => r.Level > before.Level && r.Level <= profile.Level).ToArray();
                        if (rewards.Length > 0)
                            _levelRewards.Enqueue(new(member.Presence.Session, member.Entry.Id, profile.Level, rewards));
                        _questUpdates.Enqueue(new(member.Presence.Session, member.Entry.Id,
                            new PokerQuestUpdate(false, 0, profile.Level)));
                    }
                }
                _pendingExperience.Remove(award);
                ProgressionFailure = null;
            }
            catch (Exception exception)
            {
                ProgressionFailure = exception;
                _retryExperienceAt = now.AddSeconds(2);
                break;
            }
        }
    }
}
