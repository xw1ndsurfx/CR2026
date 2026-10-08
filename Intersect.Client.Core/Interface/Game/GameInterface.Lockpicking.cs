using Intersect.Client.MiniGames;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _lockpickingInboxLock = new();
    private readonly Queue<LockpickingStatePacket> _lockpickingInbox = new();
    private readonly LockpickingClientModel _lockpickingModel = new();
    private LockpickingWindow? _lockpickingWindow;
    private bool _lockpickingDisposed;

    internal void QueueLockpickingState(LockpickingStatePacket packet)
    {
        lock (_lockpickingInboxLock)
        {
            if (_lockpickingDisposed)
                return;

            while (_lockpickingInbox.Count >= 32)
                _lockpickingInbox.Dequeue();

            _lockpickingInbox.Enqueue(packet);
        }
    }

    private void UpdateLockpicking()
    {
        LockpickingStatePacket[] packets;
        lock (_lockpickingInboxLock)
        {
            packets = _lockpickingInbox.ToArray();
            _lockpickingInbox.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var packet in packets)
            _lockpickingModel.Apply(packet, now);

        if (_lockpickingModel.Current == null)
        {
            _lockpickingWindow?.Destroy();
            _lockpickingWindow = null;
            return;
        }

        _lockpickingWindow ??= new LockpickingWindow(GameCanvas, SendLockpicking);

        if (_lockpickingWindow.ExitRequested)
        {
            CloseLockpickingWindow();
            return;
        }

        _lockpickingWindow.Update(_lockpickingModel, now);
    }

    private void SendLockpicking(LockpickingRequestKind kind, int angle)
    {
        var packet = _lockpickingModel.Request(kind, angle);
        if (packet != null)
            ClientNetwork.SendPacket(packet);
    }

    private bool CloseLockpickingWindow()
    {
        if (_lockpickingModel.Current == null && _lockpickingWindow == null)
            return false;

        SendLockpicking(LockpickingRequestKind.Cancel, 0);
        _lockpickingModel.Dismiss();
        _lockpickingWindow?.Destroy();
        _lockpickingWindow = null;
        return true;
    }

    private void DisposeLockpicking()
    {
        lock (_lockpickingInboxLock)
        {
            _lockpickingDisposed = true;
            _lockpickingInbox.Clear();
        }

        _lockpickingModel.Dismiss();
        _lockpickingWindow?.Destroy();
        _lockpickingWindow = null;
    }
}
