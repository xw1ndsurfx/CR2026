using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.Items;

public partial class ItemDescriptor
{
    [NotMapped]
    [JsonProperty]
    public string? ImageUrl =>
        string.IsNullOrWhiteSpace(Icon)
            ? null
            : $"/api/v1/game-assets/items/{Id:D}";
}
