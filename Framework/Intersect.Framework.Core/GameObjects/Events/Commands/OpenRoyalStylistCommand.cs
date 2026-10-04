using System.ComponentModel;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class OpenRoyalStylistCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenRoyalStylist;

    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid StylistId { get; set; }
}
