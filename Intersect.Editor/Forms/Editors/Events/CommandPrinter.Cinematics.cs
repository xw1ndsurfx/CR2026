using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(PlayCinematicCommand command, MapInstance map) =>
        CinematicText.T("PlayCinematic") + ": " + command.SceneEventId;
    private static string GetCommandText(BeginCinematicCommand command, MapInstance map) => CinematicText.T("BeginCinematic");
    private static string GetCommandText(CinematicActorCommand command, MapInstance map) => CinematicText.T("Actor") + ": " + command.ActorId + " · " + command.Visible;
    private static string GetCommandText(AwaitCinematicConditionCommand command, MapInstance map) => CinematicText.T("Condition") + ": " + CinematicText.T(command.ConditionKind.ToString()) + " · " + command.ConditionTimeoutMs + "ms";
    private static string GetCommandText(CinematicQuestTaskCommand command, MapInstance map) => CinematicText.T("CompleteEventTask") + ": " + command.QuestId + " / " + command.TaskId;
    private static string GetCommandText(CinematicEnvironmentCommand command, MapInstance map) => CinematicText.T("Environment") + ": " + CinematicText.T(command.RestoreEnvironment ? "RestoreEnvironment" : "EnvironmentSettings");
    private static string GetCommandText(StopCinematicCommand command, MapInstance map) => CinematicText.T("StopCinematic") + ": " + command.SceneEventId;
    private static string GetCommandText(WaitForCinematicCommand command, MapInstance map) => CinematicText.T("WaitForCinematic") + ": " + command.SceneEventId;
    private static string GetCommandText(StartCinematicChapterCommand command, MapInstance map) => CinematicText.T("StartCinematicChapter") + ": " + command.ChapterEventId;
    private static string GetCommandText(CinematicCameraCommand command, MapInstance map) =>
        CinematicText.T("CameraCommand") + $": {command.X:0.#}, {command.Y:0.#} · {command.Zoom:0.##}x · {command.DurationMs}ms";
}
