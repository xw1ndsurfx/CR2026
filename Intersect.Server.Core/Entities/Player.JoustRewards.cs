using System.Text.Json;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities;
public partial class Player
{
    private JoustMatch? _joustPendingReward;
    private int _joustRewardSide;
    private long _joustRewardRetry;
    private long _joustRedeemAt;
    private void SendJoustCareer() => SendPacket(new Intersect.Network.Packets.Server.JoustStatePacket {
        CareerJson = JsonSerializer.Serialize(PetCollection.Jousting ??= new()) });
    private void CompleteJoustCareer()
    {
        if (_joustPendingReward is not { } match || User is not { } user || Environment.TickCount64 < _joustRewardRetry) return;
        _joustRewardRetry = Environment.TickCount64 + 10000;
        lock (user.PokerSaveGate)
        {
            var before = PetCollection.Jousting ??= new();
            var next = JsonSerializer.Deserialize<JoustCareer>(JsonSerializer.Serialize(before))!;
            next.Complete(match, Intersect.Server.Jousting.JoustRuntime.Current.Economy, DateTimeOffset.UtcNow, _joustRewardSide);
            PetCollection.Jousting = next;
            try
            {
                if (user.Save(force: true) != UserSaveResult.Completed) { PetCollection.Jousting = before; return; }
            }
            catch { PetCollection.Jousting = before; throw; }
            _joustPendingReward = null; SendJoustCareer();
        }
    }
    private void RedeemJoustOffer(Guid id, Guid request)
    {
        var now = Environment.TickCount64;
        if (now - _joustRedeemAt < 1000 || IsJousting || User is not { } user) return;
        _joustRedeemAt = now;
        var economy = Intersect.Server.Jousting.JoustRuntime.Current.Economy;
        var offer = economy.Enabled ? economy.Offers.FirstOrDefault(o => o.Id == id) : null;
        if (offer == null || !offer.IsValid || ItemDescriptor.Get(offer.ItemId) is not { } item || !item.IsStackable || item.MaxInventoryStack < 1) return;
        lock (user.PokerSaveGate)
        {
            var career = PetCollection.Jousting ??= new();
            if (career.Medals < offer.Medals) return;
            var result = Intersect.Server.Jousting.JoustEconomyExchange.Redeem(career, offer, item, Items, request, () => user.Save(force: true) == UserSaveResult.Completed);
            if (result == Intersect.Server.Jousting.JoustExchangeResult.InventoryFull)
                SendPacket(new Intersect.Network.Packets.Server.JoustStatePacket { Message = "Make room in your inventory before exchanging medals." });
            if (result != Intersect.Server.Jousting.JoustExchangeResult.Success) return;
            PacketSender.SendInventory(this); SendJoustCareer(); UpdateGatherItemQuests(offer.ItemId);
        }
    }
}
