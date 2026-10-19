using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Enums;
using Intersect.Framework.Core.Metalworking;
using Intersect.Server.Achievements;
namespace Intersect.Server.Entities;

public partial class Player
{
    internal void NotifyMetalworkInventoryChanged(MetalworkRecipe recipe)
    {
        AchievementRuntime.AddProgress(this, Intersect.Framework.Core.Achievements.AchievementObjectiveType.ItemsObtained, recipe.OutputQuantity, recipe.OutputItemId);
        foreach (var item in recipe.Ingredients.Select(i => i.ItemId).Append(recipe.OutputItemId).Distinct())
            UpdateGatherItemQuests(item);
        EnqueueStartCommonEvent(Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(recipe.OutputItemId)?.GetEventTrigger(ItemEventTrigger.OnPickup));
        StartCommonEventsWithTrigger(CommonEventTrigger.InventoryChanged);
        UnequipInvalidItems();
    }
}
