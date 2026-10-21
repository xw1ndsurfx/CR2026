using Intersect.Client.General;
using Intersect.Client.Localization;
namespace Intersect.Client.Interface.Game;
internal static class MiniGameInventoryBalance
{
    internal static long Count(Guid currency) => currency==Guid.Empty?0:
        Globals.Me?.Inventory.Where(item=>item!=null && item.ItemId==currency).Sum(item=>(long)Math.Max(0,item.Quantity))??0;
    internal static string Caption(Guid currency) => ClientLanguages.Text("Inventory")+": "+Count(currency).ToString("N0",System.Globalization.CultureInfo.InvariantCulture).Replace(',',' ');
}
