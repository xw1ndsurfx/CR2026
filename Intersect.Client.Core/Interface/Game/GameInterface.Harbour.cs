using Intersect.Network.Packets.Server;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private readonly Dictionary<NavalManageAction,NavalManagementWindow> _navalPanels=[];
    private NavalManagementWindow? _navalManagement;
    private NavalAmmoShopWindow? _navalAmmoShop;
    private NavalDockWindow? _navalDockPrompt;
    private Window? _enemyBoardingPrompt;
    private Guid _dockSuppressed,_helmOfferSeen,_enemyBoardingSuppressed;
    public void OpenNavalManagement(NavalManageAction mode=NavalManageAction.Ammo)=>NavalManagementWindow.Request(mode);
    public void ApplyNavalManagement(string json)
    {
        try {if(json.Length>131072)return;var state=Newtonsoft.Json.JsonConvert.DeserializeObject<NavalManagementState>(json);if(state==null)return;
            if(state.Shop) {if(_navalPanels.TryGetValue(NavalManageAction.Ammo,out var ammoPanel))ammoPanel.Hide();_navalAmmoShop??=new NavalAmmoShopWindow(GameCanvas);_navalAmmoShop.Apply(state);return;}
            if(state.Mode is NavalManageAction.OfferHelm or NavalManageAction.AcceptHelm or NavalManageAction.ReclaimHelm && state.ShipId==Guid.Empty)return;
            if(state.Mode==NavalManageAction.Ammo)_navalAmmoShop?.Hide();
            var panel=state.Mode is NavalManageAction.Repair or NavalManageAction.RepairInput or NavalManageAction.RepairMenu or NavalManageAction.CancelRepair?NavalManageAction.Repair:
                state.Mode is NavalManageAction.OfferHelm or NavalManageAction.AcceptHelm or NavalManageAction.ReclaimHelm?NavalManageAction.OfferHelm:
                state.Mode is NavalManageAction.Skills or NavalManageAction.UseSkill or NavalManageAction.BuySkill?NavalManageAction.Skills:NavalManageAction.Ammo;
            if(!_navalPanels.TryGetValue(panel,out var window))_navalPanels[panel]=window=new NavalManagementWindow(GameCanvas,panel);
            _navalManagement=window;window.Apply(state);}
        catch(Newtonsoft.Json.JsonException) { }
    }
    private void UpdateNavalManagement()
    {
        _boardingOffer?.Tick();foreach(var panel in _navalPanels.Values)panel.Tick();var ship=Naval.NavalWorld.OwnShip;
        if(ship!=null){_navalAmmoShop?.Hide();_shipShopWindow?.Hide();}
        else foreach(var mode in new[]{NavalManageAction.Repair,NavalManageAction.OfferHelm,NavalManageAction.Skills})if(_navalPanels.TryGetValue(mode,out var seaPanel))seaPanel.Hide();
        if(ship is {EnemyBoardingTargetId:var target} && target!=Guid.Empty)
        {
            if(target==_enemyBoardingSuppressed)return;
            if(_enemyBoardingPrompt==null)
            {
                _enemyBoardingPrompt=new EnemyBoardingPromptWindow(GameCanvas){DeleteOnClose=false,Alignment=[Intersect.Client.Framework.Gwen.Alignments.Center]};
                _enemyBoardingPrompt.Closed+=(_,_)=>_enemyBoardingSuppressed=Naval.NavalWorld.OwnShip?.EnemyBoardingTargetId??Guid.Empty;
                _enemyBoardingPrompt.SetSize(360,160);_enemyBoardingPrompt.DisableResizing();
                NavalManagementWindow.Text(_enemyBoardingPrompt,"BoardingHint",NavalManagementWindow.T("Board the enemy deck with your crew?"),18,40,324,42,12);
                NavalManagementWindow.Button(_enemyBoardingPrompt,"BoardEnemy","Board enemy ship",18,90,324,34,()=>NavalManagementWindow.Request(NavalManageAction.BoardEnemy,target:Naval.NavalWorld.OwnShip?.EnemyBoardingTargetId??Guid.Empty));
            }
            if(_enemyBoardingPrompt.IsHidden){_enemyBoardingPrompt.Show();_enemyBoardingPrompt.BringToFront();}
        }
        else{_enemyBoardingPrompt?.Hide();_enemyBoardingSuppressed=Guid.Empty;}
        if(ship?.HelmOfferToken is {} offer && offer!=Guid.Empty && offer!=_helmOfferSeen)
        {_helmOfferSeen=offer;OpenNavalManagement(NavalManageAction.OfferHelm);}
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
    public bool IsHarbourVisible => _navalPanels.Values.Any(w=>!w.IsHidden) || _navalAmmoShop is {IsHidden:false} || _harbourWindow is { IsHidden: false } || _shipShopWindow is { IsHidden: false } || _navalCrewWindow is {IsHidden:false};
    public void HideHarbour() { foreach(var panel in _navalPanels.Values)panel.Dismiss();_navalAmmoShop?.Hide();_navalDockPrompt?.Hide();_enemyBoardingPrompt?.Hide(); _harbourWindow?.Hide(); _shipShopWindow?.Hide();_navalCrewWindow?.Hide(); }
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
