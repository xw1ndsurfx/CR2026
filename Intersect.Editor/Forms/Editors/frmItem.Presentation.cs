using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmItem
{
    private readonly DarkGroupBox _itemPresentationGroup=new() {Width=610,Height=190};
    private readonly DarkCheckBox _showValue=new(),_showLevel=new();
    private readonly DarkButton _chooseAmmo=new();
    private static (FrmItem Editor,Guid Item,Guid Request)? _pendingAmmo;
    private void InitializePresentationControls()
    {
        string T(string source)=>QualityOfLifeText.Get(source,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        _showValue.Text=T("Show value");_showValue.SetBounds(20,66,540,24);
        _showLevel.Text=T("Show required level");_showLevel.SetBounds(20,100,540,24);
        _chooseAmmo.Text=T("Naval ammunition");_chooseAmmo.SetBounds(20,140,540,30);
        _showValue.CheckedChanged+=(_,_)=>{if(mEditorItem!=null)mEditorItem.Presentation.ShowValue=_showValue.Checked;};
        _showLevel.CheckedChanged+=(_,_)=>{if(mEditorItem!=null)mEditorItem.Presentation.ShowRequiredLevel=_showLevel.Checked;};
        _chooseAmmo.Click+=(_,_)=>
        {
            if(mEditorItem?.ItemType!=ItemType.Ammo)return;
            var request=new NavalConfigurationRequestPacket();
            _pendingAmmo=(this,mEditorItem.Id,request.RequestId);
            Intersect.Editor.Networking.Network.SendPacket(request);
        };
        _itemPresentationGroup.Text=T("Trading and display");
        chkCanSellMarketplace.Parent=_itemPresentationGroup;
        chkCanSellMarketplace.SetBounds(20,30,540,24);
        _itemPresentationGroup.Controls.AddRange([_showValue,_showLevel,_chooseAmmo]);
    }
    private sealed record AmmoChoice(CannonAmmunition Ammo)
    {public override string ToString()=>NavalSetupText.Get(CannonAmmunition.Caption(Ammo.Kind),System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);}
    internal static bool ReceiveItemAmmoConfiguration(NavalConfigurationPacket packet)
    {
        if(_pendingAmmo is not {} pending || packet.RequestId!=pending.Request)return false;
        _pendingAmmo=null;
        var editor=pending.Editor;
        if(editor.IsDisposed || editor.mEditorItem?.Id!=pending.Item || editor.mEditorItem.ItemType!=ItemType.Ammo)return true;
        if(packet.Error.Length>0){MessageBox.Show(editor,NavalSetupText.Get(packet.Error,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));return true;}
        var config=JsonConvert.DeserializeObject<NavalConfiguration>(packet.NavigationJson);
        if(config==null)return true;
        using var dialog=new Form {Text=editor._chooseAmmo.Text,Size=new(460,320),StartPosition=FormStartPosition.CenterParent};
        var list=new ComboBox {Left=16,Top=20,Width=410,DropDownStyle=ComboBoxStyle.DropDownList};
        foreach(var ammo in config.Ammunition.Where(a=>a.RequiresItem))list.Items.Add(new AmmoChoice(ammo));
        var image=new PictureBox {Left=16,Top=70,Width=96,Height=96,SizeMode=PictureBoxSizeMode.Zoom};
        list.SelectedIndexChanged+=(_,_)=>
        {
            image.Image?.Dispose();image.Image=null;
            if(list.SelectedItem is AmmoChoice choice && NavalPresentation.SafeAsset(choice.Ammo.Icon) && File.Exists(Path.Combine("resources","spells",choice.Ammo.Icon)))
            {using var source=Image.FromFile(Path.Combine("resources","spells",choice.Ammo.Icon));image.Image=new Bitmap(source);}
        };
        if(list.Items.Count>0)list.SelectedIndex=Enumerable.Range(0,list.Items.Count).FirstOrDefault(i=>(int)((AmmoChoice)list.Items[i]).Ammo.Kind==editor.mEditorItem.Presentation.NavalAmmoKind);
        var save=new Button {Text=QualityOfLifeText.Get("Save",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),Left=280,Top=210,Width=140,Enabled=list.Items.Count>0};
        save.Click+=(_,_)=>
        {
            if(list.SelectedItem is not AmmoChoice choice)return;
            editor.mEditorItem.Presentation.NavalAmmoKind=(int)choice.Ammo.Kind;
            editor.mEditorItem.Presentation.NavalAmmoIcon=choice.Ammo.Icon;
            editor.chkStackable.Checked=true;
            dialog.DialogResult=DialogResult.OK;
        };
        dialog.Controls.AddRange([list,image,save]);dialog.ShowDialog(editor);image.Image?.Dispose();return true;
    }
}
