using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>
/// Last server-confirmed routes for the Event command picker.
/// A failed refresh never replaces the previous valid configuration.
/// </summary>
internal static class ExportationEditorState
{
    internal static ExportationConfiguration Configuration { get; private set; } = new() { Enabled = false };
    internal static event Action? Changed;

    internal static void Refresh() =>
        Intersect.Editor.Networking.Network.SendPacket(
            new ExportationRequestPacket { Action = ExportationAction.GetConfiguration, OpenEditor = false });

    internal static void Accept(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            Configuration = ExportationConfiguration.FromJson(json);
            Changed?.Invoke();
        }
        catch (Exception error) when (error is InvalidDataException or
            System.Text.Json.JsonException or ArgumentException)
        {
            // Preserve the known-good snapshot after invalid server data.
        }
    }
}
