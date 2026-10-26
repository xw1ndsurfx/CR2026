using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Client;
using Intersect.Server.Jousting;
namespace Intersect.Server.Entities;
public partial class Player
{
    private (Guid Arena, Guid Map, Guid Instance, JoustAction Action, int X, int Y)? _joustEntry;
    private long _joustEntryCheckAt, _joustEntryPromptAt;

    // The server's real position controls the invitation, just as proximity controls naval prompts.
    // Remember the entrance while playing/seated, so finishing or standing cannot reopen a dismissed window.
    internal JoustAction? UpdateJoustEntry(long now)
    {
        // Observe departures every update, even between the more expensive proximity checks.
        if (!IsJoustProtected && _joustEntry is { } previous &&
            (previous.Map != MapId || previous.Instance != MapInstanceId || previous.X != X || previous.Y != Y)) _joustEntry = null;
        if (now < _joustEntryCheckAt) return null;
        _joustEntryCheckAt = now + 200;
        if (IsJoustProtected) return null;
        var arena = JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == MapId);
        JoustAction? action = arena == null ? null : X == arena.StartX && Y == arena.StartY ? JoustAction.Open :
            arena.AllowPvp && X == arena.EndX && Y == arena.OpponentY ? JoustAction.BrowseDuels :
            arena.Seats.Any(s => s.ExitX == X && s.ExitY == Y) ? JoustAction.BrowseSeats : null;
        if (!InGame || Client == null || Client.IsEditor || arena == null || action == null)
        { _joustEntry = null; return null; }
        var entry = (arena.Id, MapId, MapInstanceId, action.Value, (int)X, (int)Y);
        if (_joustEntry == entry || now - _joustEntryPromptAt < 500 || HideEntity || IsCasting || IsBlocking ||
            NavalShipId != Guid.Empty || MoveRoute != null || !CanUseLobbyTravel || EventLookup.Values.Any(e => e.CinematicActive)) return null;
        _joustEntry = entry; _joustEntryPromptAt = now;
        if (action == JoustAction.Open) OpenJoustPreparation(now);
        else if (action == JoustAction.BrowseDuels) JoustPvpRuntime.Browse(this);
        else BrowseJoustSeats();
        return action;
    }
}
