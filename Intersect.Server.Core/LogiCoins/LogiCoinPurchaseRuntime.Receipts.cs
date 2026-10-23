using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
namespace Intersect.Server.LogiCoins;
internal static partial class LogiCoinPurchaseRuntime
{
    private static Guid DeliveryReceiptId(Player player,LogiCoinPurchaseRecord record,int index)=>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"Logiklik.delivery:{record.PurchaseId:N}:{player.User.Id:N}:{index}" )).AsSpan(0,16));
    private static bool HasDeliveryReceipt(Player player,LogiCoinPurchaseRecord record,int index)=>
        player.User.Variables.Any(value=>value.VariableId==DeliveryReceiptId(player,record,index) && value.Value.Boolean);
    private static UserVariable AddDeliveryReceipt(Player player,LogiCoinPurchaseRecord record,int index)
    {
        // A private receipt is saved in the same account graph/DB transaction as
        // its inventory or Premium grant, before the external journal is marked.
        var receipt=new UserVariable(DeliveryReceiptId(player,record,index)){Value=new VariableValue{Boolean=true}};
        player.User.Variables.Add(receipt);return receipt;
    }
    private static bool CommitItemDelivery(Player player,LogiCoinPurchaseRecord record,int index,LogiCoinBundleItem entry,out string error)
    {
        error="";
        var inventory=Naval.NavalSystem.SnapshotCargoInventory(player);
        var bank=player.Bank.Select(item=>new Item(item)).ToArray();
        UserVariable? receipt=null;var committed=false;
        try
        {
            if(!player.TryGiveItem(entry.ItemId,entry.Quantity,ItemHandling.Normal,bankOverflow:true,sendUpdate:false))
            {error="No inventory or bank space for this delivery.";return false;}
            receipt=AddDeliveryReceipt(player,record,index);
            committed=player.User.Save(force:true)==UserSaveResult.Completed;
            if(!committed)error="The delivery could not be saved.";
            return committed;
        }
        finally
        {
            if(!committed)
            {
                for(var slot=0;slot<inventory.Length;slot++)player.Items[slot].Set(inventory[slot]);
                for(var slot=0;slot<bank.Length;slot++)player.Bank[slot].Set(bank[slot]);
                if(receipt!=null)player.User.Variables.Remove(receipt);
            }
            PacketSender.SendInventory(player);
        }
    }
}
