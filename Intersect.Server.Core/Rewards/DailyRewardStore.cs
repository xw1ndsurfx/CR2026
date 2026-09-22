#nullable enable
using Microsoft.Data.Sqlite;

namespace Intersect.Server.Rewards;

internal sealed class DailyRewardStore
{
    internal sealed record State(int LastClaimDate, int CycleDay);

    private readonly object _gate = new();
    private readonly string _path = Path.GetFullPath(Path.Combine("resources", "daily-rewards.db"));

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
            CREATE TABLE IF NOT EXISTS DailyRewardAccounts (
                UserId TEXT NOT NULL PRIMARY KEY,
                LastClaimDate INTEGER NOT NULL DEFAULT 0,
                CycleDay INTEGER NOT NULL DEFAULT 0
            );
            """;
        schema.ExecuteNonQuery();
        return connection;
    }

    internal State Load(Guid userId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT LastClaimDate,CycleDay FROM DailyRewardAccounts WHERE UserId=$user;";
            command.Parameters.AddWithValue("$user", userId.ToString("N"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? new State(reader.GetInt32(0), reader.GetInt32(1)) : new State(0, 0);
        }
    }

    internal bool TryClaim(Guid userId, int today, int nextDay, out State state)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            int lastClaim = 0;
            int cycleDay = 0;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT LastClaimDate,CycleDay FROM DailyRewardAccounts WHERE UserId=$user;";
                read.Parameters.AddWithValue("$user", userId.ToString("N"));
                using var reader = read.ExecuteReader();
                if (reader.Read())
                {
                    lastClaim = reader.GetInt32(0);
                    cycleDay = reader.GetInt32(1);
                }
            }

            if (lastClaim == today)
            {
                state = new State(lastClaim, cycleDay);
                transaction.Rollback();
                return false;
            }

            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = """
                INSERT INTO DailyRewardAccounts(UserId,LastClaimDate,CycleDay)
                VALUES($user,$date,$day)
                ON CONFLICT(UserId) DO UPDATE SET LastClaimDate=excluded.LastClaimDate,CycleDay=excluded.CycleDay;
                """;
            write.Parameters.AddWithValue("$user", userId.ToString("N"));
            write.Parameters.AddWithValue("$date", today);
            write.Parameters.AddWithValue("$day", nextDay);
            write.ExecuteNonQuery();
            transaction.Commit();
            state = new State(today, nextDay);
            return true;
        }
    }
}
