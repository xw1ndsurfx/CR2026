namespace Intersect.Framework.Core.GameObjects.Events.Commands;

/// <summary>
/// Attach to the merchant event and recipient event. Their descriptor IDs must
/// be the corresponding SourceNpcId and RecipientNpcId in the authored route.
/// </summary>
public sealed class OpenExportationCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenExportation;
    public Guid RouteId { get; set; }
}
