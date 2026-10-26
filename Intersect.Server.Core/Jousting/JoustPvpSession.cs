using Intersect.Framework.Core.Jousting;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
namespace Intersect.Server.Jousting;

/// <summary>All simulation and movement require both entity locks. Acquisitions never wait for the other player.</summary>
internal sealed class JoustPvpSession
{
    internal readonly Player First, Second;
    internal readonly JoustArena Arena;
    internal readonly MapInstance Instance;
    internal readonly JoustMatch Match;
    private readonly long _began;
    private long _lastTick;
    private int _abort = -1;
    private int _closed;
    internal bool Closed => Volatile.Read(ref _closed) != 0;
    internal JoustPvpSession(Player first, Player second, JoustArena arena, MapInstance instance, long now)
    {
        First = first; Second = second; Instance = instance;
        Arena = JoustConfiguration.FromJson(new JoustConfiguration { Arenas = [arena] }.ToJson()).Arenas[0];
        Match = new(Arena.Rules, now, JoustDifficulty.Knight, pve: false); _began = _lastTick = now;
    }
    internal int Side(Player p) => ReferenceEquals(p, First) ? 0 : ReferenceEquals(p, Second) ? 1 : -1;
    internal Player Opponent(Player p) => ReferenceEquals(p, First) ? Second : First;
    private bool Present(Player p) => !p.IsDisposed && p.InGame && p.Client != null && !p.IsDead && !p.HasPendingDeath &&
        p.MapId == Arena.MapId && p.MapInstanceId == Instance.MapInstanceId && p.ActiveMountId == Arena.MountId &&
        p.CombatTimer <= Intersect.Framework.Core.Timing.Global.Milliseconds;
    internal bool NeedsSweep => !Closed && (Volatile.Read(ref _abort) >= 0 || Instance.IsDisposed || MapController.Get(Arena.MapId) == null || !Present(First) || !Present(Second));
    internal bool WithPlayers(Action action)
    {
        if (!Monitor.TryEnter(First.EntityLock)) return false;
        try
        {
            if (!Monitor.TryEnter(Second.EntityLock)) return false;
            try { action(); return true; } finally { Monitor.Exit(Second.EntityLock); }
        }
        finally { Monitor.Exit(First.EntityLock); }
    }
    internal void Tick(Player? caller, long now)
    {
        if (caller != null && Side(caller) < 0) return;
        // The second player takes over when the first leaves the map/update loop.
        if (ReferenceEquals(caller, Second) && Present(First) && Volatile.Read(ref _abort) < 0) return;
        WithPlayers(() => TickCore(now));
    }
    internal void Input(Player sender, int pass, long sequence, JoustAim aim, long now)
    {
        var side = Side(sender); if (side < 0) return;
        WithPlayers(() => { TickCore(now); if (!Closed && now >= _lastTick) Match.Submit(side, pass, sequence, aim, now); });
    }
    internal void Leave(Player player)
    {
        var side = Side(player); if (side < 0) return;
        Interlocked.CompareExchange(ref _abort, side, -1);
        Tick(player, Environment.TickCount64);
    }
    internal void Publish(Player player, long now)
    { if (Side(player) >= 0) WithPlayers(() => player.PublishPvpJoust(ViewFor(player, now))); }
    internal JoustView ViewFor(Player player, long now)
    {
        var side = Side(player); if (side < 0) throw new ArgumentException(nameof(player));
        return new() { IsPve = false, OpponentName = Opponent(player).Name, SelfName = player.Name, Sounds = Arena.Sounds, MatchId = Match.Id, ArenaId = Arena.Id,
            Phase = Match.Phase, Pass = Match.Pass, Passes = Match.Passes,
            RemainingMs = (int)Math.Max(0, Match.Deadline - now), PhaseDurationMs = (int)(Match.Deadline - Match.PhaseStarted),
            PlayerScore = Match.Scores[side], OpponentScore = Match.Scores[1 - side], Aim = Match.Aim(side),
            PlayerStrike = Match.Strikes[side], OpponentStrike = Match.Strikes[1 - side],
            Winner = Match.Winner < 0 ? -1 : Match.Winner == side ? 0 : 1 };
    }
    private void TickCore(long now)
    {
        if (Closed) return;
        var abort = Volatile.Read(ref _abort);
        if (abort >= 0 || Instance.IsDisposed || MapController.Get(Arena.MapId) == null || !Present(First) || !Present(Second))
        { Match.Abandon(abort >= 0 ? abort : !Present(First) ? 0 : 1); Finish(now); return; }
        if (now < _lastTick) return;
        _lastTick = now;
        if (now - _began > 600000)
        { Match.Abandon(0); Finish(now); return; }
        if (Match.Phase == JoustPhase.Preparation && (First.JoustIntroductionHolding || Second.JoustIntroductionHolding))
        { Match.HoldPreparation(now); PublishBoth(now); return; }
        if (now > Match.Deadline + 250) { Match.Abandon(0); Finish(now); return; }
        if (!First.MovePvpJoust(now) || !Second.MovePvpJoust(now))
        { Match.Abandon(0); Finish(now); return; }
        Match.Advance(now);
        First.PresentPvpJoust(); Second.PresentPvpJoust();
        if (Match.Done) { Finish(now); return; }
        if (!First.MovePvpJoust(now) || !Second.MovePvpJoust(now))
        { Match.Abandon(0); Finish(now); return; }
        PublishBoth(now);
    }
    private void PublishBoth(long now)
    { JoustAudienceRuntime.Publish(Arena, Instance.MapInstanceId, Match, First.Id, First.Name, Second.Id, Second.Name, now);
      First.PublishPvpJoust(ViewFor(First, now)); Second.PublishPvpJoust(ViewFor(Second, now)); }
    private void Finish(long now)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        JoustAudienceRuntime.Publish(Arena, Instance.MapInstanceId, Match, First.Id, First.Name, Second.Id, Second.Name, now);
        try { First.FinishPvpJoust(this, ViewFor(First, now), 0); }
        finally
        {
            try { Second.FinishPvpJoust(this, ViewFor(Second, now), 1); }
            finally { JoustRuntime.Release(Arena.Id, Match.Id); JoustPvpRuntime.Remove(this); }
        }
    }
}
