using Intersect.Editor.Networking;
using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private void AddWorldMapEditorMenu()
    {
        var item = new ToolStripMenuItem("World Map Editor...");
        item.Click += (_, _) => Networking.Network.SendPacket(new RequestWorldMapLegendPacket());
        contentEditorsToolStripMenuItem.DropDownItems.Add(item);
    }
}
