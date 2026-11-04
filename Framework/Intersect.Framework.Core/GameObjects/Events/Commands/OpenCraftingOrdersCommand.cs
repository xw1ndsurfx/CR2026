namespace Intersect.Framework.Core.GameObjects.Events.Commands;

/// <summary>Opens the independent player crafting commissions board.</summary>
public sealed class OpenCraftingOrdersCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.OpenCraftingOrders;
}
