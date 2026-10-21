using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using MessagePack;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void FailedSavePublishesNoReceiptAndSuccessDescribesOnlyRetainedCargo()
    {
        using var world=new EnemyTestWorld();var player=CargoPlayer();
        var ship=new OwnedShip {Cargo=[Parcel(1)]};player.NavalFleet.Add(ship);
        var high=Parcel(5);var low=Parcel(1);var awards=new[]{(ship.Id,1,high),(ship.Id,1,low)};
        Assert.That(NavalSystem.CommitNavalReward(player,275,awards,()=>false,out var failed),Is.False);
        Assert.That(failed,Is.Null);Assert.That(player.NavalFleet[0].Cargo[0].Quality,Is.EqualTo(1));
        Assert.That(NavalSystem.CommitNavalReward(player,275,awards,()=>true,out var saved),Is.True);
        Assert.That(saved.SailingExperience,Is.EqualTo(275));Assert.That(saved.Cargo.Single().Id,Is.EqualTo(high.Id));
        Assert.That(saved.ReplacedLots,Is.EqualTo(1));Assert.That(saved.DiscardedLots,Is.EqualTo(1));Assert.That(saved.CargoFull,Is.True);
        NavalSystem.PublishRewardReceipt(player,saved,100);NavalSystem.PublishRewardReceipt(player,saved,200);
        Assert.That(NavalSystem.RewardReceipts(player,600).Single().AgeMilliseconds,Is.EqualTo(500));
        Assert.That(NavalSystem.RewardReceipts(CargoPlayer(),600),Is.Empty,"A second player cannot see another hold's receipts.");
        Assert.That(NavalSystem.RewardReceipts(player,10101),Is.Empty);
    }
    [Test]
    public void ReceiptsAreBoundedAndRoundTripWithoutChangingTheRewardIdentity()
    {
        var player=CargoPlayer();
        for(var i=0;i<30;i++) NavalSystem.PublishRewardReceipt(player,new(){SailingExperience=i},i);
        var notices=NavalSystem.RewardReceipts(player,100);Assert.That(notices.Length,Is.EqualTo(16));
        var source=new NavalSnapshotPacket {Rewards=notices};
        var copy=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(source));
        Assert.That(copy.Rewards.Select(r=>r.Id),Is.EqualTo(notices.Select(r=>r.Id)));
        Assert.That(copy.Rewards[^1].SailingExperience,Is.EqualTo(29));
    }
    [Test]
    public void DisabledOrUnavailableHoldsDoNotProduceFalseFullWarnings()
    {
        using var world=new EnemyTestWorld();world.Configure(new(){Sailing=new(){MaximumLevel=1}});
        var player=CargoPlayer();var ship=new OwnedShip();player.NavalFleet.Add(ship);
        NavalSystem.CommitNavalReward(player,100,new[]{(ship.Id,0,Parcel())},()=>true,out var receipt);
        Assert.That(receipt.SailingExperience,Is.Zero);Assert.That(receipt.DiscardedLots,Is.Zero);Assert.That(receipt.CargoFull,Is.False);
        Assert.That(player.PendingNavalCharacterExperience,Is.EqualTo(100));
    }
}
