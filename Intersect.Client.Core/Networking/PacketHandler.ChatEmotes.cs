using Intersect.Network;
using Intersect.Network.Packets.ChatEmotes;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, ChatEmoteMenuPacket packet)
    { if (Intersect.Client.Interface.Interface.HasInGameUI) Intersect.Client.Interface.Interface.GameUi.ApplyChatEmotes(packet); }
}
