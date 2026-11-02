using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _diceInboxLock = new();
    private readonly Queue<DiceStatePacket> _diceInbox = new();
    private readonly DiceClientModel _diceModel = new();
    private DiceWindow? _diceWindow;
    private bool _diceDisposed;

    internal void QueueDiceState(DiceStatePacket packet)
    {
        lock (_diceInboxLock)
        {
            if (_diceDisposed)
                return;

            while (_diceInbox.Count >= 64)
                _diceInbox.Dequeue();

            _diceInbox.Enqueue(packet);
        }
    }

    private void UpdateDice()
    {
        DiceStatePacket[] packets;
        lock (_diceInboxLock)
        {
            packets = _diceInbox.ToArray();
            _diceInbox.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var packet in packets)
            _diceModel.Apply(packet, Globals.Me?.Id ?? Guid.Empty, now);

        if (_diceModel.Current == null)
        {
            _diceWindow?.Destroy();
            _diceWindow = null;
            return;
        }

        _diceWindow ??= new DiceWindow(GameCanvas, SendDice);

        if (_diceWindow.ExitRequested)
        {
            CloseDiceWindow();
            return;
        }

        if (_diceModel.NeedsRefresh(now))
            SendDice(DiceRequestKind.Refresh, 0, DiceBet.UnderSeven, -1);

        _diceWindow.Update(_diceModel);
    }

    private void SendDice(
        DiceRequestKind kind,
        long amount,
        DiceBet betType,
        int number
    )
    {
        var packet = _diceModel.Request(kind, Environment.TickCount64, amount, betType, number);
        if (packet != null)
            ClientNetwork.SendPacket(packet);
    }

    private bool CloseDiceWindow()
    {
        if (_diceModel.Current == null && _diceWindow == null)
            return false;

        SendDice(DiceRequestKind.Leave, 0, DiceBet.UnderSeven, -1);
        _diceModel.Dismiss();
        _diceWindow?.Destroy();
        _diceWindow = null;
        return true;
    }

    private void DisposeDice()
    {
        lock (_diceInboxLock)
        {
            _diceDisposed = true;
            _diceInbox.Clear();
        }

        _diceModel.Dismiss();
        _diceWindow?.Destroy();
        _diceWindow = null;
    }
}
