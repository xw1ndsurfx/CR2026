using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Console = System.Console;

// Real Gwen controls with synthetic font metrics; no GPU or live-server claim.
const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.NavalExpeditionWindow", true)!;
typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); _ = new TestContentManager();
ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ExpeditionLogProxy>();
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static IEnumerable<Base> All(Base root) { foreach (var child in root.Children) { yield return child; foreach (var next in All(child)) yield return next; } }
static void Click(Button button)
{
    Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = button;
    var input = typeof(Base).GetMethod("InputMouseButtonState", BindingFlags.Instance | BindingFlags.NonPublic)!;
    input.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), true, false]);
    input.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), false, false]);
}
foreach (var language in new[] { "en", "fr", "es" })
foreach (var size in new[] { new Point(640, 480), new Point(1280, 720), new Point(1920, 1080) })
{
    typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
    using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
    var sent = new List<NavalExpeditionRequestPacket>();
    var window = (Base)Activator.CreateInstance(type, hidden, null, [canvas, new Action<NavalExpeditionRequestPacket>(sent.Add)], null)!;
    var definition = new NavalExpeditionDefinition { Name = "North passage", Description = new string('x', 1000), Enabled = true, Mode = NavalExpeditionMode.Either,
        Stages = [new() { MapId = Guid.NewGuid(), X = 10, Y = 10 }, new() { MapId = Guid.NewGuid(), Objective = NavalExpeditionObjective.DefeatMonster, TargetId = Guid.NewGuid() }] };
    void Apply(NavalExpeditionView view, bool open = true) => type.GetMethod("Apply", hidden)!.Invoke(window, [view, open]);
    Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == name);
    Apply(new() { Available = [definition] });
    typeof(Base).GetMethod("DoLayoutIfNeeded", hidden)!.Invoke(window, [skin]);
    Check(window.Width <= size.X && window.Height <= size.Y, "Window exceeds canvas");
    Click(Find("Propose" + definition.Id));
    Check(sent.Count == 1 && sent[0].Action == NavalExpeditionAction.Propose && sent[0].Id == definition.Id, "Proposal loses definition id");
    var run = new NavalExpeditionView { RunId = Guid.NewGuid(), Definition = definition, Status = NavalExpeditionStatus.Preparing,
        Members = [new() { PlayerId = Guid.NewGuid(), ShipId = Guid.NewGuid(), Name = "Nicolas", Present = true }, new() { PlayerId = Guid.NewGuid(), ShipId = Guid.NewGuid(), Name = "Friend" }] };
    Apply(run);
    var ready = Find("ExpeditionReady"); var start = Find("ExpeditionStart"); var claim = Find("ExpeditionClaim"); var leave = Find("ExpeditionLeave");
    var list = (ScrollControl)type.GetField("_list", hidden)!.GetValue(window)!;
    Check(list.Bottom < ready.Y && ready.Right < start.X && list.Height > 150, "Roster and actions overlap");
    Check(ready.Text == NavalSetupText.Get("Ready for departure", language), "Ready is not translated");
    Check(!ready.IsHidden && start.IsDisabled && claim.IsHidden, "Preparation actions ignore server state");
    var previous = sent.Count; Click(start); Check(sent.Count == previous, "Disabled departure sent a request");
    Click(ready); Check(sent.Last().Action == NavalExpeditionAction.Ready && sent.Last().Id == run.RunId, "Ready action loses run id");
    run.CanStart = true; Apply(run, false); Check(!start.IsDisabled && !window.IsHidden, "Leader cannot depart when server authorizes it");
    var label = All(window).OfType<Label>().Single(l => l.Name == "RunName");
    run.RemainingSeconds = 60; Apply(run, false); Check(All(window).Contains(label), "Timer recreates the scroll contents");
    previous = sent.Count; Click(leave); Check(sent.Count == previous && leave.Text == NavalSetupText.Get("Confirm abandon for everyone", language), "Abandon needs confirmation");
    Click(Find("CloseExpedition")); Check(window.IsHidden && sent.Count == previous, "Closing abandons the run");
    Apply(run); Click(leave); Check(sent.Count == previous, "Closing did not reset abandonment confirmation");
    Click(leave); Check(sent.Last().Action == NavalExpeditionAction.Leave, "Confirmed abandonment not sent");
    run.Status = NavalExpeditionStatus.Active; run.StageMapName = "Ocean"; run.Progress = 1; run.Required = 2; Apply(run);
    Check(ready.IsHidden && start.IsHidden && claim.IsHidden, "Active objective exposes a completion bypass");
    Check(All(window).OfType<Label>().Any(l => l.Name == "Destination" && l.Text?.Contains("Ocean") == true), "Destination not shown");
    run.Status = NavalExpeditionStatus.Completed; run.StageIndex = 2; run.CanClaim = true; Apply(run);
    Check(!claim.IsHidden && leave.IsHidden, "Completed reward cannot be claimed");
    Click(claim); Check(sent.Last().Action == NavalExpeditionAction.Claim, "Claim action not sent");
    foreach (var button in new[] { ready, start, claim, leave, Find("CloseExpedition") }.Where(b => !b.IsHidden))
        Check(button.X >= 0 && button.Y >= 0 && button.Right <= button.Parent!.Width && button.Bottom <= button.Parent.Height, "Action clipped: " + button.Name);
    var game = (GameInterface)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
    typeof(GameInterface).GetField("<GameCanvas>k__BackingField", hidden)!.SetValue(game, canvas);
    var applyHud = typeof(GameInterface).GetMethod("ApplyNavalExpedition", hidden)!;
    var updateHud = typeof(GameInterface).GetMethod("UpdateNavalExpeditionInterface", hidden)!;
    applyHud.Invoke(game, [new Intersect.Network.Packets.Server.NavalExpeditionPacket { Json = Newtonsoft.Json.JsonConvert.SerializeObject(run) }]);
    var hud = (Button)typeof(GameInterface).GetField("_navalExpeditionHud", hidden)!.GetValue(game)!;
    Check(!hud.IsHidden && hud.X >= 0 && hud.Right <= size.X, "Expedition tracker missing or clipped");
    var modal = new Intersect.Client.Framework.Gwen.ControlInternal.Modal(canvas);
    updateHud.Invoke(game, null); Check(hud.IsHidden, "Expedition tracker covers modal dialogue");
    modal.Hide(); updateHud.Invoke(game, null); Check(!hud.IsHidden, "Expedition tracker does not return after dialogue");
    Console.WriteLine($"PASS expedition UI: roster, scrolling, translations, readiness, confirmation, reward and close at {size} / {language}");
}

sealed class TestSkin : SkinBase
{
    public TestSkin(RendererBase renderer) : base(renderer) { DefaultFont = new TestFont(); DefaultFontSize = 12; Colors.Label.Normal = Colors.Label.Disabled = Color.White; }
}
sealed class MetricsRenderer : RendererBase
{
    public override Point MeasureText(IFont? font, int fontSize, string? text, float scale = 1f) => new((int)Math.Ceiling((text?.Length ?? 0) * fontSize * .6f * scale), (int)Math.Ceiling((fontSize + 3) * scale));
}
sealed class TestFont : IFont
{
    public string Name => "expedition-test-metrics";
    public ICollection<int> SupportedSizes { get; } = new[] { 10, 11, 12, 14 };
    public int PickBestMatchFor(int size) => size;
    public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
}
public class ExpeditionLogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger"
        ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
