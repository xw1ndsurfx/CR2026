using Intersect.Network.Packets.Client;
using Intersect.Server.Rewards;

namespace Intersect.Server.Networking;

public partial class PacketHandler
{
    public void HandlePacket(Client client, RequestDailyRewardStatePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player) return;
        client.Send(DailyRewardRuntime.State(player));
    }

    public void HandlePacket(Client client, ClaimDailyRewardPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player) return;
        lock (player.EntityLock)
        {
            client.Send(DailyRewardRuntime.Claim(player));
        }
    }
}
