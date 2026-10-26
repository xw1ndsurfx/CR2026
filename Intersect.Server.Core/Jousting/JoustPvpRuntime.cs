using System.Collections.Concurrent;
using System.Text.Json;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Microsoft.Extensions.Logging;
namespace Intersect.Server.Jousting;
internal static class JoustPvpRuntime
{
    internal sealed record Invitation(Guid Id, Player Challenger, Player Target, Guid ArenaId, Guid InstanceId, long Expires);
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Invitation> Invitations = [];
    private static readonly ConcurrentDictionary<Guid, JoustPvpSession> Sessions = new();
    private static readonly System.Threading.Timer Cleanup = new(_ => Sweep(), null, 1000, 1000);
    private static JoustArena? Arena(Player p) => JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.AllowPvp && a.MapId == p.MapId);
    private static void Prune(long now) { foreach (var key in Invitations.Where(p => p.Value.Expires <= now || !p.Value.Challenger.InGame || !p.Value.Target.InGame || p.Value.Challenger.Client == null || p.Value.Target.Client == null || p.Value.Challenger.IsJousting || p.Value.Target.IsJousting).Select(p => p.Key).ToArray()) Invitations.Remove(key); }
    internal static void Browse(Player player, bool open = true)
    {
        var now = Environment.TickCount64; Invitation? invitation;
        lock (Gate) { Prune(now); invitation = Invitations.GetValueOrDefault(player.Id); if (!ReferenceEquals(invitation?.Target, player)) invitation = null; }
        var arena = invitation == null ? Arena(player) : JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Id == invitation.ArenaId && a.Enabled && a.AllowPvp);
        var candidates = MapController.TryGetInstanceFromMap(player.MapId, player.MapInstanceId, out var instance)
            ? instance.GetEntities().OfType<Player>().Where(p => p != player && p.InGame && p.Client != null && !p.IsDisposed && !p.HideEntity &&
                !p.CachedStatuses.Any(s => s.Type == Intersect.Enums.SpellEffect.Stealth)).OrderBy(p => p.Name).Take(64).Select(p => new JoustCandidate(p.Id, p.Name)).ToArray() : [];
        player.SendPacket(new JoustStatePacket { OpenDuelWindow = open, DuelJson = JsonSerializer.Serialize(new JoustDuelView {
            ArenaId = arena?.Id ?? Guid.Empty, ArenaName = arena?.Name ?? "", Candidates = candidates,
            InvitationId = invitation?.Id ?? Guid.Empty, Challenger = invitation?.Challenger.Name ?? "", ExpiresInMs = (int)Math.Max(0, (invitation?.Expires ?? now) - now),
            StartX = arena?.StartX ?? 0, StartY = arena?.StartY ?? 0, OpponentX = arena?.EndX ?? 0, OpponentY = arena?.OpponentY ?? 0 }) });
    }
    internal static string Invite(Player sender, Guid targetId, long now)
    {
        var arena = Arena(sender);
        if (arena == null) return "Player duels are not enabled on this map.";
        if (!MapController.TryGetInstanceFromMap(sender.MapId, sender.MapInstanceId, out var instance)) return "Both knights must be ready on the same map.";
        var target = instance.GetEntities().OfType<Player>().FirstOrDefault(p => p.Id == targetId && p != sender && !p.HideEntity && !p.CachedStatuses.Any(s => s.Type == Intersect.Enums.SpellEffect.Stealth));
        if (target == null || !Monitor.TryEnter(target.EntityLock)) return "This player is unavailable. Try again.";
        try
        {
            if (!sender.CanEnterPvpJoust(arena) || !target.CanEnterPvpJoust(arena)) return "Both knights must be ready on the same map.";
            if (sender.UserId == target.UserId || sender.UserId == Guid.Empty || target.UserId == Guid.Empty) return "A duel requires two different accounts.";
            if (sender.X != arena.StartX || sender.Y != arena.StartY) return "Stand at the gold starting tile before challenging a player.";
            lock (Gate)
            {
                Prune(now);
                if (Invitations.ContainsKey(target.Id)) return "This player already has a pending joust invitation.";
                if (Invitations.Count >= 1024) return "This player is unavailable. Try again.";
                foreach (var key in Invitations.Where(i => ReferenceEquals(i.Value.Challenger, sender)).Select(i => i.Key).ToArray()) Invitations.Remove(key);
                Invitations[target.Id] = new(Guid.NewGuid(), sender, target, arena.Id, sender.MapInstanceId, now + 30000);
            }
            Browse(target); return "Joust invitation sent. Your opponent has 30 seconds to accept.";
        }
        finally { Monitor.Exit(target.EntityLock); }
    }
    internal static string Accept(Player target, Guid invitationId, long now)
    {
        Invitation? invite;
        lock (Gate) { Prune(now); invite = Invitations.GetValueOrDefault(target.Id); }
        if (invite == null || invite.Id != invitationId || !ReferenceEquals(target, invite.Target)) return "This joust invitation has expired.";
        var sender = invite.Challenger;
        if (!Monitor.TryEnter(sender.EntityLock)) return "This player is unavailable. Try again.";
        try
        {
            var arena = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Id == invite.ArenaId && a.Enabled && a.AllowPvp);
            if (arena == null || !sender.CanEnterPvpJoust(arena) || !target.CanEnterPvpJoust(arena) || sender.MapInstanceId != invite.InstanceId || target.MapInstanceId != invite.InstanceId)
                return "Both knights must be ready on the same map.";
            if (sender.UserId == target.UserId || sender.UserId == Guid.Empty || target.UserId == Guid.Empty) return "A duel requires two different accounts.";
            if (sender.X != arena.StartX || sender.Y != arena.StartY || target.X != arena.EndX || target.Y != arena.OpponentY)
                return "Place the challenger on the gold start and the opponent on the blue start before accepting.";
            if (!JoustRuntime.LanesClear(arena, invite.InstanceId, sender, target) || !MapController.TryGetInstanceFromMap(arena.MapId, invite.InstanceId, out var instance))
                return "Clear both jousting lanes before starting the duel.";
            lock (Gate)
            {
                if (!Invitations.TryGetValue(target.Id, out var current) || current.Id != invitationId || current.Expires <= now) return "This joust invitation has expired.";
                Invitations.Remove(target.Id);
            }
            var session = new JoustPvpSession(sender, target, arena, instance, now);
            if (!JoustRuntime.Reserve(arena.Id, session.Match.Id)) return "This jousting lane is occupied.";
            Sessions[session.Match.Id] = session;
            try
            {
                sender.BeginPvpJoust(session); target.BeginPvpJoust(session);
                sender.IntroducePvpJoust(); target.IntroducePvpJoust();
            }
            catch { session.Leave(sender); throw; }
            return "";
        }
        finally { Monitor.Exit(sender.EntityLock); }
    }
    internal static void Decline(Player target, Guid id)
    {
        lock (Gate)
            if (Invitations.TryGetValue(target.Id, out var invite) && ReferenceEquals(invite.Target, target) && invite.Id == id) Invitations.Remove(target.Id);
        Browse(target);
    }
    internal static void Remove(JoustPvpSession session) => Sessions.TryRemove(session.Match.Id, out _);
    internal static void Sweep()
    {
        lock (Gate) Prune(Environment.TickCount64);
        foreach (var session in Sessions.Values)
            try { if (session.NeedsSweep) session.Tick(null, Environment.TickCount64); }
            catch (Exception error) { Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(error, "Joust PvP cleanup failed"); }
    }
}
