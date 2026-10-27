using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.Archery;
internal static partial class RoyalCombatRuntime
{
    internal static HashSet<Guid> LearnedSpells(Guid character)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT SpellId FROM MasteryLearnedSpells WHERE CharacterId=$id";
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); using var reader = cmd.ExecuteReader();
            var result = new HashSet<Guid>(); while (reader.Read()) result.Add(Guid.Parse(reader.GetString(0))); return result;
        }
    }
    internal static void RememberSpell(Guid character, Guid spell)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO MasteryLearnedSpells(CharacterId,SpellId) VALUES($id,$spell)";
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$spell", spell.ToString("N")); cmd.ExecuteNonQuery();
        }
    }
    private static void ValidateProgressionMappings(RoyalCombatConfiguration config)
    {
        foreach (var id in config.Masteries.SelectMany(c => c.Talents).SelectMany(t => t.RankSpellIds))
            if (SpellDescriptor.Get(id) == null) throw new InvalidDataException("mastery-spell");
        foreach (var rule in config.Ammunition)
        {
            var weapon = ItemDescriptor.Get(rule.WeaponId);
            if (weapon is not { ItemType: ItemType.Equipment } || weapon.EquipmentSlot != Intersect.Options.Instance.Equipment.WeaponSlot ||
                weapon.Projectile is not { AmmoItemId: var nativeId } || nativeId == Guid.Empty) throw new InvalidDataException("ammo-weapon");
            var projectiles = rule.ProjectileIds.Append(weapon.ProjectileId).Distinct().Select(ProjectileDescriptor.Get).ToArray();
            if (projectiles.Any(p => p == null || p.AmmoRequired is < 1 or > 100 ||
                    ItemDescriptor.Get(p.AmmoItemId) is not { Stackable: true } item || item.ItemType is not (ItemType.Ammo or ItemType.None or ItemType.Crafting)) ||
                projectiles.Select(p => p.AmmoItemId).Distinct().Count() != projectiles.Length)
                throw new InvalidDataException("ammo-projectile");
        }
    }
    internal static Dictionary<Guid, Guid> LoadAmmo(Guid character)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT WeaponId,ItemId FROM WeaponAmmoSelection WHERE CharacterId=$id";
            cmd.Parameters.AddWithValue("$id", character.ToString("N"));
            using var reader = cmd.ExecuteReader(); var result = new Dictionary<Guid, Guid>();
            while (reader.Read()) result.Add(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)));
            return result;
        }
    }
    internal static void SelectAmmo(Guid character, Guid weapon, Guid item)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO WeaponAmmoSelection(CharacterId,WeaponId,ItemId) VALUES($id,$weapon,$item)
                ON CONFLICT(CharacterId,WeaponId) DO UPDATE SET ItemId=excluded.ItemId;
                """;
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$weapon", weapon.ToString("N"));
            cmd.Parameters.AddWithValue("$item", item.ToString("N")); cmd.ExecuteNonQuery();
        }
    }
    private static Dictionary<string, int> ReadRanks(SqliteConnection db, SqliteTransaction transaction, Guid character, string category)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = "SELECT Talent,Rank FROM MasteryTalentRanks WHERE CharacterId=$id AND Category=$key";
        cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$key", category);
        using var reader = cmd.ExecuteReader(); var result = new Dictionary<string, int>();
        while (reader.Read()) result.Add(reader.GetString(0), reader.GetInt32(1)); return result;
    }
    internal static Dictionary<string, Dictionary<string, int>> LoadRanks(Guid character)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Category,Talent,Rank FROM MasteryTalentRanks WHERE CharacterId=$id";
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); using var reader = cmd.ExecuteReader();
            var result = new Dictionary<string, Dictionary<string, int>>();
            while (reader.Read())
            {
                var key = reader.GetString(0); if (!result.TryGetValue(key, out var ranks)) result[key] = ranks = new();
                ranks.Add(reader.GetString(1), reader.GetInt32(2));
            }
            return result;
        }
    }
    internal static bool BuyTalent(Guid character, MasteryCategory category, string key, Guid request)
    {
        if (character == Guid.Empty || request == Guid.Empty || category.Talents.FirstOrDefault(t => t.Key == key) is not { } talent) return false;
        lock (Gate)
        {
            using var db = Open(); using var tx = db.BeginTransaction();
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$request", request.ToString("N"));
            cmd.Parameters.AddWithValue("$category", category.Key); cmd.Parameters.AddWithValue("$talent", key);
            cmd.CommandText = "SELECT Category,Talent FROM MasteryTalentPurchases WHERE CharacterId=$id AND RequestId=$request";
            using (var reader = cmd.ExecuteReader())
                if (reader.Read()) return reader.GetString(0) == category.Key && reader.GetString(1) == key;
            cmd.CommandText = "SELECT Experience FROM WeaponMasteries WHERE CharacterId=$id AND Category=$category";
            var experience = (long)(cmd.ExecuteScalar() ?? 0L);
            var ranks = ReadRanks(db, tx, character, category.Key);
            if (!MasteryRules.CanBuy(category, talent, experience, ranks)) return false;
            cmd.Parameters.AddWithValue("$rank", ranks.GetValueOrDefault(key) + 1);
            cmd.CommandText = """
                INSERT INTO MasteryTalentRanks(CharacterId,Category,Talent,Rank) VALUES($id,$category,$talent,$rank)
                ON CONFLICT(CharacterId,Category,Talent) DO UPDATE SET Rank=excluded.Rank;
                INSERT INTO MasteryTalentPurchases(CharacterId,RequestId,Category,Talent) VALUES($id,$request,$category,$talent);
                """;
            cmd.ExecuteNonQuery(); tx.Commit(); return true;
        }
    }
}
