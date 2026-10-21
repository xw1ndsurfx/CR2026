using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
internal static class QualityOfLifeEditorTests
{
    internal static void Run()
    {
        using var editor=new FrmItem();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var item=new ItemDescriptor(Guid.NewGuid()){ItemType=ItemType.Consumable};
        typeof(FrmItem).GetField("mEditorItem",flags)!.SetValue(editor,item);
        typeof(FrmItem).GetMethod("InitLocalization",flags)!.Invoke(editor,null);
        typeof(FrmItem).GetMethod("frmItem_Load",flags)!.Invoke(editor,[editor,EventArgs.Empty]);
        var types=(ComboBox)typeof(FrmItem).GetField("cmbType",flags)!.GetValue(editor)!;
        if(types.Items.Count!=10)throw new Exception("Editor must expose all ten item types without renumbering existing types.");
        var marketplace=(CheckBox)typeof(FrmItem).GetField("chkCanSellMarketplace",flags)!.GetValue(editor)!;
        var potions=(GroupBox)typeof(FrmItem).GetField("grpConsumable",flags)!.GetValue(editor)!;
        if(marketplace.Parent==potions.Parent && marketplace.Bounds.IntersectsWith(potions.Bounds))throw new Exception("Marketplace permission overlaps potion options.");
        foreach(var field in new[]{"_showValue","_showLevel"})
            if(typeof(FrmItem).GetField(field,flags)!.GetValue(editor) is not CheckBox {Checked:true})throw new Exception("New item presentation controls must default on.");
        foreach(var language in new[]{"en","fr","es"})
            foreach(var entry in QualityOfLifeText.Catalog.Entries)
                if(!entry.Value.TryGetValue(language,out var text) || string.IsNullOrWhiteSpace(text))throw new Exception("Missing convenience localization: "+entry.Key+" / "+language);
        if(Environment.GetEnvironmentVariable("CODEX_QOL_PREVIEW") is {Length:>0} path)
        {
            editor.Opacity=0;editor.ShowInTaskbar=false;editor.Show();Application.DoEvents();editor.PerformLayout();
            using var bitmap=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(path);
            if(!((CheckBox)typeof(FrmItem).GetField("_showValue",flags)!.GetValue(editor)!).Checked)throw new Exception("Showing the editor must preserve default item presentation settings.");
            var tabs=(TabControl)typeof(FrmItem).GetField("_itemEditorTabs",flags)!.GetValue(editor)!;tabs.SelectedIndex=1;Application.DoEvents();
            using var display=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(display,new Rectangle(0,0,display.Width,display.Height));display.Save(Path.Combine(Path.GetDirectoryName(path)!,"qol-item-display.png"));editor.Hide();
        }
        var mapPropertiesType=typeof(FrmItem).Assembly.GetType("Intersect.Editor.Maps.MapProperties")!;
        var map=new Intersect.Framework.Core.GameObjects.Maps.MapDescriptor(Guid.NewGuid());
        var mapProperties=Activator.CreateInstance(mapPropertiesType,map)!;
        var properties=System.ComponentModel.TypeDescriptor.GetProperties(mapProperties);
        foreach(var name in new[]{"RandomWeather","WeatherFlashes","WeatherFlashOpacity","WeatherLightning","WeatherLightningAnimation","WeatherGroundSplashes","WeatherSplashAnimation"})
        {
            var property=properties[name]??throw new Exception("Missing map weather property: "+name);
            if(property.Category!=properties["Weather"]!.Category)throw new Exception("Weather settings must stay in Map Properties > Weather.");
            if(property.DisplayName==name)throw new Exception("Weather property lacks a localized caption: "+name);
        }
        if(map.WeatherEffects.RandomWeather || map.WeatherEffects.GroundSplashes)throw new Exception("Legacy maps must retain their existing continuous weather by default.");
        System.Console.WriteLine("PASS Map Properties exposes localized random weather, flashes, lightning and ground splash settings in Weather");
        System.Console.WriteLine("PASS item types, potion/marketplace layout, default presentation controls and en/fr/es convenience labels");
    }
}
