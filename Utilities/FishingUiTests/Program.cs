using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Point = Intersect.Point;
using Console = System.Console;

// Native fishing controls and delivered textures rendered by GDI+, with explicit
// server-state fixtures. This does not replace GPU, live multiplayer or listening QA.
internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerable<Base> All(Base root) { foreach (var c in root.Children) { yield return c; foreach (var n in All(c)) yield return n; } }
    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager(); Intersect.Client.General.Globals.ContentManager = content;
        foreach (var path in Directory.GetFiles("resources/fishing", "*.png", SearchOption.AllDirectories))
            content.AddGuiTexture("fishing/" + Path.GetRelativePath("resources/fishing", path).Replace('\\','/'), new BitmapTexture(path));
        var output = "validation-local/fishing-ui"; Directory.CreateDirectory(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.FishingWindow", true)!;
        var config = new FishingConfiguration();
        var tool = new FishingToolDefinition { ItemId = Guid.NewGuid(), Type = FishingToolType.Net };
        var zone = new FishingZoneDefinition { Id = Guid.NewGuid(), Name = "Polonya", Enabled = true };
        var fish = new FishingCatchDefinition { Id = Guid.NewGuid(), ItemId = Guid.NewGuid(), Sprite = "fishing/items/Item_Sheet_Fishing.png", SpriteX = 128, SpriteY = 128 };
        var fishItem = new ItemDescriptor(fish.ItemId) { Name = "Royal silverfish", Icon = "fishing-fixture.png" }; ItemDescriptor.Lookup.Set(fishItem.Id, fishItem);
        content.AddItemTexture(fishItem.Icon, new BitmapTexture("resources/fishing/items/Item_Sheet_Fishing.png", new System.Drawing.Rectangle(128,128,32,32)));
        zone.Catches.Add(fish); config.Tools.Add(tool); config.Zones.Add(zone);
        foreach (var lang in new[] { "en", "fr", "es" })
        foreach (var size in new[] { new Point(800,600), new Point(1280,720), new Point(1920,1080) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, lang);
            using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new DrawingSkin(renderer);
            using var canvas = new Canvas(skin) { Size = size };
            var window = (Base)Activator.CreateInstance(type, Hidden, null, [canvas], null)!;
            var state = new FishingState { SessionId = Guid.NewGuid(), ZoneId = zone.Id, ZoneName = zone.Name, ToolItemId = tool.ItemId, ToolType = tool.Type,
                AllowAutomatic = true, Level = 3, Experience = 260, ExperienceRequired = 200, ExperienceIntoLevel = 60,
                RemainingBait = 10, CatchDefinitionId = fish.Id, CatchItemId = fish.ItemId, CatchQuantity = 1 };
            foreach (var phase in new[] { FishingPhase.Ready, FishingPhase.Fighting, FishingPhase.LegendaryPrompt, FishingPhase.Loot })
            {
                state.Phase = phase; state.Revision++; state.Legendary = phase == FishingPhase.LegendaryPrompt;
                state.AutomaticResumePending = state.Legendary; state.RemainingMs = 25000;
                state.Progress = 40; state.Tension = 60; state.LineCondition = 80; state.CatchesThisSession = 4;
                state.SkillAvailable = phase == FishingPhase.Fighting ? [true, true, false, true] : [false, false, false, false];
                state.Loot = [new() { ItemId = fish.ItemId, Quantity = 1 }];
                type.GetMethod("Apply", Hidden)!.Invoke(window, [new FishingStatePacket { Open = true, ConfigurationJson = config.ToJson(), StateJson = state.ToJson() }, Guid.Empty]);
                renderer.Clear(); for (var i=0;i<3;i++) canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);
                foreach (var b in All(window).OfType<Button>().Where(b => b.IsVisibleInTree))
                    if (b.X < 0 || b.Y < 0 || b.Right > size.X || b.Bottom > size.Y) throw new Exception("Button outside canvas: " + b.Name);
                renderer.Bitmap.Save(Path.Combine(output, $"{phase}-{lang}-{size.X}x{size.Y}.png"));
            }
            type.GetMethod("Destroy", Hidden)!.Invoke(window, [false]);
            Console.WriteLine($"PASS fishing UI {lang} {size}: Ready/Fighting/Legendary/Loot");
        }
        ItemDescriptor.Lookup.Delete(fishItem);
    }
}
public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
