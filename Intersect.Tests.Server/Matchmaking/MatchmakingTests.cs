using Intersect.Framework.Core;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;
using Intersect.Network;
using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.Matchmaking;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Matchmaking;

[TestFixture, NonParallelizable]
public sealed class MatchmakingTests
{
    private sealed class EntryPlayer : Intersect.Server.Entities.Player
    {
        internal bool Refuse;
        public override void Warp(Guid map,float x,float y,Intersect.Enums.Direction direction,bool adminWarp=false,int zOverride=0,bool mapSave=false,bool fromWarpEvent=false,
            Intersect.Framework.Core.GameObjects.Maps.MapInstanceType? mapInstanceType=null,bool fromLogin=false,bool forceInstanceChange=false)
        {
            if(Refuse)return;
            MapId=map;MapInstanceId=SharedMapInstanceId;InstanceType=mapInstanceType??Intersect.Framework.Core.GameObjects.Maps.MapInstanceType.Overworld;
        }
    }

    [Test]
    public void MatchedDungeonDoesNotCommitAGroupWhenOneMemberCannotEnter()
    {
        Options.EnsureCreated();
        var online=(System.Collections.IDictionary)typeof(Intersect.Server.Entities.Player).GetField("OnlinePlayersById",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        var a=new EntryPlayer {Id=Guid.NewGuid(),Name="Leader",Level=10,InGame=true,SharedMapInstanceId=Guid.NewGuid()};
        var b=new EntryPlayer {Id=Guid.NewGuid(),Name="Member",Level=10,InGame=true,SharedMapInstanceId=a.SharedMapInstanceId,Refuse=true};
        GC.SuppressFinalize(a);GC.SuppressFinalize(b);a.Party=[a,b];b.Party=[a,b];online.Add(a.Id,a);online.Add(b.Id,b);
        var committed=false;
        try
        {
            var dungeon=new Intersect.Framework.Core.Dungeons.DungeonDefinition {MinimumPartySize=2,MapIds=[Guid.NewGuid()]};
            Assert.That(Intersect.Server.Dungeons.DungeonRunRuntime.TryStart(a,dungeon,dungeon.MapIds[0],1,1,
                Intersect.Framework.Core.GameObjects.Maps.WarpDirection.Down,true,Intersect.Framework.Core.GameObjects.Maps.MapInstanceType.Shared,out _,
                requireAllParticipants:true,onCommitted:()=>committed=true),Is.False);
            Assert.That(committed,Is.False);
            Assert.That(Intersect.Server.Dungeons.DungeonRunRuntime.IsPlayerInActiveRun(a),Is.False);
            Assert.That(Intersect.Server.Dungeons.DungeonRunRuntime.IsPlayerInActiveRun(b),Is.False);
        }
        finally {online.Remove(a.Id);online.Remove(b.Id);a.Party=[];b.Party=[];a.Dispose();b.Dispose();}
    }

    private static MatchMember Member(int level = 10) => new(Guid.NewGuid(), "Sailor", level, Guid.Empty);
    private static MatchTicket Ticket(MatchRule rule, params MatchMember[] members) => new(Guid.NewGuid(), rule.EntryId, rule.Fingerprint, members, 1000, 100000);
    private static MatchRule Rule(int target=2,int maximum=4) => new(Guid.NewGuid(),"rules v1",target,maximum);

    [Test]
    public void ReadyCheckRequiresEveryMemberAndRejectsForeignStaleAndDuplicateConsent()
    {
        var queue=new MatchmakingQueue();var rule=Rule();var a=Member();var b=Member();
        Assert.That(queue.Enqueue(Ticket(rule,a),10),Is.True);Assert.That(queue.Enqueue(Ticket(rule,b),10),Is.True);
        var proposal=queue.Find(rule,2000,15,_=>true)!;
        Assert.That(queue.Find(rule,2001,15,_=>true),Is.Null,"Reserved players cannot be matched twice.");
        Assert.That(queue.Confirm(Guid.NewGuid(),proposal.Id,2001),Is.False);
        Assert.That(queue.Confirm(a.Id,Guid.NewGuid(),2001),Is.False);
        Assert.That(queue.Confirm(a.Id,proposal.Id,2001),Is.True);
        Assert.That(queue.Confirm(a.Id,proposal.Id,2002),Is.False);
        Assert.That(queue.TryLaunch(proposal,2002),Is.False);
        Assert.That(queue.Confirm(b.Id,proposal.Id,2003),Is.True);
        Assert.That(queue.TryLaunch(proposal,2004),Is.True);
        Assert.That(queue.TryLaunch(proposal,2004),Is.False);
        Assert.That(queue.Cancel(a.Id),Is.Empty,"Departure is committed once.");
        queue.Complete(proposal);Assert.That(queue.Tickets,Is.Empty);Assert.That(queue.Proposals,Is.Empty);
        Assert.That(queue.Confirm(a.Id,proposal.Id,2005),Is.False);
    }

    [Test]
    public void TimeoutDropsEntireUnreadyCohortAndPreservesAgeOfReadyCohorts()
    {
        var queue=new MatchmakingQueue();var rule=Rule(3);var a=Member();var b=Member();var c=Member();
        var ready=Ticket(rule,a);var incomplete=Ticket(rule,b,c);
        queue.Enqueue(ready,10);queue.Enqueue(incomplete,10);var proposal=queue.Find(rule,2000,15,_=>true)!;
        queue.Confirm(a.Id,proposal.Id,2001);queue.Confirm(b.Id,proposal.Id,2001);
        Assert.That(queue.Confirm(c.Id,proposal.Id,proposal.Deadline),Is.False);
        Assert.That(queue.TryLaunch(proposal,proposal.Deadline),Is.False);
        Assert.That(queue.Timeout(proposal),Is.EqualTo(new[]{incomplete}));
        Assert.That(queue.TicketFor(a.Id),Is.SameAs(ready));Assert.That(queue.TicketFor(a.Id)!.Created,Is.EqualTo(1000));
        Assert.That(queue.TicketFor(b.Id),Is.Null);Assert.That(queue.TicketFor(c.Id),Is.Null);
        Assert.That(queue.ProposalFor(a.Id),Is.Null);
    }

    [Test]
    public void DeclineRequeuesOtherGroupsButRequiresFreshConsentForNextMatch()
    {
        var queue=new MatchmakingQueue();var rule=Rule();var a=Member();var b=Member();var c=Member();
        queue.Enqueue(Ticket(rule,a),10);queue.Enqueue(Ticket(rule,b),10);
        var first=queue.Find(rule,2000,15,_=>true)!;queue.Confirm(a.Id,first.Id,2001);queue.Cancel(b.Id);
        queue.Enqueue(Ticket(rule,c),10);var next=queue.Find(rule,2002,15,_=>true)!;
        Assert.That(next.Id,Is.Not.EqualTo(first.Id));Assert.That(next.Ready,Is.Empty);
        Assert.That(queue.Confirm(a.Id,first.Id,2003),Is.False);
        Assert.That(queue.TicketFor(b.Id),Is.Null);
    }

    [Test]
    public void QueueDoesNotSplitGroupsOverfillOrDuplicatePlayers()
    {
        var queue=new MatchmakingQueue();var rule=Rule(3,3);var a=Member();var group=Ticket(rule,a,Member());
        Assert.That(queue.Enqueue(group,4),Is.True);
        Assert.That(queue.Enqueue(Ticket(rule,a),4),Is.False);
        Assert.That(queue.Enqueue(Ticket(rule,Member(),Member(),Member()),4),Is.False);
        Assert.That(queue.Enqueue(Ticket(rule,Member(),Member()),4),Is.True);
        Assert.That(queue.Find(rule,2000,15,_=>true),Is.Null);
        Assert.That(queue.Tickets.Count,Is.EqualTo(2));
        queue.Cancel(a.Id);Assert.That(queue.Tickets.Count,Is.EqualTo(1));
    }

    [Test]
    public void IncompatibleLevelsRulesAndBlockedPairsCannotMatch()
    {
        var queue=new MatchmakingQueue();var rule=Rule() with {LevelSpread=3};var a=Member(10);var b=Member(20);var c=Member(12);
        queue.Enqueue(Ticket(rule,a),10);queue.Enqueue(Ticket(rule,b),10);queue.Enqueue(Ticket(rule,c),10);
        Assert.That(queue.Find(rule with {Fingerprint="changed"},2000,15,_=>true),Is.Null);
        Assert.That(queue.Find(rule,2000,15,m=>!m.Any(x=>x.Id==a.Id)),Is.Null);
        var proposal=queue.Find(rule,2000,15,_=>true)!;
        Assert.That(proposal.Members.Select(m=>m.Id),Is.EquivalentTo(new[]{a.Id,c.Id}));
    }

    [TestCase(1)] [TestCase(2)]
    public void NavalPlannerAssignsFreeCrewSeatsWithoutMovingExistingCrew(int ships)
    {
        var rule=Rule(3,6) with {MinimumShips=ships,MaximumShips=ships};var map=Guid.NewGuid();
        var a=Member() with {MapId=map,ShipId=Guid.NewGuid(),ShipCapacity=3};a=a with {CaptainId=a.Id};
        var b=ships==1?Member() with {MapId=map,ShipId=a.ShipId,CaptainId=a.Id}:Member() with {MapId=map,ShipId=Guid.NewGuid(),ShipCapacity=2};
        if(ships==2)b=b with {CaptainId=b.Id};var c=Member();
        var plan=MatchmakingQueue.PlanShips(rule,[a,b,c])!;
        Assert.That(plan[a.Id],Is.EqualTo(a.ShipId));Assert.That(plan[b.Id],Is.EqualTo(b.ShipId));
        Assert.That(plan[c.Id],Is.EqualTo(a.ShipId));
        Assert.That(MatchmakingQueue.PlanShips(rule,[b,c]),Is.Null);
        Assert.That(MatchmakingQueue.PlanShips(rule,[a with {ShipCapacity=1},b with {ShipCapacity=1},c]),Is.Null);
        if(ships==2)Assert.That(MatchmakingQueue.PlanShips(rule,[a,b with {MapId=Guid.NewGuid()},c]),Is.Null);
    }

    [Test]
    public void DungeonCannotMatchAboardPlayersAndExpiredTicketsNeverMatch()
    {
        var queue=new MatchmakingQueue();var rule=Rule();var a=Member();
        queue.Enqueue(Ticket(rule,a) with {Expires=2000},10);queue.Enqueue(Ticket(rule,Member()),10);
        Assert.That(queue.Find(rule,2000,15,_=>true),Is.Null);
        Assert.That(MatchmakingQueue.PlanShips(rule,[a with {ShipId=Guid.NewGuid()},Member()]),Is.Null);
    }

    [Test]
    public void LegacyConfigurationSafeDefaultsAndInvalidRulesAreRejected()
    {
        var legacy=JsonConvert.DeserializeObject<LobbyConfiguration>("{}")!;
        Assert.DoesNotThrow(legacy.Validate);Assert.That(legacy.Matchmaking.Entries,Is.Empty);
        legacy.Matchmaking.Entries=[new(){Enabled=true,ContentId=Guid.NewGuid(),EntryMapId=Guid.NewGuid()}];
        Assert.DoesNotThrow(legacy.Validate);
        var copy=JsonConvert.DeserializeObject<LobbyConfiguration>(JsonConvert.SerializeObject(legacy))!;
        Assert.That(copy.Matchmaking.Entries[0].ContentId,Is.EqualTo(legacy.Matchmaking.Entries[0].ContentId));
        copy.Matchmaking.ReadyTimeoutSeconds=0;Assert.Throws<InvalidDataException>(copy.Validate);
        copy.Matchmaking.ReadyTimeoutSeconds=45;copy.Matchmaking.Entries.Add(copy.Matchmaking.Entries[0]);Assert.Throws<InvalidDataException>(copy.Validate);
        foreach(var path in new[]{"../sound.wav","C:/sound.wav","/sound.wav","\\sound.wav"})Assert.That(MatchmakingConfiguration.AssetName(path),Is.False);
        Assert.That(MatchmakingConfiguration.AssetName("matchmaking/ready_v1.wav"),Is.True);
    }

    [Test]
    public void MatchmakingPolymorphicPacketsAndTranslationsRoundTrip()
    {
        var registry=new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(),Is.True);PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach(var action in Enum.GetValues<MatchmakingAction>())
        {
            var packet=new MatchmakingRequestPacket{Action=action,EntryId=Guid.NewGuid(),ConfirmationId=Guid.NewGuid()};
            var copy=MessagePacker.Instance.Deserialize<MatchmakingRequestPacket>(MessagePacker.Instance.Serialize((IntersectPacket)packet))!;
            Assert.That((copy.Action,copy.EntryId,copy.ConfirmationId),Is.EqualTo((action,packet.EntryId,packet.ConfirmationId)));
        }
        foreach(var entry in QualityOfLifeText.Catalog.Entries)
            foreach(var lang in new[]{"en","fr","es"})
                Assert.That(entry.Value.ContainsKey(lang)&&GameTextTokens.Compatible(entry.Key,entry.Value[lang]),Is.True,entry.Key+":"+lang);
        Assert.That(QualityOfLifeText.Get("Find an adventure","unsupported"),Is.EqualTo("Find an adventure"));
    }
}
