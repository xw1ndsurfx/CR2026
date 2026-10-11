using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestPetState(bool openWindow = false, Guid playerId = default) =>
        Network.SendPacket(new RequestPetStatePacket(openWindow, playerId));

    public static void SendPetAction(PetActionKind action, Guid petId, string name = "") =>
        Network.SendPacket(new PetActionPacket(action, petId, name));
}
