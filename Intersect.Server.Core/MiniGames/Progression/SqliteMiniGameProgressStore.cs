#nullable enable
using System;
using System.IO;
using Intersect.Framework.Core.MiniGames;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.MiniGames.Progression;

/// <summary>
/// Dedicated progression database, independent of the player's RPG level and inventory.
/// A receipt and its XP increment commit in the same transaction. Never replace unreadable data.
/// </summary>
public sealed class SqliteMiniGameProgressStore : IMiniGameProgressStore
{
    private readonly object _gate = new();
    private readonly string _path;
    public SqliteMiniGameProgressStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A database path is required.", nameof(path));
        _path = Path.GetFullPath(path);
    }
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = _path, Mode = SqliteOpenMode.ReadWriteCreate, DefaultTimeout = 2, Pooling = false }.ToString());
        try
        {
            connection.Open();
            using var schema = connection.CreateCommand();
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS MiniGameProfiles (
                    CharacterId TEXT NOT NULL, GameKey TEXT NOT NULL,
                    Experience INTEGER NOT NULL CHECK(Experience BETWEEN 0 AND 30000),
                    Wins INTEGER NOT NULL CHECK(Wins >= 0),
                    SelectedBack INTEGER NOT NULL CHECK(SelectedBack BETWEEN 0 AND 5),
                    PRIMARY KEY(CharacterId, GameKey));
                CREATE TABLE IF NOT EXISTS MiniGameWinReceipts (
                    CharacterId TEXT NOT NULL, GameKey TEXT NOT NULL,
                    TableId TEXT NOT NULL, HandId INTEGER NOT NULL CHECK(HandId > 0),
                    PRIMARY KEY(CharacterId, GameKey, TableId, HandId));
                """;
            schema.ExecuteNonQuery();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction,
        Guid character, string game, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$character", character.ToString("N"));
        command.Parameters.AddWithValue("$game", game);
        return command;
    }
    private static MiniGameProgress Read(SqliteConnection connection, SqliteTransaction? transaction, Guid character, string game)
    {
        using var command = Command(connection, transaction, character, game,
            "SELECT Experience, Wins, SelectedBack FROM MiniGameProfiles WHERE CharacterId=$character AND GameKey=$game;");
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new();
        var result = new MiniGameProgress(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2));
        if (!result.IsValid) throw new InvalidDataException("Invalid saved mini-game progression; refusing to reset it.");
        return result;
    }
    private static void Write(SqliteConnection connection, SqliteTransaction transaction,
        Guid character, string game, MiniGameProgress progress)
    {
        using var command = Command(connection, transaction, character, game, """
            INSERT INTO MiniGameProfiles(CharacterId, GameKey, Experience, Wins, SelectedBack)
            VALUES($character,$game,$xp,$wins,$back)
            ON CONFLICT(CharacterId,GameKey) DO UPDATE SET
                Experience=excluded.Experience, Wins=excluded.Wins, SelectedBack=excluded.SelectedBack;
            """);
        command.Parameters.AddWithValue("$xp", progress.Experience);
        command.Parameters.AddWithValue("$wins", progress.Wins);
        command.Parameters.AddWithValue("$back", progress.SelectedBack);
        command.ExecuteNonQuery();
    }
    public MiniGameProgress Load(Guid character, string game)
    {
        MiniGameProgressKeys.Validate(character, game);
        lock (_gate)
        {
            using var connection = Open();
            return Read(connection, null, character, game);
        }
    }
    public MiniGameProgress SelectBack(Guid character, string game, int backId)
    {
        MiniGameProgressKeys.Validate(character, game);
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var progress = Read(connection, transaction, character, game);
            if (!MiniGameProgression.IsUnlocked(backId, progress.Experience))
                throw new ArgumentOutOfRangeException(nameof(backId), "This back is locked.");
            progress = progress with { SelectedBack = backId };
            Write(connection, transaction, character, game, progress);
            transaction.Commit();
            return progress;
        }
    }
    public MiniGameProgress AwardWin(Guid character, string game, Guid tableInstance, long hand)
    {
        MiniGameProgressKeys.Validate(character, game);
        MiniGameProgressKeys.ValidateReceipt(tableInstance, hand);
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var progress = Read(connection, transaction, character, game);
            using var receipt = Command(connection, transaction, character, game, """
                INSERT INTO MiniGameWinReceipts(CharacterId,GameKey,TableId,HandId)
                VALUES($character,$game,$table,$hand) ON CONFLICT DO NOTHING;
                """);
            receipt.Parameters.AddWithValue("$table", tableInstance.ToString("N"));
            receipt.Parameters.AddWithValue("$hand", hand);
            if (receipt.ExecuteNonQuery() == 1)
            {
                progress = progress.WithWin();
                Write(connection, transaction, character, game, progress);
            }
            transaction.Commit();
            return progress;
        }
    }
}
