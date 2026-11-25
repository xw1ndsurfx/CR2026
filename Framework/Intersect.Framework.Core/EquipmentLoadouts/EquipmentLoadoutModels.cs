namespace Intersect.Framework.Core.EquipmentLoadouts;

public enum EquipmentLoadoutAction { Open, Refresh, Save, Apply, Delete }

public sealed class EquipmentLoadoutProfile
{
    public long Revision { get; set; }
    public Dictionary<int, EquipmentLoadout> Sets { get; set; } = [];
}

public sealed class EquipmentLoadout
{
    public string Name { get; set; } = "";
    public string[] SlotNames { get; set; } = [];
    public EquipmentLoadoutItem?[] Items { get; set; } = [];
}

// A signature identifies interchangeable copies with the same rolled bonuses.
// Never persist inventory indices: those change when a player sorts their bag.
public sealed class EquipmentLoadoutItem
{
    public Guid ItemId { get; set; }
    public int[] StatModifiers { get; set; } = [];
    public Intersect.Framework.Core.GameObjects.Items.EquipmentEnhancement? Enhancement { get; set; }
}

public sealed class EquipmentLoadoutView
{
    public Guid CharacterId { get; set; }
    public long Revision { get; set; }
    public Dictionary<int, EquipmentLoadout> Sets { get; set; } = [];
    public Dictionary<Guid, string> ItemNames { get; set; } = [];
    public Dictionary<int, string> Problems { get; set; } = [];
    public string BlockReason { get; set; } = "";
    public string Message { get; set; } = "";
}

public static class EquipmentLoadoutRules
{
    public const int Count = 5;
    public const int MaxNameLength = 32;
    public static bool ValidSlot(int slot) => slot is >= 0 and < Count;
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength && !name.Any(char.IsControl);
    public static bool Valid(EquipmentLoadout? set, IReadOnlyList<string> slotNames) => set != null && ValidName(set.Name) &&
        set.SlotNames != null && set.SlotNames.SequenceEqual(slotNames) && set.Items?.Length == slotNames.Count &&
        set.Items.All(item => item == null || item.ItemId != Guid.Empty && item.StatModifiers?.Length == Enum.GetValues<Intersect.Enums.Stat>().Length);
}
