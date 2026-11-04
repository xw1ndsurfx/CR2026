using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmExportationRoutes? _exportationRoutes;

    private void AddExportationRoutesMenu()
    {
        var menu = GetOrCreateMiniGameEditorsMenu();
        if (menu.DropDownItems.Cast<ToolStripItem>().Any(item => item.Name == "miniGame.ExportationRoutes")) return;
        var entry = new ToolStripMenuItem(ExportationText.Get("Exportation route editor",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
            { Name = "miniGame.ExportationRoutes" };
        entry.Click += (_, _) => Intersect.Editor.Networking.Network.SendPacket(
            new ExportationRequestPacket { Action = ExportationAction.GetConfiguration });
        menu.DropDownItems.Add(entry);
    }

    internal void ApplyExportationConfiguration(ExportationStatePacket packet)
    {
        if (!packet.Editor) return;
        if (packet.Open && packet.ConfigurationJson.Length > 0)
        {
            if (_exportationRoutes is not { IsDisposed: false })
                _exportationRoutes = new FrmExportationRoutes(packet.ConfigurationJson);
            Styling.EditorWindows.Show(_exportationRoutes, this);
            _exportationRoutes.BringToFront();
        }
        if (_exportationRoutes is { IsDisposed: false }) _exportationRoutes.Response(packet);
    }
}
