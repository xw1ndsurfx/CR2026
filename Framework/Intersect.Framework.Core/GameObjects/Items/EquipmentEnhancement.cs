using Intersect.Framework.Core.GameObjects.Events;
using MessagePack;

namespace Intersect.Framework.Core.GameObjects.Items;

public enum WeaponSpellTrigger { Manual, OnHit }

[MessagePackObject]
public sealed partial class WeaponSpellBinding
{
    [Key(0)] public Guid SpellId { get; set; }
    [Key(1)] public WeaponSpellTrigger Trigger { get; set; }
    [Key(2)] public int ChancePercent { get; set; } = 100;
    public WeaponSpellBinding Copy() => new() { SpellId = SpellId, Trigger = Trigger, ChancePercent = ChancePercent };
    public bool SameAs(WeaponSpellBinding? other) => other != null && SpellId == other.SpellId && Trigger == other.Trigger && ChancePercent == other.ChancePercent;
}

// Per-copy properties: never mutate the shared item descriptor when crafting an upgrade.
[MessagePackObject]
public sealed partial class EquipmentEnhancement
{
    [Key(0)] public int Rarity { get; set; } = -1;
    [Key(1)] public WeaponSpellBinding? Spell { get; set; }
    [Key(2)] public Guid EventId { get; set; }
    [Key(3)] public ItemEventTrigger EventTrigger { get; set; } = ItemEventTrigger.OnEquip;
    public EquipmentEnhancement Copy() => new() { Rarity = Rarity, Spell = Spell?.Copy(), EventId = EventId, EventTrigger = EventTrigger };
    public static bool Same(EquipmentEnhancement? left, EquipmentEnhancement? right) => ReferenceEquals(left, right) ||
        left != null && right != null && left.Rarity == right.Rarity && left.EventId == right.EventId && left.EventTrigger == right.EventTrigger &&
        (left.Spell == null ? right.Spell == null : left.Spell.SameAs(right.Spell));
}

public partial class ItemDescriptor
{
    public int EffectiveRarity(ItemProperties? properties) => Math.Max(Rarity, properties?.Enhancement?.Rarity ?? -1);
    public WeaponSpellBinding? EffectiveWeaponSpell(ItemProperties? properties) =>
        ItemType == ItemType.Equipment && EquipmentSlot == Options.Instance.Equipment.WeaponSlot
            ? properties?.Enhancement?.Spell ?? Presentation.WeaponSpell : null;
}
