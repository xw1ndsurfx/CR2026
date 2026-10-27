using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Crafting;

internal static class CampfireEditorTests
{
    internal static void Run()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using (var tableEditor = new FrmCraftingTables())
        {
            var table = new CraftingTableDescriptor(Guid.NewGuid()) { Name = "Empty fire table" };
            typeof(FrmCraftingTables).GetField("mEditorItem", flags)!.SetValue(tableEditor, table);
            typeof(FrmCraftingTables).GetMethod("UpdateEditor", flags)!.Invoke(tableEditor, null);
            var fireCheck = (CheckBox)typeof(FrmCraftingTables).GetField("_campfireCreator", flags)!.GetValue(tableEditor)!;
            fireCheck.Checked = true; tableEditor.UpdateList();
            if (!fireCheck.Checked || !table.CampfireCreator) throw new Exception("Campfire checkbox must remain checked even without a configured recipe.");
            var restored = new CraftingTableDescriptor(Guid.NewGuid());
            typeof(CraftingTableDescriptor).GetProperty("CraftsJson")!.SetValue(restored, table.CraftsJson);
            if (!restored.CampfireCreator) throw new Exception("Empty campfire creator flag must persist through the existing database column.");
            var laterRecipe = new CraftingRecipeDescriptor(Guid.NewGuid()) { Campfire = new() { FireAnimationId = Guid.NewGuid(), AshesAnimationId = Guid.NewGuid() } };
            CraftingRecipeDescriptor.Lookup.Set(laterRecipe.Id, laterRecipe);
            try
            {
                if (!restored.AvailableCrafts().Contains(laterRecipe.Id)) throw new Exception("A saved creator must include valid campfire recipes configured later.");
            }
            finally { CraftingRecipeDescriptor.Lookup.DeleteAt(laterRecipe.Id); }
            restored.MakeBackup(); restored.CampfireCreator = false; restored.RestoreBackup();
            if (!restored.CampfireCreator) throw new Exception("Undo/cancel must restore the campfire creator setting.");
            typeof(CraftingTableDescriptor).GetProperty("CraftsJson")!.SetValue(restored, "[]");
            if (restored.CampfireCreator || restored.Crafts.Count != 0) throw new Exception("Legacy crafting table lists must remain readable.");
            Intersect.Editor.Forms.Styling.EditorTheme.Apply(tableEditor);
            tableEditor.ShowInTaskbar = false; tableEditor.Show(); Application.DoEvents();
            foreach (var size in new[] { new Size(700, 560), new Size(1920, 1000) })
            {
                tableEditor.ClientSize = size; tableEditor.PerformLayout(); Application.DoEvents();
                Control Field(string name) => (Control)typeof(FrmCraftingTables).GetField(name, flags)!.GetValue(tableEditor)!;
                var tablePanel = Field("pnlContainer");
                if (tablePanel.Right > tableEditor.ClientSize.Width || tablePanel.Bottom > tableEditor.ClientSize.Height - 30 || tablePanel.Width < size.Width / 2)
                    throw new Exception($"Crafting editor content must expand with the window and leave room for Save/Cancel: requested={size}, client={tableEditor.ClientSize}, panel={tablePanel.Bounds}.");
                if (Field("btnSave").Bottom > tableEditor.ClientSize.Height || Field("btnCancel").Right > tableEditor.ClientSize.Width)
                    throw new Exception("Crafting editor action buttons left the window.");
                if (size.Width > 1000 && Field("grpCrafts").Width < 1000) throw new Exception("Recipe pane still uses the original narrow fixed width.");
                if (size.Width < 1000)
                {
                    var scrollPanel = (Panel)tablePanel;
                    scrollPanel.AutoScrollPosition = new Point(0, 120); Application.DoEvents();
                    var beforeScroll = scrollPanel.AutoScrollPosition;
                    tableEditor.PerformLayout(); Application.DoEvents();
                    if (beforeScroll.Y >= 0 || scrollPanel.AutoScrollPosition.Y != beforeScroll.Y) throw new Exception("Crafting editor layout resets the scrollbar before lower controls can be reached.");
                    scrollPanel.AutoScrollPosition = Point.Empty;
                }
                if (Environment.GetEnvironmentVariable("CODEX_EDITOR_BATCH_PREVIEW") is { Length: > 0 } craftingOutput)
                {
                    Directory.CreateDirectory(craftingOutput); using var image = new Bitmap(tableEditor.Width, tableEditor.Height);
                    tableEditor.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(craftingOutput, $"crafting-table-{size.Width}.png"));
                }
            }
            fireCheck.Checked = false;
            if (table.CampfireCreator) throw new Exception("Campfire checkbox cannot be switched off.");
            tableEditor.Hide();
        }
        Console.WriteLine("PASS responsive crafting table editor, persistent empty campfire creator flag, legacy database compatibility and undo");
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
        using var about = new Intersect.Editor.Forms.FrmAbout();
        IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
        var notice = Descendants(about).OfType<TextBox>().Single(control => control.Name == "EngineLicenseNotice");
        var originalNotice = new System.ComponentModel.ComponentResourceManager(typeof(Intersect.Editor.Forms.FrmAbout)).GetString("label1.Text")!;
        if (!about.Text.Contains("Logiklik 2D Engine") || !notice.Text.StartsWith(originalNotice, StringComparison.Ordinal) ||
            !notice.Text.Contains("GNU GENERAL PUBLIC LICENSE") || !notice.Text.Contains("WITHOUT ANY WARRANTY") || notice.Text.Length < 30000)
            throw new Exception("About must identify Logiklik and retain the upstream copyright, warranty notice and complete unmodified GPL.");
        Console.WriteLine("PASS Logiklik About preserves upstream attribution and embeds the full GPL license");
        if(Environment.GetEnvironmentVariable("CODEX_EDITOR_BATCH_PREVIEW") is {Length:>0} output)
        {
            Directory.CreateDirectory(output);
            about.Opacity=0;about.ShowInTaskbar=false;about.Show();Application.DoEvents();about.PerformLayout();
            using var aboutBitmap=new Bitmap(about.Width,about.Height);about.DrawToBitmap(aboutBitmap,new Rectangle(0,0,aboutBitmap.Width,aboutBitmap.Height));
            aboutBitmap.Save(Path.Combine(output,"editor-about.png"));about.Hide();

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
