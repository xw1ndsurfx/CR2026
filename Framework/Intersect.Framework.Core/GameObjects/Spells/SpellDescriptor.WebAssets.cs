using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.GameObjects;

public partial class SpellDescriptor
{
    [NotMapped]
    [JsonProperty]
    public string? ImageUrl =>
        string.IsNullOrWhiteSpace(Icon)
            ? null
            : $"/api/v1/game-assets/spells/{Id:D}";
}
