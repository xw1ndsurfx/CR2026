using System.Drawing;
using System.Windows.Forms;
using Intersect.Editor.Content;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Editor.Forms.Editors.Events;

internal sealed class PokerEffectsDialog : Form
{
    private sealed record AnimationChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly PokerEffectSettings _target;
    private readonly Dictionary<string, ComboBox> _animations = new();
    private readonly Dictionary<string, ComboBox> _sounds = new();

    public PokerEffectsDialog(PokerEffectSettings target)
    {
        _target = target;
        Text = "Poker - sounds and animations";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 610);
        MinimumSize = new Size(650, 480);
        BackColor = Color.FromArgb(45,45,48); ForeColor = Color.Gainsboro;

        var root = new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(12), ColumnCount=3, AutoScroll=true };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        root.Controls.Add(new Label { Text="Action", AutoSize=true },0,0);
        root.Controls.Add(new Label { Text="Animation", AutoSize=true },1,0);
        root.Controls.Add(new Label { Text="Sound (.wav)", AutoSize=true },2,0);

        Add(root,1,"Deal / distribution", Guid.Empty, target.DealSound, false, "deal");
        Add(root,2,"Check", target.CheckAnimationId, target.CheckSound, true, "check");
        Add(root,3,"Call", target.CallAnimationId, target.CallSound, true, "call");
        Add(root,4,"Raise / Bet", target.RaiseAnimationId, target.RaiseSound, true, "raise");
        Add(root,5,"Fold", target.FoldAnimationId, target.FoldSound, true, "fold");
        Add(root,6,"All-in", target.AllInAnimationId, target.AllInSound, true, "allin");
        Add(root,7,"Win", Guid.Empty, target.WinSound, false, "win");
        Add(root,8,"Poker level-up", target.LevelUpAnimationId, target.LevelUpSound, true, "levelup");

        var hint = new Label { AutoSize=true, MaximumSize=new Size(700,0),
            Text="Dealing and victory animations remain on the main Poker dialog. Action animations are local screen effects. " +
                 "An Intersect animation can also contain its own sound; the optional WAV cue here is additive." };
        root.Controls.Add(hint,0,9); root.SetColumnSpan(hint,3);

        var buttons = new FlowLayoutPanel { Dock=DockStyle.Bottom, AutoSize=true, FlowDirection=FlowDirection.RightToLeft, Padding=new Padding(8) };
        var cancel = new Button { Text="Cancel", AutoSize=true, DialogResult=DialogResult.Cancel };
        var save = new Button { Text="Save", AutoSize=true };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        Controls.Add(root); Controls.Add(buttons); AcceptButton=save; CancelButton=cancel;
        save.Click += (_,_) =>
        {
            _target.CheckAnimationId = Animation("check");
            _target.CallAnimationId = Animation("call");
            _target.RaiseAnimationId = Animation("raise");
            _target.FoldAnimationId = Animation("fold");
            _target.AllInAnimationId = Animation("allin");
            _target.LevelUpAnimationId = Animation("levelup");
            _target.DealSound = Sound("deal"); _target.CheckSound = Sound("check"); _target.CallSound = Sound("call");
            _target.RaiseSound = Sound("raise"); _target.FoldSound = Sound("fold"); _target.AllInSound = Sound("allin");
            _target.WinSound = Sound("win"); _target.LevelUpSound = Sound("levelup");
            if (!_target.IsValid()) { MessageBox.Show(this,"One of the sound filenames is invalid.","Poker effects",MessageBoxButtons.OK,MessageBoxIcon.Warning); return; }
            DialogResult=DialogResult.OK; Close();
        };
    }

    private void Add(TableLayoutPanel root,int row,string label,Guid animation,string sound,bool allowAnimation,string key)
    {
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label { Text=label, AutoSize=true, Anchor=AnchorStyles.Left, Margin=new Padding(3,9,3,9) },0,row);
        var a = AnimationPicker(animation); a.Enabled=allowAnimation; _animations[key]=a; root.Controls.Add(a,1,row);
        var s = SoundPicker(sound); _sounds[key]=s; root.Controls.Add(s,2,row);
    }
    private static ComboBox AnimationPicker(Guid id)
    {
        var box=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Dock=DockStyle.Fill };
        box.Items.Add(new AnimationChoice(Guid.Empty,"None / Aucune"));
        foreach(var a in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase))
            box.Items.Add(new AnimationChoice(a.Id,a.Name));
        var selected=box.Items.Cast<AnimationChoice>().FirstOrDefault(a=>a.Id==id) ?? new AnimationChoice(id,"Missing animation: "+id);
        if(!box.Items.Contains(selected)) box.Items.Add(selected); box.SelectedItem=selected; return box;
    }
    private static ComboBox SoundPicker(string value)
    {
        var box=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Dock=DockStyle.Fill };
        box.Items.Add("");
        foreach(var s in GameContentManager.SmartSortedSoundNames ?? Array.Empty<string>()) box.Items.Add(s);
        if(!string.IsNullOrEmpty(value) && !box.Items.Contains(value)) box.Items.Add(value);
        box.SelectedItem=box.Items.Contains(value) ? value : ""; return box;
    }
    private Guid Animation(string key)=>_animations.TryGetValue(key,out var box)&&box.SelectedItem is AnimationChoice a?a.Id:Guid.Empty;
    private string Sound(string key)=>_sounds.TryGetValue(key,out var box)?box.SelectedItem?.ToString()??"":"";
}
