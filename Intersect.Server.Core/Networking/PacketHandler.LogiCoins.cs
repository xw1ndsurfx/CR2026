using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.LogiCoins;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestLogiCoinShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
        {
            return;
        }

        client.Send(
            new LogiCoinShopConfigurationPacket(
                LogiCoinShopRuntime.Json,
                openEditor: true
            )
        );
    }

    public void HandlePacket(Client client, SaveLogiCoinShopConfigurationPacket packet)
    {
        if (!client.IsEditor)
        {
            return;
        }

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
            {
                throw new InvalidDataException("LogiCoin Shop configuration is empty or too large.");
            }

            LogiCoinShopRuntime.Save(packet.ConfigurationJson);
            client.Send(
                new LogiCoinShopConfigurationPacket(
                    LogiCoinShopRuntime.Json,
                    openEditor: false
                )
            );
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "LogiCoin Shop");
        }
    }

    public void HandlePacket(Client client, RequestLogiCoinShopStatePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock(player.EntityLock)
        {
            try{client.Send(LogiCoinPurchaseRuntime.BuildState(player));}
            catch(Exception){PacketSender.SendError(client,Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("The LogiCoin Shop is currently unavailable.",client.LanguageCode),"LogiCoin Shop");}
        }
    }

    public void HandlePacket(Client client,LogiCoinRecipientRequestPacket packet)
    {
        if(client.IsEditor || client.Entity is not {} player || packet.Account is not {Length:>0 and <=64})return;
        if(Intersect.Framework.Core.Timing.Global.Milliseconds-player.ServiceShopRequestAt<1000)return;
        player.ServiceShopRequestAt=Intersect.Framework.Core.Timing.Global.Milliseconds;
        var user=Intersect.Server.Database.PlayerData.User.Find(packet.Account.Trim());
        var response=new LogiCoinRecipientPacket();
        if(user!=null)
        {
            using var db=Intersect.Server.Database.DbInterface.CreatePlayerContext(readOnly:true);
            response.UserId=user.Id;response.Account=user.Name;
            response.Characters=db.Players.Where(character=>character.UserId==user.Id).OrderBy(character=>character.Name).Select(character=>new LogiCoinRecipientCharacter{Id=character.Id,Name=character.Name}).Take(1000).ToArray();
        }
        client.Send(response);
    }
    public void HandlePacket(Client client, PurchaseLogiCoinOfferPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            try{client.Send(
                LogiCoinPurchaseRuntime.Purchase(
                    player,
                    packet.OfferId,
                    packet.PurchaseId,packet.RecipientUserId,packet.RecipientCharacterId
                )
            );}
            catch(Exception){PacketSender.SendError(client,Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("The LogiCoin Shop is currently unavailable.",client.LanguageCode),"LogiCoin Shop");}
        }
    }
}
