#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Progression;

public sealed record MiniGameProgress(long Experience = 0, long Wins = 0, int SelectedBack = 0)
{
    public int Level => MiniGameProgression.Level(Experience);
    public bool IsValid => Experience >= 0 && Experience <= MiniGameProgression.MaximumExperience &&
        Wins >= 0 && MiniGameProgression.IsUnlocked(SelectedBack, Experience);
    public MiniGameProgress WithWin() => this with
    {
        Experience = Math.Min(MiniGameProgression.MaximumExperience, Experience + MiniGameProgression.ExperiencePerWin),
        Wins = Wins == long.MaxValue ? Wins : Wins + 1,
    };
}

/// <summary>Character + game separation. No client-supplied XP and no currency operations.</summary>
public interface IMiniGameProgressStore
{
    MiniGameProgress Load(Guid character, string game);
    MiniGameProgress SelectBack(Guid character, string game, int backId);
    MiniGameProgress AwardWin(Guid character, string game, Guid tableInstance, long hand);
}

public static class MiniGameProgressKeys
{
    public static void Validate(Guid character, string game)
    {
        if (character == Guid.Empty || string.IsNullOrEmpty(game) || game.Length > 32 ||
            !game.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new ArgumentException("Invalid character or mini-game key.");
    }
    public static void ValidateReceipt(Guid table, long hand)
    {
        if (table == Guid.Empty || hand <= 0) throw new ArgumentException("Invalid completed-hand receipt.");
    }
}

/// <summary>Isolated fixture/default for standalone registry tests. Runtime explicitly uses SQLite.</summary>
public sealed class MemoryMiniGameProgressStore : IMiniGameProgressStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid, string), MiniGameProgress> _profiles = new();
    private readonly HashSet<(Guid, string, Guid, long)> _receipts = new();
    public MiniGameProgress Load(Guid character, string game)
    {
        MiniGameProgressKeys.Validate(character, game);
        lock (_gate) return _profiles.GetValueOrDefault((character, game)) ?? new();
    }
    public MiniGameProgress SelectBack(Guid character, string game, int backId)
    {
        lock (_gate)
        {
            var profile = Load(character, game);
            if (!MiniGameProgression.IsUnlocked(backId, profile.Experience))
                throw new ArgumentOutOfRangeException(nameof(backId), "This back is locked.");
            return _profiles[(character, game)] = profile with { SelectedBack = backId };
        }
    }
    public MiniGameProgress AwardWin(Guid character, string game, Guid tableInstance, long hand)
    {
        MiniGameProgressKeys.ValidateReceipt(tableInstance, hand);
        lock (_gate)
        {
            var profile = Load(character, game);
            if (!_receipts.Add((character, game, tableInstance, hand))) return profile;
            return _profiles[(character, game)] = profile.WithWin();
        }
    }
}
