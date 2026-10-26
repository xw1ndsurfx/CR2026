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
            catch (Exception) { client.Send(new JoustStatePacket { Editor = true, Message = "The jousting configuration is invalid.", RequestId = packet.RequestId }); }
            return;
        }
        if (client.Entity is { InGame: true } player) player.HandleJoustRequest(packet);
    }
}
