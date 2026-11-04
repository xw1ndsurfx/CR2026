using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmExportationEditor? _exportationEditor;
    private bool _exportationEditorRequested;

    private void AddExportationEditorMenu()
    {
        var menu = GetOrCreateMiniGameEditorsMenu();
        if (menu.DropDownItems.Cast<ToolStripItem>().Any(item => item.Name == "miniGame.Exportation")) return;
        var entry = new ToolStripMenuItem("Exportation Editor") { Name = "miniGame.Exportation" };
        entry.Click += (_, _) =>
        {
            if (_exportationEditor is { IsDisposed: false })
            {
                Styling.EditorWindows.Show(_exportationEditor, this);
                _exportationEditor.BringToFront();
                return;
            }
            if (_exportationEditorRequested) return;
            _exportationEditorRequested = true;
            Intersect.Editor.Networking.Network.SendPacket(
                new ExportationRequestPacket
                {
                    Action = ExportationAction.GetConfiguration,
                    OpenEditor = true,
                });
        };
        menu.DropDownItems.Add(entry);
    }

    internal void ApplyExportationConfiguration(ExportationStatePacket packet)
    {
        if (!packet.Editor) return;
        if (!string.IsNullOrWhiteSpace(packet.ConfigurationJson))
            Intersect.Editor.Forms.Editors.Events.ExportationEditorState.Accept(packet.ConfigurationJson);

        if (_exportationEditor is { IsDisposed: false })
        {
            _exportationEditor.Response(packet);
            return;
        }

        if (!_exportationEditorRequested) return;
        if (!string.IsNullOrWhiteSpace(packet.Message) &&
            string.IsNullOrWhiteSpace(packet.ConfigurationJson))
        {
            _exportationEditorRequested = false;
            MessageBox.Show(this, packet.Message, "Exportation Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!packet.Open || string.IsNullOrWhiteSpace(packet.ConfigurationJson)) return;
        _exportationEditorRequested = false;
        try
        {
            _exportationEditor = new FrmExportationEditor(packet.ConfigurationJson);
            Styling.EditorWindows.Show(_exportationEditor, this);
        }
        catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, error.Message, "Exportation Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
