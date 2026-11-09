namespace Intersect.Framework.Core.GameObjects.Events.Commands;
public enum ShipRewardAction : byte { UnlockModel, GrantShip }
public sealed class ShipRewardCommand : EventCommand
{
    public Guid ShopId {get;set;}
    public string ModelId {get;set;}="";
    public ShipRewardAction Action {get;set;}
    public override EventCommandType Type=>EventCommandType.ShipReward;
}
