using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmMountRaceConfiguration? _raceEditor;
    private void AddMountRaceEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems.Find("mountRaceEditor", false).Length > 0) return;
        var item = new ToolStripMenuItem(FrmMountRaceConfiguration.T("Royal Races Editor")) { Name = "mountRaceEditor" };
        item.Click += (_, _) => Networking.Network.SendPacket(new MountRaceRequestPacket { Action = MountRaceAction.GetConfiguration });
        contentEditorsToolStripMenuItem.DropDownItems.Add(item);
    }
    internal void ApplyMountRaceConfiguration(MountRaceStatePacket packet)
    {
        if (_raceEditor is { IsDisposed: false })
        { if (packet.Saved || packet.Message.Length > 0) _raceEditor.SaveResponse(packet.Saved, packet.Message, packet.RequestId); else EditorWindows.Show(_raceEditor, this); return; }
        if (packet.Saved || packet.Message.Length > 0) return;
        _raceEditor = new(packet.Json, packet.MountsJson); EditorWindows.Show(_raceEditor, this);
    }
}
