using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Network.Packets.ChatEmotes;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, ChatEmoteRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { InGame: true, IsDead: false } player) return;
        if (packet.EventId != Guid.Empty)
        {
            if (EventDescriptor.Get(packet.EventId) is { IsChatEmote: true } descriptor) player.TryRunMenuEvent(descriptor);
            return;
        }
        client.Send(new ChatEmoteMenuPacket { Entries = EventDescriptor.Lookup.Values.OfType<EventDescriptor>()
            .Where(e => e.IsChatEmote).OrderBy(e => e.Name).Take(500).Select(e => new ChatEmoteEntry { Id = e.Id, Name = e.Name }).ToArray() });
    }
}
