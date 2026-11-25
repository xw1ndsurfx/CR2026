using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Intersect.Client.Android;

[Activity(Label = "Corps Royaux", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public sealed class LauncherActivity : Activity
{
    private global::Java.Lang.Thread? _resourceWorker;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Keep engine number parsing consistent with the desktop client. Game
        // translations use the catalog independently of .NET diagnostic text.
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        base.OnCreate(savedInstanceState);
        try
        {
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
            var loading = new FrameLayout(this);
            loading.AddView(new ProgressBar(this), new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Center));
            SetContentView(loading);
            var directory = FilesDir!.AbsolutePath;
            System.Environment.CurrentDirectory = directory;
            var marker = Path.Combine(directory, "android-assets.version");
            var package = PackageManager!.GetPackageInfo(PackageName!, 0)!;
            var version = $"{package.VersionName}:{package.LastUpdateTime}";
            global::Android.Util.Log.Info("CR2026", "Preparing packaged game resources.");
            var assets = Assets!;
            _resourceWorker = new global::Java.Lang.Thread(new global::Java.Lang.Runnable(() =>
            {
                try
                {
                    global::Android.Util.Log.Info("CR2026", "Resource worker started.");
                    if (!File.Exists(marker) || File.ReadAllText(marker) != version)
                    {
                        CopyAssets(assets, "resources", Path.Combine(directory, "resources"));
                        File.WriteAllText(marker, version);
                    }
                    global::Android.Util.Log.Info("CR2026", "Game resources are ready.");
                    RunOnUiThread(StartGame);
                }
                catch (Exception exception)
                {
                    global::Android.Util.Log.Error("CR2026", exception.ToString());
                    RunOnUiThread(Finish);
                }
            }), "CR2026 assets");
            _resourceWorker.Start();
            global::Android.Util.Log.Info("CR2026", "Android resource worker launched.");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("CR2026", exception.ToString());
            Finish();
        }
    }

    private void StartGame()
    {
        if (IsFinishing || IsDestroyed) return;
        var intent = new Intent(this, typeof(GameActivity));
        intent.PutExtra("cr2026.frame_diagnostics", Intent?.GetBooleanExtra("cr2026.frame_diagnostics", false) == true);
        intent.AddFlags(ActivityFlags.ReorderToFront);
        StartActivity(intent);
        Finish();
    }

    private int _copiedFiles;

    private void CopyAssets(AssetManager assets, string source, string destination)
    {
        var entries = assets.List(source) ?? [];
        if (entries.Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = assets.Open(source);
            using var output = File.Create(destination);
            input.CopyTo(output);
            if (++_copiedFiles % 1000 == 0)
                global::Android.Util.Log.Info("CR2026", $"Prepared {_copiedFiles} resource files.");
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
            CopyAssets(assets, $"{source}/{entry}", Path.Combine(destination, entry));
    }
}
