using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Archery;
internal static partial class RoyalCombatRuntime
{
    internal static string? RespecReceipt(Guid character, Guid request)
    {
        lock (Gate)
        {
            using var db = Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Category FROM MasteryRespecReceipts WHERE CharacterId=$id AND RequestId=$request";
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$request", request.ToString("N"));
            return cmd.ExecuteScalar() as string;
        }
    }
    // Only called after a native inventory/Pets save durably records the paid operation.
    internal static void CompletePaidRespec(Guid character, Guid request, string category)
    {
        if (character == Guid.Empty || request == Guid.Empty || category.Length is < 1 or > 32) throw new InvalidDataException("mastery-respec");
        lock (Gate)
        {
            var receipt = RespecReceipt(character, request);
            if (receipt != null) { if (receipt != category) throw new InvalidDataException("mastery-respec-receipt"); return; }
            using var db = Open(); using var tx = db.BeginTransaction(); using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM MasteryTalentRanks WHERE CharacterId=$id AND Category=$category; INSERT INTO MasteryRespecReceipts VALUES($id,$request,$category);";
            cmd.Parameters.AddWithValue("$id", character.ToString("N")); cmd.Parameters.AddWithValue("$request", request.ToString("N")); cmd.Parameters.AddWithValue("$category", category);
            cmd.ExecuteNonQuery(); tx.Commit();
        }
    }
    internal static (Dictionary<string,long> Experience, Dictionary<string,Dictionary<string,int>> Ranks) MasterySnapshot(Guid character)
    { lock (Gate) return (Load(character), LoadRanks(character)); }

    internal static PlayerMasteryProfilePacket[] PublicMasteries(Guid character)
    {
        lock (Gate)
        {
            var snapshot = MasterySnapshot(character);
            return Current.Masteries.Select(c => {
                var total = snapshot.Experience.GetValueOrDefault(c.Key); var level = c.Level(total);
                var ranks = snapshot.Ranks.GetValueOrDefault(c.Key) ?? new();
                var talents = c.Talents.Where(t => ranks.GetValueOrDefault(t.Key) > 0 && MasteryRules.Eligible(c, t, total, ranks))
                    .Select(t => (Talent: t, Rank: Math.Clamp(ranks.GetValueOrDefault(t.Key), 0, t.MaxRank))).ToArray();
                return new PlayerMasteryProfilePacket { Key = c.Key, Name = c.Name, Level = level, MaximumLevel = c.Cap,
                    DamageBonusPercent = (level - 1) * c.DamagePercentPerLevel + talents.Sum(t => t.Talent.DamagePercentPerRank * t.Rank),
                    AttackSpeedBonusPercent = Math.Min(50, talents.Sum(t => t.Talent.AttackSpeedPercentPerRank * t.Rank)),
                    CritChanceBonus = Math.Min(50, talents.Sum(t => t.Talent.CritChancePerRank * t.Rank)),
                    LevelExperience = c.WithinLevel(total), RequiredExperience = level == c.Cap ? 0 : c.Required(level),
                    AvailablePoints = Math.Max(0, MasteryRules.Points(c, total) - MasteryRules.Spent(c, snapshot.Ranks.GetValueOrDefault(c.Key) ?? new())) };
            }).ToArray();
        }
    }
}
