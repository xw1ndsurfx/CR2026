using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.WorldEvents.Invasions;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Cinematics;

// A recipient-only override of the existing environment drawing inputs. Never
// changes a MapDescriptor, invasion state, weather clock or server naval physics.
internal static class CinematicEnvironment
{
    private sealed record State(Guid Owner,Guid Player,Guid Map,long Started,CinematicEnvironmentSettings Settings,InvasionEnvironmentState Environment);
    private static readonly object Gate = new();
    private static State? _state;

    internal static void Receive(CinematicCameraPacket packet)
    {
        if (packet.RestoreEnvironment) { Release(packet.EventId); return; }
        if (Globals.Me is not { } me || packet.EventId == Guid.Empty || packet.MapId != me.MapId ||
            packet.EnvironmentSettings is not { } settings || !settings.Valid()) return;
        var now=Environment.TickCount64;
        var environment=new InvasionEnvironmentState(packet.EventId,now,"",settings.NightBrightness,settings.OverlayAlpha,
            settings.OverlayRed,settings.OverlayGreen,settings.OverlayBlue,settings.FogFile,settings.FogOpacity,settings.FogXSpeed,settings.FogYSpeed,false);
        lock (Gate) _state=new(packet.EventId,me.Id,packet.MapId,now,settings,environment);
        Graphics.GridSwitched=true;
    }

    private static State? Current(Guid map)
    {
        lock (Gate)
        {
            if (_state is not { } state) return null;
            if (Globals.GameState != GameStates.InGame || Globals.Me?.Id != state.Player || Globals.Me.MapId != state.Map ||
                Environment.TickCount64-state.Started > 30*60*1000)
            { _state=null; Graphics.GridSwitched=true; return null; }
            return map == state.Map ? state : null;
        }
    }

    internal static bool TryGetEnvironment(MapDescriptor map,out InvasionEnvironmentState environment)
    {
        if (Current(map.Id) is not { } state) { environment=null!; return false; }
        environment=state.Environment;
        return true;
    }

    internal static bool? WeatherActive(MapInstance map) => Current(map.Id)?.Settings.WeatherMode switch
    {
        CinematicWeatherMode.ClearWeather => false,
        CinematicWeatherMode.ForceConfiguredWeather => !map.IsIndoors && map.HasConfiguredRain,
        _ => null,
    };

    internal static void Release(Guid owner)
    { lock (Gate) if (_state?.Owner == owner) { _state=null; Graphics.GridSwitched=true; } }
    internal static void Clear()
    { lock (Gate) { _state=null; Graphics.GridSwitched=true; } }

    // Perspective covers the ordinary 2D world pass. Reuse its native atmosphere
    // inputs above that image, before any presentation bars or native UI.
    internal static void DrawPerspectiveAtmosphere()
    {
        if (Globals.Me?.MapInstance is not MapInstance map || Graphics.Renderer == null) return;
        Graphics.DrawCinematicAmbient(map);
        map.DrawFog();
        map.DrawOverlayGraphic();
        Graphics.DrawOverlay();
    }
}
