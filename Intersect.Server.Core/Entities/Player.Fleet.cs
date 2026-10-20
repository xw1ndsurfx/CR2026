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
        get => JsonConvert.SerializeObject(new NavalPlayerData {Ships=NavalFleet,SailingExperience=SailingExperience,PendingCharacterExperience=PendingNavalCharacterExperience,AdditionalData=_navalExtra});
        set {var data=NavalPlayerData.Parse(value);NavalFleet=data.Ships;SailingExperience=data.SailingExperience;PendingNavalCharacterExperience=data.PendingCharacterExperience;_navalExtra=data.AdditionalData;}
    }
    private IDictionary<string,Newtonsoft.Json.Linq.JToken>? _navalExtra;
    [JsonIgnore, NotMapped] internal List<(NavalRewardReceipt Receipt,long Born)> NavalRewardNotices {get;set;}=[];
    [JsonIgnore, NotMapped] public long PendingNavalCharacterExperience {get;set;}
    [JsonIgnore, NotMapped] internal bool NavalCharacterRewardDirty {get;set;}
    [JsonIgnore, NotMapped] internal long NavalCharacterRewardRetryAt {get;set;}
    internal void RefreshNavalCargoQuests(IEnumerable<Guid> items) {foreach(var id in items.Distinct()) UpdateGatherItemQuests(id);}
    [JsonIgnore, NotMapped] public long SailingExperience {get;set;}
    [JsonIgnore, NotMapped] public List<OwnedShip> NavalFleet {get;set;}=[];
    [JsonIgnore, NotMapped] internal Guid NavalShopMap { get; set; }
    [JsonIgnore, NotMapped] internal int NavalShopX { get; set; }
    [JsonIgnore, NotMapped] internal int NavalShopY { get; set; }
    [JsonIgnore, NotMapped] internal long NavalShopExpires { get; set; }
    [JsonIgnore, NotMapped] internal long LastHarbourRequest { get; set; }
}
