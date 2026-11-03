using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, FishingStatePacket packet)
    {
        if (packet.Open || !string.IsNullOrEmpty(packet.Message) || !string.IsNullOrEmpty(packet.NoticeSoundJson))
            Core.ClientLoopDiagnostics.Note($"fishing-packet received open={packet.Open}; editor={packet.Editor}; stateBytes={packet.StateJson.Length}; configurationBytes={packet.ConfigurationJson.Length}; message={packet.Message.Length > 0}; notice={packet.NoticeSoundJson.Length > 0}");
        if (!packet.Editor) Interface.Interface.EnqueueInGame(game => game.ApplyFishingState(packet));
    }

    public void HandlePacket(IPacketSender sender, FishingWorldPacket packet) =>
        Interface.Interface.EnqueueInGame(_ => Interface.Game.FishingWorldPresentation.Apply(packet));
}
