using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Game;
using Intersect.Core;
using Intersect.Framework.Core.Housing;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class FarmingUiTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Verify()
    {
        var oldContext = ApplicationContext.Context.Value;
        var oldPreferences = Globals.Database;
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish", BindingFlags.NonPublic | BindingFlags.Static)!;
        var apply = typeof(ClientLanguages).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!;
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.HousingWindow", true)!;
        _ = new TestContentManager();
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        IEnumerable<ControlBase> Desc(ControlBase root) => root.Children.SelectMany(c => new[] { c }.Concat(Desc(c)));
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            {
                var preferences = new InterfaceRefreshTests.MemoryPreferences { LanguageCode = language };
                preferences.SavePreferences();
                Globals.Database = new InterfaceRefreshTests.MemoryPreferences(preferences.Values);
                Globals.Database.LoadPreferences(); restore.Invoke(null, null); apply.Invoke(null, null);
                Check(ClientLanguages.Current == language, "Farm uses the persisted language preference");
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                using var canvas = new Canvas(skin) { Size = new Point(1024, 720) };
                var window = (ControlBase)Activator.CreateInstance(type, [canvas])!;
                var crop = new FarmCrop { Name = "Wheat", SeedItemId = Guid.NewGuid(), HarvestItemId = Guid.NewGuid() };
                var view = new HouseView { IsOwner = true, Enabled = true, FarmConfiguration = new() { Enabled = true, Crops = [crop] }, House = new() { Farm = new() { Learned = true, Plots = [new() { CropId = crop.Id }, new() { CropId = crop.Id, Experienced = true }] } } };
                type.GetMethod("Apply")!.Invoke(window, [view, ""]);
                type.GetField("_tab", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, 5);
                type.GetMethod("RefreshRows", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                window.Show();
                for (var i = 0; i < 4; i++) typeof(ControlBase).GetMethod("RecurseLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(canvas, [skin]);
                var rows = (ScrollControl)type.GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                Check(Desc(window).OfType<Button>().Any(b => b.Text == ClientLanguages.Text("Visit farm")), "Localized farm visit control is reachable");
                Check(Desc(window).OfType<Button>().Count(b => b.Text == ClientLanguages.Text("Pause after harvest")) == 2, "Each hired worker has its own control");
                rows.UpdateScrollBars();
                typeof(ScrollControl).GetMethod("OnMouseWheeled", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rows, [-120]);
                Check(rows.VerticalScrollBar.ScrollAmount > 0, "Long farm management content is scrollable");
                view.IsOwner = false;
                type.GetMethod("Apply")!.Invoke(window, [view, ""]);
                Check(!Desc(rows).OfType<Button>().Any(), "Visitors have no farm management controls");
            }
            System.Console.WriteLine("PASS farming UI: en/fr/es, persisted preferences, worker controls, scroll reachability and visitor restrictions");
        }
        finally
        {
            restore.Invoke(null, null); Globals.Database = oldPreferences; apply.Invoke(null, null); ApplicationContext.Context.Value = oldContext;
        }
    }
}
