using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen;
using Intersect.Framework.Core.Naval;
using Rectangle=Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;

internal sealed class RepairPuzzleBoard : Base
{
    private readonly NavalManagementState _state;
    private readonly Func<bool> _rotated;
    private readonly List<Button> _tiles=[];
    internal RepairPuzzleBoard(Base parent,NavalManagementState state,Func<bool> rotated):base(parent,"RepairHullPuzzle")
    {
        _state=state;_rotated=rotated;
        for(var i=0;i<state.RepairCells.Length;i++)
        {
            var cell=i;
            var button=new Button(this,"HullCell"+i){Text="",ShouldDrawBackground=false};
            button.Clicked+=(_,_)=>{if(NavalRepairPuzzle.CanPlace(state.RepairCells,state.RepairKind,cell,_rotated()))
                NavalManagementWindow.Request(NavalManageAction.RepairInput,quantity:state.RepairRevision,token:state.RepairToken,value:cell|(_rotated()?65536:0));};
            _tiles.Add(button);
        }
    }
    protected override void Layout(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        base.Layout(skin);var w=Width/6;
        for(var i=0;i<_tiles.Count;i++)_tiles[i].SetBounds(i%6*w,i/6*36,w,36);
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var r=skin.Renderer;var previous=r.DrawColor;var w=Width/6;
        var hovered=_tiles.FindIndex(b=>b.IsHovered);
        var shape=hovered<0?[]:NavalRepairPuzzle.Shape(_state.RepairKind,_rotated()).Select(n=>n+hovered).ToArray();
        var fits=hovered>=0 && NavalRepairPuzzle.CanPlace(_state.RepairCells,_state.RepairKind,hovered,_rotated());
        try
        {
            for(var i=0;i<_state.RepairCells.Length;i++)
            {
                var x=i%6*w;var y=i/6*36;var cell=_state.RepairCells[i];
                r.DrawColor=cell==1?new Color(255,24,65,88):cell==2?new Color(255,180,126,64):new Color(255,72,45,28);
                r.DrawFilledRect(new Rectangle(x+1,y+1,w-2,34));
                if(cell!=1){r.DrawColor=new Color(255,105,67,36);r.DrawFilledRect(new Rectangle(x+4,y+11,w-8,2));r.DrawFilledRect(new Rectangle(x+8,y+25,w-14,2));}
                if(shape.Contains(i)){r.DrawColor=fits?new Color(160,75,215,130):new Color(160,235,65,65);r.DrawFilledRect(new Rectangle(x+2,y+2,w-4,32));}
            }
        }
        finally{r.DrawColor=previous;}
    }
}
