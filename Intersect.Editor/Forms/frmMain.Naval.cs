using Intersect.Editor.Forms.Editors;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmNavalConfiguration? _navalEditor;
    private void AddNavalEditorMenu(ToolStripMenuItem system)
    {
        var item = new ToolStripMenuItem(FrmNavalConfiguration.T("Naval System Editor")) { Name = "navalSystemEditor" };
        item.Click += (_, _) =>
        {
            if (_navalEditor is { IsDisposed: false }) { _navalEditor.BringToFront(); return; }
            Intersect.Editor.Networking.Network.SendPacket(new NavalConfigurationRequestPacket());
        };
        system.DropDownItems.Add(item);
    }
    public void ReceiveNavalConfiguration(NavalConfigurationPacket packet)
    {
        if(Cinematics.FrmCinematicDirector.ReceiveVesselModels(packet))return;
        if(FrmItem.ReceiveItemAmmoConfiguration(packet))return;
        if(FrmShop.ReceiveNavalShopConfiguration(packet))return;
        if (_navalEditor is { IsDisposed: false }) { _navalEditor.AcceptResponse(packet); return; }
        if (packet.Error.Length > 0) { MessageBox.Show(this, FrmNavalConfiguration.T(packet.Error), FrmNavalConfiguration.T("Naval System Editor")); return; }
        if (packet.SavedSection == 0) { _navalEditor = new FrmNavalConfiguration(packet); Styling.EditorWindows.Show(_navalEditor, this); }
    }
}
