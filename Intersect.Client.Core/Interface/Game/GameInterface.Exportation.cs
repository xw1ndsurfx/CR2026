using Intersect.Client.General;
using Intersect.Framework.Core.Exportation;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ExportationWindow? _exportationWindow;

    /// <summary>
    /// Called only after a future authoritative server packet has been validated.
    /// Presently not exposed by an event or any client-side debug command.
    /// </summary>
    internal void ShowExportation(ExportationRoute route, ExportationRun run,
        string merchantName, string recipientName, string sourceMapName, string destinationMapName,
        bool hasUsableBoat, int level, Action<Guid, CargoGroup, int> onPack, Action onSeal)
    {
        if (!route.IsValid || run.RouteId != route.Id) return;
        if (_exportationWindow == null)
        {
            _exportationWindow = new ExportationWindow(GameCanvas, onPack, onSeal);
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
        _exportationWindow.Destroy();
        _exportationWindow = null;
        return true;
    }

    private void DisposeExportation()
    {
        // GameCanvas owns child disposal during logout; release only input blocking.
        _exportationWindow?.ReleaseForCanvasDispose();
        _exportationWindow = null;
    }
}
