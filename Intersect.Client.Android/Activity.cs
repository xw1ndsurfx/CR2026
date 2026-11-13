using System.Reflection;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using Intersect.Client.Core;

namespace Intersect.Client.Android;

// MonoGame's screen receiver needs its Game before OnCreate returns. The plain
// launcher prepares resources first, then opens this activity synchronously.
[Activity(Label = "Corps Royaux", Exported = false,
    LaunchMode = LaunchMode.SingleTask, ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public sealed class GameActivity : AndroidGameActivity
{
    internal static GameActivity? Current { get; private set; }
    internal static bool FrameDiagnosticsEnabled { get; private set; }
    internal Game? RunningGame { get; set; }
    private int _quitRequested;

    internal void FinishForQuit()
    {
        if (Interlocked.Exchange(ref _quitRequested, 1) != 0) return;
        global::Android.Util.Log.Info("CR2026", "Explicit Quit: finishing and removing the game task.");
        RunOnUiThread(() =>
        {
            Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
            FinishAndRemoveTask();
        });
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            Current = this;
            FrameDiagnosticsEnabled = Intent?.GetBooleanExtra("cr2026.frame_diagnostics", false) == true;
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
            System.Environment.CurrentDirectory = FilesDir!.AbsolutePath;
            global::Android.Util.Log.Info("CR2026", "Starting game bootstrapper.");
            Bootstrapper.Start(Assembly.GetExecutingAssembly(), []);
            if (RunningGame == null)
                throw new InvalidOperationException("The game bootstrapper did not create a platform runner.");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("CR2026", exception.ToString());
            Finish();
        }
    }

    protected override void OnDestroy()
    {
        try { RunningGame?.Dispose(); }
        catch (Exception exception) when (_quitRequested != 0)
        { global::Android.Util.Log.Warn("CR2026", "Cleanup during explicit Quit: " + exception); }
        finally
        {
            RunningGame = null;
            if (Current == this) Current = null;
            try { base.OnDestroy(); }
            finally
            {
                // Explicit Quit ends this client runtime after normal cleanup.
                // A subsequent launch starts fresh rather than reusing disposed
                // static graphics/network state. Backgrounding never enters here.
                if (_quitRequested != 0)
                {
                    global::Android.Util.Log.Info("CR2026", "Explicit Quit: game cleanup complete.");
                    global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
                }
            }
        }
    }
}
