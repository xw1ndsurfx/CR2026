using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.MountRacing;
using Intersect.Server.Maps;
using Intersect.Server.Mounts;
using Intersect.Enums;

namespace Intersect.Server.MountRacing;

internal static class MountRaceStore
{
    private static readonly object Gate = new();
    private static MountRaceConfiguration? _config;
    internal static MountRaceConfiguration Configuration { get { lock (Gate) return _config ??= Load(); } }
    internal static readonly RaceJournal Journal = new("resources/mount-race-results.json");
    private static readonly Dictionary<Guid, (long Expires, bool Valid)> WorldChecks = [];
    internal static bool AvailableWorld(MountRaceCourse course)
    {
        lock (Gate)
        {
            var now = Environment.TickCount64;
            if (WorldChecks.TryGetValue(course.Id, out var cached) && now < cached.Expires) return cached.Valid;
            var valid = ValidWorld(course);
            if (WorldChecks.Count > 256) WorldChecks.Clear();
            WorldChecks[course.Id] = (now + 2000, valid);
            return valid;
        }
    }
    private static MountRaceConfiguration Load()
    {
        try { return File.Exists("resources/mount-races.json") ? MountRaceConfiguration.FromJson(File.ReadAllText("resources/mount-races.json")) : new(); }
        catch { return new() { Enabled = false }; }
    }
    internal static void Save(string json)
    {
        var config = MountRaceConfiguration.FromJson(json);
        foreach (var c in config.Courses.Where(c => c.Enabled))
            if (!ValidWorld(c)) throw new InvalidDataException("Choose a safe circuit, registration point, mount and valid prizes.");
        lock (Gate) { AtomicWrite("resources/mount-races.json", config.ToJson()); _config = config; WorldChecks.Clear(); }
    }
    internal static void AtomicWrite(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { var bytes = Encoding.UTF8.GetBytes(json); stream.Write(bytes); stream.Flush(true); }
        File.Move(temp, path, true);
    }
    internal static bool TrackTile(MapController? map, int x, int y) => map != null && x >= 0 && y >= 0 &&
        x < Options.Instance.Map.MapWidth && y < Options.Instance.Map.MapHeight &&
        (map.Attributes[x, y] == null || map.Attributes[x, y].Type == MapAttributeType.Walkable);
    internal static bool ValidWorld(MountRaceCourse c)
    {
        var map = MapController.Get(c.MapId);
        if (!c.IsValid || map == null || map.Spawns.Count > 0 || map.LocalEvents.Count > 0 ||
            MountRuntime.Find(c.MountId) is not { IntegratedRider: false } mount || map.IsIndoors && !mount.AllowIndoors ||
            !TrackTile(map, c.StartX, c.StartY) || !TrackTile(MapController.Get(c.RegistrationMapId), c.RegistrationX, c.RegistrationY) ||
            c.Prizes.Any(p => ItemDescriptor.Get(p.ItemId) is not { IsStackable: true, MaxInventoryStack: > 0 })) return false;
        var visited = new HashSet<(int X, int Y)> { (c.StartX, c.StartY) };
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue((c.StartX, c.StartY));
        while (queue.TryDequeue(out var p))
            foreach (var next in new[] { (p.X + 1, p.Y), (p.X - 1, p.Y), (p.X, p.Y + 1), (p.X, p.Y - 1) })
                if (TrackTile(map, next.Item1, next.Item2) && visited.Add(next)) queue.Enqueue(next);
        return c.Checkpoints.All(p => TrackTile(map, p.X, p.Y) && visited.Contains((p.X, p.Y)));
    }
    internal static string Revision(MountRaceCourse c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { c.MapId, c.StartX, c.StartY, c.Checkpoints, c.Laps, c.MovementMilliseconds, c.MountId }))));
}

internal sealed class RaceJournal(string path)
{
    private readonly object _gate = new();
    private List<RaceRecord>? _records;
    internal Action<string, string> Write { get; set; } = MountRaceStore.AtomicWrite;
    private List<RaceRecord> Records => _records ??= File.Exists(path)
        ? JsonSerializer.Deserialize<List<RaceRecord>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid race journal.") : [];
    internal RaceRecord[] Snapshot() { lock (_gate) return JsonSerializer.Deserialize<RaceRecord[]>(JsonSerializer.Serialize(Records))!; }
    internal void EnsureAvailable() { lock (_gate) { if (Records.Count >= 50000) throw new IOException("Race journal capacity reached."); } }
    internal void Add(RaceRecord record, int cooldownSeconds)
    {
        lock (_gate)
        {
            if (Records.Any(r => r.RaceId == record.RaceId && r.PlayerId == record.PlayerId)) return;
            var next = Snapshot().ToList();
            if (record.Practice || next.Any(r => r.PlayerId == record.PlayerId && r.CourseId == record.CourseId && r.Quantity > 0 &&
                record.FinishedUtc - r.FinishedUtc < cooldownSeconds * 1000L)) { record.ItemId = Guid.Empty; record.Quantity = 0; }
            if (record.Quantity == 0) record.Claimed = true;
            // Keep personal bests and every unclaimed reward; compact only historical, settled rows.
            if (next.Count >= 20000)
            {
                var keep = next.Where(r => r.ElapsedMilliseconds > 0).GroupBy(r => (r.CourseId, r.Revision, r.PlayerId, r.Practice))
                    .Select(g => g.MinBy(r => r.ElapsedMilliseconds)!.Id).ToHashSet();
                var cutoff = record.FinishedUtc - 86400000L;
                next.RemoveAll(r => r.Claimed && r.FinishedUtc < cutoff && !keep.Contains(r.Id));
            }
            if (next.Count >= 50000) throw new IOException("Race journal capacity reached.");
            next.Add(record); Write(path, JsonSerializer.Serialize(next)); _records = next;
        }
    }
    internal void Acknowledge(Guid id, Guid player)
    {
        lock (_gate)
        {
            var next = Snapshot().ToList(); var row = next.FirstOrDefault(r => r.Id == id && r.PlayerId == player);
            if (row == null || row.Claimed) return;
            row.Claimed = true; Write(path, JsonSerializer.Serialize(next)); _records = next;
        }
    }
}
