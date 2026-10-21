namespace Intersect.Framework.Core.GameObjects.Events.Commands;
public sealed class OpenNavalShopCommand : EventCommand
{
    public Guid ShopId {get;set;}
    public override EventCommandType Type => EventCommandType.OpenNavalShop;
}
