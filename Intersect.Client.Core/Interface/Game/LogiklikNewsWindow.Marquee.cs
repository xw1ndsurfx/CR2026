using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using SkinBase=Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class LogiklikNewsWindow
{
    internal sealed class NewsTitleButton(Base parent,string name) : Button(parent,name)
    {
        private long? _hoverStarted;
        protected override void OnMouseLeft()
        {
            _hoverStarted=null;
            base.OnMouseLeft();
        }
        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            var now=Environment.TickCount64;
            if(!IsHovered || IsDisabledByTree) _hoverStarted=null;
            else _hoverStarted??=now;
            var available=Math.Max(1,Width-16);
            var x=TextWidth<=available?(Width-TextWidth)/2:Offset(Width,TextWidth,_hoverStarted.HasValue?now-_hoverStarted.Value:0);
            SetTextPosition(x,(Height-TextHeight)/2);
        }
        // Start at the title's beginning, then bring the complete title back from the right on every lap.
        internal static int Offset(int width,int textWidth,long elapsed)
        {
            if(elapsed<=600) return 8;
            var distance=(elapsed-600)*42d/1000;
            if(distance<=textWidth+8) return 8-(int)distance;
            return width-(int)((distance-textWidth-8)%Math.Max(1,width+textWidth));
        }
    }
}
