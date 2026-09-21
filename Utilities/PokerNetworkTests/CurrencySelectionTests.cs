using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Newtonsoft.Json;

internal static class CurrencySelectionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Existing events default to test chips", () =>
            {
                var old = JsonConvert.DeserializeObject<StartMiniGameCommand>("{\"TableId\":\"old-table\"}")!;
                Check(old.CurrencyItemId == Guid.Empty && old.HasValidSettings());
            }),
            ("Currency item ID survives JSON independently of list order or name", () =>
            {
                var id = Guid.NewGuid();
                var source = new StartMiniGameCommand { CurrencyItemId = id, StartingChips = 100, BigBlind = 10 };
                var copy = JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(source))!;
                Check(copy.CurrencyItemId == id && copy.StartingChips == 100 && copy.HasValidSettings());
            }),
            ("Currency type is compatible even with Stackable unchecked", () =>
            {
                var item = Item(ItemType.Currency, false);
                item.MaxInventoryStack = int.MaxValue;
                Check(MiniGameCurrency.IsCompatible(item));
            }),
            ("Generic stackable items are supported", () =>
            {
                Check(MiniGameCurrency.IsCompatible(Item(ItemType.None, true)));
                Check(!MiniGameCurrency.IsCompatible(Item(ItemType.None, false)));
            }),
            ("Equipment, bags, missing items and zero limits are rejected", () =>
            {
                Check(!MiniGameCurrency.IsCompatible(Item(ItemType.Equipment, true)));
                Check(!MiniGameCurrency.IsCompatible(Item(ItemType.Bag, true)));
                Check(!MiniGameCurrency.IsCompatible(null));
                var item = Item(ItemType.Currency, false); item.MaxInventoryStack = 0;
                Check(!MiniGameCurrency.IsCompatible(item));
            }),
            ("Duplicate names remain distinct and stable when the list is reordered", () =>
            {
                var a = Item(ItemType.Currency, false); var b = Item(ItemType.Currency, false);
                a.Name = b.Name = "Aureons";
                var first = MiniGameCurrency.CompatibleItems([b, a]);
                var second = MiniGameCurrency.CompatibleItems([a, b]);
                Check(first.Length == 2 && first.Select(x => x.Id).SequenceEqual(second.Select(x => x.Id)));
                Check(a.Id != b.Id);
                var oldId = a.Id; a.Name = "Renamed Aureons";
                Check(MiniGameCurrency.CompatibleItems([a, b]).Any(x => x.Id == oldId));
            }),
            ("A deleted item ID is not silently cleared during serialization", () =>
            {
                var source = new StartMiniGameCommand { CurrencyItemId = Guid.NewGuid() };
                var json = JsonConvert.SerializeObject(source);
                Check(JsonConvert.DeserializeObject<StartMiniGameCommand>(json)!.CurrencyItemId == source.CurrencyItemId);
            }),
            ("Currency selection does not relax betting limits", () =>
            {
                var source = new StartMiniGameCommand { CurrencyItemId = Guid.NewGuid(), StartingChips = 1, BigBlind = 10 };
                Check(!source.HasValidSettings());
                source.StartingChips = 100; Check(source.HasValidSettings());
            }),
        };
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("PASS currency selection: " + name); }
            catch (Exception error) { throw new InvalidOperationException("FAIL currency selection: " + name, error); }
        }
    }

    private static ItemDescriptor Item(ItemType type, bool stackable) => new(Guid.NewGuid())
    { Name = "Aureons", ItemType = type, Stackable = stackable, MaxInventoryStack = 1_000_000 };
    private static void Check(bool condition)
    { if (!condition) throw new InvalidOperationException("Currency selection regression"); }
}
