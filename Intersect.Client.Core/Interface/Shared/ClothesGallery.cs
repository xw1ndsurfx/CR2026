using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.General;
namespace Intersect.Client.Interface.Shared;
internal sealed class ClothesGallery:Base
{
    private readonly Dictionary<string,LabeledComboBox> _choices;
    private readonly ScrollControl _list;
    private readonly TextBox _search;
    private string _category;
    internal ClothesGallery(Base parent,Dictionary<string,LabeledComboBox> choices,Action close):base(parent,"ClothesGallery")
    {
        _choices=choices;_category=choices.Keys.First();MouseInputEnabled=true;
        var i=0;
        foreach(var category in choices.Keys)
        {
            var name=category;
            var button=new Button(this,"Category"+category){Text=ClientLanguages.Text(category),Font=Skin.DefaultFont,FontSize=10};
            button.SetBounds(i++*105,0,102,28);button.Clicked+=(_,_)=>{_category=name;Rebuild();};
        }
        _search=new TextBox(this,"ClothesSearch"){Text=""};_search.Font=Skin.DefaultFont;_search.FontSize=10;_search.SetBounds(0,34,430,28);
        _search.SetToolTipText(ClientLanguages.Text("Search clothes by name"));_search.TextChanged+=(_,_)=>Rebuild();
        _list=new ScrollControl(this,"ClothesItems"){OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};
        var back=new Button(this,"BackToAppearance"){Text=ClientLanguages.Text("Back"),Font=Skin.DefaultFont,FontSize=10};
        back.Clicked+=(_,_)=>close();_back=back;
    }
    private readonly Button _back;
    internal void Refresh()
    {
        _search.Width=Width;_list.SetBounds(0,68,Width,Math.Max(80,Height-108));_back.SetBounds(0,Height-34,Width,30);
        Rebuild();Show();BringToFront();
    }
    private void Rebuild()
    {
        _list.DeleteAll();var columnWidth=Math.Max(140,(_list.Width-24)/2);var index=0;
        foreach(var item in _choices[_category].Items.Where(item=>(item.Text??"").Contains((_search.Text??"").Trim(),StringComparison.OrdinalIgnoreCase)))
        {
            var style=item.UserData as string??"";
            var card=new Button(_list,"ClothesCard"+index){Text="",Font=Skin.DefaultFont,FontSize=10};
            card.SetBounds(index%2*columnWidth,index/2*190,columnWidth-6,184);
            var image=new ImagePanel(card,"ClothesPreview"){MouseInputEnabled=false,MaintainAspectRatio=true};
            image.SetBounds(10,8,columnWidth-26,140);
            if(style.Length>0 && GameContentManager.Current.GetTexture(TextureType.Paperdoll,style) is {} texture)
            {
                var width=texture.Width/Math.Max(1,Options.Instance.Sprites.NormalFrames);
                var height=texture.Height/Math.Max(1,Options.Instance.Sprites.Directions);
                var crop=SpriteVisibleBounds.Get(texture,new FloatRect(0,0,width,height));
                image.Texture=texture;image.SetTextureRect(crop.X,crop.Y,Math.Max(1,crop.Width),Math.Max(1,crop.Height));
            }
            var caption=new Label(card,"ClothesName"){Text=ClientLanguages.ContentText(item.Text??""),Font=Skin.DefaultFont,FontSize=9,MouseInputEnabled=false,
                AutoSizeToContents=false,WrappingBehavior=Intersect.Client.Framework.Gwen.WrappingBehavior.Wrapped};
            caption.SetBounds(8,151,columnWidth-24,30);
            card.Clicked+=(_,_)=>_choices[_category].SelectByUserData(style);
            index++;
        }
        _list.SetInnerSize(Math.Max(1,_list.Width-24),(index+1)/2*190);_list.UpdateScrollBars();
    }
}
