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
internal static class JoustPvpUiTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run() { try { Verify(); } catch (Exception e) { Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var previousMe = Globals.Me; var preferences = Globals.Database; var context = ApplicationContext.Context.Value; var content = Globals.ContentManager;
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
                var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.JoustDuelWindow", true)!;
                var popup = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(type, Hidden, null, [canvas], null)!;
                var update = type.GetMethod("Apply", Hidden)!;
                var player = new JoustCandidate(Guid.NewGuid(), "Gisabelle");
                var view = new JoustDuelView { ArenaId = Guid.NewGuid(), ArenaName = "Herbonia", Candidates = [player], StartX = 4, StartY = 14, OpponentX = 26, OpponentY = 16 };
                void Show() => update.Invoke(popup, [new JoustStatePacket { OpenDuelWindow = true, DuelJson = JsonSerializer.Serialize(view) }]);
                Show(); var choices = Field<ComboBox>(popup, "_players");
                Check(choices.SelectedItem?.UserData is Guid id && id == player.Id, "The named opponent picker binds the real player identity.");
                Check(!popup.IsHidden && Field<Button>(popup, "_accept").IsHidden, "Browsing cannot accept an invitation that was not received.");
                view.InvitationId = Guid.NewGuid(); view.Challenger = "William"; view.ExpiresInMs = 30000; Show();
                Check(!Field<Button>(popup, "_accept").IsHidden && !Field<Button>(popup, "_accept").IsDisabled, "A received live invitation offers acceptance.");
                Check(Field<Label>(popup, "_invitation").Text.Contains("William"), "Player names remain unchanged in every locale.");
                view.ExpiresInMs = 0; Show(); Check(Field<Button>(popup, "_accept").IsDisabled, "An expired invitation cannot be accepted in the UI.");
                view.Started = true; Show(); Check(popup.IsHidden, "A started duel closes its invitation popup.");
                var mainType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.JoustWindow", true)!;
                var main = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(mainType, Hidden, null, [canvas], null)!;
                mainType.GetMethod("Apply", Hidden)!.Invoke(main, [new JoustStatePacket { ViewJson = JsonSerializer.Serialize(new JoustView { IsPve = false, OpponentName = "Gisabelle", Phase = JoustPhase.Aim, Pass = 1, Passes = 3, PlayerScore = 2, OpponentScore = 3 }) }]);
                Check(Field<Label>(main, "_score").Text.Contains("Gisabelle"), "PvP scoreboard identifies the real opponent instead of the AI label.");
                mainType.GetField("_defending", Hidden)!.SetValue(main, true); Field<Button>(main, "_defense").ToggleState = true;
                mainType.GetMethod("Apply", Hidden)!.Invoke(main, [new JoustStatePacket { ViewJson = JsonSerializer.Serialize(new JoustView { MatchId = Guid.NewGuid(), IsPve = false, Phase = JoustPhase.Aim }) }]);
                Check(!Field<bool>(main, "_defending") && !Field<Button>(main, "_defense").ToggleState, "A new duel resets both the defense input and its visible toggle.");
                mainType.GetMethod("Apply", Hidden)!.Invoke(main, [new JoustStatePacket { ViewJson = JsonSerializer.Serialize(new JoustView { IsPve = false, Phase = JoustPhase.Abandoned, Winner = 0 }) }]);
                Check(Field<Label>(main, "_status").Text == ClientLanguages.Text("Joust interrupted"), "An interrupted duel does not announce an earned victory.");
                var me = (Intersect.Client.Entities.Player)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player)); GC.SuppressFinalize(me); me.MapId=Guid.NewGuid(); me.X=1;me.Y=2;Globals.Me=me;
                var arena=new JoustArena { Enabled=true,MapId=me.MapId,MountId=JoustMountPreset.WilliamId,AiOpponent=JoustAiOpponent.Night };
                mainType.GetMethod("Apply",Hidden)!.Invoke(main,[new JoustStatePacket { ConfigurationJson=new JoustConfiguration { Arenas=[arena] }.ToJson() }]);
                var opponents=Field<ComboBox>(main,"_opponent");Check(opponents.SelectedItem?.UserData is int knight && knight==1,"An arena's default Night opponent is selected by name.");
                Check(Field<Button>(main,"_start").IsDisabled,"The UI explains the start tile instead of allowing an impossible start from afar.");
                Check(Field<Label>(main,"_setupHint").Text.Contains("4") && Field<Label>(main,"_setupHint").Text.Contains("14"),"The actual gold start is visible in every language.");
                me.X=(byte)arena.StartX;me.Y=(byte)arena.StartY;mainType.GetMethod("RefreshSetupHint",Hidden)!.Invoke(main,null);
                Check(!Field<Button>(main,"_start").IsDisabled,"Reaching the gold start enables the AI start button.");
                opponents.SelectByUserData(0);Check(opponents.SelectedItem?.UserData is int william && william==0,"Both supported AI opponents can be selected.");
                var game=(GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
                typeof(GameInterface).GetField("<GameCanvas>k__BackingField",Hidden)!.SetValue(game,canvas);
                var route=typeof(GameInterface).GetMethod("ApplyJoustState",Hidden)!;
                route.Invoke(game,[new JoustStatePacket { Open=true,ConfigurationJson=new JoustConfiguration { Arenas=[arena] }.ToJson() }]);
                var automatic=Field<Intersect.Client.Framework.Gwen.Control.Base>(game,"_joustWindow");
                Check(!automatic.IsHidden,"A server proximity packet opens preparation without constructing or opening Character.");
                route.Invoke(game,[new JoustStatePacket { OpenSpectatorWindow=true,SpectatorJson=JsonSerializer.Serialize(new JoustSpectatorView()) }]);
                Check(automatic.IsHidden && !Field<Intersect.Client.Framework.Gwen.Control.Base>(game,"_joustSpectatorWindow").IsHidden,"A grandstand invitation replaces preparation instead of stacking the windows.");
                route.Invoke(game,[new JoustStatePacket { OpenDuelWindow=true,DuelJson=JsonSerializer.Serialize(new JoustDuelView()) }]);
                Check(Field<Intersect.Client.Framework.Gwen.Control.Base>(game,"_joustSpectatorWindow").IsHidden && !Field<Intersect.Client.Framework.Gwen.Control.Base>(game,"_joustDuelWindow").IsHidden,"The blue start opens the duel window on its own.");


            }
            Console.WriteLine("PASS joust PvP UI: named players, invitation identity/expiry, handoff, personal scores and interruption in en/fr/es.");
        }
        finally { Globals.Me = previousMe; restore.Invoke(null, null); Globals.Database = preferences; apply.Invoke(null, null); ApplicationContext.Context.Value = context; Globals.ContentManager = content; }
    }
}
