using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Entities.Events;

public partial class Event
{
    private bool _allowCinematicVisualSkip,_cinematicSkipping;

    private void SendCinematicControl(bool active,Guid closeDialogue=default) => Player.SendPacket(new CinematicControlPacket {
        EventId=Descriptor.Id,RunVersion=CinematicRunVersion,Active=active,AllowVisualSkip=_allowCinematicVisualSkip,
        Skipping=_cinematicSkipping,CloseDialogueToken=closeDialogue,PageId=PageInstance?.Id ?? Guid.Empty });

    internal bool ShouldSkipCinematicCommand(EventCommand command,CommandInstance frame)
    {
        if (!CinematicActive || !_cinematicSkipping) return false;
        if (command is CinematicCameraCommand {Reset:false} or CinematicEnvironmentCommand or CinematicVesselMoveCommand or
            CinematicVesselActionCommand or CinematicEffectPathCommand) return true;
        if (frame.Page.Cinematic == null) return false; // Native gameplay callbacks remain intact.
        return command is WaitCommand or ShowTextCommand or PlayAnimationCommand or PlayBgmCommand or FadeoutBgmCommand or PlaySoundCommand;
    }

    internal void RequestCinematicVisualSkip(long version)
    {
        if (!CinematicActive || !_allowCinematicVisualSkip || _cinematicSkipping || version != CinematicRunVersion ||
            Player.MapId != _cinematicMap || Player.MapInstanceId != _cinematicInstance) return;
        _cinematicSkipping=true;
        var close=Guid.Empty;
        if (CallStack.TryPeek(out var frame) && frame.Page.Cinematic != null)
        {
            var previous=frame.CommandIndex > 0 ? frame.CommandList.ElementAtOrDefault(frame.CommandIndex-1) : null;
            if (frame.WaitingForResponse == CommandInstance.EventResponse.Dialogue && previous?.GetType() == typeof(ShowTextCommand))
            {
                close=frame.CinematicDialogueToken;frame.CinematicDialogueToken=Guid.Empty;
                frame.WaitingForResponse=CommandInstance.EventResponse.None;
            }
            else if (frame.WaitingForResponse == CommandInstance.EventResponse.Timer && previous is WaitCommand)
            { WaitTimer=0;frame.WaitingForResponse=CommandInstance.EventResponse.None; }
        }
        RemoveCinematicVessels();
        Player.SendPacket(new CinematicCameraPacket {EventId=Descriptor.Id,Reset=true});
        SendCinematicControl(true,close);
        // Begin/Hold/Release, native actor routes, route waits, options, personal
        // conditions, gameplay Events and quest commands are deliberately retained.
    }
}
