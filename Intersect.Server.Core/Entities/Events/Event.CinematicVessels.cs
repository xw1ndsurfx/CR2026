using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Maps;
using Intersect.Server.Naval;

namespace Intersect.Server.Entities.Events;

public partial class Event
{
    private sealed class StageVessel(CinematicActor actor,ShipDefinition model)
    {
        internal readonly CinematicActor Actor=actor;
        internal readonly ShipDefinition Model=model;
        internal double X=actor.StartX+.5,Y=actor.StartY+.5,Heading=actor.StartHeading;
        internal bool Visible=actor.VisibleAtStart;
        internal CinematicVesselAppearance Appearance=actor.VesselAppearance;
        internal CinematicVesselMoveCommand? Path;
        internal long PathStarted;
    }
    private sealed record StageEffect(NavalEffectSnapshot Effect,long Born,Guid FollowActor,int OffsetX=0,int OffsetY=0);
    private sealed class StageBall(double x,double y,double dx,double dy,double range,long now)
    {
        internal readonly Guid Id=Guid.NewGuid();
        internal readonly long Born=now;
        internal readonly double DX=dx,DY=dy;
        internal double X=x,Y=y,Remaining=range,Travelled;
        internal bool Active=true;
    }
    private readonly Dictionary<Guid,StageVessel> _cinematicVessels=[];
    private readonly List<StageEffect> _cinematicVesselEffects=[];
    private readonly List<StageBall> _cinematicVesselBalls=[];
    private NavalConfiguration? _cinematicNavigation;
    private long _cinematicFleetNext,_cinematicBallUpdated;

    private bool CreateCinematicVessels(IEnumerable<CinematicActor> actors)
    {
        var ships=actors.Where(a=>a.Kind == CinematicActorKind.TemporaryVessel).ToArray();
        if (ships.Length == 0) return true;
        if (ships.Length > CinematicVessels.MaximumVessels || !OceanMapResolver.Maps().Contains(_cinematicMap) ||
            !NavalSystem.ReadCinematicModels(out var models,out var configuration)) return false;
        foreach (var actor in ships)
        {
            if (actor.TargetId == Guid.Empty || !float.IsFinite(actor.StartHeading) || actor.StartHeading is < 0 or > 360 ||
                !Enum.IsDefined(actor.VesselAppearance) || actor.ShipModelKey == null || !models.TryGetValue(actor.ShipModelKey,out var model) ||
                !NavalSystem.CinematicHullClear(_cinematicMap,model.Definition,actor.StartX+.5,actor.StartY+.5)) return false;
            _cinematicVessels.Add(actor.TargetId,new(actor,model.Definition));
        }
        _cinematicNavigation=configuration;
        _cinematicBallUpdated=Environment.TickCount64;
        PublishCinematicFleet(true);
        return true;
    }

    internal bool IsCinematicVessel(Guid actor) => CinematicActive && _cinematicVessels.ContainsKey(actor);
    private static void SampleVessel(StageVessel vessel,long now)
    {
        if (vessel.Path is not { } path) return;
        (vessel.X,vessel.Y,vessel.Heading)=CinematicVessels.Sample(path.Points,(now-vessel.PathStarted)/(float)path.DurationMs,vessel.Heading,path.Ease);
        if (now-vessel.PathStarted >= path.DurationMs) vessel.Path=null;
    }

    internal bool MoveCinematicVessel(CinematicVesselMoveCommand command)
    {
        if (!CinematicActive || !_cinematicVessels.TryGetValue(command.ActorId,out var vessel) || command.Points == null ||
            command.DurationMs is < 1 or > CinematicCompiler.MaximumDurationMs || !Enum.IsDefined(command.Ease) || command.Points.Count is < 2 or > 256 ||
            command.Points.Any(p=>p == null || p.X < 0 || p.Y < 0 || p.X >= Options.Instance.Map.MapWidth || p.Y >= Options.Instance.Map.MapHeight ||
                p.PauseMs != 0 || p.SpeedOverride != -1 || p.Facing != CinematicFacing.Unchanged)) return false;
        var now=Environment.TickCount64;SampleVessel(vessel,now);
        if (vessel.Path != null || Math.Abs(vessel.X-command.Points[0].X-.5) > .01 || Math.Abs(vessel.Y-command.Points[0].Y-.5) > .01 ||
            CinematicVessels.PathLength(command.Points) is <= 0 or > 256) return false;
        // Conservative native hull footprint, swept over every segment. No gameplay ship is moved.
        for (var i=1;i<command.Points.Count;i++)
        {
            var a=command.Points[i-1];var b=command.Points[i];
            var steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(Math.Pow(b.X-a.X,2)+Math.Pow(b.Y-a.Y,2))*4));
            for (var sample=0;sample<=steps;sample++)
                if (!NavalSystem.CinematicHullClear(_cinematicMap,vessel.Model,a.X+.5+(b.X-a.X)*sample/(double)steps,a.Y+.5+(b.Y-a.Y)*sample/(double)steps)) return false;
        }
        vessel.Path=command;vessel.PathStarted=now;
        PublishCinematicFleet(true);return true;
    }

    internal bool ActOnCinematicVessels(CinematicVesselActionCommand command)
    {
        if (!CinematicActive || _cinematicNavigation == null || !_cinematicVessels.TryGetValue(command.ActorId,out var selected) ||
            !Enum.IsDefined(command.VesselAction) || !Enum.IsDefined(command.VesselAppearance) ||
            command.X < 0 || command.Y < 0 || command.X >= Options.Instance.Map.MapWidth || command.Y >= Options.Instance.Map.MapHeight ||
            Math.Abs((long)command.AnimationOffsetX) > 1024 || Math.Abs((long)command.AnimationOffsetY) > 1024 ||
            command.AnimationId != Guid.Empty && AnimationDescriptor.Get(command.AnimationId) == null ||
            command.SynchronizedGroup && selected.Actor.GroupId == Guid.Empty) return false;
        if (command.SynchronizedGroup && command.VesselAction == CinematicVesselAction.VesselWaterImpact) return false;
        var now=Environment.TickCount64;
        var ships=command.SynchronizedGroup ? _cinematicVessels.Values.Where(v=>v.Actor.GroupId == selected.Actor.GroupId).ToArray() : [selected];
        foreach (var vessel in ships)
        {
            SampleVessel(vessel,now);
            switch (command.VesselAction)
            {
                case CinematicVesselAction.VesselAppearance:
                    if (vessel.Appearance != command.VesselAppearance && command.VesselAppearance == CinematicVesselAppearance.SinkingVessel)
                        AddStageEffect(NavalEffectKind.Sinking,vessel.X,vessel.Y,vessel.Heading,now);
                    vessel.Appearance=command.VesselAppearance;break;
                case CinematicVesselAction.FirePort:
                case CinematicVesselAction.FireStarboard:
                    var starboard=command.VesselAction == CinematicVesselAction.FireStarboard;
                    var direction=ShipActions.Direction(vessel.Heading,starboard);
                    var count=Math.Clamp(_cinematicNavigation.Gunnery.CannonsPerSide,1,4);
                    for (var gun=0;gun<count;gun++)
                    {
                        var offset=GunneryAim.StationOffset(gun,count,vessel.Model.Length);
                        var radians=vessel.Heading*Math.PI/180;
                        var x=vessel.X+Math.Sin(radians)*offset+direction.X*vessel.Model.Width/2;
                        var y=vessel.Y-Math.Cos(radians)*offset+direction.Y*vessel.Model.Width/2;
                        AddStageEffect(NavalEffectKind.Fire,x,y,ShipMovement.Normalize(vessel.Heading+(starboard?90:-90)),now,gun == 0);
                        if (_cinematicVesselBalls.Count >= 128) _cinematicVesselBalls.RemoveAt(0);
                        _cinematicVesselBalls.Add(new(x,y,direction.X,direction.Y,_cinematicNavigation.CannonRange,now));
                    }
                    break;
                default:
                    AddStageEffect(command.VesselAction == CinematicVesselAction.VesselWaterImpact ? NavalEffectKind.WaterImpact : NavalEffectKind.HullImpact,
                        command.FollowActor ? vessel.X : command.X+.5,command.FollowActor ? vessel.Y : command.Y+.5,0,now,true,
                        command.AnimationId,command.FollowActor ? vessel.Actor.TargetId : Guid.Empty,command.AnimationOffsetX,command.AnimationOffsetY);
                    break;
            }
        }
        PublishCinematicFleet(true);return true;
    }

    private void AddStageEffect(NavalEffectKind kind,double x,double y,double heading,long now,bool sound=true,Guid animation=default,Guid follow=default,int offsetX=0,int offsetY=0)
    {
        if (_cinematicVesselEffects.Count >= 128) _cinematicVesselEffects.RemoveAt(0);
        _cinematicVesselEffects.Add(new(new() { Id=Guid.NewGuid(),MapId=_cinematicMap,X=x,Y=y,Heading=heading,Kind=kind,AnimationId=animation,PlaySound=sound },now,follow,offsetX,offsetY));
    }

    private void UpdateCinematicVessels()
    {
        if (!CinematicActive || _cinematicVessels.Count == 0 || _cinematicNavigation == null) return;
        var now=Environment.TickCount64;
        foreach (var vessel in _cinematicVessels.Values) SampleVessel(vessel,now);
        var seconds=Math.Max(0,(now-_cinematicBallUpdated)/1000d);_cinematicBallUpdated=now;
        foreach (var ball in _cinematicVesselBalls)
        {
            if (!ball.Active) continue;
            var x=ball.X;var y=ball.Y;
            var travelled=ShipCannonSweep.Travel(_cinematicNavigation.CannonSpeed,seconds,ball.Remaining,distance=>
            {
                var nx=x+ball.DX*distance;var ny=y+ball.DY*distance;
                var map=MapController.Get(_cinematicMap);var tx=(int)Math.Floor(nx);var ty=(int)Math.Floor(ny);
                if (map?.Attributes == null || tx < 0 || ty < 0 || tx >= map.Attributes.GetLength(0) || ty >= map.Attributes.GetLength(1) || NavalSystem.IsBlockingAttribute(map.Attributes[tx,ty]))
                { ball.Active=false;AddStageEffect(NavalEffectKind.ObstacleImpact,Math.Clamp(nx,.01,Options.Instance.Map.MapWidth-.01),Math.Clamp(ny,.01,Options.Instance.Map.MapHeight-.01),0,now);return false; }
                return true;
            });
            ball.X=x+ball.DX*travelled;ball.Y=y+ball.DY*travelled;ball.Remaining-=travelled;ball.Travelled+=travelled;
            if (ball.Active && (ball.Remaining <= .001 || now-ball.Born > 30000))
            { ball.Active=false;AddStageEffect(NavalEffectKind.WaterImpact,ball.X,ball.Y,0,now); }
        }
        _cinematicVesselBalls.RemoveAll(b=>!b.Active);
        _cinematicVesselEffects.RemoveAll(e=>now-e.Born > 20000);
        PublishCinematicFleet(false);
    }

    private void PublishCinematicFleet(bool force)
    {
        if (_cinematicNavigation == null || !CinematicActive) return;
        var now=Environment.TickCount64;if (!force && now < _cinematicFleetNext) return;_cinematicFleetNext=now+100;
        Player.SendPacket(new CinematicFleetPacket { EventId=Descriptor.Id,MapId=_cinematicMap,Presentation=_cinematicNavigation.Presentation,
            Ships=_cinematicVessels.Values.Where(v=>v.Visible).Select(v=>new ShipSnapshot { Id=v.Actor.TargetId,MapId=_cinematicMap,X=v.X,Y=v.Y,Heading=v.Heading,
                Graphics=CinematicVessels.Graphics(v.Model,v.Appearance),Frames=v.Model.Frames,FrameMilliseconds=v.Model.FrameMilliseconds,
                Width=v.Model.Width,Length=v.Model.Length,Speed=v.Path == null ? 0 : 1,Sinking=v.Appearance == CinematicVesselAppearance.SinkingVessel,
                Health=v.Appearance == CinematicVesselAppearance.NormalVessel ? 100 : 30,Name=v.Actor.HideName ? "" : v.Actor.Name,
                // No captain, crew, cannon rights, cargo, boarding or ownership credentials.
            }).ToArray(),
            Effects=_cinematicVesselEffects.Select(e=>{
                var x=e.Effect.X;var y=e.Effect.Y;
                if (_cinematicVessels.TryGetValue(e.FollowActor,out var vessel)) { x=vessel.X;y=vessel.Y; }
                return new NavalEffectSnapshot { Id=e.Effect.Id,MapId=_cinematicMap,X=x+e.OffsetX/(double)Options.Instance.Map.TileWidth,Y=y+e.OffsetY/(double)Options.Instance.Map.TileHeight,
                    Heading=e.Effect.Heading,Kind=e.Effect.Kind,AnimationId=e.Effect.AnimationId,PlaySound=e.Effect.PlaySound,AgeMilliseconds=(int)(now-e.Born) };
            }).ToArray(),
            Cannonballs=_cinematicVesselBalls.Select(b=>new CannonballSnapshot { Id=b.Id,MapId=_cinematicMap,X=b.X,Y=b.Y,Heading=ShipMovement.Normalize(Math.Atan2(b.DX,-b.DY)*180/Math.PI),
                AgeMilliseconds=(int)(now-b.Born),Graphic=_cinematicNavigation.Presentation.CannonProjectileGraphic }).ToArray() });
    }

    private void RemoveCinematicVessels()
    {
        if (_cinematicNavigation != null) Player.SendPacket(new CinematicFleetPacket { EventId=Descriptor.Id,MapId=_cinematicMap,Reset=true });
        _cinematicVessels.Clear();_cinematicVesselEffects.Clear();_cinematicVesselBalls.Clear();_cinematicNavigation=null;
    }
}
