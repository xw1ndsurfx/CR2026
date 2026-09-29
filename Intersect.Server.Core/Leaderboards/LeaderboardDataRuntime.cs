using System.Globalization;
using System.Text;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Professions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.Leaderboards;

internal sealed record MiniGameLeaderboardRow(
    string GameKey,
    string GameName,
    string PlayerName,
    int Level,
    long Experience,
    long Wins
);

internal sealed record ProfessionLeaderboardRow(
    string ProfessionKey,
    string ProfessionName,
    int MaximumLevel,
    string PlayerName,
    int Level,
    long Experience
);

internal static class LeaderboardDataRuntime
{
    private static readonly string MiniGameProgressPath =
        Path.GetFullPath(Path.Combine("resources", "minigames-test.db"));

    private static readonly Dictionary<string, string> MiniGameNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [MiniGameProgression.Poker] = "Poker",
            [MiniGameProgression.Blackjack] = "Blackjack",
            [MiniGameProgression.Potions] = "Royal Alchemy",
            [MiniGameProgression.Roulette] = "Roulette",
        };

    internal static IReadOnlyList<MiniGameLeaderboardRow> MiniGames(int limitPerGame)
    {
        limitPerGame = Math.Clamp(limitPerGame, 1, 100);

        var profiles = new Dictionary<(Guid PlayerId, string Game), MiniGameProgress>();
        ReadStandaloneMiniGameProfiles(profiles);
        ReadFundedMiniGameProfiles(profiles);

        var names = ReadPlayerNames();
        foreach (var player in Player.OnlinePlayersSnapshot())
            names[player.Id] = player.Name;

        var rows = new List<MiniGameLeaderboardRow>();
        foreach (var game in MiniGameNames)
        {
            var ranked = profiles
                .Where(pair => string.Equals(pair.Key.Game, game.Key, StringComparison.OrdinalIgnoreCase))
                .Select(pair => new
                {
                    pair.Key.PlayerId,
                    Progress = pair.Value,
                    Name = names.GetValueOrDefault(pair.Key.PlayerId),
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .OrderByDescending(row => row.Progress.Level)
                .ThenByDescending(row => row.Progress.Experience)
                .ThenByDescending(row => row.Progress.Wins)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limitPerGame)
                .ToArray();

            rows.AddRange(
                ranked.Select(row => new MiniGameLeaderboardRow(
                    game.Key,
                    game.Value,
                    row.Name!,
                    row.Progress.Level,
                    row.Progress.Experience,
                    row.Progress.Wins
                ))
            );
        }

        return rows;
    }

    internal static IReadOnlyList<ProfessionLeaderboardRow> Professions(int limitPerProfession)
    {
        limitPerProfession = Math.Clamp(limitPerProfession, 1, 100);

        var definitions = ProfessionConfigurationRuntime.Current.Professions ?? [];
        if (definitions.Length == 0)
            return [];

        var byProfessionAndPlayer = new Dictionary<(Guid ProfessionId, Guid PlayerId), long>();

        try
        {
            var professionIds = definitions.Select(definition => definition.Id).ToArray();
            using var context = DbInterface.CreatePlayerContext();

            var variables = context.Player_Variables
                .AsNoTracking()
                .Include(variable => variable.Player)
                .Where(variable => professionIds.Contains(variable.VariableId))
                .ToArray();

            foreach (var variable in variables)
            {
                var raw = Math.Max(0L, variable.Value?.Integer ?? 0L);
                if (raw <= 0 || variable.Player == null)
                    continue;

                byProfessionAndPlayer[(variable.VariableId, variable.PlayerId)] = Math.Max(0L, raw - 1L);
            }
        }
        catch
        {
            // If the persisted snapshot is temporarily unavailable, online values below
            // can still produce a useful partial leaderboard.
        }

        var names = ReadPlayerNames();
        foreach (var player in Player.OnlinePlayersSnapshot())
        {
            names[player.Id] = player.Name;
            foreach (var definition in definitions)
            {
                if (!ProfessionRuntime.IsLearned(player, definition.Id))
                    continue;

                byProfessionAndPlayer[(definition.Id, player.Id)] =
                    ProfessionRuntime.GetExperience(player, definition.Id);
            }
        }

        var result = new List<ProfessionLeaderboardRow>();
        foreach (var definition in definitions.OrderBy(definition => definition.Name))
        {
            var ranked = byProfessionAndPlayer
                .Where(pair => pair.Key.ProfessionId == definition.Id)
                .Select(pair => new
                {
                    pair.Key.PlayerId,
                    Experience = pair.Value,
                    Name = names.GetValueOrDefault(pair.Key.PlayerId),
                    Level = definition.LevelForExperience(pair.Value),
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .OrderByDescending(row => row.Level)
                .ThenByDescending(row => row.Experience)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limitPerProfession)
                .ToArray();

            var key = Slug(definition.Name);
            result.AddRange(
                ranked.Select(row => new ProfessionLeaderboardRow(
                    key,
                    definition.Name,
                    definition.MaximumLevel,
                    row.Name!,
                    row.Level,
                    row.Experience
                ))
            );
        }

        return result;
    }

    private static Dictionary<Guid, string> ReadPlayerNames()
    {
        try
        {
            using var context = DbInterface.CreatePlayerContext();
            return context.Players
                .AsNoTracking()
                .Select(player => new { player.Id, player.Name })
                .ToDictionary(player => player.Id, player => player.Name);
        }
        catch
        {
            return [];
        }
    }

    private static void ReadStandaloneMiniGameProfiles(
        Dictionary<(Guid PlayerId, string Game), MiniGameProgress> profiles
    )
    {
        if (!File.Exists(MiniGameProgressPath))
            return;

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = MiniGameProgressPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                    DefaultTimeout = 2,
                }.ToString()
            );
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT CharacterId,GameKey,Experience,Wins,SelectedBack FROM MiniGameProfiles;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!Guid.TryParse(reader.GetString(0), out var character))
                    continue;

                var game = reader.GetString(1);
                if (!MiniGameNames.ContainsKey(game))
                    continue;

                MergeProfile(
                    profiles,
                    character,
                    game,
                    new MiniGameProgress(reader.GetInt64(2), reader.GetInt64(3), reader.GetInt32(4))
                );
            }
        }
        catch
        {
            // The Wiki API must remain available even while the mini-game database is busy.
        }
    }

    private static void ReadFundedMiniGameProfiles(
        Dictionary<(Guid PlayerId, string Game), MiniGameProgress> profiles
    )
    {
        try
        {
            using var context = DbInterface.CreatePlayerContext();
            if (context.Database.GetDbConnection() is not SqliteConnection databaseConnection)
                return;

            var builder = new SqliteConnectionStringBuilder(databaseConnection.ConnectionString)
            {
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = 2,
            };

            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();

            using (var exists = connection.CreateCommand())
            {
                exists.CommandText =
                    "SELECT 1 FROM sqlite_master WHERE type='table' AND name='PokerMoneyProfiles' LIMIT 1;";
                if (exists.ExecuteScalar() == null)
                    return;
            }

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT CharacterId,Game,Experience,Wins,SelectedBack FROM PokerMoneyProfiles;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!Guid.TryParse(reader.GetString(0), out var character))
                    continue;

                var game = reader.GetString(1);
                if (!MiniGameNames.ContainsKey(game))
                    continue;

                MergeProfile(
                    profiles,
                    character,
                    game,
                    new MiniGameProgress(reader.GetInt64(2), reader.GetInt64(3), reader.GetInt32(4))
                );
            }
        }
        catch
        {
            // Funded mini-games are optional. Standalone progression can still be published.
        }
    }

    private static void MergeProfile(
        Dictionary<(Guid PlayerId, string Game), MiniGameProgress> profiles,
        Guid playerId,
        string game,
        MiniGameProgress candidate
    )
    {
        if (!candidate.IsValid)
            return;

        var key = (playerId, game.ToLowerInvariant());
        if (!profiles.TryGetValue(key, out var current) ||
            candidate.Experience > current.Experience ||
            candidate.Experience == current.Experience && candidate.Wins > current.Wins)
        {
            profiles[key] = candidate;
        }
    }

    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var dash = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                dash = false;
            }
            else if (!dash && builder.Length > 0)
            {
                builder.Append('-');
                dash = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
