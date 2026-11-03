using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Jousting;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, JoustRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (JoustAction.GetConfiguration or JoustAction.SaveConfiguration or JoustAction.PrepareWilliam)) return;
            try
            {
                if (packet.Action == JoustAction.PrepareWilliam)
                {
                    var mounts = Intersect.Framework.Core.Mounts.MountConfiguration.FromJson(Intersect.Server.Mounts.MountRuntime.Current.ToJson());
                    mounts.Enabled = true;
                    if (!mounts.Mounts.Any(m => m.Id == Intersect.Framework.Core.Jousting.JoustMountPreset.WilliamId))
                        mounts.Mounts = [..mounts.Mounts, Intersect.Framework.Core.Jousting.JoustMountPreset.William()];
                    Intersect.Server.Mounts.MountRuntime.Save(mounts.ToJson());
                }
                if (packet.Action == JoustAction.SaveConfiguration) JoustRuntime.Save(packet.ConfigurationJson);
                client.Send(new JoustStatePacket { Editor = true, Open = true, Saved = packet.Action == JoustAction.SaveConfiguration,
                    ConfigurationJson = JoustRuntime.Current.ToJson(), RequestId = packet.RequestId,
                    MountsJson = Intersect.Server.Mounts.MountRuntime.Current.ToJson() });
            }
            catch (Exception error)
            {
                var message = error is InvalidDataException ? error.Message switch {
                    "joust-william-required" => "William is required. Click Add William preset, then save the arena.",
                    "joust-crowd-resource" => "The selected crowd NPC is missing or has no sprite. Choose another NPC or None.",
                    "joust-seat-resources" => "A seat or entrance is outside the map or has a forbidden map attribute. Check the green bench and white entrance markers.",
                    "joust-event-resource" => "A selected cinematic is missing or is not on the arena map. Choose an existing scene or None.",
                    "joust-reward-item" => "A reward item is missing or is not stackable. Fix the offers in Rewards.",
                    "joust-arena-resources" => "The arena map or William mount is missing, or a track is outside the map. Check Arena setup.",
                    _ => "The jousting configuration is invalid. Check the arena rules and the seats listed below." } : "The server could not save this arena.";
                client.Send(new JoustStatePacket { Editor = true, Message = message, RequestId = packet.RequestId });
            }
            return;
        }
        if (client.Entity is { InGame: true } player) player.HandleJoustRequest(packet);
    }
}
