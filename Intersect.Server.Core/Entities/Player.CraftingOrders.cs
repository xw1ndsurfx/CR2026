using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Server.Database;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;
public partial class Player
{
    internal void NotifyCraftingOrderInventory(Item[] before)
    {
        foreach (var id in before.Select(i => i.ItemId).Concat(Items.Select(i => i.ItemId)).Where(id => id != Guid.Empty).Distinct())
        {
            UpdateGatherItemQuests(id);
            var received = Items.Where(i => i.ItemId == id).Sum(i => (long)i.Quantity) - before.Where(i => i.ItemId == id).Sum(i => (long)i.Quantity);
            if (received > 0)
            {
                Intersect.Server.Achievements.AchievementRuntime.AddProgress(this,
                    Intersect.Framework.Core.Achievements.AchievementObjectiveType.ItemsObtained,
                    (int)Math.Min(int.MaxValue, received), id);
                EnqueueStartCommonEvent(Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(id)?.GetEventTrigger(ItemEventTrigger.OnPickup));
            }
        }
        StartCommonEventsWithTrigger(CommonEventTrigger.InventoryChanged);
        UnequipInvalidItems();
        PacketSender.SendInventory(this);
    }
}
