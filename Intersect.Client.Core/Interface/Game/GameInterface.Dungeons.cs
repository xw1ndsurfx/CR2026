using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private DungeonPanelWindow? _dungeonPanelWindow;
    private bool _houseBlockedByDungeon;
    private ObservedDungeonStatsWindow? _observedDungeonStatsWindow;
    private DungeonRunHudWindow? _dungeonRunHudWindow;
    private DungeonConfirmationWindow? _dungeonConfirmationWindow;

    public bool IsDungeonPanelVisible => _dungeonPanelWindow?.IsVisibleInTree == true;

    public void ApplyDungeonState(DungeonStatePacket packet)
    {
        if (packet.InspectedPlayerId != Guid.Empty)
        {
            if (!packet.OpenWindow)
                return;

            // Never populate the observing player's own dungeon panel with
            // another character's run statistics.
            _observedDungeonStatsWindow ??= new ObservedDungeonStatsWindow(GameCanvas);
            _observedDungeonStatsWindow.Apply(packet);
            _observedDungeonStatsWindow.ShowWindow();
            return;
        }

        _dungeonPanelWindow ??= new DungeonPanelWindow(GameCanvas);
        _dungeonPanelWindow.Apply(packet);

        if (!packet.OpenWindow)
            return;

        GameMenu?.HideWindows();
        _dungeonPanelWindow.ShowWindow();
    }

    public void ToggleDungeonPanel()
    {
        if (_dungeonPanelWindow?.IsVisibleInTree == true)
        {
            _dungeonPanelWindow.Hide();
            return;
        }

        Networking.PacketSender.SendRequestDungeonPanel(openWindow: true);
    }

    public void HideDungeonPanel()
    {
        _dungeonPanelWindow?.Hide();
    }

    public void ShowDungeonConfirmation(DungeonConfirmationPacket packet)
    {
        GameMenu?.HideWindows();
        _dungeonConfirmationWindow ??= new DungeonConfirmationWindow(GameCanvas);
        _dungeonConfirmationWindow.Apply(packet);
    }

    public void ShowDungeonRetryOffer(DungeonRetryOfferPacket packet)
    {
        GameMenu?.HideWindows();
        _dungeonConfirmationWindow ??= new DungeonConfirmationWindow(GameCanvas);
        _dungeonConfirmationWindow.ApplyRetry(packet);
    }

    public void ApplyDungeonRunState(DungeonRunStatePacket packet)
    {
        _houseBlockedByDungeon=packet.Status==DungeonRunStatus.Active;
        _dungeonRunHudWindow ??= new DungeonRunHudWindow(GameCanvas);
        _dungeonRunHudWindow.Apply(packet);
    }

    public void UpdateDungeonRunUi()
    {
        _dungeonRunHudWindow?.Update();
    }
}
