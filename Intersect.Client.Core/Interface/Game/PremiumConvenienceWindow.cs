using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal sealed class PremiumConvenienceWindow : Window
{
    private readonly ScrollControl _rows;
    private readonly Label _message;
    private readonly ComboBox _pet;
    private PremiumConvenienceView _view=new();
    private int _tab;
    internal static string T(string source)=>QualityOfLifeText.Get(source,ClientLanguages.Current);
    internal static void Request(PremiumConvenienceAction action=PremiumConvenienceAction.Open,Guid id=default,Guid pet=default,string name="",bool open=true)=>
        Intersect.Client.Networking.Network.SendPacket(new PremiumConvenienceRequestPacket {Action=action,Id=id,PetId=pet,Name=name,OpenWindow=open});
    internal PremiumConvenienceWindow(Canvas parent):base(parent,T("Premium conveniences"),false,nameof(PremiumConvenienceWindow))
    {
        SetSize(Math.Min(740,parent.Width-20),Math.Min(580,parent.Height-30));Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        var names=new[]{"Saved outfits","Favorite recipes","Pet accessories"};
        for(var i=0;i<3;i++){var tab=i;NavalManagementWindow.Button(this,"Tab"+i,T(names[i]),16+i*(Width-32)/3,30,(Width-40)/3,32,()=>{_tab=tab;Refresh();});}
        _message=NavalManagementWindow.Text(this,"Status","",20,70,Width-40,34,11);
        _pet=new ComboBox(this,"PetChoice");_pet.SetBounds(20,108,Width-40,30);_pet.ItemSelected+=(_,_)=>Refresh();
        _rows=new ScrollControl(this,"PremiumRows") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=false};_rows.SetBounds(20,150,Width-40,Height-228);Shop.ShopWindow.ConfigureNativeScrollbar(_rows);
        NavalManagementWindow.Button(this,"Close","Close",Width-150,Height-64,130,32,Hide);Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Apply(PremiumConvenienceStatePacket packet)
    {
        _view=Newtonsoft.Json.JsonConvert.DeserializeObject<PremiumConvenienceView>(packet.Json)??new();
        _message.Text=T(packet.Message.Length>0?packet.Message:_view.Premium?"Premium active":"Premium is required.");
        var selected=_pet.SelectedItem?.UserData as Guid?;
        _pet.ClearItems();foreach(var pet in _view.Pets)_pet.AddItem(pet.Name,userData:pet.Id);
        if(selected.HasValue)_pet.SelectByUserData(selected.Value);
        Refresh();if(packet.OpenWindow){Show();BringToFront();}
    }
    private void Refresh()
    {
        _rows.DeleteAll();_pet.IsHidden=_tab!=2;var y=4;var width=_rows.Width-24;
        if(_tab==0)
        {
            var save=NavalManagementWindow.Button(_rows,"SaveOutfit",T("Save current outfit"),4,y,width,32,()=>
                _=new InputBox(T("Save current outfit"),ClientLanguages.Text("Name"),InputType.TextInput,onSubmit:(_,args)=>
                {if(args.Value is StringSubmissionValue value)Request(PremiumConvenienceAction.SaveOutfit,name:value.Value);}));save.IsDisabled=!_view.Premium;y+=44;
            foreach(var outfit in _view.Profile.Outfits)
            {
                var id=outfit.Id;
                NavalManagementWindow.Text(_rows,"Name"+id,outfit.Name,4,y,width,26,13);y+=28;
                var apply=NavalManagementWindow.Button(_rows,"Apply"+id,T("Use outfit"),4,y,(width-12)/2,32,()=>Request(PremiumConvenienceAction.ApplyOutfit,id));apply.IsDisabled=!_view.Premium;
                NavalManagementWindow.Button(_rows,"Delete"+id,ClientLanguages.Text("Delete"),12+(width-12)/2,y,(width-12)/2,32,()=>Request(PremiumConvenienceAction.DeleteOutfit,id));y+=46;
            }
        }
        else if(_tab==1)
        {
            foreach(var id in _view.Profile.FavoriteRecipes)
            {
                var recipe=CraftingRecipeDescriptor.Get(id);
                NavalManagementWindow.Text(_rows,"Recipe"+id,ClientLanguages.ContentText(recipe?.Name??""),4,y,Math.Max(80,width-150),34,12);
                NavalManagementWindow.Button(_rows,"Remove"+id,ClientLanguages.Text("Remove"),width-136,y,132,32,()=>Request(PremiumConvenienceAction.ToggleRecipeFavorite,id));y+=44;
            }
        }
        else
        {
            var pet=_pet.SelectedItem?.UserData as Guid? ?? Guid.Empty;
            var clear=NavalManagementWindow.Button(_rows,"ClearAccessory",T("Remove accessory"),4,y,width,32,()=>Request(PremiumConvenienceAction.PetDecoration,pet:pet));clear.IsDisabled=!_view.Premium || pet==Guid.Empty;y+=44;
            foreach(var decoration in _view.Decorations)
            {
                var id=decoration.Id;
                var icon=new ImagePanel(_rows,"Decoration"+id) {MaintainAspectRatio=true,ShouldDrawBackground=false};icon.SetBounds(4,y,42,42);icon.Texture=Intersect.Client.Framework.File_Management.GameContentManager.Current.GetTexture(Intersect.Client.Framework.Content.TextureType.Item,decoration.Icon);
                NavalManagementWindow.Text(_rows,"DecorationName"+id,ClientLanguages.ContentText(decoration.Name),54,y,Math.Max(50,width-210),42,12);
                var use=NavalManagementWindow.Button(_rows,"UseDecoration"+id,ClientLanguages.Text("Use"),width-136,y,132,32,()=>Request(PremiumConvenienceAction.PetDecoration,id,pet));use.IsDisabled=!_view.Premium || pet==Guid.Empty;y+=54;
            }
        }
        _rows.SetInnerSize(width,y+8);_rows.UpdateScrollBars();
    }
}
