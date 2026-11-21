using System.Text.Json;
using Intersect.Framework.Core.Archery;

namespace Intersect.Server.Archery;

internal static partial class RoyalCombatRuntime
{
    internal static Dictionary<string, string[]> LoadWeaponSkillSlots(Guid character)
    {
        lock (Gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT Category,SlotsJson FROM WeaponSkillLoadouts WHERE CharacterId=$id";
            command.Parameters.AddWithValue("$id", character.ToString("N"));
            using var reader = command.ExecuteReader();
            var result = new Dictionary<string, string[]>();
            while (reader.Read())
            {
                try
                {
                    var slots = JsonSerializer.Deserialize<string[]>(reader.GetString(1));
                    if (WeaponSkillLoadout.Valid(slots)) result[reader.GetString(0)] = slots!;
                }
                catch (JsonException) { /* A damaged layout must not block mastery/login synchronization. */ }
            }
            return result;
        }
    }

    internal static void SaveWeaponSkillSlots(Guid character, string category, string[] slots)
    {
        if (character == Guid.Empty || !MasteryRules.Key(category) || !WeaponSkillLoadout.Valid(slots))
            throw new ArgumentException("weapon-skill-slots");
        lock (Gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO WeaponSkillLoadouts(CharacterId,Category,SlotsJson) VALUES($id,$category,$slots)
                ON CONFLICT(CharacterId,Category) DO UPDATE SET SlotsJson=excluded.SlotsJson;
                """;
            command.Parameters.AddWithValue("$id", character.ToString("N"));
            command.Parameters.AddWithValue("$category", category);
            command.Parameters.AddWithValue("$slots", JsonSerializer.Serialize(slots));
            command.ExecuteNonQuery();
        }
    }
}
