using Intersect.Network.Packets.Tutorials;
using Intersect.Server.Tutorials;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, TutorialRequestPacket packet)
    { if (!client.IsEditor) client.Entity?.HandleTutorial(packet); }
    public void HandlePacket(Client client, TutorialEditorPacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            if (packet.Save) TutorialRuntime.Save(packet.Json, packet.Revision);
            client.Send(new TutorialEditorPacket { Json = TutorialRuntime.Json, Revision = TutorialRuntime.Revision, Saved = packet.Save });
        }
        catch (Exception e) { client.Send(new TutorialEditorPacket { Error = e.Message }); }
    }
}
