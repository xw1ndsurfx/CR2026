using Intersect.Client.General;
using Intersect.Network.Packets.Server;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Enums;
using Intersect.Framework.Core.Fishing;
using System.Text.Json;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private FishingWindow? _fishingWindow;
    private Guid _dismissedFishingSession;

    internal void ApplyFishingState(FishingStatePacket packet)
    {
        if (packet.Editor || Globals.Me == null) return;
        if (!string.IsNullOrWhiteSpace(packet.NoticeSoundJson)) FishingAudio.PlayNotice(packet.NoticeSoundJson);
        if (_fishingWindow == null && packet.StateJson.Length > 0)
        {
            try
            {
                if (FishingState.FromJson(packet.StateJson)?.SessionId == _dismissedFishingSession) return;
            }
            catch (JsonException) { return; }
        }
        if (_fishingWindow == null && !packet.Open)
        {
            if (!string.IsNullOrWhiteSpace(packet.Message))
                ChatboxMsg.AddMessage(new ChatboxMsg(packet.Message, Color.Red, ChatMessageType.Error));
            return;
        }
        _fishingWindow ??= new FishingWindow(GameCanvas);
        _fishingWindow.Apply(packet, _dismissedFishingSession);
    }

    private void UpdateFishing()
    {
        if (_fishingWindow == null) return;
        if (_fishingWindow.ExitRequested || Globals.Me == null) { CloseFishingWindow(); return; }
        _fishingWindow.Update();
    }

    private bool CloseFishingWindow()
    {
        if (_fishingWindow == null) return false;
        if (!string.IsNullOrWhiteSpace(_fishingWindow.EndMessage))
            ChatboxMsg.AddMessage(new ChatboxMsg(_fishingWindow.EndMessage, Color.White, ChatMessageType.Notice));
        _dismissedFishingSession = _fishingWindow.SessionId;
        _fishingWindow.Leave();
        _fishingWindow.Destroy();
        _fishingWindow = null;
        return true;
    }

    private void DisposeFishing()
    {
        // The canvas owns its children during logout; detach once before its normal disposal.
        _fishingWindow?.Destroy(playClose: false);
        _fishingWindow = null;
        FishingWorldPresentation.Clear();
        FishingAudio.ClearNotices();
    }
}
