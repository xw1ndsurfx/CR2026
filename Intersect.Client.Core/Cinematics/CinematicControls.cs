using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Framework.Threading;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Cinematics;

internal static class CinematicControls
{
    private sealed record State(Guid Event, long Version, Guid Page, Guid Player, Guid Map, long Started, bool Allowed, bool Skipping, bool Pending);
    private static readonly object Gate = new();
    private static State? _state;

    internal static void Clear() { lock (Gate) _state = null; }

    internal static string SkipLabel => QualityOfLifeText.Get("Skip presentation", ClientLanguages.Current);
    internal static string SkipTooltip => QualityOfLifeText.Get("Required choices, actions and quest objectives still apply.", ClientLanguages.Current);

    private static bool Valid(State state) => Globals.GameState == GameStates.InGame && Globals.Me?.Id == state.Player &&
        Globals.Me.MapId == state.Map && Environment.TickCount64 - state.Started <= 30 * 60 * 1000;

    internal static bool CanSkip { get { lock (Gate) return _state is { Allowed:true, Skipping:false, Pending:false } state && Valid(state); } }
    internal static bool CanSkipDialog(Guid page) { lock (Gate) return CanSkip && _state?.Page == page; }

    internal static void Receive(CinematicControlPacket packet)
    {
        if (packet.EventId == Guid.Empty || packet.RunVersion <= 0 || Globals.Me is not { } me) return;
        lock (Gate)
        {
            if (_state is { } old && old.Event == packet.EventId && old.Version > packet.RunVersion) return;
            if (!packet.Active)
            {
                if (_state is { } current && current.Event == packet.EventId && current.Version == packet.RunVersion) _state = null;
            }
            else
                _state = new(packet.EventId, packet.RunVersion, packet.PageId, me.Id, me.MapId, Environment.TickCount64,
                    packet.AllowVisualSkip, packet.Skipping, false);
        }
        if (packet.CloseDialogueToken != Guid.Empty)
            ThreadQueue.Default.RunOnMainThread(() => EventWindow.CloseCinematicText(packet.CloseDialogueToken));
    }

    internal static void RequestSkip()
    {
        CinematicSkipRequestPacket packet;
        lock (Gate)
        {
            if (!CanSkip || _state is not { } state) return;
            packet = new() { EventId = state.Event, RunVersion = state.Version };
            _state = state with { Pending = true };
        }
        Networking.Network.SendPacket(packet);
    }
}
