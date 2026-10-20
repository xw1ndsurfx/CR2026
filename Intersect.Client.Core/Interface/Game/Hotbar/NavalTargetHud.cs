using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Localization;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game.Hotbar;

/// <summary>Compact canvas-level combat target. No mouse capture and no changes to normal entity targeting.</summary>
internal sealed class NavalTargetHud : Base
{
    private readonly Label _name,_health,_level;
    private ShipSnapshot? _target;
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
        if(target==null) return;
        var width=Math.Max(200,Math.Min(380,(Parent?.Width??800)-24));
        SetBounds(Math.Max(8,((Parent?.Width??800)-width)/2),22,width,76);
        _name.SetBounds(64,7,width-74,24);_health.SetBounds(64,47,width-74,21);_level.SetBounds(7,24,46,24);
        EventPanelText.Fit(_name,NavalWorld.EnemyName(target),12);
        _level.Text=string.Format(NavalSetupText.Get("Lv. {0}",ClientLanguages.Current),target.Level);
        _health.Text=string.Format(NavalSetupText.Get("Hull: {0:N0} / {1:N0}",ClientLanguages.Current),target.Health,target.MaximumHealth);
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
