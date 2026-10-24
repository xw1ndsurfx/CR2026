using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmMountConfiguration? _mountEditor;
    private void AddMountEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems.Find("mountEditorToolStripMenuItem", false).Length > 0) return;
        var item = new ToolStripMenuItem(QualityOfLifeText.Get("Mount Editor", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) { Name = "mountEditorToolStripMenuItem" };
        item.Click += (_, _) => Networking.Network.SendPacket(new MountRequestPacket { Action = MountAction.GetConfiguration });
        contentEditorsToolStripMenuItem.DropDownItems.Insert(
            contentEditorsToolStripMenuItem.DropDownItems.IndexOf(npcEditorToolStripMenuItem) + 1, item);
    }
    internal void ApplyMountConfiguration(MountStatePacket packet)
    {
        if (_mountEditor is { IsDisposed: false })
        {
            if (packet.Saved || packet.Message.Length > 0) _mountEditor.SaveResponse(packet.Saved, packet.Message, packet.RequestId);
            else EditorWindows.Show(_mountEditor, this);
            return;
        }
        if (packet.Saved || packet.Message.Length > 0) return;
        _mountEditor = new FrmMountConfiguration(packet.ConfigurationJson);
        EditorWindows.Show(_mountEditor, this);
    }
}
