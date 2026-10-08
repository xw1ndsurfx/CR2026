using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities.Events;

namespace Intersect.Server.Entities;

public partial class Player
{
    internal void ResolveLockpickingEvent(Guid eventId, bool success)
    {
        lock (mEventLock)
        {
            foreach (var entry in EventLookup)
            {
                var evt = entry.Value;
                if (evt.PageInstance?.Id != eventId || evt.CallStack.Count == 0)
                    continue;

                var stack = evt.CallStack.Peek();
                if (stack.WaitingForResponse != CommandInstance.EventResponse.MiniGame ||
                    stack.WaitingOnCommand is not StartMiniGameCommand command ||
                    command.Game != MiniGameType.Lockpicking)
                {
                    return;
                }

                if (success)
                {
                    stack.WaitingForResponse = CommandInstance.EventResponse.None;
                    stack.WaitingOnCommand = null;
                }
                else
                {
                    evt.CallStack.Clear();
                }

                return;
            }
        }
    }
}
