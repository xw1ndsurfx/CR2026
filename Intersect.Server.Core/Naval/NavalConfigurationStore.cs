using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Server.Naval;

internal sealed class NavalConfiguration
{
    public bool Enabled { get; set; }
    public ShipDefinition[] Ships { get; set; } = [];
    [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; }
    internal string SourceJson { get; set; }
}

/// <summary>Append-only editor setup. Invalid data is never replaced with a blank configuration.</summary>
internal sealed class NavalConfigurationStore(string path)
{
    private readonly object _gate = new();
    internal NavalConfiguration Load()
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException();
        var source = File.ReadAllText(path);
        var settings = JsonConvert.DeserializeObject<NavalConfiguration>(source) ?? throw new InvalidDataException();
        var rawShips = JObject.Parse(source).Properties().FirstOrDefault(p => p.Name.Equals("Ships", StringComparison.OrdinalIgnoreCase))?.Value as JArray;
        if (rawShips != null && rawShips.Any(item => item is not JObject obj ||
            !Guid.TryParse(obj.Properties().FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))?.Value.ToString(), out var id) || id == Guid.Empty))
            throw new InvalidDataException();
        if (settings.Ships == null || settings.Ships.Length > 64 ||
            settings.Ships.Any(d => d == null || !d.IsValid() || d.X >= Options.Instance.Map.MapWidth ||
                d.Y >= Options.Instance.Map.MapHeight || d.DockX >= Options.Instance.Map.MapWidth || d.DockY >= Options.Instance.Map.MapHeight) ||
            settings.Ships.Select(d => d.Id).Distinct().Count() != settings.Ships.Length) throw new InvalidDataException();
        settings.SourceJson = source;
        return settings;
    }

    internal (NavalConfiguration Configuration, ShipDefinition Ship, bool Created) Prepare(Guid mapId,
        Func<IReadOnlyList<ShipDefinition>, ShipDefinition> create)
    {
        lock (_gate)
        {
            var configuration = Load();
            var existing = configuration.Ships.FirstOrDefault(s => s.MapId == mapId);
            if (existing != null && configuration.Enabled) return (configuration, existing, false);
            if (existing == null && configuration.Ships.Length >= 64) throw new ShipLimitException();
            var ship = existing ?? create(configuration.Ships);
            if (ship == null) throw new NoShipPlacementException();
            if (ship.MapId != mapId || !ship.IsValid()) throw new InvalidDataException();
            if (existing == null) configuration.Ships = [.. configuration.Ships, ship];
            configuration.Enabled = true;
            Save(configuration);
            return (configuration, ship, existing == null);
        }
    }

    private void Save(NavalConfiguration configuration)
    {
        // Keep unknown ship/root fields, and reject edits made while placement was being calculated.
        if (File.Exists(path) ? configuration.SourceJson == null || File.ReadAllText(path) != configuration.SourceJson : configuration.SourceJson != null)
            throw new IOException("Naval configuration changed during setup.");
        var document = configuration.SourceJson == null ? new JObject() : JObject.Parse(configuration.SourceJson);
        var shipKey = document.Properties().FirstOrDefault(p => p.Name.Equals("Ships", StringComparison.OrdinalIgnoreCase))?.Name ?? "Ships";
        var enabledKey = document.Properties().FirstOrDefault(p => p.Name.Equals("Enabled", StringComparison.OrdinalIgnoreCase))?.Name ?? "Enabled";
        var previous = document[shipKey] as JArray ?? new JArray();
        document[enabledKey] = configuration.Enabled;
        document[shipKey] = new JArray(configuration.Ships.Select(ship =>
            previous.OfType<JObject>().FirstOrDefault(item => Guid.TryParse(item.Properties()
                .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))?.Value.ToString(), out var id) && id == ship.Id)?.DeepClone()
                ?? JObject.FromObject(ship)));
        var json = document.ToString(Formatting.Indented);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 1_000_000) throw new InvalidDataException();
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".naval-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, json);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal sealed class NoShipPlacementException : Exception { }
internal sealed class ShipLimitException : Exception { }
