using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Interface.Game;

/// <summary>Atlas-aware fleet portrait, including clipping bands and mirrored poses.</summary>
internal sealed class NavalShipPreview : Base
{
    internal string Graphic {get;}
    internal int Frames {get;}
    internal NavalShipPreview(Base parent,string name,string graphic,int frames):base(parent,name)
    {
        Graphic=graphic;Frames=frames;MouseInputEnabled=KeyboardInputEnabled=false;
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        if(!NavalPresentation.SafeAsset(Graphic) || string.IsNullOrEmpty(Graphic))return;
        var texture=GameContentManager.Current.GetTexture(TextureType.Ocean,Graphic);
        var atlas=texture==null?null:NavalSpriteCatalog.Get(Graphic,texture.Width,texture.Height,Frames);
        if(texture==null || atlas==null)return;
        var frame=atlas.Frame(180,0,180);
        var scale=Math.Min(Math.Max(1,Width-8)/(float)frame.Width,Math.Max(1,Height-8)/(float)frame.Height);
        var left=(Width-frame.Width*scale)/2;var top=(Height-frame.Height*scale)/2;
        var anchorX=frame.FlipHorizontal?left+frame.Width*scale-frame.AnchorX*scale:left+frame.AnchorX*scale;
        var anchorY=top+frame.AnchorY*scale;
        var renderer=skin.Renderer;var previous=renderer.DrawColor;renderer.DrawColor=Color.White;
        foreach(var part in NavalSpriteGeometry.Parts(frame))
        {
            var dest=NavalSpriteGeometry.Destination(part,(float)anchorX,(float)anchorY,scale);
            var x=dest.Width<0?dest.X+dest.Width:dest.X;
            var u1=part.X/(float)texture.Width;var u2=(part.X+part.Width)/(float)texture.Width;
            if(dest.Width<0)(u1,u2)=(u2,u1);
            renderer.DrawTexturedRect(texture,new Rectangle((int)Math.Round(x),(int)Math.Round(dest.Y),
                Math.Max(1,(int)Math.Round(Math.Abs(dest.Width))),Math.Max(1,(int)Math.Round(dest.Height))),Color.White,
                u1,part.Y/(float)texture.Height,u2,(part.Y+part.Height)/(float)texture.Height);
        }
        renderer.DrawColor=previous;
    }
}
