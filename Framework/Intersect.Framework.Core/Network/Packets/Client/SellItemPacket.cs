using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public class SellItemPacket : SlotQuantityPacket
{
    [Key(3)] public bool AllCopies { get; set; }
    //Parameterless Constructor for MessagePack
    public SellItemPacket() : base(0, 0)
    {
    }

    public SellItemPacket(int slot, int quantity) : base(slot, quantity)
    {
    }
}
