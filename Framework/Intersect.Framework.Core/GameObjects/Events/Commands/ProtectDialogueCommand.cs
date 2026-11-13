namespace Intersect.Framework.Core.GameObjects.Events.Commands;

/// <summary>Protects this event run's dialogue from NPC aggression.</summary>
public sealed class ProtectDialogueCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.ProtectDialogue;
}
