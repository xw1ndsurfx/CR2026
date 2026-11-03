using System.Reflection;
using Intersect;
using Intersect.Enums;
using Intersect.Server.Database;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;
using Intersect.Server.Entities;

internal static class FarmingServerChecks
{
    internal static void Run(Action<bool, string> check, FarmConfiguration config)
    {
        var previousOptions = Options.Instance;
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var assembly = typeof(Player).Assembly;
        var runtime = assembly.GetType("Intersect.Server.Housing.HousingRuntime", true)!;
        var field = runtime.GetField("_config", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previousConfig = field.GetValue(null);
        var handler = runtime.GetMethod("HandleFarm", BindingFlags.NonPublic | BindingFlags.Static)!;
        var coin = new ItemDescriptor(config.CurrencyItemId) { Name = "Farm test coin", ItemType = ItemType.Currency, Stackable = true };
        ItemDescriptor.Lookup[coin.Id] = coin;
        try
        {
            field.SetValue(null, new HousingConfiguration { Enabled = true, TemplateMapId = Guid.NewGuid(), Farm = config });
            var player = new Player(); GC.SuppressFinalize(player);
            player.Items[0].Set(new Item(coin.Id, 1000));
            Exception Reject(HouseActionPacket packet)
            {
                try { handler.Invoke(null, [player, packet]); }
                catch (TargetInvocationException error) when (error.InnerException != null) { return error.InnerException; }
                throw new Exception("Invalid farming request was accepted.");
            }
            var before = player.HousingJson;
            check(Reject(new() { Action = HouseAction.DepositFarmSalary, OwnerId = Guid.NewGuid(), X = 100 }) is InvalidOperationException && player.HousingJson == before, "Other owners cannot access a farm");
            check(Reject(new() { Action = HouseAction.DepositFarmSalary, X = -1 }) is InvalidOperationException && player.House.Farm.Wallet == 0 && player.Items[0].Quantity == 1000, "Negative deposits cannot mint currency");
            check(Reject(new() { Action = HouseAction.DepositFarmSalary, X = 100 }) is IOException && player.HousingJson == before && player.Items[0].Quantity == 1000, "Failed persistence rolls back salary debit and farm together");
            check(Reject(new() { Action = HouseAction.HireFarmWorker }) is IOException && player.House.Farm.Plots.Count == 0 && player.Items[0].Quantity == 1000, "Failed hiring save restores hiring price and worker ownership");
            check(Reject(new() { Action = HouseAction.SetFarmCrop, Id = Guid.NewGuid(), PlacementId = config.Crops[0].Id }) is InvalidOperationException && player.HousingJson == before, "Forged worker identifiers are rejected");
            player.House.Farm.Storage[coin.Id] = 100;
            var storageBefore = player.HousingJson;
            check(Reject(new() { Action = HouseAction.CollectFarmHarvest, Id = coin.Id }) is IOException && player.HousingJson == storageBefore && player.Items[0].Quantity == 1000, "Failed collection save restores inventory and stored harvest together");
            var professions = assembly.GetType("Intersect.Server.Professions.ProfessionRuntime", true)!;
            var raw = professions.GetMethod("Raw", BindingFlags.NonPublic | BindingFlags.Static)!;
            player.House.Farm.Learned = true; player.House.Farm.Experience = 250;
            check((long)raw.Invoke(null, [player, FarmingProfession.Id])! == 251, "Existing profession queries read persistent farming experience");
            player.HousingJson = player.HousingJson;
            check((long)raw.Invoke(null, [player, FarmingProfession.Id])! == 251, "Profession state survives character serialization");
        }
        finally
        {
            ItemDescriptor.Lookup.DeleteAt(coin.Id);
            field.SetValue(null, previousConfig);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, previousOptions);
        }
    }
}
