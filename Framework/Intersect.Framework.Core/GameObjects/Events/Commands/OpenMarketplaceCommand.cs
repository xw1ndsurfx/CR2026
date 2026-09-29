namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public partial class OpenMarketplaceCommand : EventCommand
{
    public override EventCommandType Type { get; } = EventCommandType.OpenMarketplace;
}
