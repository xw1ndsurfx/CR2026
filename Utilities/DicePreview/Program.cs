using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using Console = System.Console;

// Visual QA of the actual window, with original PNGs and a software Gwen renderer.
// This deliberately does not fabricate an in-game map or claim to be a live multiplayer capture.
var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, PreviewContextProxy>();
typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
var manager = new TestContentManager();
foreach (var file in Directory.GetFiles(Path.Combine(root, "resources/gui/roll-the-dice"), "*.png"))
    manager.AddGuiTexture("roll-the-dice/" + Path.GetFileName(file), new BitmapTexture(file));
var assembly = typeof(GameInterface).Assembly;
var type = assembly.GetType("Intersect.Client.Interface.Game.DiceWindow", true)!;
var modelType = assembly.GetType("Intersect.Client.MiniGames.DiceClientModel", true)!;
var player = Guid.NewGuid();
var currency = new Intersect.Framework.Core.GameObjects.Items.ItemDescriptor(Guid.NewGuid()) { Name = "Aureons" };
Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup[currency.Id] = currency;
var packet = new DiceStatePacket { TableInstanceId = Guid.NewGuid(), ViewId = Guid.NewGuid(), PlayerId = player,
    Sequence = 1, TableName = "dice-1", State = new DiceTableState { Balance = 2450, Bank = 10000,
        MinimumBet = 25, MaximumBet = 1000, Step = 25, OutsideReturn = 2, SevenReturn = 5,
        RoundId = Guid.NewGuid(), First = 4, Second = 5, LastBet = DiceBet.OverSeven,
        Wager = 100, Return = 200, Experience = 125, ExperienceAwarded = 25, CurrencyItemId = currency.Id,
        Participants = new[] { "Nicolas", "Alice", "Marlow" }.Select((name, i) => new DiceParticipant { PlayerId = i == 0 ? player : Guid.NewGuid(),
            Name = name, RoundId = Guid.NewGuid(), First = i == 0 ? 4 : i + 3, Second = 5,
            Bet = i == 1 ? DiceBet.UnderSeven : DiceBet.OverSeven, Wager = 100, Return = i == 1 ? 0 : 200 }).ToArray() } };
foreach (var language in new[] { "en", "fr", "es" })
foreach (var size in new[] { new Point(1280, 720), new Point(1920, 1080) })
{
    typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
    using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new DrawingSkin(renderer);
    skin.Colors.Button.Normal = skin.Colors.Button.Active = skin.Colors.Button.Hover = skin.Colors.Button.Disabled = Color.White;
    using var canvas = new Canvas(skin) { Size = size };
    var scene = (ControlBase)Activator.CreateInstance(type, [canvas, new Action<DiceRequestKind, long, DiceBet, int>((_, _, _, _) => { })])!;
    var model = Activator.CreateInstance(modelType)!;
    modelType.GetMethod("Apply")!.Invoke(model, [packet, player, 1000L]);
    type.GetMethod("Update")!.Invoke(scene, [model]);
    canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
    renderer.Bitmap.Save(Path.Combine(output, $"dice-{language}-{size.X}x{size.Y}.png"));
    type.GetField("_showRules", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, true);
    type.GetMethod("Update")!.Invoke(scene, [model]);
    canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(32));
    renderer.Bitmap.Save(Path.Combine(output, $"dice-rules-{language}-{size.X}x{size.Y}.png"));
    type.GetMethod("Destroy")!.Invoke(scene, null);
    Console.WriteLine($"Rendered real DiceWindow controls: {language}, {size.X}x{size.Y}");
}

public class PreviewContextProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger"
        ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : null;
}
