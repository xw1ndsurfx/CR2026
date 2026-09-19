namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class LeaveMiniGameCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.LeaveMiniGame;
}
