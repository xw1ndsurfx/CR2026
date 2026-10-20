using Intersect.Framework.Core.GameObjects.Animations;
using Newtonsoft.Json;
namespace Intersect.Configuration;

public sealed class MenuAnimationDefinition
{
    public Guid AnimationId
    {
        get; set;
    }
    public int XPercent { get; set; } = 50;
    public int YPercent { get; set; } = 50;
    public int ScalePercent { get; set; } = 100;
    public int TransparencyPercent
    {
        get; set;
    }
    public bool Loop { get; set; } = true;
    public bool Foreground
    {
        get; set;
    }
    // Snapshots let the login menu animate before any game-data/login packets.
    public AnimationLayer? Lower
    {
        get; set;
    }
    public AnimationLayer? Upper
    {
        get; set;
    }
}
public sealed partial class ClientConfiguration
{
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [JsonConverter(typeof(MenuAnimationListConverter))]
    public List<MenuAnimationDefinition> MenuAnimations { get; set; } = [];
}

/// <summary>Match the menu's existing 32-rule render limit while consuming old inflated files in bounded memory.</summary>
internal sealed class MenuAnimationListConverter : JsonConverter<List<MenuAnimationDefinition>>
{
    internal const int MaximumRules = 32;
    public override List<MenuAnimationDefinition> ReadJson(JsonReader reader, Type objectType,
        List<MenuAnimationDefinition>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return [];
        if (reader.TokenType != JsonToken.StartArray) throw new JsonSerializationException("MenuAnimations must be an array.");
        var result = new List<MenuAnimationDefinition>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndArray) return result;
            if (reader.TokenType == JsonToken.Comment || reader.TokenType == JsonToken.Null) continue;
            if (result.Count >= MaximumRules) { reader.Skip(); continue; }
            if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Invalid menu animation.");
            if (serializer.Deserialize<MenuAnimationDefinition>(reader) is { } rule) result.Add(rule);
        }
        throw new JsonSerializationException("Unterminated MenuAnimations array.");
    }
    public override void WriteJson(JsonWriter writer, List<MenuAnimationDefinition>? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var rule in (value ?? []).Take(MaximumRules)) serializer.Serialize(writer, rule);
        writer.WriteEndArray();
    }
}
