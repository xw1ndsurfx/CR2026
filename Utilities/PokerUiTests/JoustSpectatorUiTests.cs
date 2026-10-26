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
                void Show(bool open = true) => update.Invoke(popup,[new JoustStatePacket { OpenSpectatorWindow=open,SpectatorJson=JsonSerializer.Serialize(view) }]);
                Show();var choices=Field<ComboBox>(popup,"_seats");
                Check(choices.SelectedItem?.UserData is Guid id && id==seat.Id,"The named seat picker binds its real identity.");
                Check(Field<Label>(popup,"_entrance").Text.Contains("7"),"The entrance coordinates are visible.");
                Check(!Field<Button>(popup,"_sit").IsDisabled && Field<Button>(popup,"_stand").IsDisabled,"A free seat can be requested.");
                var item=choices.SelectedItem;Show();Check(ReferenceEquals(item,choices.SelectedItem),"Live scores must not rebuild an open seat selector.");
                view.CurrentSeat=seat.Id;view.Seats=[seat with { Occupied=true,Occupant="William" }];
                view.Match=new JoustBroadcast { FirstName="William",SecondName="Gisabelle",Pass=2,Passes=3,FirstScore=4,SecondScore=7 };
                Show();Check(Field<Button>(popup,"_sit").IsDisabled && !Field<Button>(popup,"_stand").IsDisabled,"Seated players can stand and cannot claim another seat.");
                Check(Field<Label>(popup,"_score").Text.Contains("Gisabelle") && Field<Label>(popup,"_score").Text.Contains("7"),"The spectator sees live scores with real player names.");
                view.Match.MatchId = Guid.NewGuid(); view.Match.Phase = JoustPhase.Charge;
                view.Match.PhaseDurationMs = 1500; view.Match.RemainingMs = 1200;
                view.Presentation = view.Match.Presentation(view.ArenaId, JoustSpectatorCamera.FirstKnight, 0);
                popup.Hide(); Show(false);
                var viewport = Field<Intersect.Client.Framework.Gwen.Control.Base>(popup, "_viewport");
                Check(!popup.IsHidden && !viewport.IsHidden && popup.Height == 704, "The authoritative charge automatically opens the seated spectator's closeup.");
                Check(Field<bool>(viewport, "ReadOnly") && !viewport.MouseInputEnabled && !viewport.KeyboardInputEnabled, "Spectators have no aim or keyboard input in the shared viewport.");
                var aim = Field<double>(viewport, "AimX");
                viewport.GetType().GetMethod("OnMouseMoved", Hidden)!.Invoke(viewport, [0, 0, 20, 20]);
                Check(Field<double>(viewport, "AimX") == aim, "Even a delivered mouse event cannot change a spectator's aim.");
                Check(Field<Button>(popup,"_stand").Y > viewport.Y + viewport.Height && Field<Label>(popup,"_message").Y + Field<Label>(popup,"_message").Height < popup.Height, "Stand-up and camera controls remain reachable below the closeup.");
                popup.Hide(); view.Match.Phase = JoustPhase.Aim; view.Presentation.Phase = JoustPhase.Aim; Show(false);
                Check(popup.IsHidden, "Closing a closeup is respected for the rest of the pass.");
                view.Match.Pass++; view.Presentation.Pass++; Show(false);
                Check(!popup.IsHidden, "A new pass automatically presents the next charge.");
                view.Match.Phase = JoustPhase.Impact; view.Presentation.Phase = JoustPhase.Impact;
                view.Presentation.PlayerStrike = new(JoustHit.Unhorsed, 5, true); Show(false);
                Check(Field<JoustView>(viewport, "State").PlayerStrike.Hit == JoustHit.Unhorsed, "Resolved server impacts reach the same viewport as participant impacts.");
                view.CurrentSeat=Guid.Empty;view.Seats=[];Show();Check(Field<Button>(popup,"_sit").IsDisabled,"An unconfigured arena cannot reserve a nonexistent seat.");
                Check(viewport.IsHidden && popup.Height == 360 && Field<JoustView?>(viewport,"State") == null, "Standing up removes closeup playback even if a stale presentation remains in the payload.");

            }
            Console.WriteLine("PASS joust spectator UI: named seats, entrances, ownership, stable selectors and live scores in en/fr/es.");
        }
        finally { restore.Invoke(null, null); Globals.Database = preferences; apply.Invoke(null, null); ApplicationContext.Context.Value = context; Globals.ContentManager = content; }
    }
}
