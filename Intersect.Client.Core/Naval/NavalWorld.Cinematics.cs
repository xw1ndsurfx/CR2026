using Intersect.Client.General;
using Intersect.Client.Core;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    private sealed record CinematicFleet(Guid Owner,Guid Player,Guid Map,long Received,
        Dictionary<Guid,View> Ships,Dictionary<Guid,EffectView> Effects,Dictionary<Guid,BallView> Balls,NavalPresentation Style);
    private static CinematicFleet? _cinematicFleet;
    private static CinematicFleet? CurrentCinematicFleet => _cinematicFleet is { } fleet && Globals.GameState == GameStates.InGame &&
        Globals.Me?.Id == fleet.Player && Globals.Me.MapId == fleet.Map && Environment.TickCount64-fleet.Received < 2000 ? fleet : null;

    internal static void ClearCinematicFleet(Guid owner=default)
    { if (owner == Guid.Empty || _cinematicFleet?.Owner == owner) _cinematicFleet=null; }

    internal static void ApplyCinematicFleet(CinematicFleetPacket packet)
    {
        if (packet.Reset) { ClearCinematicFleet(packet.EventId);return; }
        if (Globals.Me is not { } me || packet.EventId == Guid.Empty || packet.MapId != me.MapId ||
            packet.Ships == null || packet.Ships.Length > CinematicVessels.MaximumVessels || packet.Effects == null || packet.Effects.Length > 128 ||
            packet.Cannonballs == null || packet.Cannonballs.Length > 128 || packet.Presentation?.IsValid() != true) return;
        var now=Environment.TickCount64;
        var previous=CurrentCinematicFleet is { } old && old.Owner == packet.EventId ? old : null;
        var ships=new Dictionary<Guid,View>();var effects=new Dictionary<Guid,EffectView>();var balls=new Dictionary<Guid,BallView>();
        foreach (var ship in packet.Ships)
        {
            if (ship == null || ship.Id == Guid.Empty || ship.MapId != packet.MapId ||
                !new[] {ship.X,ship.Y,ship.Heading,ship.Width,ship.Length}.All(double.IsFinite) ||
                ship.X < 0 || ship.Y < 0 || ship.X >= Options.Instance.Map.MapWidth || ship.Y >= Options.Instance.Map.MapHeight ||
                ship.Width is < .5 or > 16 || ship.Length is < .5 or > 16 || ship.Frames is < 1 or > 16 ||
                ship.FrameMilliseconds is < 50 or > 2000 || !NavalPresentation.SafeAsset(ship.Graphics) || ships.ContainsKey(ship.Id)) return;
            var before=previous?.Ships.GetValueOrDefault(ship.Id);
            var animation=before?.Animation ?? new ShipAnimationClock();animation.Observe(ship.Graphics,now);
            ships.Add(ship.Id,new(before?.Current ?? ship,ship,now,animation));
        }
        foreach (var effect in packet.Effects)
        {
            if (effect == null || effect.Id == Guid.Empty || effect.MapId != packet.MapId || !Enum.IsDefined(effect.Kind) ||
                !double.IsFinite(effect.X) || !double.IsFinite(effect.Y) || !double.IsFinite(effect.Heading) ||
                !float.IsFinite(effect.Elevation) || effect.Elevation is < 0 or > 8) continue;
            var before=previous?.Effects.GetValueOrDefault(effect.Id);
            effects[effect.Id]=new(effect,before?.Born ?? now-Math.Clamp(effect.AgeMilliseconds,0,20000));
            if (before == null && effect.PlaySound && effect.AgeMilliseconds <= 350) PlayCinematicNavalSound(effect,packet.Presentation);
        }
        foreach (var ball in packet.Cannonballs)
        {
            if (ball == null || ball.Id == Guid.Empty || ball.MapId != packet.MapId || !double.IsFinite(ball.X) || !double.IsFinite(ball.Y) ||
                !double.IsFinite(ball.Heading) || !double.IsFinite(ball.Height) || !NavalPresentation.SafeAsset(ball.Graphic)) continue;
            balls[ball.Id]=new(previous?.Balls.GetValueOrDefault(ball.Id)?.Current ?? ball,ball,now);
        }
        _cinematicFleet=new(packet.EventId,me.Id,packet.MapId,now,ships,effects,balls,packet.Presentation);
    }

    private static void PlayCinematicNavalSound(NavalEffectSnapshot effect,NavalPresentation style)
    {
        var sound=NavalEffectSound(effect,style);
        if (NavalPresentation.SafeAsset(sound) && sound.Length > 0)
            Audio.AddMapSound(sound.ToLowerInvariant(),(int)Math.Floor(effect.X),(int)Math.Floor(effect.Y),effect.MapId,false,0,16);
    }

    private static string NavalEffectSound(NavalEffectSnapshot effect,NavalPresentation style)
    {
        var sound=effect.Kind switch { NavalEffectKind.Fire=>style.CannonFireSound,NavalEffectKind.HullImpact=>style.HullImpactSound,
            NavalEffectKind.WaterImpact=>style.WaterImpactSound,NavalEffectKind.ObstacleImpact=>style.ObstacleImpactSound,
            NavalEffectKind.Sinking=>style.SinkingSound,NavalEffectKind.Anchor=>style.AnchorSound,NavalEffectKind.Launch=>style.LaunchSound,
            NavalEffectKind.Disembark=>style.DisembarkSound,NavalEffectKind.LevelUp=>style.LevelUpSound,_=>"" };
        return AnimationDescriptor.Get(effect.AnimationId)?.Sound is {Length:>0} native ? native : sound;
    }

    internal static bool TryCinematicVesselCenter(Guid owner,Guid actor,out float x,out float y)
    {
        x=y=0;
        return CurrentCinematicFleet is { } fleet && fleet.Owner == owner && fleet.Ships.TryGetValue(actor,out var view) && Position(view,out x,out y,out _);
    }

    private static IEnumerable<View> RenderedShipViews() => (Fresh ? _ships.Values : Enumerable.Empty<View>())
        .Concat(CurrentCinematicFleet?.Ships.Values ?? Enumerable.Empty<View>());

    private static IEnumerable<(EffectView Effect,NavalPresentation Style)> RenderedNavalEffects() =>
        (Fresh ? _effects.Values : Enumerable.Empty<EffectView>()).Select(e=>(e,Presentation)).Concat(CurrentCinematicFleet is { } fleet
            ? fleet.Effects.Values.Select(e=>(e,fleet.Style)) : Enumerable.Empty<(EffectView,NavalPresentation)>());

    private static IEnumerable<(BallView Ball,NavalPresentation Style)> RenderedCannonballs() =>
        (Fresh ? _balls.Values : Enumerable.Empty<BallView>()).Select(b=>(b,Presentation)).Concat(CurrentCinematicFleet is { } fleet
            ? fleet.Balls.Values.Select(b=>(b,fleet.Style)) : Enumerable.Empty<(BallView,NavalPresentation)>());
}
