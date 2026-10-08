using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Lockpicking;

namespace Intersect.Server.Networking;

public partial class PacketHandler
{
    public void HandlePacket(Client client, LockpickingRequestPacket packet)
    {
        LockpickingRuntime.Handle(client, packet);
    }
}
