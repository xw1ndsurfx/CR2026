using System.ComponentModel;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapProperties
{
    private static MapPropertySection? _copiedSection;
    private string? SelectedCategory()
    {
        var selected=gridMapProperties.SelectedGridItem;
        return selected?.GridItemType==GridItemType.Category?selected.Label:selected?.PropertyDescriptor?.Category;
    }
    private void InitializeSectionClipboard()
    {
        string T(string text)=>QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var menu=new ContextMenuStrip();
        var copy=new ToolStripMenuItem(T("Copy section"));
        var paste=new ToolStripMenuItem(T("Paste section"));
        menu.Items.AddRange([copy,paste]);
        menu.Opening+=(_,_)=>
        {
            copy.Enabled=gridMapProperties.SelectedObject is MapProperties && SelectedCategory()!=null;
            paste.Enabled=gridMapProperties.SelectedObject is MapProperties && _copiedSection!=null;
            var category=_copiedSection==null?null:TypeDescriptor.GetProperties(typeof(MapProperties)).Cast<PropertyDescriptor>()
                .Select(p=>p.Attributes[typeof(CategoryAttribute)]).OfType<CustomCategory>().FirstOrDefault(c=>c.Key==_copiedSection.Category)?.Category;
            paste.Text=category==null?T("Paste section"):string.Format(T("Paste section: {0}"),category);
        };
        copy.Click+=(_,_)=>
        {
            if(gridMapProperties.SelectedObject is MapProperties properties && SelectedCategory() is {} category)
                _copiedSection=properties.CopySection(category);
        };
        paste.Click+=(_,_)=>
        {
            if(gridMapProperties.SelectedObject is MapProperties properties && _copiedSection!=null)
            {properties.PasteSection(_copiedSection);gridMapProperties.Refresh();}
        };
        gridMapProperties.ContextMenuStrip=menu;
        Disposed+=(_,_)=>menu.Dispose();
    }
}
