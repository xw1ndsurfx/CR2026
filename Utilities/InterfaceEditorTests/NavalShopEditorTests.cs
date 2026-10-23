using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
internal static class NavalShopEditorTests
{
    internal static void Run()
    {
        var before=CultureInfo.CurrentUICulture;CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("fr");
        try
        {
            var shop=new ShopDescriptor {ConfigurationKind=ShopConfigurationKind.NavalShipsAndSkills};
            var packet=new NavalConfigurationPacket {CatalogueJson=JsonConvert.SerializeObject(new HarbourCatalog {Enabled=true,Offers=[new(){Id="existing-model",Name="Royal ship",Price=1000,RepairPrice=300}]}),NavigationJson=JsonConvert.SerializeObject(new NavalConfiguration())};
            using var dialog=new FrmNavalShopOffers(shop,packet);
            var tabs=dialog.Controls.OfType<TabControl>().Single();
            var boats=tabs.TabPages[0].Controls.OfType<DataGridView>().Single();var skills=tabs.TabPages[1].Controls.OfType<DataGridView>().Single();
            boats.Rows[0].Cells["Price"].Value=1200;skills.Rows[1].Cells["Price"].Value=350;
            var save=dialog.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b=>b.Text=="Enregistrer");
            typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(save,[EventArgs.Empty]);
            var restored=new ShopDescriptor {JsonBuyingItems=shop.JsonBuyingItems};
            if(restored.NavalCatalogue.Offers.Single().Id!="existing-model" || restored.NavalCatalogue.Offers.Single().Price!=1200 || restored.NavalSkills.Single(s=>s.Kind==NavalSkillKind.FullSail).Price!=350)throw new Exception("Per-shop boat and skill prices must round-trip without replacing model identities.");
            using var editor=new FrmShop();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var panel=(Control)typeof(FrmShop).GetField("pnlContainer",flags)!.GetValue(editor)!;
            var general=(Control)typeof(FrmShop).GetField("grpGeneral",flags)!.GetValue(editor)!;
            var sections=(TableLayoutPanel)panel.Controls.Find("ShopEditorSections",true).Single();
            var bar=(FlowLayoutPanel)sections.GetControlFromPosition(0,0)!;
            if(general.Top<bar.Bottom)throw new Exception("Shop type selector must not overlap native shop settings.");
            var preview=Environment.GetEnvironmentVariable("NAVAL_SHOP_EDITOR_PREVIEW");
            if(!string.IsNullOrEmpty(preview))
            {
                using var fresh=new FrmNavalShopOffers(restored,packet);fresh.StartPosition=FormStartPosition.Manual;fresh.Location=new(-20000,-20000);fresh.ShowInTaskbar=false;fresh.Show();Application.DoEvents();
                using var image=new Bitmap(fresh.Width,fresh.Height);fresh.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(preview,System.Drawing.Imaging.ImageFormat.Png);fresh.Hide();
            }
            Console.WriteLine("PASS native Shop Editor separates normal/ammunition/naval shops; per-shop prices persist and controls do not overlap");
        }
        finally {CultureInfo.CurrentUICulture=before;}
    }
}
