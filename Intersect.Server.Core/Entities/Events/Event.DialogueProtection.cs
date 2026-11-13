namespace Intersect.Server.Entities.Events;

public partial class Event
{
    public bool IsDialogueProtected
    {
        get
        {
            // Snapshot rather than enumerate a stack that event processing can change.
            var frames = CallStack.ToArray();
            return frames.Length > 0 &&
                (frames[0].WaitingForResponse is CommandInstance.EventResponse.Dialogue or CommandInstance.EventResponse.Quest) &&
                frames.Any(frame => frame.ProtectDialogue);
        }
    }
}
