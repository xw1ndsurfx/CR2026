namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class ForceRainCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.ForceRain;
    public int DurationSeconds { get; set; } = 900;
}
