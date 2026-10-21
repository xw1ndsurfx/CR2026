using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private NavalManagementWindow? _navalManagement;
    private NavalDockWindow? _navalDockPrompt;
    private Guid _dockSuppressed,_helmOfferSeen;
    public void OpenNavalManagement(NavalManageAction mode=NavalManageAction.Ammo)=>NavalManagementWindow.Request(mode);
    public void ApplyNavalManagement(string json)
    {
        try {if(json.Length>131072)return;var state=Newtonsoft.Json.JsonConvert.DeserializeObject<NavalManagementState>(json);if(state==null)return;
            _navalManagement??=new NavalManagementWindow(GameCanvas);_navalManagement.Apply(state);}
        catch(Newtonsoft.Json.JsonException) { }
    }
    private void UpdateNavalManagement()
    {
        _navalManagement?.Tick();var ship=Naval.NavalWorld.OwnShip;
        if(ship?.HelmOfferToken is {} offer && offer!=Guid.Empty && offer!=_helmOfferSeen)
        {_helmOfferSeen=offer;OpenNavalManagement();}
        if(ship is not {DockAvailable:true,Sinking:false}){_navalDockPrompt?.Hide();_dockSuppressed=Guid.Empty;return;}
        if(_dockSuppressed==ship.Id)return;
        if(_navalDockPrompt==null)
        {
            _navalDockPrompt=new NavalDockWindow(GameCanvas) {DeleteOnClose=false,Alignment=[Intersect.Client.Framework.Gwen.Alignments.Center]};
            _navalDockPrompt.SetSize(330,132);_navalDockPrompt.DisableResizing();
            NavalManagementWindow.Button(_navalDockPrompt,"MoorShip","Moor ship",18,42,294,36,()=>NavalManagementWindow.Request(NavalManageAction.Dock));
            _navalDockPrompt.Closed+=(_,_)=>_dockSuppressed=Naval.NavalWorld.OwnShip?.Id??Guid.Empty;
        }
        if(_navalDockPrompt.IsHidden){_navalDockPrompt.Show();_navalDockPrompt.BringToFront();}
    }
    private HarbourWindow? _harbourWindow, _shipShopWindow;
    private NavalCrewWindow? _navalCrewWindow;
    public void UpdateNavalCrew() {UpdateNavalManagement();if(_navalCrewWindow is {IsHidden:false}) _navalCrewWindow.Update(Naval.NavalWorld.CrewState);}
    public bool IsHarbourVisible => _navalManagement is {IsHidden:false} || _harbourWindow is { IsHidden: false } || _shipShopWindow is { IsHidden: false } || _navalCrewWindow is {IsHidden:false};
    public void HideHarbour() { _navalManagement?.Dismiss();_navalDockPrompt?.Hide(); _harbourWindow?.Hide(); _shipShopWindow?.Hide();_navalCrewWindow?.Hide(); }
    public void ApplyHarbourState(HarbourStatePacket packet)
    {
        if(packet.View==HarbourView.Crew)
        {
            if(packet.Close) {_navalCrewWindow?.Hide();return;}
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
