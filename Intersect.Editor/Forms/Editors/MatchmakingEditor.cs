using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

internal sealed class MatchmakingEditor : UserControl
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly MatchmakingConfiguration _config;
    private readonly MatchmakingEditorChoice[] _catalog;
    private readonly ListBox _entries = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly Panel _detail = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private static string T(string key) => QualityOfLifeText.Get(key, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private static TableLayoutPanel Grid() { var g = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) }; g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); return g; }
    private static void Row(TableLayoutPanel grid, string name, Control control)
    {
        var row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.Controls.Add(new Label { Text = T(name), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Name = name.Replace(" ", ""); control.Dock = DockStyle.Top; control.Margin = new Padding(3, 6, 3, 3); grid.Controls.Add(control, 1, row);
    }
    private static void Number(TableLayoutPanel g, string label, int value, int min, int max, Action<int> changed)
    { var n = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value,min,max) }; n.ValueChanged += (_,_) => changed((int)n.Value); Row(g,label,n); }
    private static void Toggle(TableLayoutPanel g,string label,bool value,Action<bool> changed)
    { var c=new CheckBox {Checked=value};c.CheckedChanged+=(_,_)=>changed(c.Checked);Row(g,label,c); }
    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 420 };
    private static void Asset(TableLayoutPanel grid,string label,string value,bool sound,Action<string> changed)
    {
        var box=Combo();box.Items.Add("");
        if(sound)
        {
            var folder=Path.Combine("resources","sounds");SearchableChoices.SetResourceDirectory(box,folder);
            if(Directory.Exists(folder))foreach(var file in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Where(f=>Path.GetExtension(f).ToLowerInvariant() is ".wav" or ".ogg").Order())box.Items.Add(Path.GetRelativePath(folder,file).Replace('\\','/'));
        }
        else SearchableChoices.AddTextureNames(box,Content.GameContentManager.TextureType.Gui);
        if(!box.Items.Contains(value))box.Items.Add(value);box.SelectedItem=value;
        box.SelectedIndexChanged+=(_,_)=>changed(box.Text);Row(grid,label,box);
    }
    internal MatchmakingEditor(MatchmakingConfiguration config,MatchmakingEditorChoice[] catalog)
    {
        _config=config;_catalog=catalog;Dock=DockStyle.Fill;
        var tabs=new TabControl{Dock=DockStyle.Fill};Controls.Add(tabs);
        var settings=new TabPage(T("Settings"));var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var grid=Grid();scroll.Controls.Add(grid);settings.Controls.Add(scroll);tabs.TabPages.Add(settings);
        Toggle(grid,"Enable matchmaking",config.Enabled,v=>config.Enabled=v);
        Number(grid,"Ready timeout (seconds)",config.ReadyTimeoutSeconds,15,180,v=>config.ReadyTimeoutSeconds=v);
        Number(grid,"Search timeout (seconds)",config.QueueTimeoutSeconds,60,3600,v=>config.QueueTimeoutSeconds=v);
        Number(grid,"Maximum queued players",config.MaximumQueuedPlayers,2,1024,v=>config.MaximumQueuedPlayers=v);
        Asset(grid,"Background image",config.BackgroundImage,false,v=>config.BackgroundImage=v);
        Asset(grid,"Icons atlas (3 × 2)",config.IconsImage,false,v=>config.IconsImage=v);
        Asset(grid,"Open sound",config.OpenSound,true,v=>config.OpenSound=v);
        Asset(grid,"Search sound",config.QueuedSound,true,v=>config.QueuedSound=v);
        Asset(grid,"Group found sound",config.FoundSound,true,v=>config.FoundSound=v);
        Asset(grid,"Ready sound",config.ReadySound,true,v=>config.ReadySound=v);
        Asset(grid,"Cancel sound",config.CancelledSound,true,v=>config.CancelledSound=v);
        Asset(grid,"Departure sound",config.DepartSound,true,v=>config.DepartSound=v);
        Asset(grid,"Error sound",config.ErrorSound,true,v=>config.ErrorSound=v);
        var activities=new TabPage(T("Activities"));tabs.TabPages.Add(activities);
        var split=new SplitContainer{Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,SplitterDistance=230,Size=new Size(760,480)};
        activities.Controls.Add(split);split.Panel1.Controls.Add(_entries);split.Panel2.Controls.Add(_detail);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=80,FlowDirection=FlowDirection.TopDown};split.Panel1.Controls.Add(actions);
        var add=new Button{Text=T("Add activity"),Width=205};var remove=new Button{Text=T("Remove activity"),Width=205};actions.Controls.Add(add);actions.Controls.Add(remove);
        add.Click+=(_,_)=>{if(config.Entries.Count>=256)return;var entry=new MatchmakingEntry();config.Entries.Add(entry);RefreshList(entry.Id);};
        remove.Click+=(_,_)=>{if(Selected() is {} entry){config.Entries.Remove(entry);RefreshList();}};
        _entries.SelectedIndexChanged+=(_,_)=>ShowEntry();RefreshList();
    }
    private MatchmakingEntry? Selected()=>_entries.SelectedItem is Choice c?_config.Entries.FirstOrDefault(e=>e.Id==c.Id):null;
    private void RefreshList(Guid id=default)
    {
        _entries.Items.Clear();foreach(var e in _config.Entries)
            _entries.Items.Add(new Choice(e.Id,(e.Enabled?"✓ ":"○ ")+(_catalog.FirstOrDefault(c=>c.Id==e.ContentId&&c.Activity==e.Activity)?.Name??T("Choose an activity"))));
        for(var i=0;i<_entries.Items.Count;i++)if(((Choice)_entries.Items[i]).Id==id)_entries.SelectedIndex=i;
        if(_entries.SelectedIndex<0&&_entries.Items.Count>0)_entries.SelectedIndex=0;
        if(_entries.Items.Count==0)ShowEntry();
    }
    private void ShowEntry()
    {
        foreach(Control c in _detail.Controls.Cast<Control>().ToArray())c.Dispose();
        if(Selected() is not {} entry)return;
        var grid=Grid();_detail.Controls.Add(grid);
        Toggle(grid,"Enabled",entry.Enabled,v=>{entry.Enabled=v;});
        var kind=Combo();kind.Items.Add(T("Dungeon"));kind.Items.Add(T("Naval expedition"));kind.SelectedIndex=(int)entry.Activity;Row(grid,"Activity type",kind);
        kind.SelectedIndexChanged+=(_,_)=>{entry.Activity=(MatchmakingActivity)kind.SelectedIndex;entry.ContentId=Guid.Empty;entry.EntryMapId=Guid.Empty;ShowEntry();};
        var content=Combo();content.Items.Add(new Choice(Guid.Empty,T("Choose an activity")));
        foreach(var c in _catalog.Where(c=>c.Activity==entry.Activity).OrderBy(c=>c.Name))content.Items.Add(new Choice(c.Id,c.Name));
        if(entry.ContentId!=Guid.Empty&&!content.Items.Cast<Choice>().Any(c=>c.Id==entry.ContentId))content.Items.Add(new Choice(entry.ContentId,T("Missing activity")));
        content.SelectedItem=content.Items.Cast<Choice>().First(c=>c.Id==entry.ContentId);Row(grid,"Activity",content);
        content.SelectedIndexChanged+=(_,_)=>{entry.ContentId=((Choice)content.SelectedItem!).Id;var c=_catalog.FirstOrDefault(c=>c.Id==entry.ContentId&&c.Activity==entry.Activity);if(c!=null)entry.TargetPlayers=Math.Clamp(entry.TargetPlayers,c.MinimumPlayers,c.MaximumPlayers);entry.EntryMapId=Guid.Empty;RefreshList(entry.Id);};
        Number(grid,"Target players",entry.TargetPlayers,1,100,v=>entry.TargetPlayers=v);
        Number(grid,"Maximum level difference (0 = any)",entry.MaximumLevelDifference,0,1000,v=>entry.MaximumLevelDifference=v);
        Asset(grid,"Activity image",entry.Image,false,v=>entry.Image=v);
        if(entry.Activity==MatchmakingActivity.Dungeon)
        {
            var map=Combo();map.Items.Add(new Choice(Guid.Empty,T("Choose a map")));
            var allowed=_catalog.FirstOrDefault(c=>c.Id==entry.ContentId&&c.Activity==entry.Activity)?.MapIds??[];
            foreach(var m in MapList.OrderedMaps.Where(m=>allowed.Contains(m.MapId)))map.Items.Add(new Choice(m.MapId,m.Name));
            if(entry.EntryMapId!=Guid.Empty&&!map.Items.Cast<Choice>().Any(c=>c.Id==entry.EntryMapId))map.Items.Add(new Choice(entry.EntryMapId,T("Missing map")));
            map.SelectedItem=map.Items.Cast<Choice>().First(c=>c.Id==entry.EntryMapId);map.SelectedIndexChanged+=(_,_)=>entry.EntryMapId=((Choice)map.SelectedItem!).Id;Row(grid,"Dungeon entry map",map);
            Number(grid,"Entry X",entry.EntryX,0,Options.Instance.Map.MapWidth-1,v=>entry.EntryX=v);
            Number(grid,"Entry Y",entry.EntryY,0,Options.Instance.Map.MapHeight-1,v=>entry.EntryY=v);
            var dir=Combo();var dirs=Enum.GetValues<WarpDirection>();foreach(var d in dirs)dir.Items.Add(T(d.ToString()));dir.SelectedIndex=Array.IndexOf(dirs,entry.EntryDirection);dir.SelectedIndexChanged+=(_,_)=>entry.EntryDirection=dirs[dir.SelectedIndex];Row(grid,"Entry direction",dir);
        }
        var help=new Label{Text=T("Target players must respect the activity limits. Dungeon entry must be a walkable tile on one of its maps. Naval crews depart together from stopped ships at the same port."),AutoSize=true,MaximumSize=new Size(450,0),Padding=new Padding(4)};
        var row=grid.RowCount++;grid.Controls.Add(help,0,row);grid.SetColumnSpan(help,2);
    }
}
