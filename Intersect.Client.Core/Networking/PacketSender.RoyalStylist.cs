using Intersect.Framework.Core;
using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendApplyRoyalStylistAppearance(
        Guid stylistId,
        CharacterAppearance appearance
    )
    {
        Network.SendPacket(
            new ApplyRoyalStylistAppearancePacket(
                stylistId,
                appearance
            )
        );
    }
}
