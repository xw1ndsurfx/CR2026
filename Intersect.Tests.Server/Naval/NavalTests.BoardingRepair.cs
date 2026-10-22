using System.Reflection;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Naval;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [TestCase(NavalRepairKind.Hammering)]
    [TestCase(NavalRepairKind.Caulking)]
    [TestCase(NavalRepairKind.Pumping)]
    public void EveryRepairSizeHasASolvableHullAndNeverAllowsOverlapsOrRowWrapping(NavalRepairKind kind)
    {
        for(var pieces=3;pieces<=30;pieces++)
        {
            var board=NavalRepairPuzzle.Create(kind,pieces);var placed=0;
            while(board.Contains((byte)1))
            {
                var first=Array.IndexOf(board,(byte)1);
                Assert.That(NavalRepairPuzzle.Place(board,kind,first,false),Is.True,$"{kind}, {pieces}, cell {first}");placed++;
                var snapshot=board.ToArray();Assert.That(NavalRepairPuzzle.Place(board,kind,first,false),Is.False);Assert.That(board,Is.EqualTo(snapshot));
            }
            Assert.That(placed,Is.EqualTo(pieces));
        }
        Assert.That(NavalRepairPuzzle.Place(Enumerable.Repeat((byte)1,24).ToArray(),kind,-1,false),Is.False);
        Assert.That(NavalRepairPuzzle.Place(Enumerable.Repeat((byte)1,24).ToArray(),kind,5,false),Is.False);
    }
    [Test] public void AVerticalPieceCanFillAVerticalBreachButCannotExtendBeyondTheHull()
    {
        var board=new byte[18];board[0]=board[6]=board[12]=1;
        Assert.That(NavalRepairPuzzle.Place(board,NavalRepairKind.Caulking,0,false),Is.False);
        Assert.That(NavalRepairPuzzle.Place(board,NavalRepairKind.Caulking,0,true),Is.True);
        Assert.That(NavalRepairPuzzle.CanPlace(board,NavalRepairKind.Caulking,int.MaxValue,true),Is.False);
        Assert.That(NavalRepairPuzzle.CanPlace(board,(NavalRepairKind)255,0,false),Is.False);
    }
    [Test] public void BoardingRequiresADisabledNearbyEnemyAndCannotReserveASunkOrBusyShip()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(13.5,10.5,true);
        world.Set(ship,"OwnerId",world.State(ship).CaptainId);var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");
        Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.False);
        patrol.BoardingDungeonId=Guid.NewGuid();patrol.BoardingEntryMapId=Guid.NewGuid();world.Set(enemy,"Health",25);
        Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.True);
        world.Set(enemy,"Health",26);Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.False);world.Set(enemy,"Health",25);
        world.State(ship).Speed=1.1;Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.True);world.State(ship).Speed=0;
        world.Set(enemy,"EnemyBoardingEncounter",Guid.NewGuid());Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.False);world.Set(enemy,"EnemyBoardingEncounter",Guid.Empty);
        world.State(enemy).X=30;Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.False);world.State(enemy).X=13.5;
        world.Set(enemy,"Health",0);Assert.That(world.Invoke("CanBoardEnemy",ship,enemy),Is.False);
    }
    [TestCase(true)]
    [TestCase(false)]
    public void BoardingCompletionCreditsOnceAndFailureReleasesTheEnemyWithoutRewards(bool won)
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(13.5,10.5,true);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;var fields=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(NavalSystem).GetNestedType("EnemyBoardingRun",BindingFlags.NonPublic)!;
        var run=Activator.CreateInstance(type,true)!;var id=(Guid)type.GetField("Id",fields)!.GetValue(run)!;
        var captain=world.State(ship).CaptainId;var members=(HashSet<Guid>)type.GetField("Members",fields)!.GetValue(run)!;members.Add(captain);
        foreach(var (key,value) in new[]{("Ship",world.State(ship).Id),("Enemy",world.State(enemy).Id),("Captain",captain)})type.GetField(key,fields)!.SetValue(run,value);
        var runs=(System.Collections.IDictionary)typeof(NavalSystem).GetField("EnemyBoardings",flags)!.GetValue(null)!;runs.Add(id,run);
        try
        {
            var defeated=Guid.NewGuid();NavalSystem.CreditBoardingKill(id,captain,defeated);NavalSystem.CreditBoardingKill(id,captain,defeated);
            Assert.That(world.Field<Dictionary<Guid,long>>(enemy,"Contributions")[captain],Is.EqualTo(10));
            var participant=CargoPlayer();participant.Id=captain;
            Assert.That(NavalSystem.CanReceiveBoardingReward(participant,id),Is.True);
            participant.NavalPartyExitRequested=true;Assert.That(NavalSystem.CanReceiveBoardingReward(participant,id),Is.False);
            participant.NavalPartyExitRequested=false;
            var crew=CargoPlayer();crew.Id=Guid.NewGuid();members.Add(crew.Id);crew.Party=[participant,crew];
            Assert.That(NavalSystem.CanReceiveBoardingReward(crew,id),Is.True);crew.Party=[];
            Assert.That(NavalSystem.CanReceiveBoardingReward(crew,id),Is.False);
            members.Remove(crew.Id);world.Set(enemy,"Health",25);world.Set(enemy,"EnemyBoardingEncounter",id);
            NavalSystem.FinishEnemyBoarding(id,won);var reward=world.Reward(captain);
            Assert.That(world.Field<int>(enemy,"Health"),Is.EqualTo(won?0:26));Assert.That(reward,Is.EqualTo(won?100:0));Assert.That(world.Field<Guid>(enemy,"EnemyBoardingEncounter"),Is.EqualTo(Guid.Empty));
            NavalSystem.FinishEnemyBoarding(id,true);Assert.That(world.Reward(captain),Is.EqualTo(reward));
        }
        finally{runs.Remove(id);}
    }
}
