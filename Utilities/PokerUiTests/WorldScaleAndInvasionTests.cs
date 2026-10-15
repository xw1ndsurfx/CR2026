using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Core;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Framework.Core.GameObjects.Events;
using Newtonsoft.Json;

internal static class WorldScaleAndInvasionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try
        {
            var options = new Options(); options.Map.Validate();
            Check(options.Map.WorldScaleSteps.SequenceEqual(new float[] { 1, 2, 4, 6 }), "World Scale includes x6");
            var oldOptions = Options.Instance; var oldDatabase = Globals.Database;
            try
            {
                typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, options);
                Globals.Database = new InterfaceRefreshTests.MemoryPreferences(); Globals.Database.WorldZoom = 4;
                Input.HandleZoomIn(); Check(Globals.Database.WorldZoom == 6, "Zoom advances from x4 to x6");
                Input.HandleZoomIn(false); Check(Globals.Database.WorldZoom == 6, "Wheel stops at x6");
                Input.HandleZoomOut(); Check(Globals.Database.WorldZoom == 4, "Zoom returns from x6 to x4");
                Input.HandleZoomIn(); Input.HandleZoomIn(); Check(Globals.Database.WorldZoom == 1, "Keyboard zoom wraps after x6");
            }
            finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, oldOptions); Globals.Database = oldDatabase; }
            Check(InvasionBalance.AverageLevel(new[] { 5, 5, 50 }) == 20, "Invasions use the mean rather than median");
            Check(InvasionBalance.PopulationHealth(1, 25) == 1 && InvasionBalance.PopulationHealth(1000, 500) == 2,
                "Horde health growth is bounded");
            Check(InvasionBalance.PopulationDamage(4) > 1 && InvasionBalance.PopulationDamage(1000) == 1.25,
                "Additional defenders modestly increase damage");
            var page = JsonConvert.DeserializeObject<EventPage>(JsonConvert.SerializeObject(new EventPage { ShowNameOnWorldMap = true }))!;
            Check(page.ShowNameOnWorldMap && !new EventPage().ShowNameOnWorldMap, "Map name option persists and defaults off");
            System.Console.WriteLine("PASS World Scale x6 and invasion balance");
        }
        catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
