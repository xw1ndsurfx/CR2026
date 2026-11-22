using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
namespace Intersect.Server.Database.PlayerData.Players;
public partial class Guild
{
    [JsonIgnore, Column("Headquarters")]
    public string? HeadquartersJson { get; set; }
}
