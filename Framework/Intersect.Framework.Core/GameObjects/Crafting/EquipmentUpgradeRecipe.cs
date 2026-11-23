using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Framework.Core.GameObjects.Crafting;

// The recipe ItemId selects the existing equipment type; Ingredients contains only reagents.
public sealed class EquipmentUpgradeRecipe
{
    public int TargetRarity { get; set; } = -1;
    public WeaponSpellBinding? Spell { get; set; }
    public Guid EventId { get; set; }
    public ItemEventTrigger EventTrigger { get; set; } = ItemEventTrigger.OnEquip;
}
