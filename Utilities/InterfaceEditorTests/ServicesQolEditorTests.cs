using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Editors.Events.Event_Commands;
using Intersect.Framework.Core.GameObjects.Events;
internal static class ServicesQolEditorTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    internal static void Run()
    {
        foreach(var accepted in new[]{true,false})
        {
            var original=new EventGraphic{Filename="original.png"};
            using var parent=new Panel();
            using var selector=new EventGraphicSelector(original,null!);
            parent.Controls.Add(selector);
            var temp=(EventGraphic)typeof(EventGraphicSelector).GetField("mTmpGraphic",Hidden)!.GetValue(selector)!;
            temp.Filename="selected.png";temp.Type=EventGraphicType.Sprite;temp.X=2;
            bool? result=null;selector.SelectionClosed+=value=>result=value;
            typeof(EventGraphicSelector).GetMethod(accepted?"btnOk_Click":"btnCancel_Click",Hidden)!.Invoke(selector,[selector,EventArgs.Empty]);
            Check(result==accepted && selector.IsDisposed && !parent.Controls.Contains(selector),"Graphic selector OK/Cancel must close its actual parent panel");
            Check(original.Filename==(accepted?"selected.png":"original.png"),"OK commits the selected sprite; Cancel preserves the previous sprite");
        }
        var old=CultureInfo.CurrentUICulture;
        try
        {
            foreach(var locale in new[]{"en","fr","es"})
            {
                CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(locale);
                using var lobby=new FrmLobbyConfiguration("{}");
                var table=lobby.Controls.OfType<TableLayoutPanel>().Single();
                Check(table.Controls.OfType<NumericUpDown>().Count()==14 && table.Controls.OfType<NumericUpDown>().All(n=>n.Minimum==1 && n.Maximum==100),"Each lobby activity exposes bounded party capacity");
                Check(table.Controls.OfType<TextBox>().Count()==14,"Each lobby activity exposes its image");
                using var shop=new FrmShop();
                Check(shop.Controls.Find("lstSoldItemsSearch",true).Length==1 && shop.Controls.Find("lstBoughtItemsSearch",true).Length==1,"Both shop offer lists have search controls");
            }
        }
        finally{CultureInfo.CurrentUICulture=old;}
        Console.WriteLine("PASS sprite selector OK/Cancel lifecycle, lobby image/capacity editor en/fr/es and shop offer search");
    }
}
