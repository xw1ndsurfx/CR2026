using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;
internal sealed class EventCommandNavalShop:UserControl
{
    private sealed record Choice(Guid Id,string Name) {public override string ToString()=>Name;}
    internal EventCommandNavalShop(EventCommand command,FrmEvent editor)
    {
        string T(string text)=>NavalSetupText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Size=new(520,180);
        var ammo=command is OpenNavalAmmoShopCommand;
        var current=ammo?((OpenNavalAmmoShopCommand)command).ShopId:((OpenNavalShopCommand)command).ShopId;
        var list=new ComboBox {Left=16,Top=40,Width=480,DropDownStyle=ComboBoxStyle.DropDownList};
        list.Items.Add(new Choice(Guid.Empty,T("Default naval catalogue")));
        foreach(var shop in ShopDescriptor.Lookup.Values.OfType<ShopDescriptor>().Where(s=>s.ConfigurationKind==(ammo?ShopConfigurationKind.NavalAmmunition:ShopConfigurationKind.NavalShipsAndSkills)))list.Items.Add(new Choice(shop.Id,shop.Name));
        list.SelectedIndex=Enumerable.Range(0,list.Items.Count).FirstOrDefault(i=>((Choice)list.Items[i]).Id==current);
        var save=new Button {Text=T("Save"),Left=260,Top=100,Width=110};
        var cancel=new Button {Text=T("Cancel"),Left=380,Top=100,Width=110};
        save.Click+=(_,_)=>{var id=((Choice)list.SelectedItem!).Id;if(ammo)((OpenNavalAmmoShopCommand)command).ShopId=id;else ((OpenNavalShopCommand)command).ShopId=id;editor.FinishCommandEdit();};
        cancel.Click+=(_,_)=>editor.CancelCommandEdit();Controls.AddRange([list,save,cancel]);
    }
}
