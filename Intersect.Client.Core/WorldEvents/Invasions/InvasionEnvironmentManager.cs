using System.Collections.Concurrent;
using Intersect.Client.Core;
using Intersect.Client.Maps;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.WorldEvents.Invasions;

internal sealed record InvasionEnvironmentState(
    Guid InvasionId,
    long StartedAtUnixMilliseconds,
    string Music,
    int NightBrightness,
    int OverlayAlpha,
    int OverlayRed,
    int OverlayGreen,
    int OverlayBlue,
    string Fog,
    int FogAlpha,
    int FogXSpeed,
    int FogYSpeed,
    bool OutdoorsOnly
);

internal static class InvasionEnvironmentManager
{
    private static readonly ConcurrentDictionary<Guid, InvasionEnvironmentState> Active = [];

    internal static bool IsActive => Active.Count > 0;

    internal static InvasionEnvironmentState? Current =>
        Active.Values
            .OrderByDescending(value => value.StartedAtUnixMilliseconds)
            .ThenByDescending(value => value.InvasionId)
            .FirstOrDefault();

    internal static bool HasMusicOverride =>
        Current is { } current && !string.IsNullOrWhiteSpace(current.Music);

    internal static void ApplyStatus(InvasionStatusPacket packet)
    {
        var before = Current;

        if (packet.Active)
        {
            Active[packet.InvasionId] = new InvasionEnvironmentState(
                packet.InvasionId,
                packet.StartedAtUnixMilliseconds,
                packet.Music ?? string.Empty,
                Math.Clamp(packet.NightBrightness, 0, 100),
                Math.Clamp(packet.OverlayAlpha, 0, 255),
                Math.Clamp(packet.OverlayRed, 0, 255),
                Math.Clamp(packet.OverlayGreen, 0, 255),
                Math.Clamp(packet.OverlayBlue, 0, 255),
                packet.Fog ?? string.Empty,
                Math.Clamp(packet.FogAlpha, 0, 255),
                Math.Clamp(packet.FogXSpeed, -5, 5),
                Math.Clamp(packet.FogYSpeed, -5, 5),
                packet.EnvironmentOutdoorsOnly
            );
        }
        else
        {
            Active.TryRemove(packet.InvasionId, out _);
        }

        var after = Current;
        if (!Equals(before, after))
        {
            Graphics.GridSwitched = true;
            ApplyMusicForCurrentMap();
        }
    }

    internal static bool TryGetEnvironment(
        MapDescriptor map,
        out InvasionEnvironmentState environment
    )
    {
        environment = Current!;
        if (environment == null)
            return false;

        return !environment.OutdoorsOnly || !map.IsIndoors;
    }

    internal static void ApplyMapMusic(MapInstance map)
    {
        var environment = Current;
        if (environment != null && !string.IsNullOrWhiteSpace(environment.Music))
        {
            Audio.PlayMusic(
                environment.Music,
                ClientConfiguration.Instance.MusicFadeTimer,
                ClientConfiguration.Instance.MusicFadeTimer,
                true
            );
            return;
        }

        Audio.PlayMusic(
            map.Music,
            ClientConfiguration.Instance.MusicFadeTimer,
            ClientConfiguration.Instance.MusicFadeTimer,
            true
        );
    }

    internal static bool SuppressScriptedMusicChange()
    {
        if (!HasMusicOverride)
            return false;

        EnsureInvasionMusic();
        return true;
    }

    internal static void EnsureInvasionMusic()
    {
        var environment = Current;
        if (environment == null || string.IsNullOrWhiteSpace(environment.Music))
            return;

        Audio.PlayMusic(
            environment.Music,
            ClientConfiguration.Instance.MusicFadeTimer,
            ClientConfiguration.Instance.MusicFadeTimer,
            true
        );
    }

    private static void ApplyMusicForCurrentMap()
    {
        if (Globals.Me?.MapInstance is MapInstance map)
            ApplyMapMusic(map);
    }
}
