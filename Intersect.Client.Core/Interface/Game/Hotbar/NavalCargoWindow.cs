using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game.Hotbar;
internal sealed class NavalCargoWindow : Window
{
    private readonly Label _summary,_contents;
    private int _page;
    internal NavalCargoWindow(Base parent):base(parent,T("Cargo hold"),false,nameof(NavalCargoWindow))
    {
        Closed+=(_,_)=>Naval.NavalWorld.CargoOpen=false;
        SetSize(520,440);Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        _summary=Text("CargoSummary",16,36,485,50);_contents=Text("CargoContents",16,96,485,272);
        var previous=Button("Previous",16,385,()=>_page=Math.Max(0,_page-1));
        Button("Next",145,385,()=>_page++);
        Button("Close",365,385,()=>Naval.NavalWorld.CargoOpen=false);Hide();
    }
    protected override void EnsureInitialized() { }
    private static string T(string source)=>NavalSetupText.Get(source,ClientLanguages.Current);
    private Label Text(string id,int x,int y,int width,int height)
    {
        var text=new Label(this,id) {AutoSizeToContents=false,Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};
        text.SetBounds(x,y,width,height);return text;
    }
    private Button Button(string source,int x,int y,Action clicked)
    {
        var button=new Button(this,source) {Text=T(source),Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};
        button.SetBounds(x,y,120,30);button.Clicked+=(_,_)=>clicked();return button;
    }
    internal void Update(ShipSnapshot? ship,bool open)
    {
        if(!open || ship==null || ship.Sinking) {Hide();return;}
        if(IsHidden) Show();_page=Math.Clamp(_page,0,Math.Max(0,(ship.Cargo.Length-1)/8));
        _summary.Text=string.Format(T("Cargo: {0}/{1}\nUnload at a Q dock. Cargo is lost if your ship sinks."),ship.Cargo.Length,ship.CargoCapacity);
        _contents.Text=string.Join("\n\n",ship.Cargo.OrderByDescending(c=>c.Quality).Skip(_page*8).Take(8).Select(c=>
            string.Format(T("{0} · Quality {1}: {2} × {3}"),T(NavalCargo.Caption(c.Kind)),c.Quality,
                ClientLanguages.ContentText(ItemDescriptor.Get(c.ItemId)?.Name??T("Unknown item")),c.Quantity)));
        if(ship.Cargo.Length==0) _contents.Text=T("The cargo hold is empty.");
    }
}
