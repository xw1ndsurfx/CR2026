using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    // Stored with the inventory in the same native account SaveChanges transaction.
    [JsonIgnore, Column("NavalFleet")]
    public string NavalFleetJson
    {
        get => JsonConvert.SerializeObject(NavalFleet);
        set => NavalFleet = string.IsNullOrWhiteSpace(value) ? [] : JsonConvert.DeserializeObject<List<OwnedShip>>(value) ?? [];
    }
    [JsonIgnore, NotMapped] public List<OwnedShip> NavalFleet { get; set; } = [];
    [JsonIgnore, NotMapped] internal long LastHarbourRequest { get; set; }
}
