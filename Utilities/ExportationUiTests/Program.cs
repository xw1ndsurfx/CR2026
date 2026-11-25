using System.Reflection;
using System.Text.Json;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Items;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Point = Intersect.Point;
using Console = System.Console;

// Runs the production Gwen window and real PNGs. Server replies are explicit
// fixtures; this is not a connected voyage or a GPU/Empire-font validation.
internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static IEnumerable<Base> All(Base root) { foreach (var c in root.Children) { yield return c; foreach (var n in All(c)) yield return n; } }
    private static void Click(Button button)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = button;
        var method = typeof(Base).GetMethod("InputMouseButtonState", Hidden)!;
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), true, false]);
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), false, false]);
    }

    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager();
        Intersect.Client.General.Globals.ContentManager = content;
        var gui = Path.Combine("resources", "gui");
        foreach (var path in Directory.GetFiles(Path.Combine(gui, "exportation"), "*.png", SearchOption.AllDirectories))
            content.AddGuiTexture(Path.GetRelativePath(gui, path).Replace('\\', '/'), new BitmapTexture(path));
        var names = new[] { "Oak planks", "Iron ingots", "Glass flasks", "Apples", "Cotton cloth", "Spare planks" };
        var sprites = new[] { "planches", "lingots_fer", "potion_rouge", "pommes", "tissu", "planches" };
        var items = names.Select((name, i) => new ItemDescriptor(Guid.NewGuid()) { Name = name, Icon = sprites[i] + ".png" }).ToArray();
        foreach (var item in items)
        {
            ItemDescriptor.Lookup.Set(item.Id, item);
            content.AddItemTexture(item.Icon, new BitmapTexture(Path.Combine(gui, "exportation", "sprites", item.Icon)));
        }
        var output = Path.Combine("validation-local", "exportation-ui"); Directory.CreateDirectory(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ExportationWindow", true)!;
        foreach (var lang in new[] { "en", "fr", "es" })
        foreach (var size in new[] { new Point(800, 600), new Point(1280, 720), new Point(1600, 900), new Point(1672, 941) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, lang);
            using var renderer = new DrawingRenderer(size.X, size.Y);
            using var skin = new DrawingSkin(renderer);
            using var canvas = new Canvas(skin) { Size = size };
            var requests = new List<(Guid Item, CargoGroup Group, int Amount)>();
            var seals = 0; var deliveries = 0; var restarts = 0; var sounds = new List<string>();
            var window = (Base)Activator.CreateInstance(type, Hidden, null,
                [canvas, new Action<Guid, CargoGroup, int>((id, group, amount) => requests.Add((id, group, amount))),
                new Action(() => seals++), new Action(() => deliveries++), new Action(() => restarts++)], null)!;
            type.GetProperty("AudioCue", Hidden)!.SetValue(window, new Action<string>(sounds.Add));
            var route = new ExportationRoute
            {
                Name = "Supplies for the royal outpost", SourceMapId = Guid.NewGuid(), RecipientMapId = Guid.NewGuid(),
                SourceNpcId = Guid.NewGuid(), RecipientNpcId = Guid.NewGuid(), QuestId = Guid.NewGuid(),
                RewardItemId = items[1].Id, RewardQuantity = 25, ExperienceReward = 120,
                Lines = items.Select((item, i) => new CargoLine { ItemId = item.Id, Sprite = sprites[i], Quantity = 3,
                    Group = i == 2 ? CargoGroup.Fragile : i is 3 or 4 ? CargoGroup.Provisions : CargoGroup.Materials }).ToArray(),
            };
            var map = route.SourceMapId;
            type.GetProperty("CurrentMapId", Hidden)!.SetValue(window, new Func<Guid>(() => map));
            var run = new ExportationRun { RouteId = route.Id };
            Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == "Exportation" + name);
            Label Text(string name) => All(window).OfType<Label>().Single(b => b.Name == "Exportation" + name);
            void Render()
            {
                type.GetMethod("Update", Hidden)!.Invoke(window, null);
                canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
                canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
            }
            void Apply(bool boat = true)
            {
                var copy = JsonSerializer.Deserialize<ExportationRun>(JsonSerializer.Serialize(run))!;
                type.GetMethod("Apply", Hidden)!.Invoke(window, [route, copy, "Royal merchant", "Harbour quartermaster",
                    "Polonya Port", "Royal Outpost", boat, 3, 150L, 500L, false]);
                Render();
            }
            void Shot(string state, bool day = false)
            {
                renderer.Clear();
                renderer.DrawColor = day ? new Intersect.Color(255, 85, 145, 175) : new Intersect.Color(255, 9, 15, 28);
                renderer.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(0, 0, size.X, size.Y));
                Render(); renderer.Bitmap.Save(Path.Combine(output, $"{state}-{lang}-{size.X}x{size.Y}.png"));
            }
            Apply(false); Check(Find("Seal").IsDisabled, "Seal without boat");
            Apply(); Click(Find("Good0")); Check(requests.Last() == (items[0].Id, CargoGroup.Materials, 1), "Wrong packed item");
            Check(Text("Progress").Text?.Contains("0 / 18") == true, "Client predicted server cargo");
            run.Packed[items[0].Id] = 1; Apply(); Check(sounds.Last() == "item_wood", "Missing wood sound");
            Click(Find("Remove0")); Check(requests.Last().Amount == -1, "Remove did not send request");
            run.Packed[items[0].Id] = 0; Apply(); Check(sounds.Last() == "item_remove", "Missing remove sound");
            Check(Find("Remove0").IsHidden, "Remove visible on empty line");
            run.Packed[items[1].Id] = 1; Apply(); Check(sounds.Last() == "item_metal", "Iron uses wood sound");
            run.Packed[items[2].Id] = 1; Apply(); Check(sounds.Last() == "item_glass", "Missing glass sound");
            Click(Find("NextGoods")); Render(); Click(Find("Good0"));
            Check(requests.Last().Item == items[5].Id && Find("Good1").IsHidden && Find("NextGoods").IsDisabled, "Shelf pagination wrong");
            Click(Find("PreviousGoods")); Render();
            Click(Find("NextOrders")); Render(); Check(Find("NextOrders").IsDisabled, "Order pagination wrong");
            Click(Find("PreviousOrders")); Render();
            Shot("preparing-day", true);
            var dayPixel = renderer.Bitmap.GetPixel(size.X / 2, size.Y / 2);
            Shot("preparing-night");
            Check(dayPixel == renderer.Bitmap.GetPixel(size.X / 2, size.Y / 2), "Night world darkens the crate panel");
            Check(Text("Route").TextHeight <= Text("Route").Height, "Route name is clipped vertically");
            Click(Find("Help")); Render();
            var modal = All(window).Single(c => c.Name == "ExportationHelpOverlay");
            Check(!modal.IsHidden, "Help missing");
            var count = requests.Count; Click(Find("Good0")); Check(requests.Count == count, "Help allows packing behind it");
            Shot("help"); Click(Find("HelpClose")); Render();
            Check(modal.IsHidden, "Help cannot close"); Click(Find("Good0")); Check(requests.Count == count + 1, "Closing help leaves controls disabled");
            foreach (var line in route.Lines) run.Packed[line.ItemId] = line.Quantity;
            Apply(); Check(sounds.Last() == "order_complete" && !Find("Seal").IsDisabled, "Completed order not ready");
            var soundCount = sounds.Count; Apply(); Check(sounds.Count == soundCount, "Identical server refresh repeats audio");
            Click(Find("Seal")); Check(seals == 1 && run.Stage == DeliveryStage.Preparing, "Seal must request, not mutate state");
            run.Stage = DeliveryStage.InTransit; Apply(); Check(sounds.Last() == "cargo_seal", "Seal sound");
            Check(Find("Seal").IsDisabled && Find("Good0").IsDisabled, "Sealed order editable"); Shot("sealed");
            map = route.RecipientMapId; Apply(); Check(Find("Seal").IsDisabled, "Teleport permits delivery");
            run.BoardedByShip = true; run.ArrivedByShip = true; Apply(); Click(Find("Seal")); Check(deliveries == 1, "Confirmed arrival cannot deliver");
            Shot("arrived"); run.Stage = DeliveryStage.Delivered; Apply();
            Check(sounds.Last() == "delivery_success" && Find("Seal").IsHidden, "Delivered state incorrect"); Shot("delivered");
            run.Stage = DeliveryStage.Lost; run.Packed.Clear(); Apply();
            Check(sounds.Last() == "cargo_lost" && Find("Seal").IsDisabled, "Loss state wrong away from merchant");
            map = route.SourceMapId; Apply(); Click(Find("Seal")); Check(restarts == 1, "Cannot restart at merchant"); Shot("lost");
            run.Stage = DeliveryStage.Abandoned; Apply(); Check(Find("Seal").IsHidden, "Abandoned cargo can be sealed");
            // Changing to a single-line order clamps both pages and hides paging.
            route.Lines = [route.Lines[0]]; run.Stage = DeliveryStage.Preparing; Apply();
            Check(Find("NextGoods").IsHidden && Find("NextOrders").IsHidden, "Single page navigation visible");
            Check(Text("OtherCrateCount").Text == ExportationText.Get("Empty crate", lang), "Empty group marked done");
            Check(Find("HelpClose").Text == ExportationText.Get("Close help", lang), "Help untranslated");
            foreach (var control in window.Children.Where(c => !c.IsHidden))
                Check(control.X >= 0 && control.Y >= 0 && control.Right <= size.X && control.Bottom <= size.Y, "Control outside window: " + control.Name);
            type.GetMethod("Destroy", Hidden)!.Invoke(window, null); Check(sounds.Last() == "ui_close", "Close sound missing");
            Console.WriteLine($"PASS Exportation {lang} {size}: cargo, paging, help, server replies, sounds, voyage gates, restart, bounds");
        }
        // Missing optional art must use the item's own icon; adding the art via
        // resource reload must become visible without reopening the window.
        var fallbackContent = new TestContentManager();
        Intersect.Client.General.Globals.ContentManager = fallbackContent;
        fallbackContent.AddItemTexture(items[0].Icon, new BitmapTexture(Path.Combine(gui, "exportation", "sprites", items[0].Icon)));
        var itemLookups = 0;
        fallbackContent.TextureRequested = (kind, _) => { if (kind == Intersect.Client.Framework.Content.TextureType.Item) itemLookups++; };
        using (var renderer = new DrawingRenderer(1280, 720))
        using (var skin = new DrawingSkin(renderer))
        using (var canvas = new Canvas(skin) { Size = new Point(1280, 720) })
        {
            var window = (Base)Activator.CreateInstance(type, Hidden, null,
                [canvas, new Action<Guid, CargoGroup, int>((_, _, _) => { }), new Action(() => { }), new Action(() => { }), new Action(() => { })], null)!;
            var route = new ExportationRoute { Name = "Fallback", SourceMapId = Guid.NewGuid(), RecipientMapId = Guid.NewGuid(),
                SourceNpcId = Guid.NewGuid(), RecipientNpcId = Guid.NewGuid(), QuestId = Guid.NewGuid(),
                Lines = [new CargoLine { ItemId = items[0].Id, Sprite = "planches", Quantity = 2 }] };
            type.GetMethod("Apply", Hidden)!.Invoke(window, [route, new ExportationRun { RouteId = route.Id }, "Merchant", "Recipient", "Source", "Destination", true, 1, 0L, 100L, false]);
            type.GetMethod("Update", Hidden)!.Invoke(window, null);
            canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
            Check(itemLookups > 0, "Missing goods sprite hides the item icon");
            fallbackContent.AddGuiTexture("exportation/background.png", new BitmapTexture(Path.Combine(gui, "exportation/background.png")));
            canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
            var textures = (System.Collections.IDictionary)type.GetField("_textures", Hidden)!.GetValue(window)!;
            Check(textures["exportation/background.png"] is not null, "Resource reload kept cached missing background");
            type.GetMethod("Destroy", Hidden)!.Invoke(window, null);
        }
        Console.WriteLine("PASS missing-art item fallback and resource reload");
        foreach (var item in items) ItemDescriptor.Lookup.Delete(item);
    }
}

public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
