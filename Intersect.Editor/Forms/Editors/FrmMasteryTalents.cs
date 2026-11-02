using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.GameObjects;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmMasteryTalents : Form
{
    private sealed record SpellChoice(Guid Id, string Name) { public override string ToString() => Name; }
    public MasteryCategory Result { get; }
    private readonly ListBox _nodes = new() { Dock = DockStyle.Fill, DisplayMember = "Name" };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly ComboBox[] _spells = new ComboBox[5];
    private readonly TableLayoutPanel _rankPanel=new() { Dock=DockStyle.Bottom,Height=158,ColumnCount=2 };
    private readonly TableLayoutPanel _skillAnimations=new() { Dock=DockStyle.Bottom,Height=80,ColumnCount=2 };
    private readonly ComboBox[] _animations=new ComboBox[2];
    private sealed record AnimationChoice(Guid Id,string Name) { public override string ToString()=>Name; }
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 56 };
    private bool _refreshing;
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmMasteryTalents(MasteryCategory category)
    {
        System.ComponentModel.TypeDescriptor.AddAttributes(typeof(WeaponSkillDefinition),
            new System.ComponentModel.TypeConverterAttribute(typeof(WeaponSkillEditorConverter)));
        Result = JsonSerializer.Deserialize<MasteryCategory>(JsonSerializer.Serialize(category))!;
        Text = T("Mastery talent tree"); Size = new(900, 620); MinimumSize = new(760, 480);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        void Button(string name, Action action) { var button = new Button { Text = T(name), AutoSize = true }; button.Click += (_, _) => action(); tools.Controls.Add(button); }
        Button("New talent", () => {
            var key = "talent-" + new string(Guid.NewGuid().ToString("N")[..8].Select(c => char.IsDigit(c) ? (char)('a' + c - '0') : c).ToArray());
            Result.Talents = [..Result.Talents, new MasteryTalent { Key = key }]; RefreshNodes();
        });
        Button("Weapon talent templates", () => { if (Result.Talents.Length == 0) { Result.Talents = MasteryTalent.WeaponDefaults(Result.Key, Result.Cap); RefreshNodes(); } });
        Button("Delete", () => { if (_nodes.SelectedItem is MasteryTalent talent) { Result.Talents = Result.Talents.Where(t => t != talent).ToArray(); RefreshNodes(); } });
        Button("Save", () => { if (!MasteryRules.ValidTree(Result)) { _status.Text = T("Invalid talent ranks, prerequisites or spell mappings."); return; } DialogResult = DialogResult.OK; Close(); });
        Button("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); });
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(860, 480), SplitterDistance = 210 };
        split.Panel1.Controls.Add(_nodes); split.Panel2.Controls.Add(_properties);
        var ranks = _rankPanel;
        ranks.ColumnStyles.Add(new(SizeType.Absolute, 120)); ranks.ColumnStyles.Add(new(SizeType.Percent, 100));
        var choices = new[] { new SpellChoice(Guid.Empty, T("No spell")) }.Concat(SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>().OrderBy(s => s.Name).Select(s => new SpellChoice(s.Id, s.Name))).ToArray();
        for (var i = 0; i < 5; i++)
        {
            ranks.Controls.Add(new Label { Text = string.Format(T("Rank {0} spell"), i + 1), AutoSize = true }, 0, i);
            var combo = _spells[i] = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Name = "TalentRankSpell" + i };
            combo.Items.AddRange(choices); ranks.Controls.Add(combo, 1, i);
            combo.SelectedIndexChanged += (_, _) => {
                if (_refreshing || _nodes.SelectedItem is not MasteryTalent talent) return;
                var selected = _spells.Take(talent.MaxRank).Select(c => (c.SelectedItem as SpellChoice)?.Id ?? Guid.Empty).ToArray();
                talent.RankSpellIds = selected.All(id => id == Guid.Empty) ? [] : selected; _properties.Refresh();
            };
        }
        _skillAnimations.ColumnStyles.Add(new(SizeType.AutoSize));_skillAnimations.ColumnStyles.Add(new(SizeType.Percent,100));
        var animationChoices=new[]{new AnimationChoice(Guid.Empty,T("None"))}.Concat(
            Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Lookup.Values.OfType<Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor>()
                .OrderBy(a=>a.Name).Select(a=>new AnimationChoice(a.Id,a.Name))).ToArray();
        for(var i=0;i<2;i++)
        {
            var index=i;
            _skillAnimations.Controls.Add(new Label { Text=T(i==0?"Skill animation":"Impact animation"),AutoSize=true,Margin=new Padding(3,6,12,3) },0,i);
            var combo=_animations[i]=new ComboBox { Name=i==0?"WeaponSkillAnimation":"WeaponSkillImpactAnimation",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };
            combo.Items.AddRange(animationChoices);_skillAnimations.Controls.Add(combo,1,i);
            combo.SelectedIndexChanged+=(_,_)=>
            {
                if(_refreshing || _nodes.SelectedItem is not MasteryTalent talent)return;
                var id=(combo.SelectedItem as AnimationChoice)?.Id??Guid.Empty;
                if(index==0)talent.Skill.AnimationId=id;else talent.Skill.HitAnimationId=id;
            };
        }
        split.Panel2.Controls.Add(_skillAnimations);split.Panel2.Controls.Add(ranks); Controls.Add(split); Controls.Add(tools); Controls.Add(_status);
        _status.Text = T("Mapped spells require this talent and a compatible equipped weapon. Leave all ranks empty for a passive talent.");
        _nodes.SelectedIndexChanged += (_, _) => SelectNode();
        _properties.PropertyValueChanged += (_, _) => { SelectNode(); _nodes.Refresh(); };
        RefreshNodes();
    }
    private void RefreshNodes() { _nodes.DataSource = null; _nodes.DataSource = Result.Talents; }
    private void SelectNode()
    {
        _properties.SelectedObject = _nodes.SelectedItem; _refreshing = true;
        var selected=_nodes.SelectedItem as MasteryTalent;
        _rankPanel.Visible=selected?.RankSpellIds.Length>0;
        _skillAnimations.Visible=selected?.Skill.Enabled==true;
        _status.Text=T(selected?.Skill.Enabled==true?"Weapon skills use animations and their own cooldowns, without learning a spell.":
            selected?.RankSpellIds.Length>0?"Mapped spells require this talent and a compatible equipped weapon. Leave all ranks empty for a passive talent.":
            "Passive talent bonuses apply while its weapon is equipped.");
        for(var i=0;i<2;i++)
        {
            var id=i==0?selected?.Skill.AnimationId??Guid.Empty:selected?.Skill.HitAnimationId??Guid.Empty;
            var choice=_animations[i].Items.OfType<AnimationChoice>().FirstOrDefault(a=>a.Id==id);
            if(choice==null) { choice=new(id,id.ToString());_animations[i].Items.Add(choice); }
            _animations[i].SelectedItem=choice;
        }
        for (var i = 0; i < 5; i++)
        {
            var talent = _nodes.SelectedItem as MasteryTalent;
            _spells[i].Enabled = talent != null && i < talent.MaxRank;
            var id = talent?.RankSpellIds.ElementAtOrDefault(i) ?? Guid.Empty;
            _spells[i].SelectedItem = _spells[i].Items.OfType<SpellChoice>().FirstOrDefault(s => s.Id == id) ?? _spells[i].Items[0];
        }
        _refreshing = false;
    }
}

public sealed class WeaponSkillEditorConverter : System.ComponentModel.ExpandableObjectConverter
{
    public override object? ConvertTo(System.ComponentModel.ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture,
        object? value, Type destinationType) => destinationType==typeof(string) && value is WeaponSkillDefinition skill
        ? QualityOfLifeText.Get(skill.Enabled?"Active":"Passive",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
        : base.ConvertTo(context,culture,value,destinationType);
}
