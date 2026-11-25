using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Matchmaking;
using Intersect.Server.Naval;
using Intersect.Server.PartyLobbies;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [TestCase(false)] [TestCase(true)]
    public void MatchedNavalDepartureRequiresExactConsentAndPersistsActiveRun(bool fleet)
    {
        using var world=new ExpeditionWorld(fleet);
        var ids=world.Players.Select(p=>p.Id).ToHashSet();
        Assert.That(NavalSystem.StartMatchmaking(world.Players[0],world.Definition.Id,new HashSet<Guid>{world.Players[0].Id}),Is.False);
        Assert.That(world.Runs,Is.Empty);
        Assert.That(NavalSystem.StartMatchmaking(world.Players[0],world.Definition.Id,ids),Is.True);
        var saved=world.Store.Load(32,26).Values.Single();
        Assert.That(saved.Status,Is.EqualTo(NavalExpeditionStatus.Active));Assert.That(saved.Members.All(m=>m.Ready),Is.True);
        Assert.That(saved.Ships.Length,Is.EqualTo(fleet?2:1));
        Assert.That(NavalSystem.StartMatchmaking(world.Players[0],world.Definition.Id,ids),Is.False);
        Assert.That(world.Runs.Count,Is.EqualTo(1));
    }

    [Test]
    public void MatchedNavalDepartureDoesNotPersistWhenStorageFails()
    {
        using var world=new ExpeditionWorld(false);
        world.SetStatic("ExpeditionStore",new NavalExpeditionStore(world.Files.Directory));
        Assert.That(NavalSystem.StartMatchmaking(world.Players[0],world.Definition.Id,world.Players.Select(p=>p.Id).ToHashSet()),Is.False);
        Assert.That(world.Runs,Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void NavalMatchmakingGoesFromSeparateTicketsThroughIndividualConsentToDeparture(bool fleet)
    {
        using var world=new ExpeditionWorld(fleet);
        using var session=new MatchmakingSession(world);
        if(fleet)foreach(var p in world.Players)p.Party=[];
        session.Enqueue(world.Players[0]);if(fleet)session.Enqueue(world.Players[1]);
        Assert.That(session.Queue.Proposals.Count,Is.EqualTo(1));
        var proposal=session.Queue.Proposals.Values.Single();
        Assert.That(world.Runs,Is.Empty);
        session.Ready(world.Players[0],Guid.NewGuid());Assert.That(proposal.Ready,Is.Empty);
        session.Ready(world.Players[0],proposal.Id);Assert.That(world.Runs,Is.Empty);
        session.Ready(world.Players[1],proposal.Id);
        Assert.That(world.Runs.Values.Single().Status,Is.EqualTo(NavalExpeditionStatus.Active));
        Assert.That(session.Queue.Tickets,Is.Empty);
        Assert.That(world.Players.All(p=>p.Party.Count==2&&!p.NavalPartyExitRequested&&!p.MatchmakingDeparting),Is.True);
    }

    [TestCase("disconnect")] [TestCase("move ship")] [TestCase("party")] [TestCase("definition")]
    public void NavalMatchmakingInvalidationPreventsDeparture(string change)
    {
        using var world=new ExpeditionWorld(true);using var session=new MatchmakingSession(world);
        foreach(var p in world.Players)p.Party=[];
        session.Enqueue(world.Players[0]);session.Enqueue(world.Players[1]);
        var proposal=session.Queue.Proposals.Values.Single();session.Ready(world.Players[0],proposal.Id);
        switch(change)
        {
            case "disconnect":world.Players[1].InGame=false;break;
            case "move ship":world.World.State(world.Boats[1]).Speed=1;break;
            case "party":world.Players[1].Party=[..world.Players];break;
            case "definition":world.Definition.TimeLimitMinutes++;break;
        }
        session.Tick();Assert.That(session.Queue.Proposals,Is.Empty);Assert.That(world.Runs,Is.Empty);
        Assert.That(world.Players.All(p=>!p.MatchmakingDeparting),Is.True);
    }

    private sealed class MatchmakingSession : IDisposable
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
        private readonly FieldInfo _config=typeof(LobbyConfigurationRuntime).GetField("_current",Flags)!;
        private readonly object? _before;
        private long _now=Timing.Global.Milliseconds+1000;
        private readonly MatchmakingEntry _entry;
        internal readonly MatchmakingQueue Queue=(MatchmakingQueue)typeof(MatchmakingRuntime).GetField("Queue",Flags)!.GetValue(null)!;
        internal MatchmakingSession(ExpeditionWorld world)
        {
            _before=_config.GetValue(null);Queue.Clear();typeof(MatchmakingRuntime).GetField("_nextTick",Flags)!.SetValue(null,0L);
            _entry=new(){Enabled=true,Activity=MatchmakingActivity.Expedition,ContentId=world.Definition.Id,TargetPlayers=2};
            _config.SetValue(null,new LobbyConfiguration{Matchmaking=new(){Entries=[_entry]}});
        }
        internal void Tick()=>MatchmakingRuntime.Update(_now+=1100);
        internal void Enqueue(Player p){MatchmakingRuntime.RequestAction(p,new(){Action=MatchmakingAction.Enqueue,EntryId=_entry.Id});Tick();}
        internal void Ready(Player p,Guid token){MatchmakingRuntime.RequestAction(p,new(){Action=MatchmakingAction.Ready,ConfirmationId=token});Tick();}
        public void Dispose(){Queue.Clear();_config.SetValue(null,_before);}
    }
}
