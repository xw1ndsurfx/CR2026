using System.ComponentModel;
using Intersect.Configuration;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Animations;
internal static class MenuAnimationEditorTests
{
    internal static void Run()
    {
        var previous=ClientConfiguration.Instance.MenuAnimations;
        var first=new AnimationDescriptor(Guid.NewGuid()){Name="Menu fixture one"};
        var second=new AnimationDescriptor(Guid.NewGuid()){Name="Menu fixture two"};
        AnimationDescriptor.Lookup.Set(first.Id,first);AnimationDescriptor.Lookup.Set(second.Id,second);
        try
        {
            ClientConfiguration.Instance.MenuAnimations=[new(){AnimationId=first.Id},new(){AnimationId=second.Id}];
            using var editor=new FrmMenuAnimations {Opacity=0,ShowInTaskbar=false};
            editor.Show();Application.DoEvents();
            var grid=All(editor).OfType<DataGridView>().Single(control=>control.Name=="MenuAnimationsGrid");
            var remove=All(editor).OfType<Button>().Single(control=>control.Name=="DeleteMenuAnimation");
            var entries=(BindingList<MenuAnimationDefinition>)grid.DataSource!;
            grid.ClearSelection();grid.Rows[0].Selected=true;remove.PerformClick();Application.DoEvents();
            if(entries.Count!=1 || entries[0].AnimationId!=second.Id)throw new Exception("Remove must delete only the selected menu animation.");
            grid.ClearSelection();grid.Rows[0].Selected=true;remove.PerformClick();Application.DoEvents();
            if(entries.Count!=0 || remove.Enabled)throw new Exception("The final menu animation must be removable and the empty list must disable removal.");
            if(ClientConfiguration.Instance.MenuAnimations.Count!=2)throw new Exception("Removing draft rows must not save configuration before the user clicks Save.");
            if(AnimationDescriptor.Get(first.Id)==null || AnimationDescriptor.Get(second.Id)==null)throw new Exception("Removing a menu entry must preserve its underlying animation assets.");
            editor.Close();
            System.Console.WriteLine("PASS main menu animation removal: selected row, final row, empty list, draft isolation and preserved animation assets");
        }
        finally
        {
            ClientConfiguration.Instance.MenuAnimations=previous;
            AnimationDescriptor.Lookup.DeleteAt(first.Id);AnimationDescriptor.Lookup.DeleteAt(second.Id);
        }
    }
    private static IEnumerable<Control> All(Control root)
    {
        foreach(Control child in root.Controls){yield return child;foreach(var descendant in All(child))yield return descendant;}
    }
}
