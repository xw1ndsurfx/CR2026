using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;
namespace Intersect.Client.Interface.Game.Hotbar;

internal sealed class NavalSailingBar(Base parent,string name="NavalSailingExperience"):Label(parent,name)
{
    internal bool Hull {get;set;}
    internal float Value {get;set;}
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var previous=skin.Renderer.DrawColor;
        skin.Renderer.DrawColor=new Color(255,195,155,55);skin.Renderer.DrawFilledRect(new Rectangle(0,0,Width,Height));
        skin.Renderer.DrawColor=new Color(255,12,25,42);skin.Renderer.DrawFilledRect(new Rectangle(2,2,Width-4,Height-4));
        var fill=(int)((Width-4)*Math.Clamp(Value,0,1));
        for(var x=0;x<fill;x+=8)
        {
            var t=x/(float)Math.Max(1,Width-4);skin.Renderer.DrawColor=Hull ? new Color(255,(byte)(55+40*t),(byte)(130+65*t),75) : new Color(255,(byte)(35+35*t),(byte)(95+95*t),(byte)(150+75*t));
            skin.Renderer.DrawFilledRect(new Rectangle(2+x,2,Math.Min(8,fill-x),Height-4));
        }
        skin.Renderer.DrawColor=previous;base.Render(skin);
    }
}
