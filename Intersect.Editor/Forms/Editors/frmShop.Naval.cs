using DarkUI.Controls;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmShop
{
    private DarkComboBox _navalType=null!;
    private static FrmShop? _navalPending;
    internal static bool ReceiveNavalShopConfiguration(Intersect.Network.Packets.Server.NavalConfigurationPacket packet)
    {
        var editor=_navalPending;_navalPending=null;
        if(editor==null || editor.IsDisposed || editor.mEditorItem==null)return false;
        if(packet.Error.Length>0){MessageBox.Show(editor,packet.Error);return true;}
        using var dialog=new FrmNavalShopOffers(editor.mEditorItem,packet);dialog.ShowDialog(editor);return true;
    }
    private void InitializeNavalShopType()
    {
        string T(string text)=>NavalSetupText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        _navalType=new() {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
        _navalType.Items.AddRange(new object[]{T("Normal shop configuration"),T("Naval ammunition shop configuration"),T("Boat and Sailing skill shop configuration"),T("Spell shop"),T("Premium music shop"),Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Royal Seals shop",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)});
        _navalType.SelectedIndexChanged+=(_,_)=>{if(mEditorItem!=null){mEditorItem.ConfigurationKind=(ShopConfigurationKind)_navalType.SelectedIndex;UpdateSealShopControls();UpdateLists();}};
        foreach(Control child in pnlContainer.Controls)child.Top+=64;
        pnlContainer.Height+=64;ClientSize=new Size(ClientSize.Width,ClientSize.Height+64);btnSave.Top+=64;btnCancel.Top+=64;grpShops.Height+=64;
        var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=64,AutoSize=false};_navalType.Dock=DockStyle.None;_navalType.Width=450;
        var configure=new DarkButton {Text=T("Configure shop offers"),Width=250,Height=26};
        configure.Click+=(_,_)=>{if(mEditorItem?.ConfigurationKind==ShopConfigurationKind.RoyalSeals){grpItemsSold.Focus();return;}if(mEditorItem?.ConfigurationKind is ShopConfigurationKind.Spells or ShopConfigurationKind.PremiumMusic){using var services=new FrmShopServices(mEditorItem);services.ShowDialog(this);return;}if(mEditorItem?.ConfigurationKind!=ShopConfigurationKind.NavalShipsAndSkills)return;_navalPending=this;Intersect.Editor.Networking.Network.SendPacket(new Intersect.Network.Packets.Editor.NavalConfigurationRequestPacket());};
        bar.Controls.AddRange([_navalType,configure]);pnlContainer.Controls.Add(bar);bar.BringToFront();
        new ToolTip(components).SetToolTip(_navalType,T("Naval shops open on land. Use the item lists for ammunition prices, or Configure boats and skills for this shop."));
    }
    private void UpdateSealShopControls()
    {
        var seals=mEditorItem?.ConfigurationKind==ShopConfigurationKind.RoyalSeals;
        grpItemsBought.Enabled=!seals;cmbDefaultCurrency.Enabled=cmbSellFor.Enabled=!seals;
        lblSellFor.Text=seals?Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Royal Seals",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName):Intersect.Editor.Localization.Strings.ShopEditor.sellfor;
        cmbSellFor.Visible=cmbDefaultCurrency.Visible=!seals;_buybackDiscount.Enabled=!seals;
        lblDefaultCurrency.Text=seals?Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Royal Seals",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName):Intersect.Editor.Localization.Strings.ShopEditor.defaultcurrency;
        if(seals){cmbSellFor.SelectedIndex=-1;cmbDefaultCurrency.SelectedIndex=-1;}
        foreach(var name in new[]{"SuggestSellPrice","SuggestBuyPrice"})
            if(Controls.Find(name,true).FirstOrDefault() is {} control)control.Enabled=!seals;
    }

}
