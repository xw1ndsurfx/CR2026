using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Exportation;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, ExportationRequestPacket packet)
    {
        if (!Enum.IsDefined(packet.Action)) return;
        if (client.IsEditor)
        {
            if (packet.Action is not (ExportationAction.GetConfiguration or ExportationAction.SaveConfiguration)) return;
            var save = packet.Action == ExportationAction.SaveConfiguration;
            try
            {
                var json = save ? ExportationRuntime.Save(packet.ConfigurationJson) : ExportationRuntime.Current.ToJson();
                client.Send(new ExportationStatePacket
                {
                    Editor = true, Open = !save, Saved = save, ConfigurationJson = json,
                    Message = save ? ExportationText.Get("Exportation routes saved.", client.LanguageCode ?? "en") : "",
                });
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException or ArgumentException)
            {
                client.Send(new ExportationStatePacket
                {
                    Editor = true,
                    Message = ExportationText.Get("Exportation routes were not saved. Check the route IDs and references.",
                        client.LanguageCode ?? "en"),
                });
            }
            return;
        }
        // A game client must never read or publish editor configuration.
        if (packet.Action is ExportationAction.GetConfiguration or ExportationAction.SaveConfiguration) return;

        if (packet.ConfigurationJson.Length != 0) return;
        if (client.Entity is { InGame: true } player)
            player.HandleExportationRequest(packet);
    }
}
