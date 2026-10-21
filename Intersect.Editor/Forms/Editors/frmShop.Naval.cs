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
        _navalType.Items.AddRange(new object[]{T("Normal shop configuration"),T("Naval ammunition shop configuration"),T("Boat and Sailing skill shop configuration")});
        _navalType.SelectedIndexChanged+=(_,_)=>{if(mEditorItem!=null)mEditorItem.ConfigurationKind=(ShopConfigurationKind)_navalType.SelectedIndex;};
        foreach(Control child in pnlContainer.Controls)child.Top+=64;
        pnlContainer.Height+=64;ClientSize=new Size(ClientSize.Width,ClientSize.Height+64);btnSave.Top+=64;btnCancel.Top+=64;grpShops.Height+=64;
        var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=64,AutoSize=false};_navalType.Dock=DockStyle.None;_navalType.Width=450;
        var configure=new DarkButton {Text=T("Configure boats and skills"),Width=250,Height=26};
        configure.Click+=(_,_)=>{if(mEditorItem?.ConfigurationKind!=ShopConfigurationKind.NavalShipsAndSkills)return;_navalPending=this;Intersect.Editor.Networking.Network.SendPacket(new Intersect.Network.Packets.Editor.NavalConfigurationRequestPacket());};
        bar.Controls.AddRange([_navalType,configure]);pnlContainer.Controls.Add(bar);bar.BringToFront();
        new ToolTip(components).SetToolTip(_navalType,T("Naval shops open on land. Use the item lists for ammunition prices, or Configure boats and skills for this shop."));
    }
}
