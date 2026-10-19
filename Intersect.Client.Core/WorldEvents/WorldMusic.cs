using Intersect.Client.Core;
using Intersect.Client.General;
using Intersect.Client.WorldEvents.Invasions;
using Intersect.Configuration;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Utilities;
using Intersect.Framework.Core;
using Intersect.Client.Maps;
namespace Intersect.Client.WorldEvents;

internal static class WorldMusic
{
    internal static WorldActivitiesConfiguration Configuration { get; set; } = new();
    private static string? _selected;
    private static string _combatTrack = "", _playlist = "";
    internal static bool ContinueCombat(long combatUntil, long now, int grace) => combatUntil > 0 && now < combatUntil + Math.Clamp(grace, 0, 120) * 1000L;
    internal static bool HasCombatOverride => Globals.Me is { } me && Configuration.CombatTracksFor(me.MapId).Length > 0 && ContinueCombat(me.CombatTimer, Timing.Global.Milliseconds, Configuration.CombatMusicGraceSeconds);
    internal static string Select(string ambient, string invasion, string combat, bool fighting) => !string.IsNullOrWhiteSpace(invasion) ? invasion : fighting && !string.IsNullOrWhiteSpace(combat) ? combat : ambient;
    internal static void MapChanged()
    {
        _selected = null;
        Update();
    }
    internal static void Reset()
    {
        BoosterState.Reset();
        _selected = null;
        _combatTrack = "";
        _playlist = "";
    }
    internal static void Update()
    {
        if (Globals.GameState != GameStates.InGame || Globals.Me?.MapInstance is not MapInstance map)
            return;
        var fighting = HasCombatOverride;
        var tracks = Configuration.CombatTracksFor(Globals.Me.MapId);
        var playlist = string.Join("|", tracks);
        if (!fighting)
        {
            _combatTrack = "";
            _playlist = "";
        }
        else if (_combatTrack.Length == 0 || _playlist != playlist)
        {
            _playlist = playlist;
            _combatTrack = tracks[Random.Shared.Next(tracks.Length)];
        }
        var selected = Select(map.Music, InvasionEnvironmentManager.Current?.Music ?? "", _combatTrack, fighting);
        if (string.Equals(selected, _selected, StringComparison.OrdinalIgnoreCase))
            return;
        _selected = selected;
        Audio.PlayMusic(selected, ClientConfiguration.Instance.MusicFadeTimer, ClientConfiguration.Instance.MusicFadeTimer, true);
    }
}
