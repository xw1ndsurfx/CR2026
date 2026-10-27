using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Models;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.Crafting;

public partial class CraftingTableDescriptor : DatabaseObject<CraftingTableDescriptor>, IFolderable
{
    [NotMapped]
    public DbList<CraftingRecipeDescriptor> Crafts { get; set; } = [];

    [NotMapped]
    public bool CampfireCreator { get; set; }

    public IEnumerable<Guid> AvailableCrafts() => CampfireCreator
        ? Crafts.Concat(CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>()
            .Where(recipe => recipe.Campfire is { IsValid: true }).Select(recipe => recipe.Id)).Distinct()
        : Crafts;

    [JsonConstructor]
    public CraftingTableDescriptor(Guid id) : base(id)
    {
        Name = "New Table";
    }

    //Parameterless constructor for EF
    public CraftingTableDescriptor()
    {
        Name = "New Table";
    }

    [JsonIgnore]
    [Column("Crafts")]
    public string CraftsJson
    {
        get => CampfireCreator ? JsonConvert.SerializeObject(new { Crafts, CampfireCreator }, Formatting.None)
            : JsonConvert.SerializeObject(Crafts, Formatting.None);
        protected set
        {
            // Keep legacy list-only rows readable, using the existing column.
            var token = Newtonsoft.Json.Linq.JToken.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value);
            Crafts = (token is Newtonsoft.Json.Linq.JArray ? token : token["Crafts"])?.ToObject<DbList<CraftingRecipeDescriptor>>() ?? [];
            CampfireCreator = token is Newtonsoft.Json.Linq.JObject && token["CampfireCreator"]?.ToObject<bool>() == true;
        }
    }

    /// <inheritdoc />
    public string Folder { get; set; } = string.Empty;
}
