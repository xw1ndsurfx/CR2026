using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Items;

internal static class ItemEditorUsabilityTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run()
    {
        var category=new MasteryCategory { Key="swords",Name="Swordsmanship",Cap=30 };
        RoyalCombatEditorState.Apply(new RoyalCombatConfiguration { Masteries=[category] }.ToJson());
        var conditions=new ConditionLists();
        var first=new ConditionList { Name="Authored branch",Conditions=[new LevelOrStatCondition { ComparingLevel=true,Value=15 }] };
        var second=new ConditionList { Name="Alternative",Conditions=[new LevelOrStatCondition { ComparingLevel=true,Value=20 }] };
        conditions.Lists.Add(first);conditions.Lists.Add(second);
        FrmItem.SetItemMasteryRequirement(conditions,category,12);
        Check(conditions.Lists.All(b=>b.Conditions.OfType<WeaponMasteryLevelCondition>().Single().Value==12),"Mastery cannot be bypassed by an OR alternative");
        FrmItem.SetItemMasteryRequirement(conditions,category,6);
        Check(first.Conditions.Count==2 && first.Conditions.OfType<LevelOrStatCondition>().Single().Value==15 && second.Name=="Alternative","Editing mastery preserves other requirements and branch names");
        var copy=new ConditionLists(conditions.Data());
        Check(copy.Lists.All(b=>b.Conditions.OfType<WeaponMasteryLevelCondition>().Single().Value==6),"Mastery persists in native item conditions");
        FrmItem.SetItemMasteryRequirement(conditions,null,1);
        Check(conditions.Lists.All(b=>b.Conditions.Count==1),"Removing mastery preserves other conditions");
        var item=new ItemDescriptor(Guid.NewGuid());
        FrmItem.ApplyGeneratedItemTrading(item);Check(item.CanSellInMarketplace,"Generated items default to marketplace trading");
        FrmItem.ApplyGeneratedItemTrading(item,false);Check(!item.CanSellInMarketplace,"An explicit creator opt-out remains possible");

        using var editor=new FrmItem { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-20000,-20000) };
        var events=(System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events",Hidden)!.GetValue(editor)!;
        var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(object)&&f.Name.Contains("load",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        if(events[key] is EventHandler load)editor.Load-=load;
        typeof(FrmItem).GetMethod("InitLocalization",Hidden)!.Invoke(editor,null);
        typeof(FrmItem).GetField("mEditorItem",Hidden)!.SetValue(editor,item);
        typeof(FrmItem).GetMethod("RefreshItemMasteryRequirement",Hidden)!.Invoke(editor,null);
        var choice=editor.Controls.Find("ItemMasteryCategory",true).OfType<ComboBox>().Single();
        var level=editor.Controls.Find("ItemMasteryLevel",true).OfType<NumericUpDown>().Single();
        var tab=Ancestors(choice).OfType<TabPage>().Single();
        Check(tab.Text=="General / Trading","Mastery belongs in General / Trading");
        choice.SelectedIndex=1;level.Value=9;
        Check(item.UsageRequirements.Lists.Single().Conditions.OfType<WeaponMasteryLevelCondition>().Single().Value==9,"General controls edit actual item requirements");
        EditorTheme.Apply(editor);editor.Show();Application.DoEvents();
        editor.Controls.Find("pnlContainer",true).Single().Visible=true;
        foreach(var size in new[]{new Size(1200,745),new Size(800,600)})
        {
            editor.Size=size;Application.DoEvents();editor.ArrangeResponsiveItemEditor();Application.DoEvents();
            var bounds=(Dictionary<Control,Rectangle>)typeof(FrmItem).GetField("_itemCaptionBounds",Hidden)!.GetValue(editor)!;
            foreach(var control in bounds.Keys.Where(c=>c is Label or ButtonBase))
            {
                var measured=TextRenderer.MeasureText(control.Text,control.Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                var extra=control is CheckBox or RadioButton?28:control is ButtonBase?24:4;
                Check(control.Width>=measured.Width+extra+control.Padding.Horizontal,"Clipped caption: "+control.Name+" / "+control.Text);
            }
            var widths=bounds.Keys.ToDictionary(c=>c,c=>c.Width);
            editor.ArrangeResponsiveItemEditor();
            Check(widths.All(p=>p.Key.Width==p.Value),"Caption sizing must not grow on repeated layouts");
            if(Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is { Length:>0 } folder)
            {
                Directory.CreateDirectory(folder);
                using var capture=new Bitmap(editor.Width,editor.Height);
                editor.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));
                capture.Save(Path.Combine(folder,$"item-general-{size.Width}.png"));
            }
        }
        Console.WriteLine("PASS item editor: full captions, stable layout, General/Trading mastery controls, OR-branch persistence and marketplace defaults");
    }
    private static IEnumerable<Control> Ancestors(Control child) { for(var parent=child.Parent;parent!=null;parent=parent.Parent)yield return parent; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
}
