using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private HarbourWindow? _harbourWindow, _shipShopWindow;
    private NavalCrewWindow? _navalCrewWindow;
    public void UpdateNavalCrew() {if(_navalCrewWindow is {IsHidden:false}) _navalCrewWindow.Update(Naval.NavalWorld.CrewState);}
    public bool IsHarbourVisible => _harbourWindow is { IsHidden: false } || _shipShopWindow is { IsHidden: false } || _navalCrewWindow is {IsHidden:false};
    public void HideHarbour() { _harbourWindow?.Hide(); _shipShopWindow?.Hide();_navalCrewWindow?.Hide(); }
    public void ApplyHarbourState(HarbourStatePacket packet)
    {
        if(packet.View==HarbourView.Crew)
        {
            _navalCrewWindow??=new NavalCrewWindow(GameCanvas);
            _navalCrewWindow.Apply(packet);_navalCrewWindow.Show();_navalCrewWindow.BringToFront();return;
        }
        if (packet.View is not (HarbourView.Fleet or HarbourView.Shop)) return;
        if (packet.Close) { if (packet.View == HarbourView.Shop) _shipShopWindow?.Hide(); else _harbourWindow?.Hide(); return; }
        GameMenu?.HideWindows();
        if (packet.View == HarbourView.Shop)
        {
            _harbourWindow?.Hide();
            _shipShopWindow ??= new HarbourWindow(GameCanvas, HarbourView.Shop);
            _shipShopWindow.Apply(packet); _shipShopWindow.Show(); _shipShopWindow.BringToFront();
        }
        else
        {
            _shipShopWindow?.Hide();
            _harbourWindow ??= new HarbourWindow(GameCanvas, HarbourView.Fleet);
            _harbourWindow.Apply(packet); _harbourWindow.Show(); _harbourWindow.BringToFront();
        }
    }
}
