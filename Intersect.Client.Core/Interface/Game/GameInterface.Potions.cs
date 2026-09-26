using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _potionInboxLock = new();
    private readonly Queue<PotionStatePacket> _potionInbox = new();
    private readonly PotionClientModel _potionModel = new();
    private PotionWindow? _potionWindow;
    private bool _potionDisposed;

    // Legacy local-open packet is kept compatible while older event data rolls forward.
    internal void OpenPotionMiniGame(PotionMiniGamePacket packet)
    {
        if (!packet.IsValid) return;
    }

    internal void QueuePotionState(PotionStatePacket packet)
    {
        lock (_potionInboxLock)
        {
            if (_potionDisposed) return;
            while (_potionInbox.Count >= 32) _potionInbox.Dequeue();
            _potionInbox.Enqueue(packet);
        }
    }

    private void UpdatePotions()
    {
        PotionStatePacket[] packets;
        lock (_potionInboxLock)
        {
            packets = _potionInbox.ToArray();
            _potionInbox.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var packet in packets)
            _potionModel.Apply(packet, Globals.Me?.Id ?? Guid.Empty, now);

        if (_potionModel.Current == null)
        {
            _potionWindow?.Destroy();
            _potionWindow = null;
            return;
        }

        if (_potionWindow == null)
        {
            GameMenu?.HideWindows();
            _potionWindow = new PotionWindow(GameCanvas, SendPotionRequest);
            _potionWindow.BringToFront();
        }

        if (_potionWindow.ExitRequested)
        {
            ClosePotionWindow();
            return;
        }

        if (_potionModel.NeedsRefresh(now))
            SendPotionRequest(PotionRequestKind.Refresh, 0, Guid.Empty);

        _potionWindow.Update(_potionModel);
    }

    private void SendPotionRequest(PotionRequestKind kind, int column, Guid recipeId)
    {
        var packet = _potionModel.Request(kind, Environment.TickCount64, column, recipeId);
        if (packet != null) ClientNetwork.SendPacket(packet);
    }

    private bool ClosePotionWindow()
    {
        if (_potionModel.Current == null && _potionWindow == null) return false;
        SendPotionRequest(PotionRequestKind.Leave, 0, Guid.Empty);
        _potionModel.Dismiss();
        _potionWindow?.Destroy();
        _potionWindow = null;
        return true;
    }

    private void DisposePotions()
    {
        lock (_potionInboxLock)
        {
            _potionDisposed = true;
            _potionInbox.Clear();
        }

        _potionModel.Dismiss();
        _potionWindow?.Destroy();
        _potionWindow = null;
    }
}
