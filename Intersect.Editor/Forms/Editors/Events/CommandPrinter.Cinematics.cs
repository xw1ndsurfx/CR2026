using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(PlayCinematicCommand command, MapInstance map) =>
        CinematicText.T("PlayCinematic") + ": " + command.SceneEventId;
    private static string GetCommandText(BeginCinematicCommand command, MapInstance map) => CinematicText.T("BeginCinematic");
    private static string GetCommandText(CinematicCameraCommand command, MapInstance map) =>
        CinematicText.T("CameraCommand") + $": {command.X:0.#}, {command.Y:0.#} · {command.Zoom:0.##}x · {command.DurationMs}ms";
}
