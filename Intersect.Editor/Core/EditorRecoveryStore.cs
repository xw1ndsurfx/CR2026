using System.Security.Cryptography;
using System.Text;
using Intersect.Configuration;
using Intersect.Editor.Classes.Maps;
using Newtonsoft.Json;

namespace Intersect.Editor.Core;

// Local drafts never write to the game database. Keep three atomic generations per object.
internal sealed class EditorRecoveryStore(string directory)
{
    internal sealed class Draft
    {
        public int Version { get; set; } = 1;
        public string Kind { get; set; } = "map";
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime SavedUtc { get; set; }
        public MapSaveState? Map { get; set; }
        public string? EventJson { get; set; }
    }

    internal static string ServerDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IntersectEditor", "Recovery",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{ClientConfiguration.Instance.Host}:{ClientConfiguration.Instance.Port}"))));
    internal static EditorRecoveryStore Current => new(ServerDirectory);
    private const long MaximumBytes = 64L * 1024 * 1024;
    private string Pattern(string kind, Guid id) => $"{kind}-{id:N}-*.json";

    internal void Write(Draft draft)
    {
        if (draft.Id == Guid.Empty || draft.Kind is not ("map" or "event")) throw new InvalidDataException();
        Directory.CreateDirectory(directory);
        var latest = Read(draft.Kind, draft.Id);
        if (latest != null && JsonConvert.SerializeObject(latest.Map) == JsonConvert.SerializeObject(draft.Map) &&
            latest.EventJson == draft.EventJson) return;
        draft.SavedUtc = DateTime.UtcNow;
        var text = JsonConvert.SerializeObject(draft);
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new InvalidDataException("Recovery copy exceeds 64 MiB.");
        var path = Path.Combine(directory, $"{draft.Kind}-{draft.Id:N}-{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}.json");
        var temporary = path + ".tmp";
        try
        {
            // Flush to disk before publishing the generation; an interrupted write preserves older drafts.
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
            { writer.Write(text); writer.Flush(); file.Flush(true); }
            File.Move(temporary, path);
            foreach (var old in Directory.GetFiles(directory, Pattern(draft.Kind, draft.Id)).OrderDescending().Skip(3))
                File.Delete(old);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal Draft? Read(string kind, Guid id)
    {
        if (kind is not ("map" or "event") || !Directory.Exists(directory)) return null;
        foreach (var path in Directory.GetFiles(directory, Pattern(kind, id)).OrderDescending())
        {
            try
            {
                if (new FileInfo(path).Length > MaximumBytes) continue;
                var draft = JsonConvert.DeserializeObject<Draft>(File.ReadAllText(path));
                if (draft is { Version: 1 } && draft.Id == id && draft.Kind == kind &&
                    (kind == "map" ? draft.Map is { Metadata: not null, Tiles: not null, Attributes: not null, EventData: not null } : draft.EventJson != null))
                    return draft;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        }
        return null;
    }
}
