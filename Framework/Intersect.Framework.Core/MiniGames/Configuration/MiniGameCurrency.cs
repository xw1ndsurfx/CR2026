using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Framework.Core.MiniGames;

/// <summary>
/// Shared eligibility rule for an event's currency selector. Currency items are inherently
/// stackable in Intersect even when the editor's Stackable checkbox is unchecked/disabled.
/// This class neither grants permission to spend inventory nor transfers any items.
/// </summary>
public static class MiniGameCurrency
{
    public static bool IsCompatible(ItemDescriptor? item) => item != null &&
        item.Id != Guid.Empty && item.IsStackable && item.MaxInventoryStack > 0;

    public static ItemDescriptor[] CompatibleItems(IEnumerable<ItemDescriptor> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(IsCompatible)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    public static string DisplayName(ItemDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var folder = string.IsNullOrWhiteSpace(item.Folder) ? "" : item.Folder.Trim() + " / ";
        return $"{folder}{item.Name} [{item.Id.ToString("N")[..8]}]";
    }
}
