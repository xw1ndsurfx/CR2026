using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
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
        if (!TryResolveCinematicScene(map, command.SceneEventId, out var scene) ||
            !Conditions.CanSpawnPage(scene.Pages[0], player, instance))
        { RejectCinematic(player, callStack); return; }
        callStack.Push(new CommandInstance(scene.Pages[0]));
    }

    internal static bool TryResolveCinematicScene(MapDescriptor? map, Guid eventId, out EventDescriptor scene)
    {
        scene = null!;
        // LocalEvents is populated by the editor, not by the authoritative EF
        // loader. The server resolves EventIds through the native descriptor registry.
        return map != null && map.EventIds.Contains(eventId) && EventDescriptor.TryGet(eventId, out scene) &&
            scene.MapId == map.Id && !scene.CommonEvent && !scene.Global && scene.Pages.Count == 1 &&
            scene.Pages[0].Cinematic is { } source && source.MapId == map.Id;
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
        if (command.PresentationOnly)
        {
            if (!instance.CinematicActive || command.MapId != player.MapId ||
                command.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs || !Enum.IsDefined(command.Ease) ||
                !float.IsFinite(command.Letterbox) || command.Letterbox is < 0 or > .25f ||
                !float.IsFinite(command.FadeOpacity) || command.FadeOpacity is < 0 or > 1 ||
                !float.IsFinite(command.ShakePixels) || command.ShakePixels is < 0 or > 32) return;
            player.SendPacket(new CinematicCameraPacket { EventId = instance.Descriptor.Id, MapId = command.MapId,
                PresentationOnly = true, DurationMs = command.DurationMs, Ease = command.Ease, HideHud = command.HideHud,
                Letterbox = command.Letterbox, FadeOpacity = command.FadeOpacity, ShakePixels = command.ShakePixels });
            return;
        }
        if (!instance.CinematicActive || command.MapId != player.MapId ||
            !float.IsFinite(command.X) || !float.IsFinite(command.Y) || !float.IsFinite(command.Zoom) ||
            command.X < 0 || command.Y < 0 || command.X >= Options.Instance.Map.MapWidth || command.Y >= Options.Instance.Map.MapHeight ||
            command.Zoom is < .25f or > 4f || command.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs ||
            !Enum.IsDefined(command.Ease) || !float.IsFinite(command.OffsetX) || !float.IsFinite(command.OffsetY) ||
            Math.Abs(command.OffsetX) > Options.Instance.Map.MapWidth || Math.Abs(command.OffsetY) > Options.Instance.Map.MapHeight) return;
        var targetId = player.Id;
        if (command.FollowActor && command.ActorId != Guid.Empty)
        {
            var actor = player.EventLookup.Values.FirstOrDefault(e => e.Descriptor.Id == command.ActorId && !e.Global &&
                e.MapId == player.MapId && e.PageInstance is { GlobalClone: null });
            if (actor?.PageInstance is not { } page) return;
            targetId = page.Id;
        }
        player.SendPacket(new CinematicCameraPacket { EventId = instance.Descriptor.Id, MapId = command.MapId,
            X = command.X, Y = command.Y, Zoom = command.Zoom, DurationMs = command.DurationMs, Ease = command.Ease,
            FollowActor = command.FollowActor, FollowEntityId = targetId, OffsetX = command.OffsetX, OffsetY = command.OffsetY });
    }
}
