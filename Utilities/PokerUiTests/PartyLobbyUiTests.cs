using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Network.Packets.PartyLobbies;
internal static class PartyLobbyUiTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch(Exception e) {System.Console.Error.WriteLine(e);Environment.Exit(1);}
    }
    private static void Verify()
    {
        var old=Globals.Database; var oldContext=ApplicationContext.Context.Value;
        var restore=typeof(ClientLanguages).GetMethod("RestoreEnglish",BindingFlags.NonPublic|BindingFlags.Static)!;
        var apply=typeof(ClientLanguages).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!;
        var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PartyLobbyWindow",true)!;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        _=new TestContentManager();
        ApplicationContext.Context.Value=DispatchProxy.Create<IApplicationContext,ActivitiesLogProxy>();
        try
        {
            foreach(var language in new[]{"en","fr","es"})
            foreach(var size in new[]{new Point(640,480),new Point(1920,1080)})
            {
                restore.Invoke(null,null); Globals.Database=new InterfaceRefreshTests.MemoryPreferences {LanguageCode=language}; apply.Invoke(null,null);
                using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=size};
                var window=(Base)Activator.CreateInstance(type,flags,null,[canvas],null)!;
                var id=Guid.NewGuid();var dungeon=Guid.NewGuid();
                var state=new PartyLobbyStatePacket {CurrentLobbyId=id,Dungeons=[new LobbyDungeonChoice {Id=dungeon,Name="Rat Dungeon",MinimumLevel=5}],Lobbies=Enum.GetValues<LobbyActivity>().Select((activity,index)=>new PartyLobbyEntry {Id=index==0?id:Guid.NewGuid(),HostName="Nicolas",Title="Adventure",Activity=activity,DungeonId=dungeon,DungeonName="Rat Dungeon",Players=2,Capacity=5,Started=index==0,Members=["Nicolas","Friend"]}).ToArray()};
                type.GetMethod("Apply",flags)!.Invoke(window,[state]);
                if(window.Width>canvas.Width || window.Height>canvas.Height)throw new Exception("Lobby window must fit a small display");
                var rows=(ScrollControl)type.GetField("_rows",flags)!.GetValue(window)!;
                var create=(Button)type.GetField("_create",flags)!.GetValue(window)!;
                var message=(Label)type.GetField("_message",flags)!.GetValue(window)!;
                if(rows.Height<90 || !create.IsDisabled || message.Bottom>window.Height-25)throw new Exception("Lobby listing, member state and footer must remain usable");
                var matches=type.GetMethod("Matches",BindingFlags.Static|BindingFlags.NonPublic)!;
                bool Match(int filter,string text)=>(bool)matches.Invoke(null,[state.Lobbies[0],filter,text])!;
                if(!Match(-1,"nicolas") || !Match((int)LobbyActivity.Dungeon,"RAT") || Match((int)LobbyActivity.Poker,"") || Match(-1,"missing"))throw new Exception("Lobby search and activity filters must combine correctly");
                var pvp=state.Lobbies.Single(l=>l.Activity==LobbyActivity.PvP);
                if(!(bool)matches.Invoke(null,[pvp,(int)LobbyActivity.PvP,""])! || (bool)matches.Invoke(null,[pvp,(int)LobbyActivity.Dungeon,""])!)throw new Exception("PvP lobbies must use their own filter.");
                state.OpenFromEvent=true;state.PromptCreation=true;state.ActivityFilter=(int)LobbyActivity.PvP;
                type.GetMethod("Apply",flags)!.Invoke(window,[state]);
                var activityControl=(ComboBox)type.GetField("_activity",flags)!.GetValue(window)!;
                if((int)activityControl.SelectedItem!.UserData!=(int)LobbyActivity.PvP)throw new Exception("PvP event must select PvP room creation.");
                state.OpenFromEvent=true;state.PromptCreation=false;state.ActivityFilter=-1;
                type.GetMethod("Apply",flags)!.Invoke(window,[state]);state.OpenFromEvent=false;
                activityControl.SelectByUserData((int)LobbyActivity.Dungeon);
                activityControl.SelectByUserData((int)LobbyActivity.Jousting);
                var train=(Button)type.GetField("_joustTraining",flags)!.GetValue(window)!;
                var dungeonControl=(ComboBox)type.GetField("_dungeon",flags)!.GetValue(window)!;
                if(train.IsHidden || !dungeonControl.IsHidden || train.Bottom>create.Y)
                    throw new Exception("Jousting lobbies expose training without covering creation controls.");
                var joust=state.Lobbies.Single(l=>l.Activity==LobbyActivity.Jousting);
                if(!(bool)matches.Invoke(null,[joust,(int)LobbyActivity.Jousting,""])!)throw new Exception("Jousting has a dedicated lobby filter.");
                activityControl.SelectByUserData((int)LobbyActivity.Dungeon);
                foreach(var activity in Enum.GetValues<LobbyActivity>())
                {
                    var title=(string)type.GetMethod("ActivityName",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[activity])!;
                    if(string.IsNullOrWhiteSpace(title) || title.Contains('?'))throw new Exception("All lobby activities have readable translated names");
                }
                state.CurrentLobbyId=Guid.Empty;state.Message="Choose an available dungeon.";type.GetMethod("Apply",flags)!.Invoke(window,[state]);
                if(create.IsDisabled || message.Text!=ClientLanguages.Text(state.Message))throw new Exception("Dungeon selection and server result localization must enable creation");
                var filter=(ComboBox)type.GetField("_filter",flags)!.GetValue(window)!; filter.SelectByUserData((int)LobbyActivity.Cooking);
                var search=(TextBox)type.GetField("_search",flags)!.GetValue(window)!;search.Text="missing";
                // Repeated refreshes exercise safe control disposal without closing the native window.
                type.GetMethod("RefreshRows",flags)!.Invoke(window,null);
            }
        }
        finally {Globals.Database=old;restore.Invoke(null,null);apply.Invoke(null,null);ApplicationContext.Context.Value=oldContext;}
        System.Console.WriteLine("PASS party lobby UI: all activities including PvP, EN/FR/ES, real window at 640x480/1920x1080, dungeon selection, membership controls, filters and refresh disposal");
    }
}
