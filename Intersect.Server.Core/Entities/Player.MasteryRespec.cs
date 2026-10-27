using System.Text.Json;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.Client;
using Intersect.Server.Archery;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;
public partial class Player
{
    private long _respecRequestAt, _respecRetryAt;
    private bool _respecRecovering;
    private bool RecoverMasteryRespec()
    {
        var pending = PetCollection.MasteryRespec;
        if (pending == null || _respecRecovering) return true;
        if (User is not { } user || Environment.TickCount64 < _respecRetryAt) return false;
        _respecRetryAt = Environment.TickCount64 + 10000;
        lock (user.PokerSaveGate)
        {
            _respecRecovering = true;
            try
            {
                RoyalCombatRuntime.CompletePaidRespec(Id, pending.RequestId, pending.Category);
                _masteryRanks = RoyalCombatRuntime.LoadRanks(Id);
                var allowed = RoyalCombatRuntime.Current.Masteries.SelectMany(EffectiveTalents)
                    .SelectMany(n => n.Talent.RankSpellIds.Take(n.Rank)).ToHashSet();
                foreach (var slot in Spells)
                    if (MasteryLearnedSpells.Contains(slot.SpellId) && !allowed.Contains(slot.SpellId)) slot.Set(Spell.None);
                PetCollection.MasteryRespec = null;
                if (user.Save(force: true) != UserSaveResult.Completed) { PetCollection.MasteryRespec = pending; return false; }
                _respecRetryAt = 0; PacketSender.SendPlayerSpells(this); return true;
            }
            catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException or InvalidDataException)
            {
                PetCollection.MasteryRespec = pending;
                ApplicationContext.Context.Value?.Logger.LogError(e, "Paid mastery reset awaits recovery for {CharacterId}", Id); return false;
            }
            finally { _respecRecovering = false; }
        }
    }
    private void HandleMasteryRespec(ArcheryRequestPacket packet)
    {
        lock (EntityLock)
        {
            if (Environment.TickCount64 - _respecRequestAt < 1000) return;
            _respecRequestAt = Environment.TickCount64;
            if (!RecoverMasteryRespec()) { SendMasteries(true, "Your paid mastery reset is awaiting recovery. No further payment was taken."); return; }
            var config = RoyalCombatRuntime.Current;
            if (!config.RespecEnabled || User is not { } user || packet.RequestId == Guid.Empty ||
                IsDead || HasPendingDeath || IsCasting || IsJoustProtected || !CanFireWorldWeapon ||
                !CanUseLobbyTravel || CombatTimer > Timing.Global.Milliseconds) return;
            var category = config.Masteries.FirstOrDefault(c => c.Key == packet.CategoryKey);
            if (category == null) return;
            if (packet.ExpectedCost != config.RespecCost || packet.ItemId != config.RespecCurrencyId)
            { SendMasteries(true, "The mastery reset price changed. Review it before confirming again."); return; }
            try
            {
                var receipt = RoyalCombatRuntime.RespecReceipt(Id, packet.RequestId);
                if (receipt != null) { SendMasteries(true, receipt == category.Key ? "Mastery talents reset. Your experience is unchanged." : "This reset request is invalid."); return; }
                if (Intersect.Framework.Core.Archery.MasteryRules.Spent(category, MasteryRanks.GetValueOrDefault(category.Key) ?? new()) <= 0)
                { SendMasteries(true, "This mastery has no allocated talents to reset."); return; }
                if (Statuses.Keys.Any(s => MasteryLearnedSpells.Contains(s.Id)))
                { SendMasteries(true, "Wait for mastery spell effects to end before resetting talents."); return; }
                if (!MiniGameCurrency.IsCompatible(ItemDescriptor.Get(config.RespecCurrencyId))) return;
                lock (user.PokerSaveGate)
                {
                    PetCollection.MasteryRespec = new() { RequestId = packet.RequestId, Category = category.Key,
                        CurrencyId = config.RespecCurrencyId, Cost = config.RespecCost };
                    var paid = false;
                    try { paid = MasteryRespecPayment.Debit(Items, config.RespecCurrencyId, config.RespecCost, () => user.Save(force: true) == UserSaveResult.Completed); }
                    finally { if (!paid) PetCollection.MasteryRespec = null; }
                    if (!paid) { SendMasteries(true, "Payment could not be saved or you lack the required currency. No reset was applied."); return; }
                    PacketSender.SendInventory(this); _respecRetryAt = 0;
                    var complete = RecoverMasteryRespec();
                    SendMasteries(true, complete ? "Mastery talents reset. Your experience is unchanged." : "Your paid mastery reset is awaiting recovery. No further payment was taken.");
                }
            }
            catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
            {
                ApplicationContext.Context.Value?.Logger.LogError(e, "Mastery reset failed for {CharacterId}", Id);
                SendMasteries(true, "Mastery reset is temporarily unavailable.");
            }
        }
    }
}
