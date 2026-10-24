using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Server;
namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(PlayCinematicCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (command.MapId != player.MapId || callStack.Count >= 32)
        { RejectCinematic(player, callStack); return; }
        var map = Intersect.Server.Maps.MapController.Get(command.MapId);
        if (map == null || !map.LocalEvents.TryGetValue(command.SceneEventId, out var scene) || scene.Global ||
            scene.Pages.Count != 1 || scene.Pages[0].Cinematic == null ||
            !Conditions.CanSpawnPage(scene.Pages[0], player, instance))
        { RejectCinematic(player, callStack); return; }
        callStack.Push(new CommandInstance(scene.Pages[0]));
    }

    private static void ProcessCommand(BeginCinematicCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (instance.TryBeginCinematic(command)) return;
        RejectCinematic(player, callStack);
    }

    private static void RejectCinematic(Player player, Stack<CommandInstance> callStack)
    {
        callStack.Clear();
        Intersect.Server.Networking.PacketSender.SendChatMsg(player,
            Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(
                "The cinematic could not start. Check the scene's map, starting positions and active actions.", player.Client?.LanguageCode ?? "en"),
            Intersect.Enums.ChatMessageType.Error, Color.Red);
    }

    private static void ProcessCommand(CinematicCameraCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (command.Reset) { instance.FinishCinematic(); return; }
        if (!instance.CinematicActive || command.MapId != player.MapId ||
            !float.IsFinite(command.X) || !float.IsFinite(command.Y) || !float.IsFinite(command.Zoom) ||
            command.X < 0 || command.Y < 0 || command.X >= Options.Instance.Map.MapWidth || command.Y >= Options.Instance.Map.MapHeight ||
            command.Zoom is < .25f or > 4f || command.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs) return;
        player.SendPacket(new CinematicCameraPacket { EventId = instance.Descriptor.Id, MapId = command.MapId,
            X = command.X, Y = command.Y, Zoom = command.Zoom, DurationMs = command.DurationMs, Ease = command.Ease });
    }
}
