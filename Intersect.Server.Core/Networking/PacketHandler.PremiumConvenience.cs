using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.RoyalStylist;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,PremiumConvenienceRequestPacket packet)
    {
        if(client.IsEditor || client.Entity is not {InGame:true,User:not null} player || !Enum.IsDefined(packet.Action) || packet.Name is not {Length:<=64})return;
        lock(player.EntityLock)
        {
            var now=Environment.TickCount64;if(now-player.LastConvenienceRequest<250)return;player.LastConvenienceRequest=now;
            var premium=LogiCoinPurchaseRuntime.HasActivePremium(player);
            var message=PremiumConvenienceTransactions.Apply(player,packet,premium,()=>player.User.Save(force:true)==UserSaveResult.Completed);
            if(message=="Saved.")
            {
                PacketSender.SendEntityDataToProximity(player);
                if(player.ActivePet is {} activePet)PacketSender.SendEntityDataToProximity(activePet);
            }
            client.Send(new PremiumConvenienceStatePacket {OpenWindow=packet.OpenWindow,Message=message,Json=Newtonsoft.Json.JsonConvert.SerializeObject(new PremiumConvenienceView {
                Premium=premium,Profile=player.PetCollection.Convenience,Decorations=RoyalStylistRuntime.Current.PetDecorations,
                Pets=player.PetCollection.OwnedPets.Select(pair=>new ConveniencePet {Id=pair.Key,Name=NPCDescriptor.Get(pair.Key) is {} descriptor?player.GetPetDisplayName(descriptor):"",DecorationId=pair.Value.DecorationId}).ToArray()})});
        }
    }
}
