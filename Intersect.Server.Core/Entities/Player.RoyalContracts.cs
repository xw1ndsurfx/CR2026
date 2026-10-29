using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    internal long RoyalContractOpenUntil, RoyalContractRequestAt;
    internal Guid RoyalContractBoardMap;
    internal float RoyalContractBoardX, RoyalContractBoardY;

    internal void RefreshRoyalContractInventory(Guid itemId)
    {
        PacketSender.SendInventory(this);
        UpdateGatherItemQuests(itemId);
        UnequipInvalidItems();
    }
}
