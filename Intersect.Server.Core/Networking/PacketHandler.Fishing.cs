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
            if (packet.Action is not (FishingAction.GetConfiguration or FishingAction.SaveConfiguration or
                FishingAction.GetDraftConfiguration or FishingAction.SaveDraftConfiguration)) return;
            var draft = packet.Action is FishingAction.GetDraftConfiguration or FishingAction.SaveDraftConfiguration;
            var save = packet.Action is FishingAction.SaveConfiguration or FishingAction.SaveDraftConfiguration;
            try
            {
                var json = packet.Action switch
                {
                    FishingAction.SaveConfiguration => FishingRuntime.Save(packet.ConfigurationJson),
                    FishingAction.SaveDraftConfiguration => FishingRuntime.SaveDraft(packet.ConfigurationJson),
                    FishingAction.GetDraftConfiguration => FishingRuntime.ReadDraft().ToJson(),
                    _ => FishingRuntime.Current.ToJson(),
                };
                client.Send(new FishingStatePacket
                {
                    Editor = true, Open = true, Draft = draft, Saved = save,
                    ConfigurationJson = json,
                    Message = save ? FishingText.Get(draft ? "Fishing draft saved." : "Fishing configuration saved.", client.LanguageCode ?? "en") : "",
                });
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                var message = exception.Message is FishingRuntime.InvalidDraftMessage or
                    "Fishing configuration references a missing item, map, profession or animation."
                    ? exception.Message
                    : exception is InvalidDataException or System.Text.Json.JsonException or ArgumentException
                        ? "Invalid fishing configuration."
                        : save ? "Fishing configuration could not be saved. The active configuration was not changed."
                            : "Fishing configuration could not be read. Existing files were preserved.";
                client.Send(new FishingStatePacket { Editor = true, Draft = draft,
                    Message = FishingText.Get(message, client.LanguageCode ?? "en") });
            }
            return;
        }
        // Configuration and Event authorization are never accepted from a game client.
        if (packet.Action is FishingAction.GetConfiguration or FishingAction.SaveConfiguration or
            FishingAction.GetDraftConfiguration or FishingAction.SaveDraftConfiguration) return;
        if (client.Entity is { InGame: true } player) player.HandleFishingRequest(packet);
    }
}
