using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmNavalPatrolCreator : DarkForm
{
    private sealed record MapChoice(Guid Id,string Name) {public override string ToString()=>Name;}
    private readonly HerboniaPatrol _template;
    private readonly NavalConfiguration _settings;
    private readonly bool _canCreate;
    private readonly NumericUpDown _level=new(){Name="PatrolLevel",Minimum=1,Maximum=200};
    private readonly ComboBox _difficulty=new(){Name="PatrolDifficulty",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TextBox _name=new(){Name="PatrolName",MaxLength=32};
    private readonly ComboBox _map=new(){Name="PatrolMap",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly NumericUpDown _x=new(){Name="PatrolX",DecimalPlaces=2,Increment=.5m};
    private readonly NumericUpDown _y=new(){Name="PatrolY",DecimalPlaces=2,Increment=.5m};
    private readonly NumericUpDown _heading=new(){Name="PatrolHeading",DecimalPlaces=2,Maximum=359.99m};
    private readonly CheckBox _enabled=new(){Name="PatrolEnabled"};
    private readonly CheckBox _replace=new(){Name="ReplacePatrol"};
    private readonly DataGridView _preview=new(){Name="PatrolBalancePreview",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    private readonly Button _apply=new(){Name="GeneratePatrol",AutoSize=true};
    internal HerboniaPatrol? Result {get;private set;}
    internal bool ReplaceSelected=>_replace.Checked;
    private static string T(string text)=>FrmNavalConfiguration.T(text);

    public FrmNavalPatrolCreator(HerboniaPatrol? selected,NavalConfiguration settings,int width,int height,bool canCreate=true)
    {
        _template=JsonConvert.DeserializeObject<HerboniaPatrol>(JsonConvert.SerializeObject(selected??new HerboniaPatrol()))!;
        _settings=settings;_canCreate=canCreate;
        Text=T("Naval Patrol Auto Creator");Size=new(900,640);MinimumSize=new(760,520);StartPosition=FormStartPosition.CenterParent;
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,Padding=new(6),FlowDirection=FlowDirection.RightToLeft};
        var cancel=new Button{Text=T("Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};
        footer.Controls.AddRange([cancel,_apply]);CancelButton=cancel;AcceptButton=_apply;
        var split=new SplitContainer{Dock=DockStyle.Fill,Size=new(880,500),SplitterDistance=360};
        var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,Padding=new(8)};
        inputs.ColumnStyles.Add(new(SizeType.Percent,48));inputs.ColumnStyles.Add(new(SizeType.Percent,52));
        void Row(string text,Control field)
        {
            var row=inputs.RowCount++;inputs.RowStyles.Add(new(SizeType.Absolute,38));
            inputs.Controls.Add(new Label{Text=T(text),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            field.Dock=DockStyle.Fill;inputs.Controls.Add(field,1,row);
        }
        _level.Value=Math.Clamp(_template.Level,1,200);
        _difficulty.Items.AddRange(Enum.GetNames<NavalPatrolDifficulty>().Select(T).Cast<object>().ToArray());_difficulty.SelectedIndex=0;
        _name.Text=string.IsNullOrWhiteSpace(_template.Ship.Name)?T("Naval patrol"):_template.Ship.Name;
        _x.Maximum=Math.Max(0,width-.01m);_y.Maximum=Math.Max(0,height-.01m);
        _x.Value=Math.Clamp((decimal)_template.Ship.X,0,_x.Maximum);_y.Value=Math.Clamp((decimal)_template.Ship.Y,0,_y.Maximum);
        if(selected==null){_x.Value=width/2m;_y.Value=height/2m;}
        _heading.Value=Math.Clamp((decimal)ShipMovement.Normalize(_template.Ship.Heading),0,_heading.Maximum);
        var maps=MapList.OrderedMaps.Select(m=>new MapChoice(m.MapId,m.Name)).ToList();
        if(_template.Ship.MapId!=Guid.Empty && !maps.Any(m=>m.Id==_template.Ship.MapId))maps.Add(new(_template.Ship.MapId,_template.Ship.MapId.ToString()));
        _map.Items.AddRange(maps.Cast<object>().ToArray());
        _map.SelectedItem=maps.FirstOrDefault(m=>m.Id==_template.Ship.MapId)??maps.FirstOrDefault();
        _enabled.Checked=_template.Enabled;
        _replace.Enabled=selected!=null;_replace.Checked=selected!=null;
        Row("Level",_level);Row("Patrol difficulty",_difficulty);Row("Name",_name);Row("MapId",_map);
        Row("X",_x);Row("Y",_y);Row("Heading",_heading);Row("Enable patrol",_enabled);Row("Update selected patrol",_replace);
        var help=new Label{Text=T("Preview the generated stats. Artwork, loot and boarding settings are preserved. Save the naval section to publish your changes."),Dock=DockStyle.Fill};
        var helpRow=inputs.RowCount++;inputs.RowStyles.Add(new(SizeType.Absolute,112));inputs.Controls.Add(help,0,helpRow);inputs.SetColumnSpan(help,2);
        _preview.Columns.Add("Stat",T("Generated stat"));_preview.Columns.Add("Value",T("Generated value"));
        split.Panel1.Controls.Add(inputs);split.Panel2.Controls.Add(_preview);
        Controls.Add(split);Controls.Add(footer);
        _level.ValueChanged+=(_,_)=>RefreshPreview();_difficulty.SelectedIndexChanged+=(_,_)=>RefreshPreview();
        _map.SelectedIndexChanged+=(_,_)=>RefreshPreview();_replace.CheckedChanged+=(_,_)=>RefreshPreview();
        _name.TextChanged+=(_,_)=>RefreshPreview();_x.ValueChanged+=(_,_)=>RefreshPreview();_y.ValueChanged+=(_,_)=>RefreshPreview();
        _apply.Click+=(_,_)=>
        {
            if(!_apply.Enabled)return;
            var generated=BuildPatrol();if(!generated.IsValid())return;
            Result=generated;DialogResult=DialogResult.OK;Close();
        };
        MiniGameEditorTheme.Apply(this);RefreshPreview();
    }

    internal HerboniaPatrol BuildPatrol()
    {
        var patrol=JsonConvert.DeserializeObject<HerboniaPatrol>(JsonConvert.SerializeObject(_template))!;
        if(!_replace.Checked)patrol.Ship.Id=Guid.NewGuid();
        NavalPatrolAutoBalance.Apply(patrol,(int)_level.Value,(NavalPatrolDifficulty)_difficulty.SelectedIndex,_settings);
        patrol.Ship.Name=_name.Text.Trim();patrol.Ship.MapId=(_map.SelectedItem as MapChoice)?.Id??Guid.Empty;
        patrol.Ship.X=(double)_x.Value;patrol.Ship.Y=(double)_y.Value;patrol.Ship.Heading=(double)_heading.Value;
        patrol.Enabled=_enabled.Checked;
        return patrol;
    }

    private void RefreshPreview()
    {
        _preview.Rows.Clear();HerboniaPatrol p;
        try {p=BuildPatrol();}
        catch(ArgumentException)
        {
            _apply.Enabled=false;
            _preview.Rows.Add(T("Generated stat"),T("Set valid cannon and Sailing settings before using the creator."));return;
        }
        foreach(var (key,value) in new (string,object)[]{("Level",p.Level),("MaximumHealth",p.MaximumHealth),("CannonDamage",p.CannonDamage),
            ("SailingExperienceReward",p.SailingExperienceReward),("MaxSpeed",p.Ship.MaxSpeed),("ReverseSpeed",p.Ship.ReverseSpeed),
            ("Acceleration",p.Ship.Acceleration),("Drag",p.Ship.Drag),("TurnRate",p.Ship.TurnRate),("DetectionRange",p.DetectionRange),
            ("LeashRadius",p.LeashRadius),("PatrolRadius",p.PatrolRadius),("DockSafeRange",p.DockSafeRange),("RespawnSeconds",p.RespawnSeconds)})
            _preview.Rows.Add(T(key),value);
        _apply.Text=T(_replace.Checked?"Apply patrol balance":"Create patrol");
        _apply.Enabled=(_replace.Checked || _canCreate) && p.IsValid();
    }
}
