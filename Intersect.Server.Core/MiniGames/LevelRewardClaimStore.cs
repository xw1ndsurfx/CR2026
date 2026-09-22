#nullable enable
using Microsoft.Data.Sqlite;

namespace Intersect.Server.MiniGames;

internal sealed class LevelRewardClaimStore
{
    private readonly object _gate = new();
    private readonly string _path = Path.GetFullPath(Path.Combine("resources", "reward-claims.db"));

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 2,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var schema = connection.CreateCommand();
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS MiniGameLevelRewardClaims (
                CharacterId TEXT NOT NULL,
                GameKey TEXT NOT NULL,
                Level INTEGER NOT NULL CHECK(Level BETWEEN 2 AND 25),
                ClaimedUtc INTEGER NOT NULL,
                PRIMARY KEY(CharacterId, GameKey, Level)
            );
            """;
        schema.ExecuteNonQuery();
        return connection;
    }

    internal bool IsClaimed(Guid characterId, string gameKey, int level)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT 1 FROM MiniGameLevelRewardClaims
                WHERE CharacterId=$character AND GameKey=$game AND Level=$level LIMIT 1;
                """;
            command.Parameters.AddWithValue("$character", characterId.ToString("N"));
            command.Parameters.AddWithValue("$game", gameKey);
            command.Parameters.AddWithValue("$level", level);
            return command.ExecuteScalar() != null;
        }
    }

    internal bool TryMarkClaimed(Guid characterId, string gameKey, int level)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO MiniGameLevelRewardClaims(CharacterId,GameKey,Level,ClaimedUtc)
                VALUES($character,$game,$level,$utc)
                ON CONFLICT(CharacterId,GameKey,Level) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$character", characterId.ToString("N"));
            command.Parameters.AddWithValue("$game", gameKey);
            command.Parameters.AddWithValue("$level", level);
            command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return command.ExecuteNonQuery() == 1;
        }
    }
}
