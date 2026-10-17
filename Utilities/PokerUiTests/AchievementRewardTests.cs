using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Core;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
internal static class AchievementRewardTests
{
    [ModuleInitializer] internal static void Run() { try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var oldContext = ApplicationContext.Context.Value; var oldDatabase = Globals.Database;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        Globals.Database = new InterfaceRefreshTests.MemoryPreferences();
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = string.Join(" ", Enumerable.Repeat("Royal Potion of Recovery", 8)) };
        var money = new ItemDescriptor(Guid.NewGuid()) { Name = "Auragems" };
        ItemDescriptor.Lookup[item.Id] = item; ItemDescriptor.Lookup[money.Id] = money;
        try
        {
            _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin);
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.AchievementsWindow", true)!;
            var build = type.GetMethod("BuildRewardText", BindingFlags.Static | BindingFlags.NonPublic)!;
            var reward = new AchievementRewardDefinition { ItemId = item.Id, ItemQuantity = 10, CurrencyItemId = money.Id, CurrencyQuantity = 50, Experience = 123, CommonEventId = Guid.NewGuid() };
            var text = (string)build.Invoke(null, [reward])!;
            Check(text.Contains(item.Name) && text.Contains("50 x Auragems") && text.Contains("10 x ") && text.Contains("123 XP"), "Reward text resolves actual item/currency names and preserves quantities and XP");
            Check(text.Split('\n').Length == 4, "Each reward has its own line");
            var window = (ControlBase)Activator.CreateInstance(type, [canvas, false])!;
            var definition = new AchievementDefinition { Reward = reward };
            var nextY = (int)type.GetMethod("AddAchievementCard", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [definition, new AchievementProgressEntry(), 0])!;
            IEnumerable<ControlBase> Desc(ControlBase root) { foreach(var child in root.Children) { yield return child; foreach(var nested in Desc(child)) yield return nested; } }
            var card = Desc(window).Single(c => c.Name == "AchievementCard" + definition.Id);
            var label = Desc(card).OfType<RichLabel>().Single();
            Check(label.Text.Contains(item.Name) && label.Height > 22 && label.Bottom <= card.Height && nextY >= card.Bottom + 12, "Long names and multiple reward lines wrap within expanded achievement cards");
            reward.ItemId = Guid.NewGuid(); reward.CurrencyItemId = Guid.NewGuid();
            Check(((string)build.Invoke(null, [reward])!).Contains("Unknown item"), "Missing descriptors use a readable fallback");
        }
        finally { ItemDescriptor.Lookup.DeleteAt(item.Id); ItemDescriptor.Lookup.DeleteAt(money.Id); ApplicationContext.Context.Value = oldContext; Globals.Database = oldDatabase; }
        System.Console.WriteLine("PASS achievement rewards: actual names/quantities, missing data fallback and expanded wrapped cards");
    }
}
