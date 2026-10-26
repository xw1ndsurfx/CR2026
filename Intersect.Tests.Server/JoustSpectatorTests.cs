using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Jousting;
using Intersect.Server.Maps;
using Intersect.Server.Mounts;
using Intersect.Server.Networking;
using MessagePack;
using NUnit.Framework;
namespace Intersect.Tests.Server;
[TestFixture, NonParallelizable]
public sealed class JoustSpectatorTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object? Field(object p, string key) => p.GetType().GetField(key, Hidden)!.GetValue(p);
    private sealed class World : IDisposable
    {
        internal readonly Player A, B;
        internal readonly MapController Map;
        internal readonly MapInstance Instance;
        internal readonly JoustArena Arena;
        private readonly object? _mounts, _arenas;
        private static readonly FieldInfo MountCache = typeof(MountRuntime).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo ArenaCache = typeof(JoustRuntime).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        internal World()
        {
            Options.EnsureCreated(); _mounts = MountCache.GetValue(null); _arenas = ArenaCache.GetValue(null);
            Map = new(Guid.NewGuid()); MapController.Lookup.Set(Map.Id, Map);
            Player Make(string name, int x, int y)
            {
                var p = new Player { Name = name, MapId = Map.Id, MapInstanceId = MapInstance.OverworldInstanceId,
                    InGame = true, Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)), X = x, Y = y };
                typeof(Player).GetProperty(nameof(Player.UserId))!.SetValue(p, Guid.NewGuid());
                GC.SuppressFinalize(p); p.BaseStats[(int)Stat.Speed] = 20; return p;
            }
            A = Make("William", 4, 14); B = Make("Gisabelle", 26, 16);
            Instance = new(Map, MapInstance.OverworldInstanceId, A);
            ((ConcurrentDictionary<Guid, MapInstance>)Field(Map, "mInstances")!)[MapInstance.OverworldInstanceId] = Instance;
            typeof(MapController).GetField("mSurroundingMapsWithSelf", Hidden)!.SetValue(Map, new[] { Map });
            Instance.CacheMapBlocks(); Instance.AddEntity(A); Instance.AddEntity(B);
            var mount = JoustMountPreset.William();
            MountCache.SetValue(null, new MountConfiguration { Mounts = [mount] });
            Arena = new() { MapId = Map.Id, Enabled = true, MountId = mount.Id }; Arena.Rules.UnhorseEndsMatch = false;
            Arena.Seats = [new JoustSeat { X = 7, Y = 6, ExitX = 7, ExitY = 7, Name = "Tribune royale" }];
            ArenaCache.SetValue(null, new JoustConfiguration { Arenas = [Arena] });
        }
        internal JoustPvpSession? Session => (JoustPvpSession?)Field(A, "_joustPvp");
        internal JoustPvpRuntime.Invitation Invite()
        {
            A.HandleJoustRequest(new() { Action = JoustAction.InviteDuel, TargetId = B.Id });
            var list = (Dictionary<Guid, JoustPvpRuntime.Invitation>)typeof(JoustPvpRuntime).GetField("Invitations", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            return list[B.Id];
        }
        internal JoustPvpSession Start()
        {
            var invite = Invite();
            B.HandleJoustRequest(new() { Action = JoustAction.AcceptDuel, InvitationId = invite.Id });
            Assert.That(Session, Is.Not.Null, "Native invitation and acceptance must start one shared session."); return Session!;
        }
        public void Dispose()
        {
            A.LeaveJoustSeat(true); B.LeaveJoustSeat(true); Session?.Leave(A); JoustPvpRuntime.Sweep();
            Instance.RemoveEntity(A); Instance.RemoveEntity(B); A.Client = B.Client = null;
            MapController.Lookup.Delete(Map); MountCache.SetValue(null, _mounts); ArenaCache.SetValue(null, _arenas);
        }
    }
    private static void Request(Player p, JoustAction action, Guid seat = default)
    {
        typeof(Player).GetField("_joustSeatRequestAt", Hidden)!.SetValue(p, 0L);
        p.HandleJoustRequest(new() { Action = action, SeatId = seat });
    }
    [Test] public void ReservationMovesRealPlayerPublishesPoseAndBlocksTravelMovementCombatAndMount()
    {
        using var w = new World(); var seat = w.Arena.Seats[0];
        Request(w.A, JoustAction.ReserveSeat, seat.Id); Assert.That(w.A.IsJoustSeated, Is.False, "Remote requests cannot teleport to a seat.");
        w.A.X = seat.ExitX; w.A.Y = seat.ExitY;
        Request(w.A, JoustAction.ReserveSeat, seat.Id);
        Assert.That(w.A.IsJoustSeated, Is.True); Assert.That((w.A.X,w.A.Y), Is.EqualTo((seat.X,seat.Y)));
        Assert.That(((PlayerEntityPacket)w.A.EntityPacket()).JoustSeated, Is.True);
        Assert.That(MessagePackSerializer.Deserialize<PlayerEntityPacket>(MessagePackSerializer.Serialize((PlayerEntityPacket)w.A.EntityPacket())).JoustSeated, Is.True);
        Assert.That(w.A.CanUseLobbyTravel, Is.False); Assert.That(w.A.CanAttack(w.B, null!), Is.False);
        w.A.Move(Direction.Right, null!); Assert.That(w.A.X, Is.EqualTo(seat.X));
        w.A.ChangeDir(Direction.Left); Assert.That((int)w.A.Dir, Is.EqualTo(seat.Facing));
        var c = JoustAudienceRuntime.Get(w.Arena,w.A.MapInstanceId)!;
        Assert.That(JoustAudienceRuntime.View(c,w.A).CurrentSeat, Is.EqualTo(seat.Id));
        Assert.That(JoustAudienceRuntime.View(c,w.B).Seats[0].Occupant, Is.EqualTo("William"));
        Request(w.A, JoustAction.LeaveSeat);
        Assert.That(w.A.IsJoustSeated, Is.False); Assert.That(((PlayerEntityPacket)w.A.EntityPacket()).JoustSeated, Is.False);
        Assert.That((w.A.X,w.A.Y), Is.EqualTo((seat.ExitX,seat.ExitY)));
    }
    [Test] public void SimultaneousReservationHasOneWinnerAndCannotReleaseAnotherLease()
    {
        using var w = new World(); var seat = w.Arena.Seats[0];
        JoustAudienceRuntime.Lease? a = null, b = null;
        Parallel.Invoke(() => a = JoustAudienceRuntime.Reserve(w.A,w.Arena,seat), () => b = JoustAudienceRuntime.Reserve(w.B,w.Arena,seat));
        Assert.That(new[] { a,b }.Count(x => x != null), Is.EqualTo(1));
        var first = a ?? b!; JoustAudienceRuntime.Release(first);
        var next = JoustAudienceRuntime.Reserve(w.B,w.Arena,seat)!;
        JoustAudienceRuntime.Release(first);
        Assert.That(JoustAudienceRuntime.Reserve(w.A,w.Arena,seat), Is.Null);
        JoustAudienceRuntime.Release(next);
    }
    [Test] public void NpcFillerIsSharedReplacedByRealPlayerAndRestoredWithoutLoot()
    {
        using var w = new World();
        var npc = new Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor(Guid.NewGuid()) { Name = "Public", Sprite = "villager.png" };
        Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor.Lookup.Set(npc.Id,npc); w.Arena.CrowdNpcId = npc.Id;
        try
        {
            var c = JoustAudienceRuntime.Get(w.Arena,w.A.MapInstanceId)!; JoustAudienceRuntime.EnsureCrowd(c);
            var filler = w.Instance.GetEntities().OfType<JoustAudienceActor>().Single();
            Assert.That(((NpcEntityPacket)filler.EntityPacket()).JoustSeated, Is.True);
            Assert.That(MessagePackSerializer.Deserialize<NpcEntityPacket>(MessagePackSerializer.Serialize((NpcEntityPacket)filler.EntityPacket())).JoustSeated, Is.True);
            Assert.That(filler.CanAttack(w.A,null!), Is.False); Assert.That(w.A.CanAttack(filler,null!), Is.False);
            w.A.X=7;w.A.Y=7; Request(w.A,JoustAction.ReserveSeat,w.Arena.Seats[0].Id);
            Assert.That(w.Instance.GetEntities().OfType<JoustAudienceActor>(), Is.Empty);
            w.A.LeaveJoustSeat(false); JoustAudienceRuntime.EnsureCrowd(c);
            Assert.That(w.Instance.GetEntities().OfType<JoustAudienceActor>().Count(), Is.EqualTo(1));
            w.Arena.CrowdNpcId=Guid.Empty;JoustAudienceRuntime.EnsureCrowd(c);
            Assert.That(w.Instance.GetEntities().OfType<JoustAudienceActor>(), Is.Empty);
        }
        finally { Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor.Lookup.Delete(npc); }
    }
    [TestCase("disconnect"),TestCase("removed"),TestCase("moved")]
    public void NativeLifecycleReleasesReservations(string reason)
    {
        using var w = new World();w.A.X=7;w.A.Y=7; Request(w.A,JoustAction.ReserveSeat,w.Arena.Seats[0].Id);
        var c=JoustAudienceRuntime.Get(w.Arena,w.A.MapInstanceId)!;
        if(reason=="disconnect")w.A.Client=null;
        if(reason=="removed")w.Arena.Seats=[];
        if(reason=="moved")w.A.X=8;
        w.A.UpdateJoustSeat(); Assert.That(w.A.IsJoustSeated,Is.False);
        Assert.That(JoustAudienceRuntime.View(c,w.B).Seats.All(s=>!s.Occupied),Is.True);
    }
    [Test] public void LivePvpScoresAreSharedWithoutAimOrDefenseInputs()
    {
        using var w=new World();var session=w.Start();
        session.Tick(w.A,session.Match.PhaseStarted+50);
        var c=JoustAudienceRuntime.Get(w.Arena,w.A.MapInstanceId)!;
        var view=JoustAudienceRuntime.View(c,w.B);Assert.That(view.Match,Is.Not.Null);
        Assert.That(view.Match!.FirstId,Is.EqualTo(w.A.Id));Assert.That(view.Match.SecondId,Is.EqualTo(w.B.Id));
        var json=System.Text.Json.JsonSerializer.Serialize(view.Match);
        Assert.That(json,Does.Not.Contain("Defending"));Assert.That(json,Does.Not.Contain("AimX"));
        Assert.That(view.CurrentSeat,Is.EqualTo(Guid.Empty));
    }
    [Test] public void SeatValidationRejectsOverlapLanesAndNonAdjacentExits()
    {
        using var w=new World();var a=w.Arena;var seat=a.Seats[0];Assert.That(a.IsValid,Is.True);
        seat.ExitY+=1;Assert.That(a.IsValid,Is.False);seat.ExitY-=1;
        a.Seats=[seat,new JoustSeat { X=seat.X,Y=seat.Y,ExitX=seat.ExitX,ExitY=seat.ExitY }];Assert.That(a.IsValid,Is.False);
        a.Seats=[seat];seat.Y=a.StartY;seat.X=a.StartX;seat.ExitY=seat.Y+1;seat.ExitX=seat.X;Assert.That(a.IsValid,Is.False);
    }
    [Test] public void OldResultsCannotReplaceTheCurrentMatch()
    {
        using var w=new World();var session=w.Start();session.Tick(w.A,session.Match.PhaseStarted+50);
        var key=new JoustAudienceRuntime.Key(w.Arena.Id,w.A.MapInstanceId);
        var old=new JoustMatch(w.Arena.Rules,0,JoustDifficulty.Novice);
        old.Abandon(0);JoustAudienceRuntime.Publish(w.Arena,w.A.MapInstanceId,old,Guid.NewGuid(),"Old",Guid.Empty,"");
        Assert.That(JoustAudienceRuntime.CurrentMatch(key)!.MatchId,Is.EqualTo(session.Match.Id));
    }
    [Test] public void ConfigurationReplacementCannotReserveAnObsoleteSeat()
    {
        using var w = new World(); var seat = w.Arena.Seats[0];
        var c = JoustAudienceRuntime.Get(w.Arena,w.A.MapInstanceId)!;
        w.Arena.Seats = []; JoustAudienceRuntime.EnsureCrowd(c);
        Assert.That(JoustAudienceRuntime.Reserve(w.A,w.Arena,seat),Is.Null);
        w.Arena.Seats = [seat]; var lease = JoustAudienceRuntime.Reserve(w.A,w.Arena,seat);
        Assert.That(lease,Is.Not.Null); JoustAudienceRuntime.Release(lease!);
    }
    [Test] public void GoldStartOpensOnceAndOnlyRearmsAfterLeavingTheTile()
    {
        using var w = new World(); const long now = 10000;
        Assert.That(w.A.UpdateJoustEntry(now),Is.EqualTo(JoustAction.Open));
        Assert.That(w.A.UpdateJoustEntry(now+1000),Is.Null,"Closing a window cannot make the server reopen it on the next tick.");
        w.A.X++; Assert.That(w.A.UpdateJoustEntry(now+2000),Is.Null);
        w.A.X--; Assert.That(w.A.UpdateJoustEntry(now+3000),Is.EqualTo(JoustAction.Open));
        Assert.That(w.A.IsJousting,Is.False,"The automatic invitation never starts or mounts the player without a choice.");
        Assert.That(w.A.ActiveMountId,Is.EqualTo(Guid.Empty));
    }
    [Test] public void BlueStartOffersDuelsOnlyWhenPvpIsEnabled()
    {
        using var w=new World();
        Assert.That(w.B.UpdateJoustEntry(10000),Is.EqualTo(JoustAction.BrowseDuels));
        w.B.X--;w.B.UpdateJoustEntry(11000);w.B.X++;w.Arena.AllowPvp=false;
        Assert.That(w.B.UpdateJoustEntry(12000),Is.Null);
    }
    [Test] public void GrandstandEntranceOffersSeatsWithoutBookingAndDoesNotReopenAfterStanding()
    {
        using var w=new World();w.A.X=7;w.A.Y=7;
        Assert.That(w.A.UpdateJoustEntry(10000),Is.EqualTo(JoustAction.BrowseSeats));
        Assert.That(w.A.IsJoustSeated,Is.False);Assert.That((w.A.X,w.A.Y),Is.EqualTo((7,7)));
        Request(w.A,JoustAction.ReserveSeat,w.Arena.Seats[0].Id);
        Assert.That(w.A.UpdateJoustEntry(11000),Is.Null);
        w.A.LeaveJoustSeat(false);Assert.That(w.A.UpdateJoustEntry(12000),Is.Null);
    }
    [Test] public void BusyPlayersWaitAndDisabledArenasNeverPrompt()
    {
        using var w=new World();w.A.CombatTimer=Intersect.Framework.Core.Timing.Global.Milliseconds+10000;
        Assert.That(w.A.UpdateJoustEntry(10000),Is.Null);
        w.A.CombatTimer=0;w.A.BoardedShipId=Guid.NewGuid();Assert.That(w.A.UpdateJoustEntry(11000),Is.Null,"A ship passenger keeps the naval interface.");
        w.A.BoardedShipId=Guid.Empty;Assert.That(w.A.UpdateJoustEntry(12000),Is.EqualTo(JoustAction.Open));
        w.A.X++;w.A.UpdateJoustEntry(13000);w.A.X--;w.Arena.Enabled=false;
        Assert.That(w.A.UpdateJoustEntry(14000),Is.Null);
    }
    [Test] public void ARunningDuelDoesNotReopenPreparationDuringOrAfterItsOwnCleanup()
    {
        using var w=new World();Assert.That(w.A.UpdateJoustEntry(10000),Is.EqualTo(JoustAction.Open));
        var duel=w.Start();Assert.That(w.A.UpdateJoustEntry(11000),Is.Null);
        duel.Leave(w.A);Assert.That(w.A.UpdateJoustEntry(12000),Is.Null);
    }
}
