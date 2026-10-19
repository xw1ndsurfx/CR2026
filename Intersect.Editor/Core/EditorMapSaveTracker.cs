using Intersect.Editor.Classes.Maps;
using Intersect.Editor.Maps;

namespace Intersect.Editor.Core;

internal static class EditorMapSaveTracker
{
    // Accessed on the UI thread. A server echo must not erase edits made after SendMap.
    private static readonly Dictionary<Guid, Queue<(MapSaveState State, DateTime Sent)>> Pending = [];

    internal static void Sent(MapInstance map)
    {
        foreach (var id in Pending.Where(p => DateTime.UtcNow - p.Value.Last().Sent > TimeSpan.FromMinutes(1)).Select(p => p.Key).ToArray())
            Pending.Remove(id);
        if (!Pending.TryGetValue(map.Id, out var queue)) Pending[map.Id] = queue = new();
        queue.Enqueue((map.SaveInternal(), DateTime.UtcNow));
    }

    internal static bool PreserveNewerEdits(MapInstance existing, MapInstance received)
    {
        if (!Pending.TryGetValue(existing.Id, out var queue)) return false;
        var pending = queue.Dequeue();
        if (queue.Count == 0) Pending.Remove(existing.Id);
        // The first echo replaces the MapInstance. Immutable snapshots identify later saves too.
        var local = existing.SaveInternal();
        if (local.Matches(pending.State)) return false;
        var revision = received.Revision;
        // received has already established its server baseline. Loading newer local changes keeps it dirty.
        received.LoadInternal(local, import: true);
        received.Revision = revision;
        return true;
    }
}
