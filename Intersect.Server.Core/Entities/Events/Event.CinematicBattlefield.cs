using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Entities.Events;

public partial class Event
{
    private sealed record EffectMotion(double FromX,double FromY,double ToX,double ToY,int Duration,
        CinematicEase Ease,float ArcHeight,Guid ImpactAnimation);

    internal bool StartCinematicEffectPath(CinematicEffectPathCommand command)
    {
        if (!CinematicActive || command.DurationMs is < 1 or > 30000 || !Enum.IsDefined(command.Ease) ||
            !float.IsFinite(command.ArcHeight) || command.ArcHeight is < 0 or > 8 || command.X < 0 || command.Y < 0 ||
            command.X >= Options.Instance.Map.MapWidth || command.Y >= Options.Instance.Map.MapHeight ||
            AnimationDescriptor.Get(command.AnimationId) == null ||
            command.ImpactAnimationId != Guid.Empty && AnimationDescriptor.Get(command.ImpactAnimationId) == null) return false;
        double x,y;
        if (_cinematicVessels.TryGetValue(command.ActorId,out var ship))
        { SampleVessel(ship,Environment.TickCount64);x=ship.X;y=ship.Y; }
        else if (command.ActorId == Guid.Empty) { x=Player.X+.5;y=Player.Y+.5; }
        else
        {
            var actor=Player.EventLookup.Values.FirstOrDefault(e=>e.Descriptor.Id == command.ActorId && !e.Global && e.MapId == _cinematicMap && e.PageInstance is {GlobalClone:null});
            if (actor?.PageInstance is not { } page) return false;
            x=page.X+.5;y=page.Y+.5;
        }
        _cinematicNavigation ??=new();
        var now=Environment.TickCount64;
        if (_cinematicVesselEffects.Count >= 128) _cinematicVesselEffects.RemoveAt(0);
        var heading=ShipMovement.Normalize(Math.Atan2(command.X+.5-x,-(command.Y+.5-y))*180/Math.PI);
        _cinematicVesselEffects.Add(new(new NavalEffectSnapshot { Id=Guid.NewGuid(),MapId=_cinematicMap,X=x,Y=y,AnimationId=command.AnimationId,
            Kind=NavalEffectKind.HullImpact,Heading=heading,LoopAnimation=true,RotateAnimation=command.RotateWithPath },now,Guid.Empty,0,0,
            new(x,y,command.X+.5,command.Y+.5,command.DurationMs,command.Ease,command.ArcHeight,command.ImpactAnimationId)));
        PublishCinematicFleet(true);return true;
    }

    private void FinishCinematicEffectPaths(long now)
    {
        foreach (var effect in _cinematicVesselEffects.Where(e=>e.Motion != null && now-e.Born >= e.Motion.Duration).ToArray())
        {
            _cinematicVesselEffects.Remove(effect);
            if (effect.Motion is {ImpactAnimation:var animation} motion && animation != Guid.Empty)
                AddStageEffect(NavalEffectKind.HullImpact,motion.ToX,motion.ToY,0,now,true,animation);
        }
    }
}
