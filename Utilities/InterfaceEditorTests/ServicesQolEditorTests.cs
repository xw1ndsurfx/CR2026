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
        var respawn=typeof(FrmNpc).GetMethod("NpcPresetRespawnMilliseconds",BindingFlags.Static|BindingFlags.NonPublic)!;
        Check((int)respawn.Invoke(null,[1,20m])! ==40000 && (int)respawn.Invoke(null,[3,20m])! ==360000,"Generated normal/boss respawns are milliseconds, not raw seconds");
        using(var layers=new Intersect.Editor.Forms.DockingElements.FrmMapLayers())
        {
            layers.Size=new Size(600,800);layers.Show();Application.DoEvents();
            var picture=(PictureBox)typeof(Intersect.Editor.Forms.DockingElements.FrmMapLayers).GetField("picTileset",BindingFlags.Instance|BindingFlags.Public)!.GetValue(layers)!;
            var viewport=(Panel)picture.Parent!;picture.Size=new Size(1200,1600);viewport.PerformLayout();Application.DoEvents();
            Check(viewport.AutoScrollMinSize==picture.Size && viewport.HorizontalScroll.Visible && viewport.VerticalScroll.Visible,"Oversized tilesets retain both actual scrollbars");
            viewport.AutoScrollPosition=new Point(400,500);Application.DoEvents();Check(picture.Left<0 && picture.Top<0,"Both tile axes scroll into offscreen content");
            layers.Close();
        }
        var old=CultureInfo.CurrentUICulture;
        try
        {
            foreach(var locale in new[]{"en","fr","es"})
            {
                CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(locale);
                using var lobby=new FrmLobbyConfiguration("{}");
                var table=lobby.Controls.Find("LobbyActivities",true).OfType<TableLayoutPanel>().Single();
                Check(table.Controls.OfType<NumericUpDown>().Count()==Enum.GetValues<Intersect.Network.Packets.PartyLobbies.LobbyActivity>().Length && table.Controls.OfType<NumericUpDown>().All(n=>n.Minimum==1 && n.Maximum==100),"Each lobby activity exposes bounded party capacity");
                Check(table.Controls.OfType<ComboBox>().Count()==Enum.GetValues<Intersect.Network.Packets.PartyLobbies.LobbyActivity>().Length,"Each lobby activity exposes its image");
                var dice=table.Controls.OfType<Label>().Single(l=>l.Text==Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Roll the Dice",locale));
                lobby.Show();lobby.Size=new Size(1100,950);Application.DoEvents();
                if(locale=="fr" && Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is {Length:>0} previews)
                {Directory.CreateDirectory(previews);using var bitmap=new Bitmap(lobby.Width,lobby.Height);lobby.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(previews,"lobby-images.png"));}
                var diceRow=table.GetRow(dice);
                Check(table.GetControlFromPosition(1,diceRow) is ComboBox && table.GetControlFromPosition(2,diceRow) is NumericUpDown,"Roll the Dice image and capacity stay on the same row");
                using var shop=new FrmShop();
                Check(shop.Controls.Find("lstSoldItemsSearch",true).Length==1 && shop.Controls.Find("lstBoughtItemsSearch",true).Length==1,"Both shop offer lists have search controls");
            }
            using(var shop=new FrmShop())
            {
                var draft=new Intersect.GameObjects.ShopDescriptor(Guid.NewGuid()){Name="Layout preview"};
                typeof(FrmShop).GetField("mEditorItem",Hidden)!.SetValue(shop,draft);
                shop.Show();Application.DoEvents();
                foreach(var size in new[]{new Size(1100,760),new Size(800,600)})
                {
                    shop.Size=size;Application.DoEvents();
                    foreach(var name in new[]{"grpItemsSold","grpItemsBought"})
                    {
                        var group=(GroupBox)typeof(FrmShop).GetField(name,Hidden)!.GetValue(shop)!;
                        Check(group.Left==0 && group.Top==0,"Shop offer groups start at their viewport origin after docking/reparenting");
                    }
                }
                var kind=(ComboBox)typeof(FrmShop).GetField("_navalType",Hidden)!.GetValue(shop)!;
                kind.SelectedIndex=(int)Intersect.GameObjects.ShopConfigurationKind.RoyalSeals;
                Check(!((GroupBox)typeof(FrmShop).GetField("grpItemsBought",Hidden)!.GetValue(shop)!).Enabled,"Royal Seals shops disable inventory buyback");
                Check(!((ComboBox)typeof(FrmShop).GetField("cmbSellFor",Hidden)!.GetValue(shop)!).Enabled,"Royal Seals is an explicit non-item currency");
                if(Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is {Length:>0} folder)
                {Directory.CreateDirectory(folder);shop.Size=new Size(1100,760);Application.DoEvents();using var bmp=new Bitmap(shop.Width,shop.Height);shop.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(folder,"shop-layout.png"));}
                shop.Close();
            }
        }
        finally{CultureInfo.CurrentUICulture=old;}
        Console.WriteLine("PASS sprite selector OK/Cancel lifecycle, lobby image/capacity editor en/fr/es and shop offer search");
    }
}
