using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.GameObjects;

public partial class SpellDescriptor
{
    /// <summary>
    /// Safe public image endpoint for this spell's configured icon.
    /// </summary>
    [NotMapped]
    [JsonProperty]
    public string? ImageUrl =>
        string.IsNullOrWhiteSpace(Icon)
            ? null
            : $"/api/v1/game-assets/spells/{Id:D}";
}
