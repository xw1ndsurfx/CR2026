using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Localization;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game.Hotbar;

/// <summary>Compact combat target; only effect badges accept mouse input for their tooltips.</summary>
internal sealed class NavalTargetHud : Base
{
    private readonly Label _name,_health,_level;
    private ShipSnapshot? _target;
    private readonly Dictionary<CannonAmmoKind,(Panel Panel,ImagePanel Icon,Label Timer)> _statuses=[];
    internal NavalTargetHud(Base parent):base(parent,nameof(NavalTargetHud))
    {
        MouseInputEnabled=KeyboardInputEnabled=false;
        _name=Caption("NavalEnemyName",12);_health=Caption("NavalEnemyHealth",10);_level=Caption("NavalEnemyLevel",11);
        SetSize(360,76);Hide();
    }
    private Label Caption(string name,int size) => new(this,name) {AutoSizeToContents=false,
        Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,
        FontSize=size,TextAlign=Pos.Center,TextColorOverride=new Color(255,240,220,165),MouseInputEnabled=false};
    internal void Update(ShipSnapshot? target)
    {
        _target=target;IsHidden=target==null;
        foreach(var row in _statuses.Values)row.Panel.Hide();
        if(target==null)return;
        var effects=(target.CannonStatuses??[]).Where(e=>NavalWorld.StatusRemaining(target.Id,e)>0).Take(8).ToArray();
        var width=Math.Max(200,Math.Min(380,(Parent?.Width??800)-24));
        var columns=Math.Max(1,(width-24)/36);
        SetBounds(Math.Max(8,((Parent?.Width??800)-width)/2),22,width,76+46*((effects.Length+columns-1)/columns));
        _name.SetBounds(64,7,width-74,24);_health.SetBounds(64,47,width-74,21);_level.SetBounds(7,24,46,24);
        EventPanelText.Fit(_name,NavalWorld.EnemyName(target)+(target.BoardingReady?" - "+NavalSetupText.Get("Boardable",ClientLanguages.Current):""),12);
        _level.Text=string.Format(NavalSetupText.Get("Lv. {0}",ClientLanguages.Current),target.Level);
        _health.Text=string.Format(NavalSetupText.Get("Hull: {0:N0} / {1:N0}",ClientLanguages.Current),target.Health,target.MaximumHealth);
        for(var index=0;index<effects.Length;index++)
        {
            var effect=effects[index];
            if(!_statuses.TryGetValue(effect.Kind,out var row))
            {
                var panel=new Panel(this,"CannonStatus"+effect.Kind) {BackgroundColor=new Color(240,65,32,24),MouseInputEnabled=true,KeyboardInputEnabled=false};
                var icon=new ImagePanel(panel,"Icon") {MaintainAspectRatio=true,ShouldDrawBackground=false,MouseInputEnabled=false};
                icon.SetBounds(2,2,26,26);
                var timer= new Label(panel,"Remaining") {AutoSizeToContents=false,Font=Skin.DefaultFont,FontSize=9,TextColorOverride=new Color(255,240,220,165),TextAlign=Pos.Center,MouseInputEnabled=false};
                timer.SetBounds(0,28,30,14);
                _statuses[effect.Kind]=row=(panel,icon,timer);
            }
            var remaining=NavalWorld.StatusRemaining(target.Id,effect);
            row.Panel.SetBounds(12+index%columns*36,77+index/columns*46,30,43);row.Panel.Show();
            row.Icon.Texture=NavalPresentation.SafeAsset(effect.Icon) && !string.IsNullOrEmpty(effect.Icon)
                ?GameContentManager.Current.GetTexture(TextureType.Spell,effect.Icon):null;
            row.Timer.Text=((remaining+999)/1000).ToString();
            row.Panel.SetToolTipText(NavalCannonStatusText.Tooltip(effect,remaining,ClientLanguages.Current));
        }
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        if(_target==null) return;
        var renderer=skin.Renderer;var previous=renderer.DrawColor;
        void Fill(int x,int y,int width,int height,Color color)
        {renderer.DrawColor=color;renderer.DrawFilledRect(new Rectangle(x,y,width,height));}
        Fill(0,0,Width,Height,new Color(245,190,147,64));
        Fill(2,2,Width-4,Height-4,new Color(242,17,28,38));
        // Angular gold frame around the level badge, matching the game's pixel interface.
        Fill(6,15,48,47,new Color(255,170,132,61));Fill(9,18,42,41,new Color(255,47,26,31));
        Fill(64,33,Width-76,13,new Color(255,93,74,44));Fill(65,34,Width-78,11,new Color(255,44,27,30));
        var ratio=Math.Clamp(_target.Health/(double)Math.Max(1,_target.MaximumHealth),0,1);
        var filled=(int)((Width-78)*ratio);
        if(filled>0)
        {
            Fill(65,34,filled,11,new Color(255,73,169,91));
            Fill(65,34,filled,3,new Color(255,141,208,121));
        }
        renderer.DrawColor=previous;
    }
}
