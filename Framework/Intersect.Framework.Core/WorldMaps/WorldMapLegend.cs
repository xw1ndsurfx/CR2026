using Newtonsoft.Json;

namespace Intersect.Framework.Core.WorldMaps;

public sealed class WorldMapLegend
{
    public List<WorldMapLegendEntry> Entries { get; set; } = [];

    public static WorldMapLegend FromJson(string json)
    {
        var legend = JsonConvert.DeserializeObject<WorldMapLegend>(json)
            ?? throw new InvalidDataException("World Map legend is empty.");
        if (legend.Entries == null || legend.Entries.Count > 100)
            throw new InvalidDataException("The legend can contain at most 100 entries.");
        foreach (var entry in legend.Entries)
        {
            if (entry == null || entry.AnimationId == Guid.Empty || string.IsNullOrWhiteSpace(entry.Label) || entry.Label.Length > 100)
                throw new InvalidDataException("Each legend entry needs an animation and a label (100 characters maximum).");
            entry.Label = entry.Label.Trim();
        }
        return legend;
    }

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

public sealed class WorldMapLegendEntry
{
    public Guid AnimationId { get; set; }
    public string Label { get; set; } = string.Empty;
}
