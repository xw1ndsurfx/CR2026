using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Interface.Game.Hotbar;

/// <summary>Small code-drawn pixel icons; no external asset or unverified cannon sheet is assumed.</summary>
internal sealed class NavalActionButton(Base parent,string name) : Button(parent,name)
{
    internal ShipAction NavalAction { get; set; }
    internal string IconFilename {get;set;}="";
    internal int ReloadMilliseconds { get; set; }
    internal static string[] Glyph(ShipAction action)
    {
        string[] cannon=["           "," SSSSSSS   ","MDDDDDDSS  ","MDDDDDDDDSS"," SSSSSSSSS ","   BBBBB   ","  GGG GGG  ","  GGG GGG  "];
        if(action==ShipAction.PortCannons) return cannon;
        if(action==ShipAction.StarboardCannons) return cannon.Select(row=>new string(row.Reverse().ToArray())).ToArray();
        return action switch
        {
            ShipAction.Cargo=>[" GGGGGGGGG "," G B G B G "," G B G B G "," GGGGGGGGG "," G B G B G "," G B G B G "," GGGGGGGGG ","           "],
            ShipAction.Anchor=>["    GGG    ","    G G    ","    GGG    ","     G     "," GGGGGGGGG "," G   G   G ","  GG G GG  ","    GGG    "],
            ShipAction.LeaveHelm=>[" BBBB      "," B  B  G   "," B  B  GG  "," B GGGGGGG "," B  B  GG  "," B  B  G   "," BBBB      "," SSSSSSSSS "],
            _=>["           ","    SSS    ","   S   S   ","  S     S  ","  S     S  ","   S   S   ","    SSS    ","           "]
        };
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        base.Render(skin);
        if(ReloadMilliseconds>0) return;
        if(NavalPresentation.SafeAsset(IconFilename) && IconFilename.Length>0 && GameContentManager.Current.GetTexture(TextureType.Spell,IconFilename) is {} icon)
        {
            var size=Math.Min(Width-8,Height-14);skin.Renderer.DrawTexturedRect(icon,new Rectangle((Width-size)/2,3,size,size),IsDisabled?new Color(100,255,255,255):Color.White);return;
        }
        var glyph=Glyph(NavalAction);var scale=Math.Max(1,Math.Min(Width/13,(Height-12)/8));
        var ox=(Width-11*scale)/2;var oy=3;
        var previous=skin.Renderer.DrawColor;
        for(var y=0;y<glyph.Length;y++) for(var x=0;x<glyph[y].Length;x++)
        {
            var pixel=glyph[y][x];if(pixel==' ') continue;
            var color=pixel switch { 'G'=>new Color(255,225,185,65),'B'=>new Color(255,45,95,145),
                'D'=>new Color(255,80,90,105),'M'=>new Color(255,20,25,35),_=>new Color(255,190,205,215) };
            if(IsDisabled) color=new Color(100,color.R,color.G,color.B);
            skin.Renderer.DrawColor=color;
            skin.Renderer.DrawFilledRect(new Rectangle(ox+x*scale,oy+y*scale,scale,scale));
        }
        skin.Renderer.DrawColor=previous;
    }
}
