using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Interface.Game.Hotbar;

internal sealed class NavalRewardHud : Base
{
    private readonly Label[] _lines=new Label[7];
    internal NavalRewardHud(Base parent):base(parent,nameof(NavalRewardHud))
    {
        MouseInputEnabled=KeyboardInputEnabled=false;
        for(var i=0;i<_lines.Length;i++) _lines[i]=new Label(this,"NavalRewardLine"+i) {
            AutoSizeToContents=false,MouseInputEnabled=false,TextAlign=Pos.Center,
            Font=GameContentManager.Current.GetFont(i==0?"sourcesansproblack":"sourcesanspro")??Skin.DefaultFont,
            FontSize=i==0?12:11,TextColorOverride=new Color(255,245,222,168)};
        Hide();
    }
    internal static string[] Format(NavalRewardReceipt reward,string language,Func<Guid,string> itemName)
    {
        string T(string source,params object[] values)=>string.Format(NavalSetupText.Get(source,language),values);
        var lines=new List<string> {reward.SailingExperience>0?T("Enemy sunk · Sailing +{0:N0} EXP",reward.SailingExperience):T("Enemy sunk")};
        if(reward.CharacterExperience>0)lines.Add(T("Character +{0:N0} EXP",reward.CharacterExperience));
        var items=reward.Cargo.GroupBy(c=>c.ItemId).Select(g=>(Id:g.Key,Quantity:g.Sum(c=>(long)c.Quantity))).ToArray();
        lines.AddRange(items.Take(3).Select(i=>i.Id==Guid.Empty?T("Sealed cargo secured: {0}",reward.Cargo.Count(c=>c.ItemId==Guid.Empty)):T("Cargo secured: {0} × {1:N0}",itemName(i.Id),i.Quantity)));
        if(items.Length>3) lines.Add(T("And {0} more item types in the hold.",items.Length-3));
        if(reward.ReplacedLots>0) lines.Add(T("Better cargo replaced {0} lower-quality lots.",reward.ReplacedLots));
        if(reward.DiscardedLots>0) lines.Add(T("Hold full · {0} lots could not be kept. Unload at Q.",reward.DiscardedLots));
        else if(reward.CargoFull) lines.Add(T("Hold full · Unload your cargo at Q."));
        return lines.Take(7).Select(s=>s.Replace('\u202f',' ').Replace('\u00a0',' ').Replace('×','x').Replace('·','-')).ToArray();
    }
    internal void Update(NavalRewardReceipt? reward,int anchorY,bool suppressed)
    {
        IsHidden=reward==null || suppressed;
        if(IsHidden) return;
        var lines=Format(reward!,ClientLanguages.Current,id=>ClientLanguages.ContentText(ItemDescriptor.Get(id)?.Name??NavalSetupText.Get("Unknown item",ClientLanguages.Current)));
        var width=Math.Max(220,Math.Min(560,(Parent?.Width??800)-24));var height=lines.Length*21+18;
        SetBounds(Math.Max(8,((Parent?.Width??800)-width)/2),Math.Max(106,anchorY-height),width,height);
        for(var i=0;i<_lines.Length;i++)
        {
            _lines[i].IsHidden=i>=lines.Length;
            if(i>=lines.Length) continue;
            _lines[i].SetBounds(12,8+i*21,width-24,21);
            EventPanelText.Fit(_lines[i],lines[i],i==0?12:11);
        }
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var renderer=skin.Renderer;var previous=renderer.DrawColor;
        renderer.DrawColor=new Color(245,190,147,64);renderer.DrawFilledRect(new Rectangle(0,0,Width,Height));
        renderer.DrawColor=new Color(240,17,28,38);renderer.DrawFilledRect(new Rectangle(2,2,Width-4,Height-4));
        renderer.DrawColor=previous;
    }
}
