using Intersect.Client.General;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, WorldMapStatePacket packet)
    {
        var legend = WorldMapLegend.FromJson(packet.LegendJson);
        Globals.WorldMapSocialMarkers = packet.Players ?? [];
        Globals.WorldMapLegend = legend;
        Globals.WorldMapStateReceivedAt = Environment.TickCount64;
    }
}
