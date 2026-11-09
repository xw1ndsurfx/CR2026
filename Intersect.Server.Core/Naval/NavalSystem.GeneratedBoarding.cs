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
    private sealed record GeneratedDeck(Guid Map,Guid[] Npcs,long CleanupAfter);
    private static readonly Dictionary<Guid,GeneratedDeck> GeneratedDecks=[];
    internal static (DungeonDefinition Dungeon,MapController Map,int X,int Y)? CreateBoardingDeck(HerboniaPatrol patrol,long now)
        =>CreateBoardingDeckCore(patrol,now,null);
    internal static (DungeonDefinition Dungeon,MapController Map,int X,int Y)? CreateBoardingDeckOnShips(HerboniaPatrol patrol,ShipDefinition playerShip,long now)
        =>CreateBoardingDeckCore(patrol,now,NavalBoardingScene.Create(playerShip.Graphics,patrol.Ship.Graphics,Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight));
    private static (DungeonDefinition Dungeon,MapController Map,int X,int Y)? CreateBoardingDeckCore(HerboniaPatrol patrol,long now,NavalBoardingScene? scene)
    {
        var layout=new NavalBoardingDeckLayout(Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight);
        var count=scene==null?patrol.BoardingCrewCount:7;
        var positions=scene?.CrewPositions(count)??layout.CrewPositions(count);
        var entryX=scene?.EntryX??layout.EntryX;var entryY=scene?.EntryY??layout.EntryY;
        if(positions.Length!=count || !(scene?.Walkable(entryX,entryY)??layout.Walkable(entryX,entryY)))return null;
        var source=NPCDescriptor.Get(patrol.BoardingCrewNpcId)??NPCDescriptor.Get(patrol.DeckCaptainNpcId);
        if(source==null && patrol.BoardingCrewNpcId==Guid.Empty)
            source=NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n=>!string.IsNullOrWhiteSpace(n.Sprite))
                .OrderByDescending(n=>n.Name.Contains("Herbonia",StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Pirate",StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Sailor",StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(n=>n.Aggressive).ThenBy(n=>Math.Abs(n.Level-patrol.Level)).FirstOrDefault();
        if(source==null || string.IsNullOrWhiteSpace(source.Sprite))return null;
        var sightRange = (int)Math.Ceiling(Math.Sqrt((double)layout.Width * layout.Width + (double)layout.Height * layout.Height));
        NPCDescriptor CloneCrew(NPCDescriptor appearance)
        {
        var npc=new NPCDescriptor(Guid.NewGuid()){Name=appearance.Name,Sprite=appearance.Sprite,Aggressive=true,
            // Wait at spawn between fights while keeping combat movement enabled.
            Movement=(byte)NpcMovement.StandStill,SightRange=Math.Min(byte.MaxValue,sightRange),
            Level=patrol.Level,Damage=Math.Max(2,patrol.Level*2),AttackSpeedValue=1000,Experience=0,ExperienceMaximum=0};
        npc.MaxVitals[(int)Vital.Health]=40+patrol.Level*12;npc.MaxVitals[(int)Vital.Mana]=100;
        for(var i=0;i<npc.Stats.Length;i++)npc.Stats[i]=Math.Max(1,patrol.Level*2);
        return npc;
        }
        var authored=scene==null?Enumerable.Repeat(source,count).ToArray():patrol.DeckCrew().Select(id=>id==Guid.Empty?source:NPCDescriptor.Get(id)).ToArray();
        if(authored.Any(n=>n==null || string.IsNullOrWhiteSpace(n.Sprite)))return null;
        var crew=authored.DistinctBy(n=>n.Id).ToDictionary(n=>n.Id,CloneCrew);
        var map=new MapController(Guid.NewGuid()){Name=string.IsNullOrWhiteSpace(patrol.Ship.Name)?source.Name:patrol.Ship.Name,GeneratedBoardingDeck=true,BoardingScene=scene,IsIndoors=true,Brightness=100};
        map.SurroundingMaps=[];map.SurroundingMapIds=[];
        var attributes=new MapAttribute[layout.Width,layout.Height];
        for(var x=0;x<layout.Width;x++)for(var y=0;y<layout.Height;y++)if(!(scene?.Walkable(x,y)??layout.Walkable(x,y)))attributes[x,y]=new MapBlockedAttribute();
        map.Attributes=attributes;
        for(int i=0;i<positions.Length;i++)map.Spawns.Add(new NpcSpawn{NpcId=crew[authored[i].Id].Id,X=positions[i].X,Y=positions[i].Y});
        lock(Gate)
        {
            if(GeneratedDecks.Count>=128)return null;
            foreach(var npc in crew.Values)NPCDescriptor.Lookup.Set(npc.Id,npc);MapController.Lookup.Set(map.Id,map);
            DbInterface.RegisterBoardingGrid(map);
            GeneratedDecks[map.Id]=new(map.Id,crew.Values.Select(n=>n.Id).ToArray(),now+30000);
        }
        var dungeon=new DungeonDefinition{Name=map.Name,MapIds=[map.Id],MinimumPartySize=1,MaximumPartySize=100,
            TimeLimitMinutes=5,NpcRespawnEnabled=false,CompletionRequirements=DungeonCompletionRequirement.DefeatAllMonsters};
        return(dungeon,map,entryX,entryY);
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
            foreach(var npcId in deck.Npcs)
            {
                var npc = NPCDescriptor.Get(npcId);
                if (npc != null) PacketSender.SendGameObjectToAll(npc, deleted: true);
                NPCDescriptor.Lookup.DeleteAt(npcId);
            }
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
