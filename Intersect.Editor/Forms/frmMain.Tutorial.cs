using Intersect.Editor.Forms.Tutorials;
using Intersect.Editor.Networking;
using Intersect.Network.Packets.Tutorials;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmTutorial? _tutorialEditor;
    private void AddTutorialMenu()
    {
        var menu = new ToolStripMenuItem(FrmTutorial.T("Tutorial editor")) { Name = "royalTutorialEditor" };
        menu.Click += (_, _) => { if (_tutorialEditor is { IsDisposed: false }) _tutorialEditor.Activate(); else Intersect.Editor.Networking.Network.SendPacket(new TutorialEditorPacket()); };
        contentEditorsToolStripMenuItem.DropDownItems.Add(menu);
    }
    internal void ReceiveTutorial(TutorialEditorPacket packet)
    {
        if (_tutorialEditor is { IsDisposed: false }) { _tutorialEditor.Receive(packet); return; }
        if (packet.Error.Length > 0) { MessageBox.Show(this, packet.Error); return; }
        _tutorialEditor = new FrmTutorial(packet); _tutorialEditor.Show(this);
    }
}
