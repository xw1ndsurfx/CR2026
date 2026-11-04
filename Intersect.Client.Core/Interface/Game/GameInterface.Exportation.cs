using System.Text.Json;
using Intersect.Client.General;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.Exportation;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ExportationWindow? _exportationWindow;
    private Guid _exportationRunId;
    private long _exportationSequence;

    internal void ApplyExportationState(ExportationStatePacket packet)
    {
        if (packet.Close)
        {
            CloseExportationWindow();
            return;
        }
        if (!string.IsNullOrWhiteSpace(packet.Message))
            ChatboxMsg.AddMessage(new ChatboxMsg(packet.Message, Color.White, ChatMessageType.Notice));
        if (packet.RunJson.Length == 0 || packet.RouteJson.Length == 0) return;
        try
        {
            var route = JsonSerializer.Deserialize<ExportationRoute>(packet.RouteJson);
            var run = JsonSerializer.Deserialize<ExportationRun>(packet.RunJson);
            if (route is not { IsValid: true } || run is null || run.RouteId != route.Id ||
                run.Id == Guid.Empty) return;
            if (_exportationRunId != run.Id)
            {
                CloseExportationWindow();
                _exportationRunId = run.Id;
                _exportationSequence = 0;
            }
            if (!packet.Open && _exportationWindow == null) return;
            ShowExportation(route, run, packet.MerchantName, packet.RecipientName,
                packet.SourceMapName, packet.DestinationMapName, packet.HasUsableBoat,
                packet.ProfessionLevel,
                (item, group, delta) => SendExportation(ExportationAction.Pack, item, group, delta),
                () => SendExportation(ExportationAction.Seal),
                () => SendExportation(ExportationAction.Deliver),
                () => SendExportation(ExportationAction.Restart));
        }
        catch (JsonException)
        {
            ChatboxMsg.AddMessage(new ChatboxMsg(
                ExportationText.Get("Exportation data could not be read.", ClientLanguages.Current),
                Color.Red, ChatMessageType.Error));
            CloseExportationWindow();
        }
    }

    private void SendExportation(ExportationAction action, Guid itemId = default,
        CargoGroup group = CargoGroup.Materials, int delta = 0)
    {
        if (_exportationRunId == Guid.Empty) return;
        Networking.Network.SendPacket(new ExportationRequestPacket
        {
            Action = action,
            RunId = _exportationRunId,
            Sequence = ++_exportationSequence,
            ItemId = itemId,
            Group = group,
            QuantityChange = delta,
        });
    }

    private void ShowExportation(ExportationRoute route, ExportationRun run,
        string merchantName, string recipientName, string sourceMapName, string destinationMapName,
        bool hasUsableBoat, int level, Action<Guid, CargoGroup, int> onPack,
        Action onSeal, Action onDeliver, Action onRestart)
    {
        if (_exportationWindow == null)
        {
            _exportationWindow = new ExportationWindow(GameCanvas,
                onPack, onSeal, onDeliver, onRestart);
            GameMenu?.HideWindows();
        }
        _exportationWindow.Apply(route, run, merchantName, recipientName,
            sourceMapName, destinationMapName, hasUsableBoat, level);
    }

    private void UpdateExportation()
    {
        if (_exportationWindow is not { } window) return;
        if (window.ExitRequested || Globals.Me == null) { CloseExportationWindow(); return; }
        window.Update();
    }

    private bool CloseExportationWindow()
    {
        if (_exportationWindow == null) return false;
        SendExportation(ExportationAction.Leave);
        _exportationWindow.Destroy();
        _exportationWindow = null;
        _exportationRunId = Guid.Empty;
        return true;
    }

    private void DisposeExportation()
    {
        _exportationWindow?.ReleaseForCanvasDispose();
        _exportationWindow = null;
        _exportationRunId = Guid.Empty;
        _exportationSequence = 0;
    }
}
