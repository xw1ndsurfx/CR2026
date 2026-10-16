using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Interface.Game;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
internal static class ActivitiesMusicTests
{
    [ModuleInitializer] internal static void Run()
    {
        try
        {
            var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.WorldEvents.WorldMusic",true)!;
            var select=type.GetMethod("Select",BindingFlags.Static|BindingFlags.NonPublic)!;
            string Choose(string ambient,string invasion,bool fighting)=>(string)select.Invoke(null,[ambient,invasion,"combat.wav",fighting])!;
            if(Choose("town.wav","",true)!="combat.wav" || Choose("forest.wav","",true)!="combat.wav" || Choose("forest.wav","invasion.wav",true)!="invasion.wav" || Choose("forest.wav","",false)!="forest.wav")throw new Exception("Music priority must be invasion > uninterrupted combat > latest map");
            var grace=type.GetMethod("ContinueCombat",BindingFlags.Static|BindingFlags.NonPublic)!;
            bool Active(long combat,long now)=>(bool)grace.Invoke(null,[combat,now,8])!;
            if(!Active(10000,9999)||!Active(10000,17999)||Active(10000,18000)||Active(0,1000)||!Active(20000,18001))throw new Exception("Combat music bridges nearby fights, then returns to ambient at the configured boundary");
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(640,480)};
            var area=new ScrollControl(canvas);area.SetBounds(0,0,220,180);
            var previousContext=Intersect.Core.ApplicationContext.Context.Value;
            Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,ActivitiesLogProxy>();
            var text=new RichLabel(area);
            var configure=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.QuestTextScroller",true)!.GetMethod("Configure",BindingFlags.Static|BindingFlags.NonPublic)!;
            configure.Invoke(null,[area,text]);
            text.SetBounds(0,0,190,20);
            var template=new Label(canvas) {Font=skin.DefaultFont,FontSize=11};template.Hide();
            text.AddText(string.Join(" ",Enumerable.Repeat("Long quest information must remain readable.",80)),template);
            var layout=typeof(Intersect.Client.Framework.Gwen.Control.Base).GetMethod("RecurseLayout",BindingFlags.NonPublic|BindingFlags.Instance)!;
            for(var pass=0;pass<5;pass++)layout.Invoke(area,[skin]);
            area.UpdateScrollBars();
            if(text.Height<=area.Height)throw new Exception("Long quest text must expand its content height instead of clipping to the viewport");
            var wheel=typeof(ScrollControl).GetMethod("OnMouseWheeled",BindingFlags.NonPublic|BindingFlags.Instance)!;
            if(!area.MouseInputEnabled || !(bool)wheel.Invoke(area,[-120])! || area.VerticalScrollBar.ScrollAmount<=0)throw new Exception("Quest descriptions accept the mouse wheel and move vertically through long content");
            Intersect.Core.ApplicationContext.Context.Value=previousContext;
            System.Console.WriteLine("PASS combat music grace boundaries, map continuity and invasion priority");
        }
        catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}
    }
}

public class ActivitiesLogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod,object?[]? args) => targetMethod?.Name=="get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : targetMethod?.ReturnType.IsValueType==true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
}
