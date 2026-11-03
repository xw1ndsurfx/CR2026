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
public sealed class JoustPvpTests
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
            Session?.Leave(A); JoustPvpRuntime.Sweep();
            Instance.RemoveEntity(A); Instance.RemoveEntity(B); A.Client = B.Client = null;
            MapController.Lookup.Delete(Map); MountCache.SetValue(null, _mounts); ArenaCache.SetValue(null, _arenas);
        }
    }
    [Test] public void BothRealPlayersShareThreePassesWithoutAnNpcCloneAndSeeRelativeScores()
    {
        using var w = new World(); var s = w.Start(); var began = s.Match.PhaseStarted;
        Assert.That(s.Match.IsPve, Is.False); Assert.That(w.Instance.GetEntities().OfType<JoustOpponent>(), Is.Empty);
        Assert.That(w.A.CanUseLobbyTravel || w.B.CanUseLobbyTravel, Is.False);
        long sequence = 0;
        for (var now = began; now < began + 60000 && !s.Closed; now += 50)
        {
            s.Tick(w.A, now);
            if (s.Closed) break;
            if (s.Match.Phase == JoustPhase.Aim)
            {
                s.Input(w.A, s.Match.Pass, ++sequence, new(.5, .44, false), now);
                s.Input(w.B, s.Match.Pass, sequence, new(.61, .44, true), now);
            }
            Assert.That(w.A.X + w.B.X, Is.EqualTo(w.Arena.StartX + w.Arena.EndX));
            Assert.That(w.A.Y, Is.EqualTo(w.Arena.StartY)); Assert.That(w.B.Y, Is.EqualTo(w.Arena.OpponentY));
            Assert.That(w.A.ScriptedMovementMilliseconds, Is.EqualTo(w.B.ScriptedMovementMilliseconds));
            w.A.UpdateMount(); w.B.UpdateMount();
            Assert.That(w.A.ActiveMountId, Is.EqualTo(w.Arena.MountId)); Assert.That(w.B.ActiveMountId, Is.EqualTo(w.Arena.MountId));
        }
        Assert.That(s.Match.Phase, Is.EqualTo(JoustPhase.Finished)); Assert.That(s.Match.Pass, Is.EqualTo(3));
        Assert.That(s.Match.Scores.All(x => x > 0), Is.True, "Both players, rather than an AI, submit their own inputs.");
        var a = s.ViewFor(w.A, began + 60000); var b = s.ViewFor(w.B, began + 60000);
        Assert.That(a.PlayerScore, Is.EqualTo(b.OpponentScore)); Assert.That(a.OpponentScore, Is.EqualTo(b.PlayerScore));
        Assert.That(a.OpponentName, Is.EqualTo(w.B.Name)); Assert.That(b.OpponentName, Is.EqualTo(w.A.Name));
        Assert.That(w.A.ActiveMountId == Guid.Empty && w.B.ActiveMountId == Guid.Empty, Is.True);
        Assert.That(w.A.IsDisposed || w.B.IsDisposed, Is.False, "Cleaning a duel must never dispose the opposing real player.");
        Assert.That(w.Instance.GetEntities(), Does.Contain(w.A).And.Contain(w.B));
        Assert.That(w.A.IsJousting || w.B.IsJousting, Is.False);
        Assert.That(JoustRuntime.Reserve(w.Arena.Id, Guid.Empty), Is.True); JoustRuntime.Release(w.Arena.Id, Guid.Empty);
    }
    [TestCase("disconnect-a")] [TestCase("disconnect-b")] [TestCase("both-offline")] [TestCase("map")] [TestCase("instance")] [TestCase("death")] [TestCase("obstacle")] [TestCase("stall")] [TestCase("leave")] [TestCase("map-unload")]
    public void InterruptionRestoresBothPlayersAndReleasesTheLane(string reason)
    {
        using var w = new World(); var s = w.Start(); var now = s.Match.PhaseStarted;
        switch (reason)
        {
            case "disconnect-a": w.A.Client = null; break;
            case "disconnect-b": w.B.Client = null; break;
            case "both-offline": w.A.InGame = w.B.InGame = false; break;
            case "map": w.A.MapId = Guid.NewGuid(); break;
            case "instance": w.B.MapInstanceId = Guid.NewGuid(); break;
            case "death": w.B.IsDead = true; break;
            case "map-unload": MapController.Lookup.Delete(w.Map); break;
            case "obstacle": w.Map.Attributes[5, 14] = new MapBlockedAttribute(); now = s.Match.Deadline; break;
            case "stall": now = s.Match.Deadline + 1000; break;
            case "leave": s.Leave(w.B); break;
        }
        if (reason is "disconnect-a" or "disconnect-b" or "both-offline" or "map" or "instance" or "death" or "map-unload") JoustPvpRuntime.Sweep();
        else s.Tick(w.A, now);
        Assert.That(s.Closed, Is.True); Assert.That(s.Match.Phase, Is.EqualTo(JoustPhase.Abandoned));
        Assert.That(w.A.IsJousting || w.B.IsJousting, Is.False); Assert.That(w.A.ActiveMountId == Guid.Empty && w.B.ActiveMountId == Guid.Empty, Is.True);
        Assert.That(Field(w.A, "_joustPendingReward"), Is.Null); Assert.That(Field(w.B, "_joustPendingReward"), Is.Null);
        Assert.That(JoustRuntime.Reserve(w.Arena.Id, Guid.Empty), Is.True); JoustRuntime.Release(w.Arena.Id, Guid.Empty);
    }
    [Test] public void ExpiredSpoofedAndReplayedAcceptancesCannotStartAnotherDuel()
    {
        using var w = new World(); var invite = w.Invite();
        Assert.That(JoustPvpRuntime.Accept(w.A, invite.Id, Environment.TickCount64), Is.Not.Empty);
        Assert.That(JoustPvpRuntime.Accept(w.B, Guid.NewGuid(), Environment.TickCount64), Is.Not.Empty);
        Assert.That(JoustPvpRuntime.Accept(w.B, invite.Id, invite.Expires + 1), Is.Not.Empty);
        Assert.That(w.Session, Is.Null);
    }
    [Test] public void WrongStartSameAccountAndDisabledModeAreRejected()
    {
        using var w = new World(); var invite = w.Invite();
        w.B.X = 25;
        Assert.That(JoustPvpRuntime.Accept(w.B, invite.Id, Environment.TickCount64), Is.Not.Empty);
        w.B.X = 26; typeof(Player).GetProperty(nameof(Player.UserId))!.SetValue(w.B, w.A.UserId);
        Assert.That(JoustPvpRuntime.Accept(w.B, invite.Id, Environment.TickCount64), Is.Not.Empty);
        typeof(Player).GetProperty(nameof(Player.UserId))!.SetValue(w.B, Guid.NewGuid()); w.Arena.AllowPvp = false;
        Assert.That(JoustPvpRuntime.Accept(w.B, invite.Id, Environment.TickCount64), Is.Not.Empty);
        Assert.That(w.Session, Is.Null);
    }
    [Test] public void BusyPeerLockCannotDeadlockTheWorldThreadOrMoveOnePlayerAlone()
    {
        using var w = new World(); var s = w.Start(); using var held = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var peer = Task.Run(() => { lock (w.B.EntityLock) { held.Set(); release.Wait(TimeSpan.FromSeconds(5)); } });
        Assert.That(held.Wait(2000), Is.True);
        try
        {
            var tick = Task.Run(() => { lock (w.A.EntityLock) s.Tick(w.A, s.Match.Deadline); });
            Assert.That(tick.Wait(1000), Is.True, "The owner must not block while a peer holds its lock.");
            Assert.That(w.A.X, Is.EqualTo(4)); Assert.That(w.B.X, Is.EqualTo(26));
        }
        finally { release.Set(); peer.Wait(2000); }
        s.Tick(w.A, s.Match.Deadline);
        Assert.That(w.A.X + w.B.X, Is.EqualTo(30));
    }
    [Test] public void ThirdPartyCombatAndMovementAreBlockedDuringTheDuel()
    {
        using var w = new World(); w.Start();
        Assert.That(w.A.CanAttack(w.B, null!), Is.False); Assert.That(w.B.CanAttack(w.A, null!), Is.False);
        var x = w.A.X; w.A.Move(Direction.Right, w.A, correction: true); Assert.That(w.A.X, Is.EqualTo(x));
    }
    [Test] public void InvitationWireFieldsPreserveOldIdsAndRoundTrip()
    {
        Assert.That((int)JoustAction.SaveConfiguration, Is.EqualTo(5)); Assert.That((int)JoustAction.PrepareWilliam, Is.EqualTo(7));
        var p = new JoustRequestPacket { Action = JoustAction.AcceptDuel, TargetId = Guid.NewGuid(), InvitationId = Guid.NewGuid() };
        var decoded = MessagePackSerializer.Deserialize<JoustRequestPacket>(MessagePackSerializer.Serialize(p));
        Assert.That(decoded.InvitationId, Is.EqualTo(p.InvitationId)); Assert.That(decoded.TargetId, Is.EqualTo(p.TargetId));
        var state = new JoustStatePacket { OpenDuelWindow = true, DuelJson = "{}" };
        Assert.That(MessagePackSerializer.Deserialize<JoustStatePacket>(MessagePackSerializer.Serialize(state)).OpenDuelWindow, Is.True);
    }
    [Test] public void SideTwoWinsAndItsCareerReceiptUsesTheCorrectSideOnlyOnce()
    {
        using var w = new World(); var s = w.Start(); long seq = 0; var end = s.Match.PhaseStarted + 60000;
        for (var now = s.Match.PhaseStarted; !s.Closed && now < end; now += 50)
        {
            s.Tick(w.A, now);
            if (!s.Closed && s.Match.Phase == JoustPhase.Aim) s.Input(w.B, s.Match.Pass, ++seq, new(.5, .44, false), now);
        }
        Assert.That(s.Match.Winner, Is.EqualTo(1)); Assert.That(s.ViewFor(w.A, 0).Winner, Is.EqualTo(1)); Assert.That(s.ViewFor(w.B, 0).Winner, Is.Zero);
        Assert.That(Field(w.B, "_joustRewardSide"), Is.EqualTo(1)); Assert.That(Field(w.B, "_joustPendingReward"), Is.SameAs(s.Match));
        var career = new JoustCareer(); var utc = DateTimeOffset.UtcNow;
        Assert.That(career.Complete(s.Match, new(), utc, 1), Is.EqualTo(15)); Assert.That(career.Complete(s.Match, new(), utc, 1), Is.Zero);
        Assert.That(career.Wins, Is.EqualTo(1)); Assert.That(career.Matches, Is.EqualTo(1));
        s.Tick(w.A, s.Match.Deadline); s.Leave(w.B);
        Assert.That(Field(w.B, "_joustPendingReward"), Is.SameAs(s.Match));
    }
    [Test] public void WrongParticipantAndLateInputsCannotDriveTheOtherLance()
    {
        using var w = new World(); var s = w.Start(); var other = new Player(); GC.SuppressFinalize(other);
        var began = s.Match.PhaseStarted;
        s.Input(other, 1, 100, new(.5, .44, false), began + 10000);
        Assert.That(s.Match.Phase, Is.EqualTo(JoustPhase.Preparation));
        s.Tick(w.A, s.Match.Deadline); s.Tick(w.A, s.Match.Deadline);
        var aim = s.Match.PhaseStarted;
        s.Input(w.A, 1, 1, new(.5, .44, false), aim + 100);
        Assert.That(s.Match.Aim(1).Y, Is.EqualTo(.75));
        var position = s.Match.Aim(0); s.Input(w.A, 1, 1, new(.8, .8, true), aim + 150);
        Assert.That(s.Match.Aim(0), Is.EqualTo(position));
        s.Input(w.B, 1, 999, new(.5, .44, false), s.Match.Deadline);
        Assert.That(s.Match.Strikes[1].Hit, Is.EqualTo(JoustHit.Miss));
    }
}
