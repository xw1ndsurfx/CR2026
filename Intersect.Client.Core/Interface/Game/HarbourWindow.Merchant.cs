using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game.Shop;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game;
internal sealed partial class HarbourWindow
{
    private ScrollControl _buyList=null!,_sellList=null!;
    private void InitializeMerchant()
    {
        _cards.Hide();_previous.Hide();_next.Hide();_pageLabel.Hide();_refresh.Hide();
        Titlebar.Show();SetSize(780,620);
        _message.Show();_message.SetBounds(24,570,600,28);
        foreach(var (x,name) in new[]{(16,"Buy"),(398,"Sell")}){var pane=new ShopWindow.MerchantPanelChrome(this,name+"Pane");pane.SetBounds(x,48,363,512);pane.SendToBack();}
        NavalManagementWindow.Text(this,"BuyHeader",Strings.Shop.BuyItem,24,52,340,28,14);
        NavalManagementWindow.Text(this,"SellHeader",Strings.Shop.SellItem,406,52,340,28,14);
        _buyList=MerchantList("BuyList",24);_sellList=MerchantList("SellList",406);
        Button(this,"CloseShipShop","Close",640,565,110,Hide);
    }
    private ScrollControl MerchantList(string name,int x)
    {var list=new ScrollControl(this,name) {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};list.SetBounds(x,90,350,468);ShopWindow.ConfigureNativeScrollbar(list);return list;}
    private void RenderMerchant()
    {
        _buyList.DeleteAllChildren();_sellList.DeleteAllChildren();var by=0;var sy=0;
        string T(string text)=>NavalManagementWindow.T(text);
        void Row(ScrollControl parent,ref int y,string name,string detail,string price,string caption,bool enabled,Action action,string icon="",string graphic="",int frames=4)
        {var row=ShopProductRow.Service(parent,"NavalOffer"+y,name,detail,price,caption,enabled,action,icon);row.SetBounds(0,y,332,75);if(graphic.Length>0)row.ShowShipPreview(graphic,frames);y+=76;}
        foreach(var entry in _state.Entries)
        {
            var name=ClientLanguages.ContentText(entry.ShipName.Length>0?entry.ShipName:entry.Name);
            var condition=string.Format(ClientLanguages.Text("Hull: {0}/100"),entry.Health);
            if(!entry.Owned)Row(_buyList,ref by,name,entry.PremiumRequired?ClientLanguages.Text("Premium required"):T("Not owned"),$"{entry.Price} {entry.Currency}",T("Buy ship"),HarbourUiRules.CanBuy(entry,_state.Premium),()=>Request(HarbourAction.Buy,entry.ModelId),graphic:entry.Graphics,frames:entry.Frames);
            else
            {
                Row(_buyList,ref by,name,condition,$"{entry.RepairPrice} {entry.Currency}",T("Fully repair"),HarbourUiRules.CanRepair(entry),()=>Request(HarbourAction.Repair,entry.ModelId),graphic:entry.Graphics,frames:entry.Frames);
                Row(_sellList,ref sy,name,condition,$"{entry.SellPrice} {entry.Currency}",Strings.Shop.SellItem,entry.SellPrice>0,()=>Request(HarbourAction.Sell,entry.ModelId),graphic:entry.Graphics,frames:entry.Frames);
            }
        }
        foreach(var skill in _state.Skills)
        {
            var d=skill.Definition;
            Row(_buyList,ref by,T(NavalSkillDefinition.Caption(d.Kind)),string.Format(T("Required level: {0}"),d.RequiredLevel),$"{d.Price} {skill.Currency}",T(skill.Owned?"Owned":"Learn skill"),!skill.Owned && skill.CurrencyAvailable && skill.Balance>=d.Price && _state.SailingLevel>=d.RequiredLevel,()=>NavalManagementWindow.Request(NavalManageAction.BuySkill,skill:d.Kind),d.Icon);
        }
        _buyList.UpdateScrollBars();_sellList.UpdateScrollBars();
    }
}
