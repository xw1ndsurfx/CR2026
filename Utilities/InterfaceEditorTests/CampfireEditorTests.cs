using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Crafting;

internal static class CampfireEditorTests
{
    internal static void Run()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var editor = new FrmCrafts();
        var recipe = new CraftingRecipeDescriptor(Guid.NewGuid()) { Name = "Campfire",
            Campfire = new() { FireAnimationId = Guid.NewGuid(), AshesAnimationId = Guid.NewGuid() } };
        typeof(FrmCrafts).GetField("mEditorItem", flags)!.SetValue(editor, recipe);
        typeof(FrmCrafts).GetMethod("UpdateEditor", flags)!.Invoke(editor, null);
        var panel = (Control)typeof(FrmCrafts).GetField("pnlContainer", flags)!.GetValue(editor)!;
        var tabs = panel.Controls.OfType<TabControl>().Single();
        if(tabs.TabPages.Count != 2) throw new Exception("Craft recipes must expose the campfire configuration page.");
        var result = (ComboBox)typeof(FrmCrafts).GetField("cmbResult", flags)!.GetValue(editor)!;
        var quantity = (NumericUpDown)typeof(FrmCrafts).GetField("nudCraftQuantity", flags)!.GetValue(editor)!;
        if(result.Enabled || quantity.Enabled) throw new Exception("Placed campfires cannot also create inventory items.");
        recipe.Campfire = null;
        typeof(FrmCrafts).GetMethod("UpdateCampfireResultButton", flags)!.Invoke(editor, null);
        if(!result.Enabled || !quantity.Enabled) throw new Exception("Switching back to an item restores normal recipe controls.");
        Console.WriteLine("PASS recipe result switches cleanly between inventory item and configured campfire");

        using var resources = new FrmResource();
        var resourcePanel = (Control)typeof(FrmResource).GetField("pnlContainer", flags)!.GetValue(resources)!;
        var resourceTabs = resourcePanel.Controls.OfType<TabControl>().Single();
        if(resourceTabs.TabPages.Count != 3) throw new Exception("Resources use General, Rewards and Appearance sections.");
        resources.CreateControl(); resourcePanel.Visible = true; resources.PerformLayout();
        var check = (Control)typeof(FrmResource).GetField("_fallingTree", flags)!.GetValue(resources)!;
        check.Parent!.PerformLayout();
        var checks = check.Parent.Controls.Cast<Control>().ToArray();
        if(checks.Length != 4) throw new Exception("All resource behavior checkboxes must remain accessible.");
        foreach(var a in checks)
            foreach(var b in checks.Where(b => b != a))
                if(a.Bounds.IntersectsWith(b.Bounds)) throw new Exception("Resource checkboxes must not overlap.");
        Console.WriteLine("PASS resource behavior checkboxes have separate rows in the modernized editor");
        if(Environment.GetEnvironmentVariable("CODEX_EDITOR_BATCH_PREVIEW") is {Length:>0} output)
        {
            Directory.CreateDirectory(output);
            using var login=new Intersect.Editor.Forms.FrmLogin(false);
            typeof(Intersect.Editor.Forms.FrmLogin).GetMethod("ModernizeLogin",flags)!.Invoke(login,null);
            var load=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),login,typeof(Intersect.Editor.Forms.FrmLogin).GetMethod("frmLogin_Load",flags)!);
            login.Load-=load;login.Opacity=0;login.ShowInTaskbar=false;login.Show();Application.DoEvents();login.PerformLayout();
            using var loginBitmap=new Bitmap(login.Width,login.Height);login.DrawToBitmap(loginBitmap,new Rectangle(0,0,loginBitmap.Width,loginBitmap.Height));
            loginBitmap.Save(Path.Combine(output,"editor-login.png"));login.Hide();
            resources.Opacity=0;resources.ShowInTaskbar=false;resources.Show();
            var source=new Intersect.Framework.Core.GameObjects.Resources.ResourceDescriptor(Guid.NewGuid()){Name="Normal Dead Tree",MinHp=125,MaxHp=165};
            typeof(FrmResource).GetField("_editorItem",flags)!.SetValue(resources,source);
            typeof(FrmResource).GetMethod("UpdateEditor",flags)!.Invoke(resources,null);
            resourcePanel.Visible=true;Application.DoEvents();resources.PerformLayout();
            using var resourceBitmap=new Bitmap(resources.Width,resources.Height);resources.DrawToBitmap(resourceBitmap,new Rectangle(0,0,resourceBitmap.Width,resourceBitmap.Height));
            resourceBitmap.Save(Path.Combine(output,"resource-editor.png"));
        }
    }
}
