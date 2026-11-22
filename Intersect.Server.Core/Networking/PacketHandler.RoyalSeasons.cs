using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
using Intersect.Server.RoyalSeasons;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RoyalSeasonRequestPacket packet)
    { if (!client.IsEditor && client.Entity is {} player) RoyalSeasonRuntime.Handle(player, packet); }
    public void HandlePacket(Client client, RoyalSeasonConfigurationRequestPacket packet)
    { if (client.IsEditor) SendRoyalSeasons(client, true); }
    public void HandlePacket(Client client, RoyalSeasonConfigurationSavePacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            RoyalSeasonRuntime.Store.Save(packet.Json, packet.Revision, DateTimeOffset.UtcNow, RoyalSeasonRuntime.Validate);
            Localization.ContentTranslationRuntime.QueueJson(packet.Json);
            SendRoyalSeasons(client, false);
        }
        catch (Exception error)
        { SendRoyalSeasons(client, false, error is InvalidOperationException ? error.Message : "Unable to save the seasons. Please try again."); }
    }
    private static void SendRoyalSeasons(Client client, bool open, string error = "")
    {
        lock (RoyalSeasonRuntime.Store.Gate) client.Send(new RoyalSeasonConfigurationPacket
        { Json = RoyalSeasonRules.Json(RoyalSeasonRuntime.Store.Configuration), Revision = RoyalSeasonRuntime.Store.Revision, Open = open, Error = error,
            ExpeditionNames = Naval.NavalSystem.SeasonExpeditionNames() });
    }
}
