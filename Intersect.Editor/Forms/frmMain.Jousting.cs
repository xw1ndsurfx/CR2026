using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmJoustConfiguration? _joustEditor;
    private void AddJoustEditorMenu()
    {
        var item = new ToolStripMenuItem(QualityOfLifeText.Get("Jousting Editor", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) { Name = "miniGame.Jousting" };
        item.Click += (_, _) => Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.GetConfiguration });
        menuStrip.Items.OfType<ToolStripMenuItem>().First(m => m.Name == "miniGameEditorsToolStripMenuItem").DropDownItems.Add(item);
    }
    internal void ApplyJoustConfiguration(JoustStatePacket packet)
    {
        if (_joustEditor is { IsDisposed: false })
        {
            _joustEditor.ApplyMounts(packet.MountsJson);
            if (packet.RequestId != Guid.Empty) _joustEditor.Response(packet);
            else EditorWindows.Show(_joustEditor, this);
            return;
        }
        if (packet.ConfigurationJson.Length == 0) return;
        _joustEditor = new(packet.ConfigurationJson); _joustEditor.ApplyMounts(packet.MountsJson); EditorWindows.Show(_joustEditor, this);
    }
}
