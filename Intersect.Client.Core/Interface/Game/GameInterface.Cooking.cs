using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private readonly object _cookingInboxLock = new();
    private readonly Queue<CookingStatePacket> _cookingInbox = new();
    private readonly CookingClientModel _cookingModel = new();
    private CookingWindow? _cookingWindow;
    private bool _cookingDisposed;

    internal void QueueCookingState(CookingStatePacket packet)
    {
        lock (_cookingInboxLock)
        {
            if (_cookingDisposed) return;
            while (_cookingInbox.Count >= 64) _cookingInbox.Dequeue();
            _cookingInbox.Enqueue(packet);
        }
    }

    private void UpdateCooking()
    {
        CookingStatePacket[] packets;
        lock (_cookingInboxLock)
        {
            packets = _cookingInbox.ToArray();
            _cookingInbox.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var packet in packets)
            _cookingModel.Apply(packet, Globals.Me?.Id ?? Guid.Empty, now);

        if (_cookingModel.Current == null)
        {
            _cookingWindow?.Destroy();
            _cookingWindow = null;
            return;
        }

        _cookingWindow ??= new CookingWindow(GameCanvas, SendCooking);

        if (_cookingWindow.ExitRequested)
        {
            CloseCookingWindow();
            return;
        }

        if (_cookingModel.NeedsRefresh(now))
            SendCooking(CookingRequestKind.Refresh, Guid.Empty, Guid.Empty, false);

        _cookingWindow.Update(_cookingModel);
    }

    private void SendCooking(
        CookingRequestKind kind,
        Guid recipeId,
        Guid partnerId,
        bool accept
    )
    {
        var packet = _cookingModel.Request(
            kind,
            Environment.TickCount64,
            recipeId,
            partnerId,
            accept
        );
        if (packet != null)
            ClientNetwork.SendPacket(packet);
    }

    private bool CloseCookingWindow()
    {
        if (_cookingModel.Current == null && _cookingWindow == null)
            return false;

        SendCooking(CookingRequestKind.Leave, Guid.Empty, Guid.Empty, false);
        _cookingModel.Dismiss();
        _cookingWindow?.Destroy();
        _cookingWindow = null;
        return true;
    }

    private void DisposeCooking()
    {
        lock (_cookingInboxLock)
        {
            _cookingDisposed = true;
            _cookingInbox.Clear();
        }

        _cookingModel.Dismiss();
        _cookingWindow?.Destroy();
        _cookingWindow = null;
    }
}
