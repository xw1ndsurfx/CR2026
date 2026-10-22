using Intersect.Server.Database;
using Intersect.Server.Networking;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private sealed record GeneratedDeck(Guid Map,Guid Npc,long CleanupAfter);
    private static readonly Dictionary<Guid,GeneratedDeck> GeneratedDecks=[];
    internal static (DungeonDefinition Dungeon,MapController Map,int X,int Y)? CreateBoardingDeck(HerboniaPatrol patrol,long now)
    {
        var layout=new NavalBoardingDeckLayout(Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight);
        var positions=layout.CrewPositions(patrol.BoardingCrewCount);
        if(positions.Length!=patrol.BoardingCrewCount || !layout.Walkable(layout.EntryX,layout.EntryY))return null;
        var source=NPCDescriptor.Get(patrol.BoardingCrewNpcId);
        if(source==null && patrol.BoardingCrewNpcId==Guid.Empty)
            source=NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n=>!string.IsNullOrWhiteSpace(n.Sprite))
                .OrderByDescending(n=>n.Name.Contains("Herbonia",StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Pirate",StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Sailor",StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(n=>n.Aggressive).ThenBy(n=>Math.Abs(n.Level-patrol.Level)).FirstOrDefault();
        if(source==null || string.IsNullOrWhiteSpace(source.Sprite))return null;
        var npc=new NPCDescriptor(Guid.NewGuid()){Name=source.Name,Sprite=source.Sprite,Aggressive=true,SightRange=12,
            Level=patrol.Level,Damage=Math.Max(2,patrol.Level*2),AttackSpeedValue=1000,Experience=0,ExperienceMaximum=0};
        npc.MaxVitals[(int)Vital.Health]=40+patrol.Level*12;npc.MaxVitals[(int)Vital.Mana]=100;
        for(var i=0;i<npc.Stats.Length;i++)npc.Stats[i]=Math.Max(1,patrol.Level*2);
        var map=new MapController(Guid.NewGuid()){Name=source.Name,GeneratedBoardingDeck=true,IsIndoors=true,Brightness=100};
        map.SurroundingMaps=[];map.SurroundingMapIds=[];
        var attributes=new MapAttribute[layout.Width,layout.Height];
        for(var x=0;x<layout.Width;x++)for(var y=0;y<layout.Height;y++)if(!layout.Walkable(x,y))attributes[x,y]=new MapBlockedAttribute();
        map.Attributes=attributes;
        foreach(var point in positions)map.Spawns.Add(new NpcSpawn{NpcId=npc.Id,X=point.X,Y=point.Y});
        lock(Gate)
        {
            if(GeneratedDecks.Count>=128)return null;
            NPCDescriptor.Lookup.Set(npc.Id,npc);MapController.Lookup.Set(map.Id,map);
            DbInterface.RegisterBoardingGrid(map);
            GeneratedDecks[map.Id]=new(map.Id,npc.Id,now+30000);
        }
        var dungeon=new DungeonDefinition{Name=map.Name,MapIds=[map.Id],MinimumPartySize=1,MaximumPartySize=100,
            TimeLimitMinutes=5,NpcRespawnEnabled=false,CompletionRequirements=DungeonCompletionRequirement.DefeatAllMonsters};
        return(dungeon,map,layout.EntryX,layout.EntryY);
    }
    private static void PruneGeneratedBoardingDecks(long now)
    {
        GeneratedDeck[] decks;
        lock (Gate) decks = GeneratedDecks.Values.Where(deck => now >= deck.CleanupAfter).ToArray();
        foreach (var deck in decks)
        {
            // Reserve deletion before taking map locks, so a delayed start cannot use a retired deck.
            lock (Gate)
            {
                if (EnemyBoardings.Values.Any(run => run.GeneratedMap == deck.Map) || !GeneratedDecks.Remove(deck.Map))
                    continue;
            }
            var map = MapController.Get(deck.Map);
            if (map != null && !map.DisposeGeneratedBoardingDeck())
            {
                lock (Gate) GeneratedDecks[deck.Map] = deck;
                continue;
            }
            if (map != null)
            {
                DbInterface.ReleaseBoardingGrid(map);
                MapController.Lookup.Delete(map);
            }
            var npc = NPCDescriptor.Get(deck.Npc);
            if (npc != null) PacketSender.SendGameObjectToAll(npc, deleted: true);
            NPCDescriptor.Lookup.DeleteAt(deck.Npc);
        }
    }
    private static bool EnemyBoardingReady(Vessel enemy)=>enemy.Enemy is {} p && (p.AutomaticBoardingDeck || p.BoardingDungeonId!=Guid.Empty && p.BoardingEntryMapId!=Guid.Empty) &&
        enemy.SunkUntil==0 && enemy.Health>0 && !enemy.RewardClaimed && enemy.EnemyBoardingEncounter==Guid.Empty && enemy.Health*100L<=enemy.MaximumHealth*(long)p.BoardingHealthPercent;
    private static double BoardingDistance(Vessel a,Vessel b)
    {
        var point=a.State.MapId==b.State.MapId?(b.State.X,b.State.Y):Relative(a.State.MapId,b.State.MapId,b.State.X,b.State.Y,OceanMapResolver.Maps());
        if(point==null)return double.PositiveInfinity;
        var radiusA=Math.Sqrt(a.Definition.Width*a.Definition.Width+a.Definition.Length*a.Definition.Length)/2;
        var radiusB=Math.Sqrt(b.Definition.Width*b.Definition.Width+b.Definition.Length*b.Definition.Length)/2;
        return Math.Max(0,Distance(a.State.X,a.State.Y,point.Value.Item1,point.Value.Item2)-radiusA-radiusB);
    }
    private static bool BoardingCeasefire(Vessel enemy)=>EnemyBoardingReady(enemy) && Ships.Values.Any(ship=>ship.Enemy==null && ship.State.CaptainId!=Guid.Empty && ship.Health>0 && ship.SunkUntil==0 && BoardingDistance(ship,enemy)<=enemy.Enemy.BoardingRange+1);
}
