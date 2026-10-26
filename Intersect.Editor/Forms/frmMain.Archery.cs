using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private FrmRoyalCombatConfiguration? _royalCombatEditor;
    private bool _royalMasteries;
    private void AddRoyalCombatEditorMenus()
    {
        foreach (var masteries in new[] { true, false })
        {
            var item = new ToolStripMenuItem(QualityOfLifeText.Get(masteries ? "Weapon Mastery Editor" : "Archery Mini-Game Editor",
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) { Name = masteries ? "weaponMasteryEditor" : "miniGame.Archery" };
            item.Click += (_, _) => {
                if (_royalCombatEditor is { IsDisposed: false }) { _royalCombatEditor.ShowMode(masteries); EditorWindows.Show(_royalCombatEditor, this); return; }
                _royalMasteries = masteries;
                Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.GetConfiguration });
            };
            var menu = masteries ? "contentEditorsToolStripMenuItem" : "miniGameEditorsToolStripMenuItem";
            menuStrip.Items.OfType<ToolStripMenuItem>().First(m => m.Name == menu).DropDownItems.Add(item);
        }
    }
    internal void ApplyRoyalCombatConfiguration(ArcheryStatePacket packet)
    {
        if (_royalCombatEditor is { IsDisposed: false }) { _royalCombatEditor.Response(packet); return; }
        if (packet.ConfigurationJson.Length == 0) return;
        _royalCombatEditor = new(packet.ConfigurationJson, _royalMasteries); EditorWindows.Show(_royalCombatEditor, this);
    }
}
