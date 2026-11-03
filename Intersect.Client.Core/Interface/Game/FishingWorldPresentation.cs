using System.Text.Json;
using Intersect.Client.Framework.Entities;
using Intersect.Framework.Core.Fishing;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

/// <summary>Replicated presentation only; no catch or input decisions are made here.</summary>
internal static class FishingWorldPresentation
{
    internal sealed record Presentation(Guid ToolItemId, FishingWorldAction Action, FishingWorldProfile Profile, long Received, long LastSeen, long Sequence);
    private static readonly Dictionary<Guid, Presentation> Players = [];
    private static readonly Dictionary<Guid, long> Sequences = [];

    internal static void Apply(FishingWorldPacket packet)
    {
        if (Sequences.TryGetValue(packet.EntityId, out var previousSequence) && previousSequence >= packet.Sequence) return;
        Sequences[packet.EntityId] = packet.Sequence;
        if (packet.Action == FishingWorldAction.Stop) { Players.Remove(packet.EntityId); return; }
        try
        {
            var profile = JsonSerializer.Deserialize<FishingWorldProfile>(packet.ProfileJson, FishingConfiguration.JsonOptions);
            if (profile == null || !profile.Enabled) { Players.Remove(packet.EntityId); return; }
            profile.Validate();
            var received = Players.TryGetValue(packet.EntityId, out var existing) && existing.Action == packet.Action && existing.ToolItemId == packet.ToolItemId
                ? existing.Received : Environment.TickCount64;
            Players[packet.EntityId] = new(packet.ToolItemId, packet.Action, profile, received, Environment.TickCount64, packet.Sequence);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException) { Players.Remove(packet.EntityId); }
    }

    internal static bool TryGet(IEntity entity, out Presentation presentation, out FishingWorldClip clip, out int frame)
    {
        clip = null!; frame = 0;
        if (!Players.TryGetValue(entity.Id, out presentation!)) return false;
        // Replicas expire if an interruption/disconnection packet is lost; server refreshes active profiles.
        if (Environment.TickCount64 - presentation.LastSeen > 15000 || entity.IsAttacking() || entity.IsCasting || entity.IsDashing)
        { Players.Remove(entity.Id); return false; }
        var action = entity.IsMoving ? FishingWorldAction.Move : presentation.Action;
        if (!presentation.Profile.Clips.TryGetValue(action, out clip!)) return false;
        var elapsedFrames = Math.Max(0, (Environment.TickCount64 - presentation.Received) * presentation.Profile.FramesPerSecond / 1000);
        frame = clip.Loop ? (int)(elapsedFrames % Math.Max(1, clip.Frames)) : (int)Math.Min(clip.Frames - 1, elapsedFrames);
        return true;
    }

    private static bool IsAttacking(this IEntity entity) => entity is Entities.Entity { IsAttacking: true };
    internal static void Remove(Guid entityId) { Players.Remove(entityId); Sequences.Remove(entityId); }
    internal static void Clear() { Players.Clear(); Sequences.Clear(); }
}
