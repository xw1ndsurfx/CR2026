using Intersect.Editor.Localization;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(ProtectDialogueCommand command, MapInstance map) =>
        EditorSafetyText.Get("Protect player during dialogue");
}
