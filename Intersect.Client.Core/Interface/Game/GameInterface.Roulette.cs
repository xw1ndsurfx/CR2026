using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Roulette;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _rouletteInboxLock = new();
    private readonly Queue<RouletteStatePacket> _rouletteInbox = new();
    private readonly RouletteClientModel _rouletteModel = new();
    private RouletteWindow? _rouletteWindow;
    private bool _rouletteDisposed;

    internal void QueueRouletteState(RouletteStatePacket packet)
    {
        lock (_rouletteInboxLock)
        {
            if (_rouletteDisposed)
                return;

            while (_rouletteInbox.Count >= 64)
                _rouletteInbox.Dequeue();

            _rouletteInbox.Enqueue(packet);
        }
    }

    private void UpdateRoulette()
    {
        RouletteStatePacket[] packets;
        lock (_rouletteInboxLock)
        {
            packets = _rouletteInbox.ToArray();
            _rouletteInbox.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var packet in packets)
            _rouletteModel.Apply(packet, Globals.Me?.Id ?? Guid.Empty, now);

        if (_rouletteModel.Current == null)
        {
            _rouletteWindow?.Destroy();
            _rouletteWindow = null;
            return;
        }

        _rouletteWindow ??= new RouletteWindow(GameCanvas, SendRoulette);

        if (_rouletteWindow.ExitRequested)
        {
            CloseRouletteWindow();
            return;
        }

        if (_rouletteModel.NeedsRefresh(now))
            SendRoulette(RouletteRequestKind.Refresh, 0, RouletteBetType.Straight, -1);

        _rouletteWindow.Update(_rouletteModel);
    }

    private void SendRoulette(
        RouletteRequestKind kind,
        long amount,
        RouletteBetType betType,
        int number
    )
    {
        var packet = _rouletteModel.Request(kind, Environment.TickCount64, amount, betType, number);
        if (packet != null)
            ClientNetwork.SendPacket(packet);
    }

    private bool CloseRouletteWindow()
    {
        if (_rouletteModel.Current == null && _rouletteWindow == null)
            return false;

        SendRoulette(RouletteRequestKind.Leave, 0, RouletteBetType.Straight, -1);
        _rouletteModel.Dismiss();
        _rouletteWindow?.Destroy();
        _rouletteWindow = null;
        return true;
    }

    private void DisposeRoulette()
    {
        lock (_rouletteInboxLock)
        {
            _rouletteDisposed = true;
            _rouletteInbox.Clear();
        }

        _rouletteModel.Dismiss();
        _rouletteWindow?.Destroy();
        _rouletteWindow = null;
    }
}
