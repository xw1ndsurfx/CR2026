using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.Resources;

public partial class ResourceDescriptor
{
    /// <summary>
    /// Safe public image endpoint for the resource's primary visual state.
    /// </summary>
    [NotMapped]
    [JsonProperty]
    public string? ImageUrl =>
        States is { Count: > 0 }
            ? $"/api/v1/game-assets/resources/{Id:D}"
            : null;
}
