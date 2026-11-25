using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Point = Intersect.Point;
using Console = System.Console;

// Production controls and approved PNGs, with explicit server-response fixtures.
// This validates layout and requests, not a network session or the GPU/audio device.
internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static IEnumerable<Base> All(Base root) { foreach (var c in root.Children) { yield return c; foreach (var n in All(c)) yield return n; } }
    private static void Click(Button b)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = b;
        var method = typeof(Base).GetMethod("InputMouseButtonState", Hidden)!;
        method.Invoke(b, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), true, false]);
        method.Invoke(b, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), false, false]);
    }
    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager(); Intersect.Client.General.Globals.ContentManager = content;
        var folder = Path.Combine("resources", "gui", "crafting-orders");
        foreach (var path in Directory.GetFiles(folder, "*.png")) content.AddGuiTexture("crafting-orders/" + Path.GetFileName(path), new BitmapTexture(path));
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Royal ceremonial longsword", Icon = "tools.png" };
        ItemDescriptor.Lookup.Set(item.Id, item); content.AddItemTexture(item.Icon, new BitmapTexture(Path.Combine(folder, "icon-tools.png")));
        var output = Path.Combine("validation-local", "crafting-ui"); Directory.CreateDirectory(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.CraftingOrdersWindow", true)!;
        foreach (var lang in new[] { "en", "fr", "es" })
        foreach (var size in new[] { new Point(800,600), new Point(1280,720), new Point(1600,900) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, lang);
            using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new DrawingSkin(renderer);
            using var canvas = new Canvas(skin) { Size = size };
            var window = (Base)Activator.CreateInstance(type, [canvas])!;
            var sounds = new List<string>(); var requests = 0; var actions = new List<byte>();
            type.GetProperty("AudioCue", Hidden)!.SetValue(window, new Action<string>(sounds.Add));
            type.GetProperty("RequestState", Hidden)!.SetValue(window, new Action(() => {}));
            type.GetProperty("SendPost", Hidden)!.SetValue(window, new Action<Guid,int,int,int,Guid>((id,q,r,h,request) => { Check(id == item.Id && request != Guid.Empty, "Invalid publish"); requests++; }));
            type.GetProperty("SendAction", Hidden)!.SetValue(window, new Action<Guid,byte>((id,a) => actions.Add(a)));
            var packet = new CraftingOrdersStatePacket { OpenWindow = true, Aureons = 1000, CraftableItemIds = [item.Id],
                Orders = [new() { Id = Guid.NewGuid(), ItemId = item.Id, Quantity = 3, Reward = 500, Customer = "Royal customer", DeadlineUtcMs = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds() }] };
            Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == name);
            void Render() { for(var i=0;i<3;i++) canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero); }
            void Apply() { type.GetMethod("Apply")!.Invoke(window, [packet]); window.Show(); Render(); }
            void Shot(string name) { renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"{name}-{lang}-{size.X}x{size.Y}.png")); }
            Apply(); Check(sounds.Contains("ui_open"), "Opening sound"); Shot("available");
            Check(window.Width <= size.X && window.Height <= size.Y, "Window outside screen");
            Click(Find("OpenComposer")); Render(); Shot("publish");
            Click(Find("Recipe_" + item.Id.ToString("N"))); Check(requests == 0, "Recipe selection posts order");
            Click(Find("PublishOrder")); Click(Find("PublishOrder")); Check(requests == 1, "Double publish while waiting");
            packet.MessageKey = "Posted"; packet.Orders[0].IsCustomer = true; packet.Aureons = 500; Apply(); Shot("posted");
            Check(sounds.Last() == "order_posted", "Posting sound");
            Click(Find("OrderAction")); Check(actions.Last() == 2, "Customer cannot cancel own available order");
            packet.Orders[0].IsCustomer = false; packet.MessageKey = ""; Apply(); Click(Find("Tab_0")); Render();
            // A server response releases the outstanding request before the next action.
            packet.MessageKey = "Unavailable"; Apply(); Click(Find("OrderAction")); Check(actions.Last() == 0, "Accept action");
            packet.Orders[0].Status = 1; packet.Orders[0].IsCrafter = true; packet.MessageKey = "Accepted"; Apply(); Shot("accepted");
            Click(Find("OrderAction")); Check(actions.Last() == 1, "Delivery action");
            packet.MessageKey = "MissingItems"; Apply(); Shot("missing-items"); Check(sounds.Last() == "ui_error", "Missing items error sound");
            packet.MessageKey = "Delivered"; packet.Orders[0].Status = 2; packet.PendingAureons = 500; packet.PendingItems = 3; Apply(); Shot("pending");
            Check(sounds.Last() == "order_delivered", "Pending money falsely sounds paid");
            Check(All(window).OfType<Label>().Single(l => l.Name == "PendingDelivery").Text.Length > 0, "Pending delivery invisible");
            packet.MessageKey = "SaveFailed"; Apply(); Check(sounds.Last() == "ui_error", "Save failure sound"); Shot("save-failed");
            packet.MessageKey = ""; packet.PendingAureons = 0; packet.PendingItems = 0; Apply();
            Click(Find("RefreshOrders")); Render();
            if (size.X == 1280)
            {
                const string atlasName = "crafting-orders/royal-crafting-atlas.png";
                var atlas = content.GetTexture(Intersect.Client.Framework.Content.TextureType.Gui, atlasName)!;
                content.AddGuiTexture(atlasName, null!); Render(); Shot("missing-atlas");
                var ink = All(window).OfType<Label>().Single(l => l.Name == "DetailCustomer");
                var fallbackColor = ink.TextColorOverride;
                content.AddGuiTexture(atlasName, atlas); Render();
                Check(ink.TextColorOverride != fallbackColor, "Atlas restoration does not restore readable parchment text");
                canvas.SetSize(800,600); Render();
                Check(window.Width <= 800 && window.Height <= 600, "Live resize outside screen");
            }
            type.GetMethod("Close")!.Invoke(window, null);
            Check(sounds.Last() == "ui_close", "Closing sound");
            Console.WriteLine($"PASS Crafting UI {lang} {size}: publishing, pending requests, actions, errors, audio cues, bounds");
        }
        ItemDescriptor.Lookup.Delete(item);
    }
}
public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
