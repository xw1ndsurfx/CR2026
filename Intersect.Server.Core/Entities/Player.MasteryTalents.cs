using Intersect.Core;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Server.Archery;
using Intersect.Server.Database;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;
public partial class Player
{
    private Dictionary<string, Dictionary<string, int>> _masteryRanks;
    private HashSet<Guid> _masteryLearnedSpells;
    private HashSet<Guid> MasteryLearnedSpells => _masteryLearnedSpells ??= RoyalCombatRuntime.LearnedSpells(Id);
    private Dictionary<string, Dictionary<string, int>> MasteryRanks => _masteryRanks ??= RoyalCombatRuntime.LoadRanks(Id);
    private long _talentRequestAt;
    private IEnumerable<(MasteryTalent Talent, int Rank)> EffectiveTalents(MasteryCategory category)
    {
        if (category.Talents.Length == 0) yield break;
        var ranks = MasteryRanks.GetValueOrDefault(category.Key) ?? new();
        var xp = MasteryExperience.GetValueOrDefault(category.Key);
        foreach (var talent in category.Talents)
        {
            var rank = Math.Clamp(ranks.GetValueOrDefault(talent.Key), 0, talent.MaxRank);
            if (rank > 0 && MasteryRules.Eligible(category, talent, xp, ranks)) yield return (talent, rank);
        }
    }
    internal (double Damage, double Speed, int Crit) MasteryPassives(ItemDescriptor weapon)
    {
        lock (EntityLock)
        {
            if (weapon == null) return default;
            if (!RecoverMasteryRespec()) return default;
            var category = RoyalCombatRuntime.Current.Masteries.FirstOrDefault(c => c.WeaponIds.Contains(weapon.Id));
            if (category == null || category.Talents.Length == 0) return default;
            try
            {
                var nodes = EffectiveTalents(category).ToArray();
                return (nodes.Sum(n => n.Talent.DamagePercentPerRank * n.Rank),
                    Math.Min(50, nodes.Sum(n => n.Talent.AttackSpeedPercentPerRank * n.Rank)),
                    Math.Min(50, nodes.Sum(n => n.Talent.CritChancePerRank * n.Rank)));
            }
            catch (Exception error) when (error is IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
            { ApplicationContext.Context.Value?.Logger.LogError(error, "Mastery talents unavailable for {CharacterId}", Id); return default; }
        }
    }
    internal bool CanUseMasterySpell(Guid spell)
    {
        lock (EntityLock) return CanUseMasterySpellCore(spell);
    }

    private bool CanUseMasterySpellCore(Guid spell)
    {
        var categories = RoyalCombatRuntime.Current.Masteries.Where(c => c.Talents.Any(t => t.RankSpellIds.Contains(spell))).ToArray();
        if (categories.Length == 0) return !MasteryLearnedSpells.Contains(spell);
        if (!RecoverMasteryRespec()) return false;
        if (!TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var weapon)) return false;
        return categories.Where(c => c.WeaponIds.Contains(weapon.ItemId)).Any(c =>
            EffectiveTalents(c).Any(n => n.Talent.RankSpellIds.Take(n.Rank).Contains(spell)));
    }
    private void SynchronizeMasterySpells()
    {
        if (!RecoverMasteryRespec()) return;
        if (!RoyalCombatRuntime.Current.Masteries.Any(c => c.Talents.Any(t => t.RankSpellIds.Length > 0))) return;
        var spells = RoyalCombatRuntime.Current.Masteries.SelectMany(EffectiveTalents)
            .SelectMany(n => n.Talent.RankSpellIds.Take(n.Rank)).Distinct();
        var changed = false;
        foreach (var id in spells)
        {
            if (KnowsSpell(id) || Spells.All(s => s.SpellId != Guid.Empty)) continue;
            // A remapped/deleted talent must not leave an unrestricted native spell behind.
            RoyalCombatRuntime.RememberSpell(Id, id); MasteryLearnedSpells.Add(id);
            changed |= TryTeachSpell(new Spell(id), sendUpdate: false);
        }
        if (changed) PacketSender.SendPlayerSpells(this);
    }
    private void HandleTalentPurchase(ArcheryRequestPacket packet)
    {
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            if (now - _talentRequestAt < 300) return;
            _talentRequestAt = now;
            if (!RecoverMasteryRespec()) { SendMasteries(true, "Your paid mastery reset is awaiting recovery. No further payment was taken."); return; }
            if (IsDead || HasPendingDeath || IsArcheryActive || CombatTimer > Intersect.Framework.Core.Timing.Global.Milliseconds) return;
            var category = RoyalCombatRuntime.Current.Masteries.FirstOrDefault(c => c.Key == packet.CategoryKey);
            var talent = category?.Talents.FirstOrDefault(t => t.Key == packet.TalentKey);
            if (talent == null) { SendMasteries(true, "This mastery talent is unavailable."); return; }
            var ranks = MasteryRanks.GetValueOrDefault(category.Key) ?? new();
            var rank = Math.Clamp(ranks.GetValueOrDefault(talent.Key), 0, talent.MaxRank);
            if (rank < talent.RankSpellIds.Length && !KnowsSpell(talent.RankSpellIds[rank]) && Spells.All(s => s.SpellId != Guid.Empty))
            { SendMasteries(true, "Free a spellbook slot before learning this talent."); return; }
            if (!RoyalCombatRuntime.BuyTalent(Id, category, talent.Key, packet.RequestId))
            { SendMasteries(true, "Talent requirements or available mastery points are not met."); return; }
            _masteryRanks = RoyalCombatRuntime.LoadRanks(Id);
            SynchronizeMasterySpells();
            SendMasteries(true, "Mastery talent learned.");
        }
    }
}
