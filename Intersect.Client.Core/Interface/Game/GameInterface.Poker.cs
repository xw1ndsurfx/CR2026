using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _pokerInboxLock = new();
    private readonly Queue<PokerStatePacket> _pokerInbox = new();
    private readonly PokerClientModel _pokerModel = new();
    private PokerWindow? _pokerWindow;
    private bool _pokerDisposed;

    // Packet thread: no Gwen/UI work here. Memory is bounded even if rendering pauses.
    internal void QueuePokerState(PokerStatePacket packet)
    {
        lock (_pokerInboxLock)
        {
            if (_pokerDisposed) return;
            while (_pokerInbox.Count >= 64) _pokerInbox.Dequeue();
            _pokerInbox.Enqueue(packet);
        }
    }

    private void UpdatePoker()
    {
        UpdateBlackjack();
        PokerStatePacket[] packets;
        lock (_pokerInboxLock) { packets = _pokerInbox.ToArray(); _pokerInbox.Clear(); }
        var now = Environment.TickCount64;
        foreach (var packet in packets) _pokerModel.Apply(packet, Globals.Me?.Id ?? Guid.Empty, now);
        if (_pokerModel.Current == null)
        {
            _pokerWindow?.Destroy();
            _pokerWindow = null;
            return;
        }
        _pokerWindow ??= new PokerWindow(GameCanvas, SendPokerRequest);
        if (_pokerWindow.ExitRequested) { ClosePokerWindow(); return; }
        if (_pokerModel.NeedsRefresh(now)) SendPokerRequest(PokerRequestKind.Refresh, 0);
        _pokerWindow.Update(_pokerModel);
    }

    private void SendPokerRequest(PokerRequestKind kind, long amount)
    {
        var packet = _pokerModel.Request(kind, Environment.TickCount64, amount);
        if (packet != null) ClientNetwork.SendPacket(packet);
    }

    private bool ClosePokerWindow()
    {
        if (CloseBlackjackWindow()) return true;
        if (_pokerModel.Current == null && _pokerWindow == null) return false;
        SendPokerRequest(PokerRequestKind.Leave, 0);
        _pokerModel.Dismiss();
        _pokerWindow?.Destroy();
        _pokerWindow = null;
        return true;
    }

    private void DisposePoker()
    {
        DisposeBlackjack();
        lock (_pokerInboxLock) { _pokerDisposed = true; _pokerInbox.Clear(); }
        _pokerModel.Dismiss();
        _pokerWindow?.Destroy();
        _pokerWindow = null;
        // Disconnect cleanup belongs to the server. Do not send on a replaced connection.
    }
}
