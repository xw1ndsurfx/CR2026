using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmFishing? _fishingEditor;
    private bool _fishingEditorRequested;
    private void AddFishingEditorMenu()
    {
        var menu = GetOrCreateMiniGameEditorsMenu();
        if (menu.DropDownItems.Cast<ToolStripItem>().Any(i => i.Name == "miniGame.Fishing")) return;
        var item = new ToolStripMenuItem(FishingEditorText.Get("Fishing Editor")) { Name = "miniGame.Fishing" };
        item.Click += (_, _) => {
            if (_fishingEditor is { IsDisposed: false }) { EditorWindows.Show(_fishingEditor, this); _fishingEditor.BringToFront(); return; }
            _fishingEditorRequested = true; FishingEditorState.RequestDraft();
        };
        menu.DropDownItems.Add(item);
    }
    internal void ApplyFishingConfiguration(FishingStatePacket packet)
    {
        if (!packet.Editor) return;
        try
        {
            if (!packet.Draft) FishingEditorState.Apply(packet.ConfigurationJson);
            else if (!string.IsNullOrWhiteSpace(packet.ConfigurationJson))
                FishingRules.ValidateDraft(FishingConfiguration.FromJson(packet.ConfigurationJson));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
        {
            ReportFishingLoadFailure(FishingEditorText.Get("The server returned an invalid fishing configuration. Your draft was preserved."), packet.Draft);
            return;
        }
        if (_fishingEditor is { IsDisposed: false }) { _fishingEditor.Response(packet); return; }
        // An Event picker can refresh the active cache while the hub waits for its draft.
        if (!_fishingEditorRequested || !packet.Draft) return;
        if (string.IsNullOrEmpty(packet.ConfigurationJson))
        {
            ReportFishingLoadFailure(string.IsNullOrEmpty(packet.Message) ? FishingEditorText.Get("The server did not return a fishing configuration.") :
                FishingUiText.Get(packet.Message, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName), packet.Draft);
            return;
        }
        _fishingEditorRequested = false;
        _fishingEditor = new FrmFishing(packet.ConfigurationJson); EditorWindows.Show(_fishingEditor, this);
    }
    private void ReportFishingLoadFailure(string message, bool draft)
    {
        var requested = _fishingEditorRequested && draft;
        if (draft) _fishingEditorRequested = false;
        if (_fishingEditor is { IsDisposed: false })
            _fishingEditor.Response(new FishingStatePacket { Editor = true, Draft = draft, Message = message });
        else if (requested) MessageBox.Show(this, message, FishingEditorText.Get("Fishing Editor"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
