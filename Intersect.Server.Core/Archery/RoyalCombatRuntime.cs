using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Maps;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.Archery;

internal static class RoyalCombatRuntime
{
    private static readonly object Gate = new();
    private static RoyalCombatConfiguration _current;
    internal static string ProgressPath = "resources/weapon-masteries.db";
    internal static RoyalCombatConfiguration Current
    {
        get
        {
            lock (Gate)
                return _current ??= File.Exists("resources/royal-combat.json")
                    ? RoyalCombatConfiguration.FromJson(File.ReadAllText("resources/royal-combat.json")) : new();
        }
    }
    internal static void Save(string json)
    {
        var config = RoyalCombatConfiguration.FromJson(json);
        foreach (var range in config.Ranges.Where(r => r.Enabled))
            if (MapController.Get(range.MapId) == null || range.StartX >= Intersect.Options.Instance.Map.MapWidth ||
                range.StartY >= Intersect.Options.Instance.Map.MapHeight) throw new InvalidDataException("royal-map");
        foreach (var id in config.Masteries.SelectMany(m => m.WeaponIds))
            if (ItemDescriptor.Get(id) is not { ItemType: ItemType.Equipment } item ||
                item.EquipmentSlot != Intersect.Options.Instance.Equipment.WeaponSlot) throw new InvalidDataException("royal-weapon");
        lock (Gate)
        {
            Directory.CreateDirectory("resources");
            File.WriteAllText("resources/royal-combat.json.tmp", config.ToJson());
            File.Move("resources/royal-combat.json.tmp", "resources/royal-combat.json", true);
            _current = config;
        }
    }
    private static SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ProgressPath))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = ProgressPath, Pooling = false, DefaultTimeout = 2 }.ToString());
        try
        {
            connection.Open();
            using var schema = connection.CreateCommand();
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS WeaponMasteries(CharacterId TEXT NOT NULL, Category TEXT NOT NULL,
                    Experience INTEGER NOT NULL CHECK(Experience>=0), PRIMARY KEY(CharacterId,Category));
                """;
            schema.ExecuteNonQuery(); return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    internal static Dictionary<string, long> Load(Guid character)
    {
        lock (Gate)
        {
            using var connection = Open(); using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Category,Experience FROM WeaponMasteries WHERE CharacterId=$id";
            cmd.Parameters.AddWithValue("$id", character.ToString("N"));
            using var reader = cmd.ExecuteReader(); var result = new Dictionary<string, long>();
            while (reader.Read()) result.Add(reader.GetString(0), reader.GetInt64(1));
            return result;
        }
    }
    internal static long Award(Guid character, MasteryCategory category, int experience)
    {
        if (character == Guid.Empty || experience is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(experience));
        lock (Gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO WeaponMasteries(CharacterId,Category,Experience) VALUES($id,$key,MIN($xp,$cap))
                ON CONFLICT(CharacterId,Category) DO UPDATE SET Experience=MIN($cap,Experience+$xp)
                RETURNING Experience;
                """;
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$key", category.Key);
            cmd.Parameters.AddWithValue("$xp", experience); cmd.Parameters.AddWithValue("$cap", category.Maximum);
            var result = (long)cmd.ExecuteScalar(); transaction.Commit(); return result;
        }
    }
}
