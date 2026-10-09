using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities.Events;
using Intersect.Server.MiniGames.Lockpicking;
using Intersect.Server.Networking;

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
                else if (command.LockpickFailureChoicesEnabled)
                {
                    ShowLockpickingFailureChoices(evt, stack, command);
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

                switch (responseId)
                {
                    case 1:
                        LockpickingRuntime.ClearFailureCooldown(
                            this,
                            evt.Descriptor?.Id ?? Guid.Empty,
                            MapInstanceId
                        );

                        if (LockpickingRuntime.Join(
                                this,
                                command,
                                evt.PageInstance.Id,
                                evt.Descriptor?.Id ?? Guid.Empty,
                                out var retryError
                            ))
                        {
                            stack.WaitingForResponse = CommandInstance.EventResponse.MiniGame;
                            stack.WaitingOnCommand = command;
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
                        ShowLockpickingFailureChoices(evt, stack, command);
                        return;

                    case 2:
                        if (LockpickingRuntime.TryUnlockWithKey(
                                this,
                                command,
                                evt.Descriptor?.Id ?? Guid.Empty,
                                out var keyMessage
                            ))
                        {
                            PacketSender.SendChatMsg(
                                this,
                                keyMessage,
                                ChatMessageType.Local,
                                Color.White
                            );
                            stack.WaitingForResponse = CommandInstance.EventResponse.None;
                            stack.WaitingOnCommand = null;
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
                            evt.CallStack.Clear();
                            return;
                        }

                        PacketSender.SendChatMsg(
                            this,
                            "[Lockpicking] You do not have the configured key and no key-search event is configured.",
                            ChatMessageType.Error,
                            Color.White
                        );
                        ShowLockpickingFailureChoices(evt, stack, command);
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
                            evt.CallStack.Clear();
                            return;
                        }

                        PacketSender.SendChatMsg(
                            this,
                            "[Lockpicking] No guardian encounter is configured for this lock.",
                            ChatMessageType.Error,
                            Color.White
                        );
                        ShowLockpickingFailureChoices(evt, stack, command);
                        return;

                    default:
                        LockpickingRuntime.NotifyCrewDecision(
                            this,
                            "The Crew abandoned the lock for now."
                        );
                        evt.CallStack.Clear();
                        return;
                }
            }
        }
    }

    private void ShowLockpickingFailureChoices(
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
