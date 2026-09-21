using System.Drawing;
using System.Windows.Forms;
using Intersect.Editor.Content;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Editor.Forms.Editors.Events;

internal sealed class PokerEffectsDraft
{
    internal readonly Dictionary<PokerEffectKind, Guid> Animations = new();
    internal readonly Dictionary<PokerEffectKind, string> Sounds = new();
    internal static PokerEffectsDraft From(StartMiniGameCommand c)
    {
        var d = new PokerEffectsDraft();
        d.Set(PokerEffectKind.Join, c.JoinAnimationId, c.JoinSound);
        d.Set(PokerEffectKind.Deal, c.DealAnimationId, c.DealSound);
        d.Set(PokerEffectKind.Check, c.CheckAnimationId, c.CheckSound);
        d.Set(PokerEffectKind.Call, c.CallAnimationId, c.CallSound);
        d.Set(PokerEffectKind.Raise, c.RaiseAnimationId, c.RaiseSound);
        d.Set(PokerEffectKind.Fold, c.FoldAnimationId, c.FoldSound);
        d.Set(PokerEffectKind.AllIn, c.AllInAnimationId, c.AllInSound);
        d.Set(PokerEffectKind.Victory, c.VictoryAnimationId, c.VictorySound);
        d.Set(PokerEffectKind.LevelUp, c.LevelUpAnimationId, c.LevelUpSound);
        return d;
    }
    private void Set(PokerEffectKind k, Guid a, string? s) { Animations[k] = a; Sounds[k] = s ?? string.Empty; }
    internal void ApplyTo(StartMiniGameCommand c)
    {
        c.JoinAnimationId=Animations[PokerEffectKind.Join]; c.JoinSound=Sounds[PokerEffectKind.Join];
        c.DealAnimationId=Animations[PokerEffectKind.Deal]; c.DealSound=Sounds[PokerEffectKind.Deal];
        c.CheckAnimationId=Animations[PokerEffectKind.Check]; c.CheckSound=Sounds[PokerEffectKind.Check];
        c.CallAnimationId=Animations[PokerEffectKind.Call]; c.CallSound=Sounds[PokerEffectKind.Call];
        c.RaiseAnimationId=Animations[PokerEffectKind.Raise]; c.RaiseSound=Sounds[PokerEffectKind.Raise];
        c.FoldAnimationId=Animations[PokerEffectKind.Fold]; c.FoldSound=Sounds[PokerEffectKind.Fold];
        c.AllInAnimationId=Animations[PokerEffectKind.AllIn]; c.AllInSound=Sounds[PokerEffectKind.AllIn];
        c.VictoryAnimationId=Animations[PokerEffectKind.Victory]; c.VictorySound=Sounds[PokerEffectKind.Victory];
        c.LevelUpAnimationId=Animations[PokerEffectKind.LevelUp]; c.LevelUpSound=Sounds[PokerEffectKind.LevelUp];
    }
}

internal sealed class PokerEffectsDialog : Form
{
    private sealed record Choice(Guid Id,string Name){public override string ToString()=>Name;}
    private readonly Dictionary<PokerEffectKind,ComboBox> _animations=new();
    private readonly Dictionary<PokerEffectKind,ComboBox> _sounds=new();
    public PokerEffectsDialog(PokerEffectsDraft draft)
    {
        Text="Poker - action animations and sounds"; StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(760,500); MinimumSize=new Size(650,420); BackColor=Color.FromArgb(45,45,48); ForeColor=Color.Gainsboro;
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=3,RowCount=PokerEffects.Count+2,AutoScroll=true};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        grid.Controls.Add(new Label{Text="Action",AutoSize=true},0,0);grid.Controls.Add(new Label{Text="Animation",AutoSize=true},1,0);
        grid.Controls.Add(new Label{Text="Extra sound (optional)",AutoSize=true},2,0);
        foreach(var kind in Enum.GetValues<PokerEffectKind>())
        {
            var row=(int)kind+1; var anim=AnimationPicker(draft.Animations.GetValueOrDefault(kind)); var sound=SoundPicker(draft.Sounds.GetValueOrDefault(kind,""));
            _animations[kind]=anim;_sounds[kind]=sound;
            grid.Controls.Add(new Label{Text=Label(kind),AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,8,3,8)},0,row);
            grid.Controls.Add(anim,1,row);grid.Controls.Add(sound,2,row);
        }
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true};var save=new Button{Text="Save",AutoSize=true};
        buttons.Controls.Add(cancel);buttons.Controls.Add(save);Controls.Add(grid);Controls.Add(buttons);CancelButton=cancel;AcceptButton=save;
        save.Click+=(_,_)=>{foreach(var kind in Enum.GetValues<PokerEffectKind>()){draft.Animations[kind]=((Choice)_animations[kind].SelectedItem!).Id;draft.Sounds[kind]=_sounds[kind].Text=="None"?"":_sounds[kind].Text;}DialogResult=DialogResult.OK;Close();};
    }
    private static string Label(PokerEffectKind k)=>k switch{PokerEffectKind.Join=>"Join table",PokerEffectKind.Deal=>"Deal cards",PokerEffectKind.Check=>"Check",PokerEffectKind.Call=>"Call",PokerEffectKind.Raise=>"Raise",PokerEffectKind.Fold=>"Fold",PokerEffectKind.AllIn=>"All-in",PokerEffectKind.Victory=>"Victory (winner)",PokerEffectKind.LevelUp=>"Poker level up",_=>k.ToString()};
    private static ComboBox AnimationPicker(Guid id){var p=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};p.Items.Add(new Choice(Guid.Empty,"None"));foreach(var a in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase))p.Items.Add(new Choice(a.Id,a.Name));var s=p.Items.Cast<Choice>().FirstOrDefault(x=>x.Id==id)??new Choice(id,"Missing: "+id);if(!p.Items.Contains(s))p.Items.Add(s);p.SelectedItem=s;return p;}
    private static ComboBox SoundPicker(string value){var p=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DropDownWidth=350};p.Items.Add("None");p.Items.AddRange(GameContentManager.SmartSortedSoundNames);p.SelectedItem=p.Items.Cast<object>().FirstOrDefault(x=>string.Equals(x.ToString(),value,StringComparison.OrdinalIgnoreCase))??"None";return p;}
}
