using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestPetState(bool openWindow = false) =>
        Network.SendPacket(new RequestPetStatePacket(openWindow));

    public static void SendPetAction(PetActionKind action, Guid petId) =>
        Network.SendPacket(new PetActionPacket(action, petId));
}
