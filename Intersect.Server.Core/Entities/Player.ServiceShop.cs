using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    [NotMapped,JsonIgnore] internal Guid ServiceShopMap;
    [NotMapped,JsonIgnore] internal int ServiceShopX,ServiceShopY;
    [NotMapped,JsonIgnore] internal long ServiceShopRequestAt;
    [NotMapped,JsonIgnore] internal long IncomingGiftAt;
    [NotMapped,JsonIgnore] internal long FriendTeleportAt;
    [NotMapped,JsonIgnore] internal DateTime CampfireSyncAt;
    [NotMapped,JsonIgnore] internal string? CampfireSignature;
}
