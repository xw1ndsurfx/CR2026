using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class DungeonAbandonButtonTests
{
    private sealed class RecordingSocket : Intersect.Client.Framework.Network.GameSocket
    {
        internal readonly List<object> Sent = [];
        public override void SendPacket(object packet) => Sent.Add(packet);
        public override void Connect(string host, int port) { }
        public override void Update() { }
        public override void Disconnect(string reason) { }
        public override void Dispose() { }
        public override bool IsConnected => true;
        public override int Ping => 0;
        public override Intersect.Network.INetwork Network => null!;
    }

    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<ControlBase> All(ControlBase node) => new[] { node }.Concat(node.Children.SelectMany(All));

    private static void Verify()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer();
        using var skin = new TestSkin(renderer);
        // Reproduce a skin whose default button foreground is dark.
        skin.Colors.Button.Normal = skin.Colors.Button.Hover = skin.Colors.Button.Active = Color.Black;
        using var canvas = new Canvas(skin) { Size = new Point(1920, 1080) };
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.DungeonRunHudWindow", true)!;
        var hud = (ControlBase)Activator.CreateInstance(type, [canvas])!;
        var button = (Button)hud.Children.Single(child => child.Name == "DungeonRunAbandon");
        var apply = type.GetMethod("Apply")!;
        var state = new DungeonRunStatePacket { Status = DungeonRunStatus.Active, RunId = Guid.NewGuid() };
        apply.Invoke(hud, [state]);
        Check(!button.IsHidden && button.Bottom <= hud.Height && button.MouseInputEnabled,
            "Active dungeon exposes an accessible abandon button inside the HUD.");
        Check(hud.GetComponentAt(button.X + button.Width / 2, button.Y + button.Height / 2) == button,
            "An actual mouse hit reaches the abandon button.");
        var uiField = typeof(Intersect.Client.Interface.Interface).GetField("_uiInGame", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldUi = uiField.GetValue(null);
        var oldContext = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        var ui = (GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
        typeof(Intersect.Client.Interface.MutableInterface).GetField("<Root>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ui, canvas);
        uiField.SetValue(null, ui);
        try
        {
            type.GetMethod("ConfirmAbandon", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, null);
            var modal = canvas.Children.Single(c => c is Intersect.Client.Framework.Gwen.ControlInternal.Modal);
            var confirmation = modal.Children.Single(c => c.Name == "DungeonAbandonConfirmationWindow");
            Check(!confirmation.IsHidden, "Clicking abandon creates a visible confirmation.");
            var cancel = All(confirmation).OfType<Button>().Single(c => c.Name == "CancelDungeonAbandon");
            Check(cancel.Font != null && cancel.FontSize >= 12, "Confirmation actions have explicit readable fonts.");
            typeof(Button).GetMethod("OnKeyReturn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cancel, [false]);
            var network = type.Assembly.GetType("Intersect.Client.Networking.Network", true)!;
            var socketField = network.GetField("_socket", BindingFlags.Static | BindingFlags.NonPublic)!;
            var previousSocket = socketField.GetValue(null);
            var socket = new RecordingSocket();
            socketField.SetValue(null, socket);
            try
            {
                type.GetMethod("ConfirmAbandon", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, null);
                var next = All(canvas).Single(c => c.Name == "DungeonAbandonConfirmationWindow" && !c.IsHidden);
                var yes = All(next).OfType<Button>().Single(c => c.Name == "ConfirmDungeonAbandon");
                typeof(Button).GetMethod("OnKeyReturn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(yes, [false]);
                Check(socket.Sent.Count == 1 && socket.Sent[0] is Intersect.Network.Packets.Client.AbandonDungeonPacket request &&
                    request.RunId == state.RunId && next.IsHidden,
                    "Yes closes the confirmation and sends exactly one request for the current run.");
            }
            finally { socketField.SetValue(null, previousSocket); }
        }
        finally { uiField.SetValue(null, oldUi); Intersect.Core.ApplicationContext.Context.Value = oldContext; }
        foreach (var locale in new[] { "en", "fr", "es" })
        {
            button.Text = QualityOfLifeText.Get("Abandon dungeon", locale);
            var measured = renderer.MeasureText(button.Font, button.FontSize, button.Text);
            Check(measured.X + 12 <= button.Width && measured.Y + 8 <= button.Height,
                "Abandon button caption fits without clipping in " + locale);
        }
        button.UpdateColors();
        Check(button.TextColorOverride == Color.White && button.Font != null,
            "The abandon caption has its own white foreground and explicit font, even on dark-default skins.");
        var render = button.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rectangles = renderer.Rectangles;
        render.Invoke(button, [skin]);
        Check(renderer.Rectangles >= rectangles + 2 && !button.ShouldDrawBackground,
            "The button draws an opaque fill and border independently of external skin textures.");
        foreach (var status in new[] { DungeonRunStatus.Completed, DungeonRunStatus.Failed })
        {
            state.Status = status; apply.Invoke(hud, [state]);
            Check(button.IsHidden && hud.Height == 126, "Results cannot offer an abandon action.");
        }
        foreach (var status in new[] { DungeonRunStatus.Abandoned, DungeonRunStatus.None })
        {
            state.Status = DungeonRunStatus.Active; apply.Invoke(hud, [state]);
            state.Status = status; apply.Invoke(hud, [state]);
            type.GetMethod("Update")!.Invoke(hud, null);
            Check(hud.IsHidden, "The server-confirmed exit clears the dungeon HUD permanently.");
        }
        state.Status = DungeonRunStatus.Active; state.RunId = Guid.Empty; apply.Invoke(hud, [state]);
        Check(button.IsHidden, "A missing run identifier never exposes abandon.");
        System.Console.WriteLine("PASS dungeon abandon button: locale fit, explicit font/contrast, opaque background, active/result visibility");
    }
}
