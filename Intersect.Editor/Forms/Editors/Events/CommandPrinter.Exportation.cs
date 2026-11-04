using Intersect.Editor.Maps;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(OpenExportationCommand command, MapInstance map)
    {
        var name = ExportationText.Get("Open Exportation",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var route = ExportationEditorState.Configuration.Routes.FirstOrDefault(r => r.Id == command.RouteId);
        return name + ": " + (route?.Name ?? command.RouteId.ToString());
    }
}
