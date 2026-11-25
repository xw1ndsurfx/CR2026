using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.General;
using Intersect.Framework.Core.GuildHeadquarters;
namespace Intersect.Client.Interface.Game;

internal static class HeadquartersPresentation
{
    internal static HeadquartersView View { get; private set; } = new();
    private static Sound? _ambience;
    private static string _ambienceFile = "";
    internal static bool Inside => View.Inside && Globals.Me?.MapId == View.MapId;
    internal static string? Music => Inside && !string.IsNullOrEmpty(View.Assets.ThemeMusic) &&
        Globals.ContentManager?.GetMusic(View.Assets.ThemeMusic) != null ? View.Assets.ThemeMusic : null;
    internal static void Play(string file, int volume)
    {
        if (!HeadquartersAssets.File(file, ".wav") || string.IsNullOrEmpty(file) || volume <= 0 || Globals.ContentManager == null) return;
        var sound = Intersect.Client.Interface.InterfaceAudio.PlayConfigured(file, false);
        if (sound is { Loaded: true }) sound.Volume = volume;
    }
    internal static void Apply(HeadquartersView view, bool open)
    {
        var old = View; View = view;
        var file = view.BankOpened ? view.Assets.BankSound : !old.Inside && view.Inside ? view.Assets.EnterSound : old.Inside && !view.Inside ? old.Assets.LeaveSound :
            old.GuildId == view.GuildId && old.Inside && view.Revision > old.Revision ? view.Assets.UpgradeSound :
            open && !string.IsNullOrEmpty(view.Message) && view.Message != "Headquarters upgraded." && view.Message != "Invitation sent." && view.Message != "Visitor invitations revoked." ? view.Assets.ErrorSound :
            open ? view.Assets.OpenSound : "";
        Play(file, view.Assets.EffectVolume);
        Update();
    }
    internal static void Update()
    {
        var file = Inside && View.Assets.AmbienceVolume > 0 ? View.Assets.AmbienceSound : "";
        if (_ambienceFile != file)
        {
            _ambience?.Stop(); _ambience = null; _ambienceFile = file;
            if (!string.IsNullOrEmpty(file) && Globals.ContentManager != null) _ambience = Intersect.Client.Interface.InterfaceAudio.PlayConfigured(file, true);
        }
        if (_ambience is { Loaded: true }) _ambience.Volume = View.Assets.AmbienceVolume;
    }
    internal static void Reset()
    { _ambience?.Stop(); _ambience = null; _ambienceFile = ""; View = new(); }
}
