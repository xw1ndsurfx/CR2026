using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public class BuyItemPacket : SlotQuantityPacket
{
    [Key(3)]
    public int? ExpectedUnitPrice { get; set; }
    //Parameterless Constructor for MessagePack
    public BuyItemPacket() : base(0, 0)
    {
    }

    public BuyItemPacket(int slot, int quantity) : base(slot, quantity)
    {
    }
}
