using Intersect.Client.General;
using Intersect.Client.Core;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Enums;
using Intersect.Framework.Core.Fishing;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private FishingWindow? _fishingWindow;
    private Guid _dismissedFishingSession;

    internal void ApplyFishingState(FishingStatePacket packet)
    {
        if (packet.Editor || Globals.Me == null)
        {
            if (packet.Open) ClientLoopDiagnostics.Note($"fishing-window ignored editor={packet.Editor}; playerPresent={Globals.Me != null}");
            return;
        }
        try
        {
            ApplyFishingSession(packet);
        }
        catch (Exception error)
        {
            FishingInterfaceFailure(error, packet.StateJson);
        }
        // A refusal is already visible before its optional audio is attempted.
        if (!string.IsNullOrWhiteSpace(packet.NoticeSoundJson))
        {
            try { FishingAudio.PlayNotice(packet.NoticeSoundJson); }
            catch (Exception error)
            {
                ClientLoopDiagnostics.Note($"fishing-notice audio-failed error={error.GetType().Name}");
                ApplicationContext.Context.Value?.Logger.LogWarning(error, "Fishing notice sound failed");
            }
        }
    }

    private void ApplyFishingSession(FishingStatePacket packet)
    {
        if (_fishingWindow == null && packet.StateJson.Length > 0)
        {
            try
            {
                if (FishingState.FromJson(packet.StateJson)?.SessionId == _dismissedFishingSession) return;
            }
            catch (JsonException error) { FishingInterfaceFailure(error); return; }
        }
        if (_fishingWindow == null && !packet.Open)
        {
            if (!string.IsNullOrWhiteSpace(packet.Message))
                ChatboxMsg.AddMessage(new ChatboxMsg(packet.Message, Color.Red, ChatMessageType.Error));
            return;
        }
        _fishingWindow ??= new FishingWindow(GameCanvas);
        _fishingWindow.Apply(packet, _dismissedFishingSession);
        if (packet.Open)
            ClientLoopDiagnostics.Note($"fishing-window displayed session={_fishingWindow.SessionId}; visible={_fishingWindow.IsVisibleInTree}; width={_fishingWindow.Width}; height={_fishingWindow.Height}");
    }

    private void UpdateFishing()
    {
        if (_fishingWindow == null) return;
        try
        {
            if (_fishingWindow.ExitRequested || Globals.Me == null) { CloseFishingWindow(); return; }
            _fishingWindow.Update();
        }
        catch (Exception error) { FishingInterfaceFailure(error); }
    }

    private bool CloseFishingWindow()
    {
        if (_fishingWindow == null) return false;
        try
        {
            ClientLoopDiagnostics.Note($"fishing-window closing session={_fishingWindow.SessionId}; exitRequested={_fishingWindow.ExitRequested}; playerPresent={Globals.Me != null}");
            if (!string.IsNullOrWhiteSpace(_fishingWindow.EndMessage))
                ChatboxMsg.AddMessage(new ChatboxMsg(_fishingWindow.EndMessage, Color.White, ChatMessageType.Notice));
            _dismissedFishingSession = _fishingWindow.SessionId;
            _fishingWindow.Leave();
            _fishingWindow.Destroy();
            _fishingWindow = null;
        }
        catch (Exception error) { FishingInterfaceFailure(error); }
        return true;
    }

    private void FishingInterfaceFailure(Exception error, string stateJson = "")
    {
        ClientLoopDiagnostics.Note($"fishing-window failed error={error.GetType().Name}; session={_fishingWindow?.SessionId}");
        ApplicationContext.Context.Value?.Logger.LogError(error, "Fishing interface could not be displayed");
        ChatboxMsg.AddMessage(new ChatboxMsg(FishingUiText.Get(
            "The fishing window could not be displayed. Try again or contact an administrator.", ClientLanguages.Current),
            Color.Red, ChatMessageType.Error));

        // A constructor can fail after attaching to the canvas but before assignment.
        // Remove that incomplete surface as well so it cannot keep gameplay input blocked.
        var windows = GameCanvas.Children.OfType<FishingWindow>().ToArray();
        var session = _fishingWindow?.SessionId ?? Guid.Empty;
        _fishingWindow = null;
        foreach (var window in windows)
        {
            if (window.SessionId != Guid.Empty) session = window.SessionId;
            try { window.Leave(); }
            catch (Exception cleanupError)
            { ApplicationContext.Context.Value?.Logger.LogWarning(cleanupError, "Fishing session cleanup failed"); }
            try { window.Destroy(playClose: false); }
            catch (Exception cleanupError)
            {
                Interface.InputBlockingComponents.Remove(window);
                window.Hide();
                window.Parent?.RemoveChild(window, false);
                ApplicationContext.Context.Value?.Logger.LogWarning(cleanupError, "Fishing window cleanup failed");
            }
        }
        if (session == Guid.Empty && !string.IsNullOrEmpty(stateJson))
        {
            try
            {
                session = FishingState.FromJson(stateJson)?.SessionId ?? Guid.Empty;
                if (session != Guid.Empty)
                    Networking.Network.SendPacket(new FishingRequestPacket { SessionId = session, Sequence = 1, Action = FishingAction.Leave });
            }
            catch (Exception cleanupError)
            { ApplicationContext.Context.Value?.Logger.LogWarning(cleanupError, "Fishing opening cancellation failed"); }
        }
        if (session != Guid.Empty) _dismissedFishingSession = session;
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
