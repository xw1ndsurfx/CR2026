namespace Intersect.Editor.Forms.Editors;

public partial class FrmItem
{
    private readonly Dictionary<Control,Rectangle> _itemCaptionBounds=[];

    private void FitItemEditorText()
    {
        foreach(var (_,groups) in _itemTabGroups)
            foreach(var group in groups.Where(g=>g!=_masteryRequirementsGroup)) FitGroup(group);
    }

    private void FitGroup(Control group)
    {
        if(!_itemCaptionBounds.ContainsKey(group)) _itemCaptionBounds[group]=group.Bounds;
        var children=group.Controls.Cast<Control>().ToArray();
        foreach(var child in children)
        {
            if(!_itemCaptionBounds.ContainsKey(child)) _itemCaptionBounds[child]=child.Bounds;
            child.Bounds=_itemCaptionBounds[child];
            if(child is GroupBox) FitGroup(child);
            else if(child is Label or ButtonBase)
            {
                child.AutoSize=false;
                var text=TextRenderer.MeasureText(child.Text,child.Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                var extra=child is CheckBox or RadioButton?28:child is ButtonBase?24:4;
                child.Width=Math.Max(child.Width,text.Width+extra+child.Padding.Horizontal);
                child.Height=Math.Max(child.Height,text.Height+child.Padding.Vertical+4);
            }
            else if(child is ComboBox combo)
                combo.DropDownWidth=Math.Max(combo.Width,combo.Items.Cast<object>().Select(i=>TextRenderer.MeasureText(combo.GetItemText(i),combo.Font).Width+30).DefaultIfEmpty(0).Max());
        }
        // Retain the authored rows, expanding space between columns to fit the
        // current font/language. Long forms stay reachable through tab scrolling.
        foreach(var left in children.OrderBy(c=>_itemCaptionBounds[c].Left))
        foreach(var right in children.Where(c=>c!=left))
        {
            var a=_itemCaptionBounds[left];var b=_itemCaptionBounds[right];
            if(a.Right<=b.Left && a.Top<b.Bottom && b.Top<a.Bottom)
                right.Left=Math.Max(right.Left,left.Right+(b.Left-a.Right));
        }
        var original=_itemCaptionBounds[group];
        group.Width=Math.Max(original.Width,Math.Max(TextRenderer.MeasureText(group.Text,group.Font).Width+24,children.Select(c=>c.Right+12).DefaultIfEmpty(0).Max()));
        group.Height=Math.Max(original.Height,children.Select(c=>c.Bottom+12).DefaultIfEmpty(0).Max());
    }
}
