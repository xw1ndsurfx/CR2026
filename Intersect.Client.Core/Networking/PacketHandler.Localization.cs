using Intersect.Client.Localization;
using Intersect.Enums;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(GameObjectType Type, Guid Id), string> _originalContent = new();
    public void HandlePacket(IPacketSender sender, ContentTranslationsPacket packet)
    {
        if (packet.Replace) _originalContent.Clear();
        ClientLanguages.MergeContent(packet.CatalogJson, packet.Replace);
        foreach (var ((type, id), data) in _originalContent.ToArray())
            HandlePacket(sender, new GameObjectPacket(id, type, data, false, true));
    }
}
