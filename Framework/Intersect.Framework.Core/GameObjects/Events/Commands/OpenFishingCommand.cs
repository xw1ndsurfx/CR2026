namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class OpenFishingCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenFishing;
    public Guid ZoneId { get; set; }
}
