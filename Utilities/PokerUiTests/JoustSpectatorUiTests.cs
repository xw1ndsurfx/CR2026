using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Server;
internal static class JoustSpectatorUiTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run() { try { Verify(); } catch (Exception e) { Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var preferences = Globals.Database; var context = ApplicationContext.Context.Value; var content = Globals.ContentManager;
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish", BindingFlags.Static | BindingFlags.NonPublic)!;
        var apply = typeof(ClientLanguages).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
            Globals.ContentManager = new TestContentManager();
            foreach (var lang in new[] { "en", "fr", "es" })
            {
                restore.Invoke(null, null); Globals.Database = new InterfaceRefreshTests.MemoryPreferences { LanguageCode = lang }; apply.Invoke(null, null);
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin);
                canvas.SetSize(1920, 1080);
                var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.JoustSpectatorWindow", true)!;
                var popup = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(type, Hidden, null, [canvas], null)!;
                var update = type.GetMethod("Apply", Hidden)!;
                var seat = new JoustSeatView(Guid.NewGuid(),"Tribune Gisabelle",7,6,7,7,false,"");
                var view = new JoustSpectatorView { ArenaId=Guid.NewGuid(),Seats=[seat] };
                void Show() => update.Invoke(popup,[new JoustStatePacket { OpenSpectatorWindow=true,SpectatorJson=JsonSerializer.Serialize(view) }]);
                Show();var choices=Field<ComboBox>(popup,"_seats");
                Check(choices.SelectedItem?.UserData is Guid id && id==seat.Id,"The named seat picker binds its real identity.");
                Check(Field<Label>(popup,"_entrance").Text.Contains("7"),"The entrance coordinates are visible.");
                Check(!Field<Button>(popup,"_sit").IsDisabled && Field<Button>(popup,"_stand").IsDisabled,"A free seat can be requested.");
                var item=choices.SelectedItem;Show();Check(ReferenceEquals(item,choices.SelectedItem),"Live scores must not rebuild an open seat selector.");
                view.CurrentSeat=seat.Id;view.Seats=[seat with { Occupied=true,Occupant="William" }];
                view.Match=new JoustBroadcast { FirstName="William",SecondName="Gisabelle",Pass=2,Passes=3,FirstScore=4,SecondScore=7 };
                Show();Check(Field<Button>(popup,"_sit").IsDisabled && !Field<Button>(popup,"_stand").IsDisabled,"Seated players can stand and cannot claim another seat.");
                Check(Field<Label>(popup,"_score").Text.Contains("Gisabelle") && Field<Label>(popup,"_score").Text.Contains("7"),"The spectator sees live scores with real player names.");
                view.CurrentSeat=Guid.Empty;view.Seats=[];Show();Check(Field<Button>(popup,"_sit").IsDisabled,"An unconfigured arena cannot reserve a nonexistent seat.");

            }
            Console.WriteLine("PASS joust spectator UI: named seats, entrances, ownership, stable selectors and live scores in en/fr/es.");
        }
        finally { restore.Invoke(null, null); Globals.Database = preferences; apply.Invoke(null, null); ApplicationContext.Context.Value = context; Globals.ContentManager = content; }
    }
}
