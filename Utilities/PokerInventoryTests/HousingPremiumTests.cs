using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Housing;
using Intersect.Server.Entities;
using Newtonsoft.Json;
internal static class HousingPremiumTests
{
    [ModuleInitializer] internal static void Run() { try { Verify(); } catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var runtime = typeof(Player).Assembly.GetType("Intersect.Server.Housing.HousingRuntime", true)!;
        var configField = runtime.GetField("_config", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = configField.GetValue(null);
        var check = runtime.GetMethod("RequireFurniturePremium", BindingFlags.Static | BindingFlags.NonPublic)!;
        var premium = new FurnitureDefinition { PremiumRequired = true };
        var ordinary = new FurnitureDefinition();
        var staleOwned = new FurnitureDefinition { Id = premium.Id, PremiumRequired = false };
        var config = new HousingConfiguration { Catalog = [premium, ordinary] };
        bool Allowed(FurnitureDefinition item, bool active)
        {
            try { check.Invoke(null, [item, active]); return true; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return false; }
        }
        try
        {
            configField.SetValue(null, config);
            Check(Allowed(ordinary, false) && Allowed(ordinary, true), "Ordinary furniture remains available to everyone");
            Check(!Allowed(premium, false) && Allowed(premium, true), "The server gate requires active Premium for restricted furniture");
            Check(!Allowed(staleOwned, false), "Current catalog requirements override an old non-Premium ownership snapshot");
            var house = new HouseProfile(); house.Furniture[premium.Id] = new() { Definition = premium, Quantity = 1 };
            var placed = house.PlaceFurniture(premium.Id, Guid.Empty, 3, 4, 0, 32, 26, 100);
            Check(!Allowed(premium, false) && house.Placements.Count == 1 && house.Furniture[premium.Id].Quantity == 1, "Expiry preserves placed furniture and quantities");
            house.Placements.RemoveAll(p => p.Id == placed.Id);
            Check(house.Furniture[premium.Id].Quantity == 1 && !Allowed(premium, false) && Allowed(premium, true), "Stored premium furniture remains owned and can be placed after renewal");
            config.Catalog.Remove(premium);
            Check(!Allowed(premium, false), "Expired monthly offers retain their saved Premium requirement");
            var restored = HouseProfile.Read(JsonConvert.SerializeObject(house));
            Check(restored.Furniture[premium.Id].Definition.PremiumRequired && !JsonConvert.DeserializeObject<FurnitureDefinition>("{}")!.PremiumRequired, "Premium flags persist and legacy furniture defaults to unrestricted");
        }
        finally { configField.SetValue(null, previous); }
        System.Console.WriteLine("PASS Premium furniture: server access, stale ownership, retired offers, expiry/store/renewal, persistence and legacy defaults");
    }
}
