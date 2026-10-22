using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Maps;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private DungeonChestPacket? _dungeonChest;
    private Animation? _chestAnimation;
    private Button? _chestButton;
    public void ApplyDungeonChest(DungeonChestPacket packet)
    {
        if(_dungeonChest?.RunId != packet.RunId || _dungeonChest?.AnimationId != packet.AnimationId || !packet.Visible)
        { _chestAnimation?.Dispose(); _chestAnimation=null; }
        _dungeonChest=packet.Visible?packet:null;
        if(!packet.Visible) _chestButton?.Hide();
    }
    private void UpdateDungeonChest()
    {
        if(_dungeonChest is not {Visible:true} state || Globals.Me?.MapId != state.MapId || MapInstance.Get(state.MapId) is not {} map)
        { _chestButton?.Hide(); _chestAnimation?.Dispose(); _chestAnimation=null; return; }
        if(_chestAnimation==null || _chestAnimation.IsDisposed)
        {
            _chestAnimation=map.AddTileAnimation(state.AnimationId,state.X,state.Y) as Animation;
            if(_chestAnimation!=null)_chestAnimation.InfiniteLoop=true;
        }
        _chestButton ??= NavalManagementWindow.Button(GameCanvas,"DungeonCompletionChest","Open completion chest",0,0,210,30,
            ()=> {if(_dungeonChest is {} chest)Networking.Network.SendPacket(new DungeonChestRequestPacket{RunId=chest.RunId});});
        _chestButton.SendToBack();
        var nearby=Math.Abs(Globals.Me.X-state.X)<=2 && Math.Abs(Globals.Me.Y-state.Y)<=2;
        _chestButton.IsDisabled=!nearby || state.Claimed;
        _chestButton.Text=ClientLanguages.Text(state.Claimed?"Rewards collected":"Open completion chest");
        _chestButton.SetToolTipText(string.Format(ClientLanguages.Text("Automatic collection in {0}s"),state.SecondsRemaining));
        _chestButton.IsHidden=SuppressStatusHuds || Interface.InputBlockingComponents.Any(control=>control.IsVisibleInTree && control.IsBlockingInput);
        var zoom=Globals.Database.WorldZoom;
        _chestButton.SetPosition((int)((map.X+(state.X+.5f)*Options.Instance.Map.TileWidth-Graphics.CurrentView.Left)*zoom)-105,
            (int)((map.Y+(state.Y+1)*Options.Instance.Map.TileHeight-Graphics.CurrentView.Top)*zoom)+4);
    }
}
