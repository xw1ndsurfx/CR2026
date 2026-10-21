using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
using DrawingColor=System.Drawing.Color;
namespace Intersect.Editor.Forms.Editors;

internal sealed class AchievementRewardItemSelector:UserControl
{
    private readonly PictureBox _icon=new(){SizeMode=PictureBoxSizeMode.Zoom,BackColor=DrawingColor.FromArgb(30,30,32)};
    private readonly Label _name=new(){AutoEllipsis=true};
    private readonly Label _detail=new(){AutoEllipsis=true};
    private readonly Button _choose=new(),_clear=new();
    private ItemDescriptor[] _items=[];
    internal Guid SelectedId {get;private set;}
    internal event EventHandler? SelectedIdChanged;
    internal static string T(string source)=>QualityOfLifeText.Get(source,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal AchievementRewardItemSelector()
    {
        Dock=DockStyle.Fill;BackColor=DrawingColor.FromArgb(37,37,38);ForeColor=DrawingColor.Gainsboro;
        _choose.Text=T("Browse rewards");_choose.Name="BrowseRewardItems";
        _clear.Text=T("Clear");_clear.Name="ClearRewardItem";
        _choose.Click+=(_,_)=>
        {
            using var dialog=new AchievementRewardItemDialog(_items,SelectedId);
            if(dialog.ShowDialog(FindForm())==DialogResult.OK)SetSelectedId(dialog.SelectedId);
        };
        _clear.Click+=(_,_)=>SetSelectedId(Guid.Empty);
        Controls.AddRange([_icon,_name,_detail,_choose,_clear]);
        foreach(var button in new[]{_choose,_clear}){button.FlatStyle=FlatStyle.Flat;button.BackColor=DrawingColor.FromArgb(65,65,69);button.FlatAppearance.BorderColor=DrawingColor.FromArgb(95,95,98);}
        Resize+=(_,_)=>Arrange();SetSelectedId(Guid.Empty);
    }
    private void Arrange()
    {
        _icon.SetBounds(8,8,64,64);_name.SetBounds(82,8,Math.Max(1,Width-214),24);
        _detail.SetBounds(82,34,Math.Max(1,Width-214),42);
        _choose.SetBounds(Math.Max(78,Width-124),8,116,30);_clear.SetBounds(Math.Max(78,Width-124),44,116,30);
    }
    internal void SetItems(IEnumerable<ItemDescriptor> items)=>_items=items.OrderBy(item=>item.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(item=>item.Id).ToArray();
    internal void SetSelectedId(Guid id)
    {
        var changed=SelectedId!=id;SelectedId=id;
        var item=ItemDescriptor.Get(id);
        _icon.Image?.Dispose();_icon.Image=RewardItemImages.Load(item);
        _name.Text=item?.Name??(id==Guid.Empty?T("No item reward"):string.Format(T("Missing item: {0}"),id));
        _detail.Text=item==null?"":T(item.ItemType.ToString())+" • "+string.Format(T("Required level: {0}"),item.MinimumLevel);
        _detail.ForeColor=DrawingColor.Silver;_clear.Enabled=id!=Guid.Empty;Arrange();
        if(changed)SelectedIdChanged?.Invoke(this,EventArgs.Empty);
    }
    protected override void Dispose(bool disposing){if(disposing){_icon.Image?.Dispose();_icon.Image=null;}base.Dispose(disposing);}
}

internal sealed class AchievementRewardItemDialog:DarkForm
{
    private readonly ItemDescriptor[] _items;
    private readonly List<ItemDescriptor?> _visible=[];
    private readonly Dictionary<Guid,Image?> _images=[];
    private readonly TextBox _search=new(){Dock=DockStyle.Fill,Name="RewardSearch"};
    private readonly DataGridView _grid=new(){Dock=DockStyle.Fill,Name="RewardItemList",VirtualMode=true,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoGenerateColumns=false,BackgroundColor=DrawingColor.FromArgb(37,37,38)};
    private readonly PictureBox _preview=new(){SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Left,Width=78};
    private readonly RichTextBox _description=new(){Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,ScrollBars=RichTextBoxScrollBars.Vertical,TabStop=false};
    private readonly Label _count=new(){Dock=DockStyle.Bottom,Height=24};
    private readonly Button _select=new(){Width=130,Height=32,Name="SelectRewardItem"};
    internal Guid SelectedId {get;private set;}
    private static string T(string source)=>AchievementRewardItemSelector.T(source);
    internal AchievementRewardItemDialog(IEnumerable<ItemDescriptor> items,Guid selected)
    {
        _items=items.OrderBy(item=>item.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(item=>item.Id).ToArray();SelectedId=selected;
        Text=T("Choose reward item");Size=new Size(780,600);MinimumSize=new Size(620,450);StartPosition=FormStartPosition.CenterParent;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=4};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,110));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        var searchPanel=new Panel{Dock=DockStyle.Fill};var searchLabel=new Label{Dock=DockStyle.Top,Height=26,Text=T("Search reward items")};
        searchPanel.Controls.Add(_search);searchPanel.Controls.Add(searchLabel);
        _grid.Columns.Add(new DataGridViewImageColumn{Width=52,ImageLayout=DataGridViewImageCellLayout.Zoom,HeaderText="",DefaultCellStyle=new DataGridViewCellStyle{NullValue=null}});
        _grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=T("Item"),AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
        _grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=T("Type"),Width=130});
        _grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=T("Level"),Width=64});
        foreach(DataGridViewColumn column in _grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
        _grid.RowTemplate.Height=42;_grid.AllowUserToResizeRows=false;_grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;_grid.GridColor=DrawingColor.FromArgb(65,65,69);
        _grid.CellValueNeeded+=(_,e)=>
        {
            if(e.RowIndex<0 || e.RowIndex>=_visible.Count)return;
            var item=_visible[e.RowIndex];
            e.Value=e.ColumnIndex switch {0=>Icon(item),1=>item?.Name??T("No item reward"),2=>item==null?"":T(item.ItemType.ToString()),3=>item?.MinimumLevel.ToString()??"",_=>null};
        };
        _grid.SelectionChanged+=(_,_)=>UpdatePreview();
        _grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Confirm();};
        _search.TextChanged+=(_,_)=>RefreshItems();
        var previewPanel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(4)};
        previewPanel.Controls.Add(_description);previewPanel.Controls.Add(_preview);previewPanel.Controls.Add(_count);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,4,0,0)};
        var cancel=new Button{Text=T("Cancel"),Width=120,Height=32,DialogResult=DialogResult.Cancel};
        _select.Text=T("Use selected item");_select.Click+=(_,_)=>Confirm();buttons.Controls.Add(cancel);buttons.Controls.Add(_select);
        root.Controls.Add(searchPanel,0,0);root.Controls.Add(_grid,0,1);root.Controls.Add(previewPanel,0,2);root.Controls.Add(buttons,0,3);Controls.Add(root);
        AcceptButton=_select;CancelButton=cancel;MiniGameEditorTheme.Apply(this);
        _grid.EnableHeadersVisualStyles=false;_grid.ColumnHeadersDefaultCellStyle.BackColor=DrawingColor.FromArgb(57,60,62);_grid.ColumnHeadersDefaultCellStyle.ForeColor=DrawingColor.Gainsboro;_grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=_grid.ColumnHeadersDefaultCellStyle.BackColor;
        _grid.DefaultCellStyle.BackColor=DrawingColor.FromArgb(37,37,38);_grid.DefaultCellStyle.ForeColor=DrawingColor.Gainsboro;_grid.DefaultCellStyle.SelectionBackColor=DrawingColor.FromArgb(90,75,46);_grid.DefaultCellStyle.SelectionForeColor=DrawingColor.White;
        RefreshItems();Shown+=(_,_)=>_search.Focus();
    }
    private Image? Icon(ItemDescriptor? item)
    {
        if(item==null)return null;
        if(!_images.TryGetValue(item.Id,out var image))_images[item.Id]=image=RewardItemImages.Load(item);
        return image;
    }
    private void RefreshItems()
    {
        var selected=Current()?.Id??SelectedId;
        _grid.RowCount=0;_visible.Clear();
        var query=_search.Text.Trim();
        if(query.Length==0)_visible.Add(null);
        _visible.AddRange(_items.Where(item=>query.Length==0 || System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(item.Name,query,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0));
        _grid.RowCount=_visible.Count;
        if(_visible.Count>0){var index=Math.Max(0,_visible.FindIndex(item=>(item?.Id??Guid.Empty)==selected));_grid.CurrentCell=_grid.Rows[index].Cells[1];_grid.Rows[index].Selected=true;}
        _count.Text=string.Format(T("{0} items found"),_visible.Count(item=>item!=null));UpdatePreview();
    }
    private ItemDescriptor? Current()=>_grid.CurrentCell is {} cell && cell.RowIndex>=0 && cell.RowIndex<_visible.Count?_visible[cell.RowIndex]:null;
    private void UpdatePreview()
    {
        var item=Current();_preview.Image=Icon(item);_select.Enabled=_grid.CurrentCell!=null && _visible.Count>0;
        _description.Text=item==null?(_visible.Count==0?T("No matching items"):T("No item reward")):item.Name+Environment.NewLine+T(item.ItemType.ToString())+" • "+string.Format(T("Required level: {0}"),item.MinimumLevel)+Environment.NewLine+item.Description;
    }
    private void Confirm()
    {
        if(!_select.Enabled)return;SelectedId=Current()?.Id??Guid.Empty;DialogResult=DialogResult.OK;Close();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){_preview.Image=null;foreach(var image in _images.Values)image?.Dispose();_images.Clear();}
        base.Dispose(disposing);
    }
}
internal static class RewardItemImages
{
    internal static Bitmap? Load(ItemDescriptor? item)
    {
        if(item==null || string.IsNullOrWhiteSpace(item.DisplayIcon))return null;
        try
        {
            var root=Path.GetFullPath(Path.Combine("resources",item.UsesSpellIcon?"spells":"items"))+Path.DirectorySeparatorChar;
            var path=Path.GetFullPath(Path.Combine(root,item.DisplayIcon));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase) || !File.Exists(path))return null;
            using var source=Image.FromFile(path);return new Bitmap(source);
        }
        catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException){return null;}
    }
}
