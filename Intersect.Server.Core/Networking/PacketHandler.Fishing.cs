using Intersect.Framework.Core.Fishing;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Fishing;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, FishingRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (FishingAction.GetConfiguration or FishingAction.SaveConfiguration)) return;
            try
            {
                if (packet.Action == FishingAction.SaveConfiguration) FishingRuntime.Save(packet.ConfigurationJson);
                client.Send(new FishingStatePacket
                {
                    Editor = true, Open = true, Saved = packet.Action == FishingAction.SaveConfiguration,
                    ConfigurationJson = FishingRuntime.Current.ToJson(),
                    Message = packet.Action == FishingAction.SaveConfiguration ? FishingText.Get("Fishing configuration saved.", client.LanguageCode ?? "en") : "",
                });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                client.Send(new FishingStatePacket { Editor = true,
                    Message = FishingText.Get(exception.Message == "Fishing configuration references a missing item, map, profession or animation."
                        ? exception.Message : "Invalid fishing configuration.", client.LanguageCode ?? "en") });
            }
            return;
        }
        // Configuration and Event authorization are never accepted from a game client.
        if (packet.Action is FishingAction.GetConfiguration or FishingAction.SaveConfiguration) return;
        if (client.Entity is { InGame: true } player) player.HandleFishingRequest(packet);
    }
}
