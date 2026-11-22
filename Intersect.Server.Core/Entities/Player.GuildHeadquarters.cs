using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.GuildHeadquarters;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    [JsonIgnore, Column("GuildHeadquartersVisit")]
    public string? GuildHeadquartersVisitJson { get; set; }
    [JsonIgnore, NotMapped] internal long HeadquartersNextRequest { get; set; }
    [JsonIgnore, NotMapped] internal long HeadquartersNextUpdate { get; set; }
    [JsonIgnore, NotMapped] internal HeadquartersVisit? HeadquartersVisit
    {
        get => string.IsNullOrWhiteSpace(GuildHeadquartersVisitJson) ? null : GuildHeadquartersVisitJson.Length > 2048 ?
            throw new InvalidDataException("Invalid headquarters return location.") :
            JsonConvert.DeserializeObject<HeadquartersVisit>(GuildHeadquartersVisitJson, new JsonSerializerSettings { MaxDepth = 8, TypeNameHandling = TypeNameHandling.None });
        set => GuildHeadquartersVisitJson = value == null ? null : JsonConvert.SerializeObject(value);
    }
}
