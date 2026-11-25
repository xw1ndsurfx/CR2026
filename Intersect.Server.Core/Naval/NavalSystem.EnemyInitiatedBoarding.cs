using Intersect.Server.Entities;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static readonly Dictionary<Guid,(Guid Captain,long Time)> EnemyBoardingAttempts=[];
    private static readonly Dictionary<Guid,Guid> EnemyBoardingRequests=[];
    private static bool CanEnemyBoardPlayer(Vessel enemy,Vessel ship)=>
        enemy.Enemy is {CanBoardPlayerShips:true} patrol && (patrol.AutomaticBoardingDeck || patrol.BoardingDungeonId!=Guid.Empty && patrol.BoardingEntryMapId!=Guid.Empty) &&
        enemy.Health>0 && enemy.SunkUntil==0 && enemy.EnemyBoardingEncounter==Guid.Empty && !enemy.RewardClaimed &&
        ship.Enemy==null && ship.OwnerId!=Guid.Empty && ship.OwnerId==ship.State.CaptainId && ship.Health>0 &&
        ship.Health*100L<=ship.MaximumHealth*(long)patrol.PlayerBoardingHealthPercent && ship.SunkUntil==0 && ship.EnemyBoardingEncounter==Guid.Empty &&
        BoardingDistance(ship,enemy)<=patrol.BoardingRange && !SafeDock(enemy,ship.State.MapId,ship.State.X,ship.State.Y) && !SafeDock(enemy,enemy.State.MapId,enemy.State.X,enemy.State.Y);
    private static void QueueEnemyBoarding(Vessel enemy,Vessel ship,long now)
    {
        if(!CanEnemyBoardPlayer(enemy,ship) || EnemyBoardingAttempts.TryGetValue(enemy.State.Id,out var attempt) && now-attempt.Time<30000)return;
        EnemyBoardingAttempts[enemy.State.Id]=(ship.State.CaptainId,now);EnemyBoardingRequests[enemy.State.Id]=ship.State.CaptainId;
        foreach(var id in EnemyBoardingAttempts.Where(p=>now-p.Value.Time>60000).Select(p=>p.Key).ToArray())EnemyBoardingAttempts.Remove(id);
    }
    private static void ProcessEnemyBoardingRequests(long now)
    {
        KeyValuePair<Guid,Guid>[] requests;
        lock(Gate){requests=EnemyBoardingRequests.ToArray();EnemyBoardingRequests.Clear();}
        // Native map creation, account work and player locks stay outside Gate.
        foreach(var request in requests)
            if(Player.FindOnline(request.Value) is {InGame:true,IsOnline:true} captain)
                lock(captain.EntityLock)StartEnemyBoarding(captain,request.Key,now,enemyInitiated:true);
    }
}
