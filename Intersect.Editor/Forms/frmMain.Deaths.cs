using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Networking;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmDeathConfiguration? _deathEditor;
    private void AddSystemEditorMenu()
    {
        if (menuStrip.Items.Cast<ToolStripItem>().Any(item => item.Name == "systemEditorToolStripMenuItem")) return;
        var system = new ToolStripMenuItem("System Editor") { Name = "systemEditorToolStripMenuItem", ForeColor = System.Drawing.Color.Gainsboro };
        var death = new ToolStripMenuItem("Death Editor...") { ForeColor = System.Drawing.Color.Gainsboro };
        death.Click += (_, _) =>
        {
            if (_deathEditor is { IsDisposed: false }) { _deathEditor.BringToFront(); return; }
            Intersect.Editor.Networking.Network.SendPacket(new DeathConfigurationRequestPacket());
        };
        system.DropDownItems.Add(death);
        var events = menuStrip.Items.Cast<ToolStripItem>().FirstOrDefault(item => item.Name == "worldEventsToolStripMenuItem");
        menuStrip.Items.Insert(events == null ? menuStrip.Items.Count : menuStrip.Items.IndexOf(events) + 1, system);
    }
    public void ReceiveDeathConfiguration(DeathConfigurationPacket packet)
    {
        if (_deathEditor is { IsDisposed: false }) { _deathEditor.AcceptResponse(packet); return; }
        if (packet.Error.Length > 0) { MessageBox.Show(this, packet.Error, "Death Editor"); return; }
        if (!packet.Saved) { _deathEditor = new FrmDeathConfiguration(packet.ConfigurationJson); _deathEditor.Show(this); }
    }
}
