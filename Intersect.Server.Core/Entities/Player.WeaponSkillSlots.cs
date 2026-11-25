using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Client;
using Intersect.Server.Archery;
using Intersect.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;

public partial class Player
{
    private long _weaponSkillSlotRequestAt;
    private void HandleWeaponSkillSlot(ArcheryRequestPacket packet)
    {
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            if (now - _weaponSkillSlotRequestAt < 200)
            {
                SendPacket(new Intersect.Network.Packets.Server.ArcheryStatePacket { RequestId = packet.RequestId,
                    Message = "Please wait before updating the quickbar." });
                return;
            }
            _weaponSkillSlotRequestAt = now;
            if (packet.WeaponSkillSlot is < 0 or >= WeaponSkillLoadout.SlotCount ||
                packet.TalentKey == null || packet.CategoryKey == null) return;
            var category = RoyalCombatRuntime.Current.Masteries.FirstOrDefault(c => c.Key == packet.CategoryKey);
            if (category == null)
            {
                SendPacket(new Intersect.Network.Packets.Server.ArcheryStatePacket { RequestId = packet.RequestId,
                    Message = "Weapon skill quickbar could not be saved. Try again." });
                return;
            }
            try
            {
                var ranks = MasteryRanks.GetValueOrDefault(category.Key) ?? [];
                if (packet.TalentKey.Length > 0 && !category.Talents.Any(t => t.Key == packet.TalentKey &&
                        WeaponSkillLoadout.Available(category, t, MasteryExperience.GetValueOrDefault(category.Key), ranks)))
                { SendMasteries(false, "Learn this weapon skill before adding it to the quickbar."); return; }
                var saved = RoyalCombatRuntime.LoadWeaponSkillSlots(Id).GetValueOrDefault(category.Key);
                var slots = WeaponSkillLoadout.Resolve(category, MasteryExperience.GetValueOrDefault(category.Key), ranks, saved);
                RoyalCombatRuntime.SaveWeaponSkillSlots(Id, category.Key,
                    WeaponSkillLoadout.Assign(slots, packet.WeaponSkillSlot, packet.TalentKey));
                SendMasteries(false, "Weapon skill quickbar saved.");
            }
            catch (Exception error) when (error is IOException or Microsoft.Data.Sqlite.SqliteException or System.Text.Json.JsonException)
            {
                ApplicationContext.Context.Value?.Logger.LogError(error, "Weapon quickbar save failed for {CharacterId}", Id);
                SendPacket(new Intersect.Network.Packets.Server.ArcheryStatePacket { RequestId = packet.RequestId,
                    Message = "Weapon skill quickbar could not be saved. Try again." });
            }
        }
    }
}
