using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Network.Packets.Server;
using Base=Intersect.Client.Framework.Gwen.Control.Base;
internal static class GuildManagementTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous=Globals.Me;var previousContext=Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,ActivitiesLogProxy>();
        try
        {
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1280,768)};
            var p=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(p);p.Id=Guid.NewGuid();p.Guild="Crew";p.Rank=0;
            p.GuildMembers=[new(){Id=p.Id,Name="Owner",Rank=0,Online=true},new(){Id=Guid.NewGuid(),Name="Member",Rank=1,Online=true}];Globals.Me=p;
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.UserListWindow",true)!;
            var legacyCalls=0;
            var window=(Base)Activator.CreateInstance(type,[canvas,new Action(()=>legacyCalls++)])!;
            var page=type.GetNestedType("Page",BindingFlags.NonPublic)!;
            type.GetField("_currentPage",hidden)!.SetValue(window,Enum.Parse(page,"Guild"));
            type.GetMethod("Refresh",hidden)!.Invoke(window,[false]);window.Show();
            Base Find(string name)=>Descendants(window).Single(c=>c.Name==name);
            void Click(string name)=>typeof(Button).GetMethod("OnKeyReturn",hidden)!.Invoke(Find(name),[false]);
            Click("GuildManage");
            if(legacyCalls!=0 || !Descendants(window).Any(c=>c.Name=="GuildManagedMember") || Find("GuildKick").IsDisabled || Find("GuildTransfer").IsDisabled)throw new Exception("Manage must open dedicated owner actions inside the user list rather than the legacy member window.");
            Click("GuildMembersBack");
            if(!Descendants(window).Any(c=>c.Name=="GuildManage") || Descendants(window).Any(c=>c.Name=="GuildManagedMember"))throw new Exception("Management must return to the normal member roster.");
            System.Console.WriteLine("PASS guild Manage: dedicated rank/kick/transfer controls, owner permissions and return to members without legacy popup");
        }
        catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}
        finally{Globals.Me=previous;Intersect.Core.ApplicationContext.Context.Value=previousContext;}
    }
    private static IEnumerable<Base> Descendants(Base p)=>p.Children.SelectMany(c=>new[]{c}.Concat(Descendants(c)));
}
