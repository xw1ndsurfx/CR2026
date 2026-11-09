using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
using Intersect.Server.Entities;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static bool CanAccessShip(Player player,HarbourOffer offer,bool premium)=>
        offer.CanAccess(premium,offer.RequiredQuestId!=Guid.Empty && player.QuestCompleted(offer.RequiredQuestId),player.NavalCareer.UnlockedShipModels.Contains(offer.Id));

    internal static bool RewardShipFromEvent(Player player,ShipRewardCommand command)
    {
        // This is called only by the server's event interpreter, never by a
        // client harbour action. A reward does not charge or launch the ship.
        lock(player.EntityLock)
        {
            if(!Enum.IsDefined(command.Action) || player.User==null || !player.InGame || string.IsNullOrWhiteSpace(command.ModelId))return false;
            HarbourCatalog catalog;
            try
            {
                var shop=ShopDescriptor.Get(command.ShopId);
                if(command.ShopId!=Guid.Empty && shop?.ConfigurationKind!=ShopConfigurationKind.NavalShipsAndSkills)return false;
                catalog=shop?.NavalCatalogue??HarbourCatalog.Load(CataloguePath);catalog.Validate();
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or ArgumentException){return false;}
            var offer=catalog.Offers.FirstOrDefault(o=>o.Id==command.ModelId);if(offer==null)return false;
            var owned=player.NavalFleet.Any(s=>s.ModelId==offer.Id);
            if(!player.NavalCareer.UnlockedShipModels.Contains(offer.Id) && player.NavalCareer.UnlockedShipModels.Count>=256)return false;
            if(command.Action==ShipRewardAction.GrantShip && !owned && player.NavalFleet.Count>=ShipFleetRules.MaximumShips)return false;
            if(player.NavalCareer.UnlockedShipModels.Contains(offer.Id) && (command.Action==ShipRewardAction.UnlockModel || owned))return true;
            var before=player.NavalFleetJson;var saved=false;
            try
            {
                player.NavalCareer.UnlockedShipModels.Add(offer.Id);
                if(command.Action==ShipRewardAction.GrantShip && !owned)player.NavalFleet.Add(ShipFleetRules.FromOffer(offer,command.ShopId));
                return saved=SaveManagement(player);
            }
            finally{if(!saved)player.NavalFleetJson=before;}
        }
    }
}
