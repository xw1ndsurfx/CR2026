using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmRoyalContracts? _royalContractsEditor;
    private void AddRoyalContractsEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems.Cast<ToolStripItem>().Any(i => i.Name == "royalContractsEditorToolStripMenuItem")) return;
        var item = new ToolStripMenuItem(RoyalContractsText.Get("Royal Contracts management", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
        { Name = "royalContractsEditorToolStripMenuItem" };
        item.Click += (_, _) => Intersect.Editor.Networking.Network.SendPacket(new RoyalContractsConfigurationRequestPacket { OpenEditor = true });
        contentEditorsToolStripMenuItem.DropDownItems.Add(item);
    }
    internal void ApplyRoyalContractsConfiguration(RoyalContractsConfigurationPacket packet)
    {
        if (packet.OpenEditor)
        {
            if (_royalContractsEditor is not { IsDisposed: false }) _royalContractsEditor = new FrmRoyalContracts(packet.ConfigurationJson);
            Styling.EditorWindows.Show(_royalContractsEditor, this); _royalContractsEditor.BringToFront();
        }
        if (_royalContractsEditor is { IsDisposed: false }) _royalContractsEditor.SetStatus(packet.Message);
    }
}
