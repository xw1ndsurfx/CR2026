using Intersect.Editor.Localization;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(OpenFishingCommand command, MapInstance map) =>
        FishingEditorText.Get("Open Fishing") + ": " +
        (FishingEditorState.Configuration.Zones.FirstOrDefault(z => z.Id == command.ZoneId)?.Name ?? command.ZoneId.ToString());
}
