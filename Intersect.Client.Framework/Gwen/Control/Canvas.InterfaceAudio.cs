using Intersect.Client.Framework.File_Management;

namespace Intersect.Client.Framework.Gwen.Control;

public partial class Canvas
{
    // Disabled for editor/test canvases unless the host deliberately enables it.
    public bool InterfaceSoundsEnabled { get; set; }
    public Func<Base, UiWindowSounds?>? InterfaceWindowSoundResolver { get; set; }
    private readonly UiAudioQueue _interfaceAudio = new();
    private readonly UiWindowAudioTracker _interfaceWindows = new();

    public void RequestInterfaceSound(UiCue cue)
    {
        if (InterfaceSoundsEnabled && !IsHidden) _interfaceAudio.Request(cue);
    }

    public void DedicatedInterfaceSoundPlayed() => _interfaceAudio.DedicatedSoundPlayed();

    internal void ObserveInterfaceWindow(Base control)
    {
        if (!InterfaceSoundsEnabled || !control.AllowsDefaultInterfaceSounds) return;
        if ((control.InterfaceWindowSounds ?? InterfaceWindowSoundResolver?.Invoke(control)) is { } sounds)
            _interfaceWindows.Observe(control, sounds);
    }

    private void CompleteInterfaceAudioFrame()
    {
        if (!InterfaceSoundsEnabled) { _interfaceAudio.Clear(); _interfaceWindows.Clear(); return; }
        _interfaceWindows.Complete(_interfaceAudio);
        if (_interfaceAudio.Take() is not { } cue || mUISounds.Count >= 8) return;
        TryPlayUiSound(UiAudioFiles.File(cue), UiAudioFiles.Volume(cue), dedicated: false);
    }

    internal bool TryPlayUiSound(string name, int volume = 100, bool dedicated = true)
    {
        if (string.IsNullOrWhiteSpace(name) || IsDisposed || mUISounds.Count >= 16) return false;
        Intersect.Client.Framework.Audio.GameAudioInstance? instance = null;
        try
        {
            var sound = GameContentManager.Current?.GetSound(GameContentManager.RemoveExtension(name).ToLowerInvariant());
            if (sound is null) return false;
            // Suppress fallback even while effects are muted, without allocating an audio voice.
            if (sound.TypeVolume <= 0)
            {
                if (dedicated) DedicatedInterfaceSoundPlayed();
                return true;
            }
            instance = sound.CreateInstance();
            if (instance is null) return false;
            instance.Volume = volume;
            instance.Play();
            mUISounds.Add(instance);
            if (dedicated) DedicatedInterfaceSoundPlayed();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            instance?.Dispose();
            return false;
        }
    }
}
