namespace Intersect.Framework.Core.GameObjects.Events.Commands;

/// <summary>Place on a stable NPC or map event to open mount management.</summary>
public sealed class OpenStableCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenStable;
}
