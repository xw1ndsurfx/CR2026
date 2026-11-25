using System.Text.Json;
using Intersect.Framework.Core.MountRacing;
using Intersect.Network.Packets.Server;
using Intersect.Client.General;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private MountRaceWindow? _races;
    private MountRaceHud? _raceHud;
    internal MountRaceView RaceView { get; private set; } = new();
    internal bool RaceRider(Guid id) => RaceView.OnTrack && RaceView.Riders.Any(r => r.Id == id);
    internal bool RaceMovementBlocked => RaceView.OnTrack && (RaceView.Phase != MountRacePhase.Running || RaceView.Riders.Any(r => r.Id == Globals.Me?.Id && (r.Withdrawn || r.Place > 0)));
    public void OpenMountRaces() => (_races ??= new MountRaceWindow(GameCanvas)).Open();
    public void ApplyMountRace(MountRaceStatePacket packet)
    {
        if (packet.Editor || JsonSerializer.Deserialize<MountRaceView>(packet.Json) is not {} view) return;
        var previous = RaceView; RaceView = view;
        if (Globals.Me is {} player)
        {
            if (view.OnTrack) player.ServerMovementMilliseconds = view.MovementMilliseconds;
            else if (previous.OnTrack) player.ServerMovementMilliseconds = 0;
        }
        (_races ??= new MountRaceWindow(GameCanvas)).Apply(view, packet.Open);
        (_raceHud ??= new MountRaceHud(GameCanvas, () => _races.Open(), () => _races.Request(Intersect.Network.Packets.Client.MountRaceAction.Leave))).Apply(view);
    }
}
