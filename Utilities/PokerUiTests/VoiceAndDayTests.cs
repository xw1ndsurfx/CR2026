using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using MessagePack;
using Newtonsoft.Json;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class VoiceAndDayTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }

    private static void RunCore()
    {
        Check(VoiceSoundFiles.Key("Greeting.WAV") == "voicesound/greeting" && VoiceSoundFiles.Key("../greeting.wav") == null &&
            VoiceSoundFiles.Key("sounds/greeting.wav") == null, "Voice playback stays inside the separate voice namespace");
        var root = Path.Combine(Path.GetTempPath(), "CR2026-voice-test-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, VoiceSoundFiles.Folder); Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "z.wav"), []); File.WriteAllBytes(Path.Combine(folder, "a.wav"), []);
            Check(VoiceSoundFiles.List(root).SequenceEqual(new[] { "a.wav", "z.wav" }), "Voice picker lists and sorts resources/voicesound files");
        }
        finally
        {
            File.Delete(Path.Combine(folder, "a.wav")); File.Delete(Path.Combine(folder, "z.wav"));
            Directory.Delete(folder); Directory.Delete(root);
        }
        var sound = new PlaySoundPacket("Greeting.WAV") { VoiceSound = true };
        Check(MessagePackSerializer.Deserialize<PlaySoundPacket>(MessagePackSerializer.Serialize(sound)).VoiceSound,
            "Voice flag survives the server packet");
        var command = new PlaySoundCommand { File = "Greeting.WAV", VoiceSound = true };
        Check(JsonConvert.DeserializeObject<PlaySoundCommand>(JsonConvert.SerializeObject(command))!.VoiceSound &&
            !JsonConvert.DeserializeObject<PlaySoundCommand>("{\"File\":\"old.wav\"}")!.VoiceSound,
            "Voice event command persists while old sounds remain sound effects");
        var assembly = typeof(GameInterface).Assembly;
        var trackerType = assembly.GetType("Intersect.Client.Interface.Game.DayPhaseNotificationTracker", true)!;
        var tracker = Activator.CreateInstance(trackerType)!;
        var observe = trackerType.GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        bool Observe(DayPhase phase) => (bool)observe.Invoke(tracker, [phase])!;
        Check(!Observe(DayPhase.Night) && !Observe(DayPhase.Night) && Observe(DayPhase.Sunrise) &&
            !Observe(DayPhase.Sunrise) && Observe(DayPhase.Day) && Observe(DayPhase.Sunset) && Observe(DayPhase.Night),
            "Day notifications trigger once per transition without login spam");
        var schedule = new DayPhaseSchedule { IntervalMinutes = 720, IntervalPhases = [DayPhase.Sunset, DayPhase.Day] };
        Intersect.Client.General.Time.LoadTime(new DateTime(2026, 1, 1, 8, 0, 0), Color.White, 1, JsonConvert.SerializeObject(schedule));
        Check(Intersect.Client.General.Time.CurrentPhase == DayPhase.Sunset, "Client notifications follow the configured server phase intervals");
        _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(640, 480) };
        var hudType = assembly.GetType("Intersect.Client.Interface.Game.DayPhaseHud", true)!;
        var hud = (Label)Activator.CreateInstance(hudType, [canvas])!;
        foreach (var phase in Enum.GetValues<DayPhase>())
        {
            hudType.GetMethod("UpdatePhase")!.Invoke(hud, [phase, true]);
            Check(!hud.IsHidden && hud.Text == phase.ToString(), "Persistent phase HUD displays the current server phase");
        }
        Check(hud.X == 8 && hud.Y == 4 && hud.Height <= 20 && hud.FontSize <= 10 && !hud.MouseInputEnabled,
            "Current phase is a small non-interactive label at the top-left");
        hudType.GetMethod("UpdatePhase")!.Invoke(hud, [DayPhase.Day, false]);
        Check(hud.IsHidden, "Phase HUD stays hidden until server time is available");
        var type = assembly.GetType("Intersect.Client.Interface.Game.ItemChangeNotificationWindow", true)!;
        var panel = (ControlBase)Activator.CreateInstance(type, [canvas])!;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var phase in Enum.GetValues<DayPhase>()) type.GetMethod("EnqueuePhase")!.Invoke(panel, [phase]);
        foreach (var phase in Enum.GetValues<DayPhase>())
        {
            var name = (Label)type.GetField("_name", flags)!.GetValue(panel)!;
            var message = (Label)type.GetField("_quantity", flags)!.GetValue(panel)!;
            Check(name.Text == phase.ToString() && message.Text == "has arrived" && !panel.IsHidden,
                "Each phase shares the notification queue and English arrival text");
            type.GetField("_hideAt", flags)!.SetValue(panel, 0L); type.GetMethod("Update")!.Invoke(panel, null);
        }
        Check(panel.IsHidden, "Day phase cards dismiss when the shared queue is empty");
        System.Console.WriteLine("PASS voice/day: separate files, muted preference, command/packet persistence, phase transitions and queued English cards");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
