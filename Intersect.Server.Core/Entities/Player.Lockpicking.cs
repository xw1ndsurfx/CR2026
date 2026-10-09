using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities.Events;
using Intersect.Server.MiniGames.Lockpicking;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    internal void ResolveLockpickingEvent(
        Guid eventId,
        bool success,
        bool offerFailureChoices = false
    )
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
                else if (offerFailureChoices && command.LockpickFailureChoicesEnabled)
                {
                    ShowLockpickingFailureChoicesLocked(evt, stack, command);
                }
                else
                {
                    evt.CallStack.Clear();
                }

                return;
            }
        }
    }

    internal void RespondToLockpickingFailureChoice(
        Guid eventId,
        StartMiniGameCommand command,
        int responseId
    )
    {
        Event? targetEvent = null;
        CommandInstance? targetStack = null;
        Guid pageInstanceId = Guid.Empty;
        Guid lockId = Guid.Empty;

        lock (mEventLock)
        {
            foreach (var entry in EventLookup)
            {
                var evt = entry.Value;
                if (evt.PageInstance?.Id != eventId || evt.CallStack.Count == 0)
                    continue;

                var stack = evt.CallStack.Peek();
                if (stack.WaitingForResponse != CommandInstance.EventResponse.Dialogue ||
                    stack.WaitingOnCommand is not StartMiniGameCommand waiting ||
                    waiting.Game != MiniGameType.Lockpicking)
                {
                    return;
                }

                // Claim this response before leaving the event lock so duplicate packets
                // cannot execute two failure actions.
                stack.WaitingForResponse = CommandInstance.EventResponse.None;

                targetEvent = evt;
                targetStack = stack;
                pageInstanceId = evt.PageInstance.Id;
                lockId = evt.Descriptor?.Id ?? Guid.Empty;
                break;
            }
        }

        if (targetEvent == null || targetStack == null)
            return;

        switch (responseId)
        {
            case 1:
                LockpickingRuntime.ClearFailureCooldown(this, lockId, MapInstanceId);

                if (LockpickingRuntime.IsUnlockedFor(
                        this,
                        command,
                        lockId,
                        MapId,
                        MapInstanceId
                    ))
                {
                    ResumeLockpickingEvent(targetEvent, targetStack);
                    LockpickingRuntime.NotifyCrewDecision(
                        this,
                        "The lock was already opened for the Crew."
                    );
                    return;
                }

                if (LockpickingRuntime.Join(
                        this,
                        command,
                        pageInstanceId,
                        lockId,
                        out var retryError
                    ))
                {
                    lock (mEventLock)
                    {
                        if (!IsCurrentLockpickingStack(targetEvent, targetStack))
                            return;

                        targetStack.WaitingForResponse = CommandInstance.EventResponse.MiniGame;
                        targetStack.WaitingOnCommand = command;
                    }

                    LockpickingRuntime.NotifyCrewDecision(
                        this,
                        "The Crew chose to retry the lock."
                    );
                    return;
                }

                PacketSender.SendChatMsg(
                    this,
                    $"[Lockpicking] {retryError}",
                    ChatMessageType.Error,
                    Color.White
                );
                RedisplayLockpickingFailureChoices(targetEvent, targetStack, command);
                return;

            case 2:
                if (LockpickingRuntime.TryUnlockWithKey(
                        this,
                        command,
                        lockId,
                        out var keyMessage
                    ))
                {
                    PacketSender.SendChatMsg(
                        this,
                        keyMessage,
                        ChatMessageType.Local,
                        Color.White
                    );
                    ResumeLockpickingEvent(targetEvent, targetStack);
                    LockpickingRuntime.NotifyCrewDecision(
                        this,
                        "The Crew used a key to open the lock."
                    );
                    return;
                }

                if (command.LockpickKeySearchCommonEventId != Guid.Empty)
                {
                    LockpickingRuntime.TriggerFailureChoiceCommonEvent(
                        this,
                        command.LockpickKeySearchCommonEventId,
                        command.LockpickFailureChoicesAffectCrew
                    );
                    LockpickingRuntime.NotifyCrewDecision(
                        this,
                        "The Crew decided to search for another way to obtain the key."
                    );
                    ClearLockpickingEvent(targetEvent, targetStack);
                    return;
                }

                PacketSender.SendChatMsg(
                    this,
                    "[Lockpicking] You do not have the configured key and no key-search event is configured.",
                    ChatMessageType.Error,
                    Color.White
                );
                RedisplayLockpickingFailureChoices(targetEvent, targetStack, command);
                return;

            case 3:
                if (command.LockpickGuardianCommonEventId != Guid.Empty)
                {
                    LockpickingRuntime.TriggerFailureChoiceCommonEvent(
                        this,
                        command.LockpickGuardianCommonEventId,
                        command.LockpickFailureChoicesAffectCrew
                    );
                    LockpickingRuntime.NotifyCrewDecision(
                        this,
                        "The Crew chose to face the guardian."
                    );
                    ClearLockpickingEvent(targetEvent, targetStack);
                    return;
                }

                PacketSender.SendChatMsg(
                    this,
                    "[Lockpicking] No guardian encounter is configured for this lock.",
                    ChatMessageType.Error,
                    Color.White
                );
                RedisplayLockpickingFailureChoices(targetEvent, targetStack, command);
                return;

            default:
                LockpickingRuntime.NotifyCrewDecision(
                    this,
                    "The Crew abandoned the lock for now."
                );
                ClearLockpickingEvent(targetEvent, targetStack);
                return;
        }
    }

    private void ResumeLockpickingEvent(Event evt, CommandInstance stack)
    {
        lock (mEventLock)
        {
            if (!IsCurrentLockpickingStack(evt, stack))
                return;

            stack.WaitingForResponse = CommandInstance.EventResponse.None;
            stack.WaitingOnCommand = null;
        }
    }

    private void ClearLockpickingEvent(Event evt, CommandInstance stack)
    {
        lock (mEventLock)
        {
            if (!IsCurrentLockpickingStack(evt, stack))
                return;

            evt.CallStack.Clear();
        }
    }

    private void RedisplayLockpickingFailureChoices(
        Event evt,
        CommandInstance stack,
        StartMiniGameCommand command
    )
    {
        lock (mEventLock)
        {
            if (!IsCurrentLockpickingStack(evt, stack))
                return;

            ShowLockpickingFailureChoicesLocked(evt, stack, command);
        }
    }

    private bool IsCurrentLockpickingStack(Event evt, CommandInstance stack) =>
        evt.CallStack.Count > 0 &&
        ReferenceEquals(evt.CallStack.Peek(), stack);

    private void ShowLockpickingFailureChoicesLocked(
        Event evt,
        CommandInstance stack,
        StartMiniGameCommand command
    )
    {
        stack.WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        stack.WaitingOnCommand = command;

        var keyOption = command.LockpickKeyItemId != Guid.Empty
            ? "Use / Find Key"
            : "Find Another Way";

        PacketSender.SendEventDialog(
            this,
            "The lock resisted your attempt. What should the Crew do?",
            "Retry Lock",
            keyOption,
            "Face Guardian",
            "Give Up",
            string.Empty,
            evt.PageInstance!.Id
        );

        LockpickingRuntime.NotifyCrewDecision(
            this,
            "The lockpicking attempt failed. A decision is required."
        );
    }
}
