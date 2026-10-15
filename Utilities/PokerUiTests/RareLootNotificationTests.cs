using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using MessagePack;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class RareLootNotificationTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }

    private static void RunCore()
    {
        _ = new TestContentManager();
        var oldOptions = Options.Instance;
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var rare = new ItemDescriptor(Guid.NewGuid()) { Name = "Test rare loot", Rarity = 3 };
        var common = new ItemDescriptor(Guid.NewGuid()) { Name = "Test common loot", Rarity = 1 };
        ItemDescriptor.Lookup[rare.Id] = rare; ItemDescriptor.Lookup[common.Id] = common;
        var colors = CustomColors.Items.Rarities; var oldColor = colors.GetValueOrDefault(3);
        var configured = new Color(190, 35, 80); colors[3] = configured;
        try
        {
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(640, 480) };
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ItemChangeNotificationWindow", true)!;
            var window = (ControlBase)Activator.CreateInstance(type, [canvas])!;
            var enqueue = type.GetMethod("Enqueue")!;
            void Add(Guid id, int amount, bool loot = true) => enqueue.Invoke(window, [new ItemChangeNotificationPacket(id, amount) { IsLoot = loot }]);
            Add(common.Id, 1); Add(rare.Id, 0); Add(rare.Id, -1);
            Check(window.IsHidden, "Common/failed/negative loot must not trigger congratulations");
            Add(rare.Id, 2);
            var heading = (Label)type.GetField("_heading", Hidden)!.GetValue(window)!;
            var name = (Label)type.GetField("_name", Hidden)!.GetValue(window)!;
            var quantity = (Label)type.GetField("_quantity", Hidden)!.GetValue(window)!;
            Check(!window.IsHidden && !heading.IsHidden && heading.Text.Contains("Félicitations") && name.Text == rare.Name && quantity.Text.Contains("2"),
                "Confirmed rare loot must identify the actual reward and quantity");
            Check(name.TextColorOverride == configured && quantity.TextColorOverride == configured && window.Height == 150,
                "Congratulations must use the exact configured rarity hue");
            Check(quantity.Text.Contains("x2") && !quantity.Text.Contains('×'), "Quantity must use the bitmap-font-supported x glyph");
            Check(window.Bottom <= canvas.Height && window.Right <= canvas.Width && window.Children.All(child => child.Right <= window.Width && child.Bottom <= window.Height),
                "Loot notification remains visible at 640x480");
            Add(common.Id, -3, loot: false);
            type.GetField("_hideAt", Hidden)!.SetValue(window, 0L); type.GetMethod("Update")!.Invoke(window, null);
            Check(heading.IsHidden && window.Height == 92 && quantity.Text.Contains("-3"), "Ordinary Change Item notifications retain their behavior and queue order");
            type.GetField("_hideAt", Hidden)!.SetValue(window, 0L); type.GetMethod("Update")!.Invoke(window, null);
            Check(window.IsHidden, "Notification must dismiss automatically when the queue ends");
            var packet = new ItemChangeNotificationPacket(rare.Id, 2) { IsLoot = true };
            var copy = MessagePackSerializer.Deserialize<ItemChangeNotificationPacket>(MessagePackSerializer.Serialize(packet));
            Check(copy.IsLoot && copy.ItemId == rare.Id && copy.SignedQuantity == 2, "Loot confirmation wire round-trip");
            var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2); MessagePackSerializer.Serialize(ref writer, rare.Id); writer.Write(1); writer.Flush();
            Check(!MessagePackSerializer.Deserialize<ItemChangeNotificationPacket>(buffer.WrittenMemory).IsLoot, "Legacy Change Item packets must not become loot celebrations");
        }
        finally
        {
            ItemDescriptor.Lookup.DeleteAt(rare.Id); ItemDescriptor.Lookup.DeleteAt(common.Id);
            if (oldColor == null) colors.Remove(3); else colors[3] = oldColor;
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, oldOptions);
        }
        System.Console.WriteLine("PASS rare loot notifications: filtering, exact rarity hue, responsive reward content, queue/dismissal and packet compatibility");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
