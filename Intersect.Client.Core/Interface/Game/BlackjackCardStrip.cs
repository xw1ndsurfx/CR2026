using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
namespace Intersect.Client.Interface.Game;

internal sealed class BlackjackCardStrip
{
    private readonly ImagePanel[] _images=new ImagePanel[22];
    private readonly Label[] _labels=new Label[22];
    private readonly Dictionary<string,IGameTexture?> _cache=new(StringComparer.Ordinal);
    public BlackjackCardStrip(Base parent,string name)
    {
        for(var i=0;i<22;i++)
        {
            _images[i]=new ImagePanel(parent,name+"Art"+i){IsHidden=true,ShouldDrawBackground=false,MouseInputEnabled=false,KeyboardInputEnabled=false};
            _labels[i]=new Label(parent,name+"Fallback"+i){IsHidden=true,Font=parent.Skin.DefaultFont,FontSize=12,AutoSizeToContents=false,
                MouseInputEnabled=false,KeyboardInputEnabled=false,TextColorOverride=Color.White};
        }
    }
    public IGameTexture? Texture(string file)
    {if(!_cache.TryGetValue(file,out var t)){t=GameContentManager.Current?.GetTexture(TextureType.Misc,file);_cache[file]=t;}return t;}
    public IGameTexture? Back(int id)=>Texture(PokerCardAssets.BackFileName(id))??Texture(PokerCardAssets.Back)??Texture(PokerCardAssets.LegacyBack);
    public void Update(int[] cards,bool hole,int back,PokerSceneLayout layout,int x,int y,int width,int height,bool placeholders=false)
    {
        var count=placeholders?2:Math.Min(22,cards.Length+(hole?1:0));
        var cardWidth=Math.Min(48,height*3/4);var step=count>1?Math.Min(cardWidth+6,(width-cardWidth)/(float)(count-1)):0;
        for(var i=0;i<22;i++)
        {
            if(i>=count){_images[i].IsHidden=_labels[i].IsHidden=true;continue;}
            var hidden=placeholders || i>=cards.Length;
            var file=hidden?null:PokerCardAssets.FileNameFor(cards[i]);
            var t=hidden?Back(back):file==null?null:Texture(file);
            var r=layout.Rect(x+(int)(i*step),y,cardWidth,height);
            Fit(_images[i],t,r);_labels[i].IsHidden=t!=null;
            _labels[i].Text=hidden?"[??]":"["+(file?[..2]??"--")+"]";
            _labels[i].FontSize=layout.FontSize(12);
            _labels[i].SetBounds(r.X,r.Y+r.Height/3,r.Width+8,Math.Max(18,r.Height/2));
        }
    }
    public static void Fit(ImagePanel image,IGameTexture? texture,PokerSceneRect bounds)
    {
        image.Texture=texture;image.IsHidden=texture==null || texture.Width<1 || texture.Height<1;
        if(image.IsHidden || texture==null)return;
        image.ResetUVs();var scale=Math.Min(bounds.Width/(float)texture.Width,bounds.Height/(float)texture.Height);
        var w=Math.Max(1,(int)(texture.Width*scale));var h=Math.Max(1,(int)(texture.Height*scale));
        image.SetBounds(bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h);
    }
}
