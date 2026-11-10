using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;
internal sealed class EventCommandShipReward : UserControl
{
    private sealed record ShopChoice(Guid Id,string Name) {public override string ToString()=>Name;}
    private sealed record ModelChoice(string Id,string Name) {public override string ToString()=>Name+" ["+Id+"]";}
    internal EventCommandShipReward(ShipRewardCommand command,FrmEvent editor)
    {
        string T(string text)=>NavalSetupText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Size=new(560,300);
        var shop=new ComboBox {Left=190,Top=20,Width=350,DropDownStyle=ComboBoxStyle.DropDownList};
        shop.Items.Add(new ShopChoice(Guid.Empty,T("Default naval catalogue")));
        foreach(var item in ShopDescriptor.Lookup.Values.OfType<ShopDescriptor>().Where(s=>s.ConfigurationKind==ShopConfigurationKind.NavalShipsAndSkills).OrderBy(s=>s.Name))shop.Items.Add(new ShopChoice(item.Id,item.Name));
        if(!shop.Items.Cast<ShopChoice>().Any(s=>s.Id==command.ShopId))shop.Items.Add(new ShopChoice(command.ShopId,command.ShopId.ToString()));
        var model=new ComboBox {Left=190,Top=65,Width=350,DropDownStyle=ComboBoxStyle.DropDown};
        void LoadModels()
        {
            var previous=model.SelectedItem is ModelChoice selected?selected.Id:model.Text;
            model.Items.Clear();
            try
            {
                var id=((ShopChoice)shop.SelectedItem!).Id;
                var catalog=id==Guid.Empty?HarbourCatalog.Load(Path.Combine("resources","naval-shop.json")):ShopDescriptor.Get(id)?.NavalCatalogue;
                foreach(var offer in catalog?.Offers??[])model.Items.Add(new ModelChoice(offer.Id,offer.Name));
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or ArgumentException) { }
            model.SelectedItem=model.Items.Cast<ModelChoice>().FirstOrDefault(m=>m.Id==previous);
            if(model.SelectedItem==null)model.Text=previous;
        }
        shop.SelectedIndexChanged+=(_,_)=>LoadModels();
        shop.SelectedItem=shop.Items.Cast<ShopChoice>().First(s=>s.Id==command.ShopId);
        model.SelectedItem=model.Items.Cast<ModelChoice>().FirstOrDefault(m=>m.Id==command.ModelId);
        if(model.SelectedItem==null)model.Text=command.ModelId;
        var action=new ComboBox {Left=190,Top=110,Width=350,DropDownStyle=ComboBoxStyle.DropDownList};
        action.Items.AddRange([T("Unlock ship model"),T("Give owned ship")]);
        action.SelectedIndex=Enum.IsDefined(command.Action)?(int)command.Action:0;
        var note=new Label {Left=16,Top=155,Width=524,Height=65,Text=T("Giving a ship also unlocks its model. Sailing still requires its configured Premium and quest conditions."),AutoEllipsis=true};
        var save=new Button {Left=300,Top=240,Width=110,Text=T("Save")};
        var cancel=new Button {Left=430,Top=240,Width=110,Text=T("Cancel")};
        save.Click+=(_,_)=>
        {
            var id=model.SelectedItem is ModelChoice choice?choice.Id:model.Text.Trim();
            if(string.IsNullOrWhiteSpace(id) || id.Length>64)return;
            command.ModelId=id;command.ShopId=((ShopChoice)shop.SelectedItem!).Id;command.Action=(ShipRewardAction)action.SelectedIndex;editor.FinishCommandEdit();
        };
        cancel.Click+=(_,_)=>editor.CancelCommandEdit();
        foreach(var (text,y) in new[]{("Ship catalogue",20),("Ship model ID",65),("Ship reward action",110)})Controls.Add(new Label {Left=16,Top=y,Width=165,Height=30,Text=T(text)});
        Controls.AddRange([shop,model,action,note,save,cancel]);
    }
}
