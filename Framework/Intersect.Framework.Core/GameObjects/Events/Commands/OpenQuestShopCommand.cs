using System.ComponentModel;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class OpenQuestShopCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenQuestShop;

    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid QuestShopId { get; set; }
}
