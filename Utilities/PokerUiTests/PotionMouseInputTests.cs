using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class PotionMouseInputTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer();
        using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
        var inputType = typeof(GameInterface).Assembly
            .GetType("Intersect.Client.Interface.Game.PotionWindow+PotionBoardInput", true)!;
        var rotations = 0; var drops = new List<int>();
        var input = (ControlBase)Activator.CreateInstance(inputType,
            [canvas, new Action<int>(_ => { }), new Action<int>(drops.Add), new Action(() => rotations++)])!;
        input.SetBounds(0, 0, 800, 600);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var down = inputType.GetMethod("OnMouseDown", flags)!;
        var clicked = inputType.GetMethod("OnMouseClicked", flags)!;
        var up = inputType.GetMethod("OnMouseUp", flags)!;
        void Event(MethodInfo method, MouseButton button) => method.Invoke(input, [button, new Point(450, 100), true]);
        Event(down, MouseButton.Right);
        Check(rotations == 1 && drops.Count == 0, "First right press did not rotate exactly once");
        Event(clicked, MouseButton.Right); Event(up, MouseButton.Right);
        Check(rotations == 1, "Right release rotated a second time");
        for (var i = 0; i < 3; i++)
        {
            Event(down, MouseButton.Right); Event(clicked, MouseButton.Right); Event(up, MouseButton.Right);
        }
        Check(rotations == 4, "Rapid right clicks were swallowed");
        Event(down, MouseButton.Left); Event(clicked, MouseButton.Left); Event(up, MouseButton.Left);
        Check(drops.SequenceEqual(new[] { 4 }) && rotations == 4, "Left click did not drop exactly once in its column");
        Event(clicked, MouseButton.Right);
        Check(rotations == 5, "Click-only backend fallback did not rotate");
        input.IsDisabled = true;
        Event(down, MouseButton.Right); Event(down, MouseButton.Left);
        Check(rotations == 5 && drops.Count == 1, "Disabled board accepted input");
        System.Console.WriteLine("PASS potions mouse: first/rapid right-clicks, single rotation on release, left drop, disabled board");

        var sceneType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PotionWindow", true)!;
        var modelType = typeof(GameInterface).Assembly.GetType("Intersect.Client.MiniGames.PotionClientModel", true)!;
        var requests = new List<(PotionRequestKind Kind, int Column, int Orientation)>();
        var scene = (ControlBase)Activator.CreateInstance(sceneType,
            [canvas, new Action<PotionRequestKind, int, Guid, int>((kind, column, _, orientation) =>
                requests.Add((kind, column, orientation)))])!;
        try
        {
            var model = Activator.CreateInstance(modelType)!;
            var player = Guid.NewGuid(); var session = Guid.NewGuid(); var recipe = Guid.NewGuid();
            var state = new PotionSessionState
            {
                RecipeId = recipe, RecipeName = "Test brew", RecipeRound = 1, RequiredLevel = 1,
                Board = new int[80], CurrentFirst = 1, CurrentSecond = 1, NextFirst = 5, NextSecond = 9,
                OutputItemId = Guid.NewGuid(), OutputItemName = "Test potion", OutputQuantity = 1,
                CompletionExperience = 10, Level = 1,
                Requirements = [new() { Family = 0, Level = 2, Needed = 20 }],
                RecipeChoices = [new() { Id = recipe, Name = "Test brew", RequiredLevel = 1,
                    OutputItemName = "Test potion", OutputQuantity = 1, CompletionExperience = 10, Unlocked = true }],
            };
            state.Board[9 * 8] = state.Board[8 * 8] = 1;
            var packet = new PotionStatePacket { PlayerId = player, SessionId = session, Sequence = 1, State = state };
            Check(packet.IsValid, "Invalid live-window fixture");
            Check((bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, Environment.TickCount64])!,
                "Fixture packet rejected");
            sceneType.GetMethod("Update")!.Invoke(scene, [model]);
            var board = (ControlBase)sceneType.GetField("_boardInput", flags)!.GetValue(scene)!;
            var position = new Point(board.X + board.Width / 16, board.Y + board.Height / 2);
            down.Invoke(board, [MouseButton.Right, position, true]);
            clicked.Invoke(board, [MouseButton.Right, position, true]);
            up.Invoke(board, [MouseButton.Right, position, true]);
            Check((PotionPairOrientation)sceneType.GetField("_localOrientation", flags)!.GetValue(scene)! ==
                  PotionPairOrientation.Horizontal, "Real window did not rotate the pair");
            down.Invoke(board, [MouseButton.Left, position, true]);
            Check(requests.SequenceEqual(new[] { (PotionRequestKind.Drop, 0, 1) }),
                "Real window sent a wrong column/orientation or extra action");

            state = new PotionSessionState
            {
                RecipeId = recipe, RecipeName = "Test brew", RecipeRound = 1, RequiredLevel = 1, Revision = 1,
                Board = new int[80], CurrentFirst = 5, CurrentSecond = 9, NextFirst = 1, NextSecond = 5,
                OutputItemId = state.OutputItemId, OutputItemName = "Test potion", OutputQuantity = 1,
                CompletionExperience = 10, Level = 1, Requirements = state.Requirements, RecipeChoices = state.RecipeChoices,
            };
            packet.State = state; packet.Sequence = 2;
            modelType.GetMethod("Apply")!.Invoke(model, [packet, player, Environment.TickCount64]);
            sceneType.GetMethod("Update")!.Invoke(scene, [model]);
            var presentation = sceneType.GetField("_presentation", flags)!.GetValue(scene)!;
            Check((bool)presentation.GetType().GetMethod("IsAnimating")!.Invoke(presentation, [Environment.TickCount64])!,
                "Real window erased the drop when the server returned an empty board");
            System.Console.WriteLine("PASS potions scene: right rotation, left drop packet, empty server response preserves falling pair");
        }
        finally { sceneType.GetMethod("Destroy")!.Invoke(scene, null); }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Potion mouse: " + message);
    }
}
