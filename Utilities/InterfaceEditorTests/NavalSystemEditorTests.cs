using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

internal static class NavalSystemEditorTests
{
    internal static void Run()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var packet = new NavalConfigurationPacket { NavigationJson = "{\"Ships\":[]}", NavigationVersion = "nav-1",
                CatalogueJson = JsonConvert.SerializeObject(new HarbourCatalog { Enabled = true, Offers = [new() { Id = "royal" }] }),
                CatalogueVersion = "shop-1" };
            using var editor = new FrmNavalConfiguration(packet);
            if (editor.Text != "Éditeur du système naval") throw new Exception("Naval editor must be localized.");
            var tabs = editor.Controls.OfType<TabControl>().Single();
            if (tabs.TabPages.Count != 3) throw new Exception("Navigation, test ships and shop must be separate sections.");
            tabs.SelectedIndex = 2;
            var prepare = typeof(FrmNavalConfiguration).GetMethod("PrepareSave", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var request = (NavalConfigurationRequestPacket)prepare.Invoke(editor, null)!;
            if (request.Section != 2 || request.ExpectedVersion != "shop-1") throw new Exception("Save the selected section.");
            var pendingRequest = typeof(FrmNavalConfiguration).GetField("_pendingRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var acknowledgementId = Guid.NewGuid(); pendingRequest.SetValue(editor, acknowledgementId);
            editor.AcceptResponse(new() { RequestId = Guid.NewGuid(), SavedSection = 1, NavigationVersion = "stale" });
            if ((Guid)pendingRequest.GetValue(editor)! != acknowledgementId) throw new Exception("Ignore acknowledgements from another save request.");
            editor.AcceptResponse(new() { RequestId = acknowledgementId, SavedSection = 1, NavigationVersion = "nav-2", CatalogueVersion = "other-editor-shop-2" });
            request = (NavalConfigurationRequestPacket)prepare.Invoke(editor, null)!;
            if (request.ExpectedVersion != "shop-1") throw new Exception("A navigation acknowledgement must not authorize overwriting another editor's shop changes.");
            var preview = Environment.GetEnvironmentVariable("NAVAL_EDITOR_PREVIEW");
            if (!string.IsNullOrEmpty(preview))
            {
                editor.StartPosition = FormStartPosition.Manual;
                editor.Location = new System.Drawing.Point(-20000, -20000);
                editor.ShowInTaskbar = false;
                editor.Show(); Application.DoEvents(); editor.PerformLayout();
                using var image = new System.Drawing.Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
                image.Save(preview, System.Drawing.Imaging.ImageFormat.Png);
                editor.Hide();
            }
            Console.WriteLine("PASS localized Naval System Editor opens three sections and keeps independent save versions");
        }
        finally { CultureInfo.CurrentUICulture = culture; }
    }
}
