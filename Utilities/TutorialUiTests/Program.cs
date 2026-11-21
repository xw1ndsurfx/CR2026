using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Newtonsoft.Json;
using Base=Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase=Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase=Intersect.Client.Framework.Gwen.Skin.Base;
using Console=System.Console;

// Real Gwen controls with synthetic font metrics: no GPU or live-server acceptance.
const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
var assembly=typeof(GameInterface).Assembly;
var windowType=assembly.GetType("Intersect.Client.Interface.Game.TutorialWindow",true)!;
var spotlightType=assembly.GetType("Intersect.Client.Interface.Game.TutorialSpotlight",true)!;
var groups=0;
Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,TutorialLogProxy>();
void Check(bool value,string message){if(!value)throw new Exception(message);}
object? Call(object instance,string method,params object?[] values)=>instance.GetType().GetMethod(method,hidden)!.Invoke(instance,values);
T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,hidden)!.GetValue(instance)!;
IEnumerable<Base> All(Base parent){yield return parent;foreach(var child in parent.Children)foreach(var nested in All(child))yield return nested;}

foreach(var sample in from size in new[]{new Point(320,480),new Point(640,480),new Point(1024,576),new Point(1280,720),new Point(1920,1080)}
                     from language in new[]{"en","fr","es"} select (Size:size,Language:language))
{
 var size=sample.Size;
 typeof(Intersect.Client.Localization.ClientLanguages).GetProperty("Current")!.SetValue(null,sample.Language);
 using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=size};
 var sent=new List<TutorialRequestPacket>();
 var guide=(Base)Activator.CreateInstance(windowType,hidden,null,[canvas,new Action<TutorialRequestPacket>(sent.Add)],null)!;
 var config=TutorialConfiguration.CreateDefault();var step=config.Steps[3];
 TutorialStatePacket State(bool open=true,bool canAdvance=true)=>new(){ConfigurationJson=JsonConvert.SerializeObject(config),Revision="v1",StepId=step.Id,Open=open,CanAdvance=canAdvance};
 void Apply(TutorialStatePacket state)=>Call(guide,"Apply",state);
 Apply(State());typeof(Base).GetMethod("DoLayoutIfNeeded",hidden)!.Invoke(guide,[skin]);
 Check(guide.X>=0&&guide.Y>=0&&guide.Right<=size.X&&guide.Bottom<=size.Y,"Guide outside canvas "+size);
 var buttons=All(guide).OfType<Button>().Where(button=>button.Name.StartsWith("Tutorial")).ToArray();
 foreach(var button in buttons.Where(button=>!button.IsHidden))
  Check(button.X>=0&&button.Y>=0&&button.Right<=button.Parent!.Width&&button.Bottom<=button.Parent.Height,"Button clipped: "+button.Name+" at "+size);
 var pause=buttons.Single(button=>button.Name=="TutorialPauseguide");var next=buttons.Single(button=>button.Name=="TutorialNext");
 Check(next.Text==(sample.Language=="fr"?"Suivant":sample.Language=="es"?"Siguiente":"Next"),"Untranslated guide button");
 Check(pause.Right<=next.X,"Pause and Next overlap at "+size);
 Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Quests);Check(sent.Count==0,"Wrong target advanced the guide");
 Apply(State(canAdvance:false));Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Inventory);Check(sent.Count==0,"Client bypassed server CanAdvance");
 Apply(State());Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Inventory);Check(sent.Count==1&&sent[0].StepId==step.Id&&sent[0].Revision=="v1","Actual HUD action lost its server step/revision");
 Apply(State(open:false));Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Inventory);Check(sent.Count==1,"Paused guide advanced");
 Apply(State());Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Inventory);Check(sent.Count==2,"Retry/resume of same step was permanently suppressed");
 step.HighlightTarget=TutorialHighlightTarget.None;Apply(State());Check(Field<TutorialHighlightTarget>(guide,"_target")==TutorialHighlightTarget.None,"Explicit None overridden by title");
 step.HighlightTarget=TutorialHighlightTarget.Minimap;step.HighlightStyle=TutorialHighlightStyle.Outline;Apply(State());Check(Field<TutorialHighlightStyle>(guide,"_style")==TutorialHighlightStyle.Outline,"Authored minimap style overridden");
 step.Objective=TutorialObjective.InteractEvent;Apply(State(canAdvance:false));
 var hint=Field<Label>(guide,"_interactionHint");
 Check(next.IsHidden&&!hint.IsHidden,"Interaction lesson offers a Next bypass or has no instruction");
 Check(hint.X>=pause.Right&&hint.Right<=hint.Parent!.Width&&hint.Bottom<=hint.Parent.Height,"Interaction hint overlaps buttons or clips at "+size);
 Check(hint.Text==TutorialText.Get("Interact with the highlighted target.",sample.Language),"Untranslated interaction instruction");
 var beforeInteraction=sent.Count;Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Minimap);Check(sent.Count==beforeInteraction,"HUD action replaces interaction");
 step.Objective=TutorialObjective.CombatDodge;Apply(State());Check(guide.IsHidden,"Combat guide stole the screen without explicit opening");
 Call(guide,"OpenGuide");Check(!guide.IsHidden,"Combat guide cannot be opened on demand");
 var sentBeforeCombat=sent.Count;Call(guide,"CompleteOnHudAction",TutorialHighlightTarget.Minimap);Check(sent.Count==sentBeforeCombat,"HUD action credited a combat drill");
 step.Objective=TutorialObjective.Read;Apply(State());
 using(var other=new Panel(canvas,"OtherPlayerWindow")){other.BringToFront();Call(guide,"RefreshVisibility");Check(canvas.Children.Last()==other,"Guide steals foreground every frame");other.Parent=null;}
 var modal=new Panel(canvas,"ModalContents");var modalHost=modal.MakeModal();Call(guide,"RefreshVisibility");Check(guide.IsHidden,"Modal does not suspend tutorial");modalHost.Parent=null;modalHost.Dispose();Call(guide,"RefreshVisibility");Check(!guide.IsHidden,"Guide fails to resume after modal");
 canvas.Size=new Point(640,480);Call(guide,"RefreshVisibility");Check(guide.Right<=640&&guide.Bottom<=480,"Resize leaves guide offscreen");
 var spotlight=Field<Base>(guide,"_spotlight");var worldMarker=Field<Base>(guide,"_worldMarker");guide.Parent=null;guide.Dispose();Check(!canvas.Children.Contains(spotlight)&&!canvas.Children.Contains(worldMarker),"Overlay leaked after guide disposal");
 Console.WriteLine("PASS tutorial UI: bounds, retry, authored targets/style, combat, modality and disposal at "+size+" / "+sample.Language);groups++;
}
{
 using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(640,480)};
 using var parent=new Panel(canvas);parent.SetBounds(20,20,100,50);parent.ClipContents=true;
 using var target=new Panel(parent);target.SetBounds(10,40,50,30);
 using var spotlight=(Base)Activator.CreateInstance(spotlightType,hidden,null,[canvas],null)!;
 Call(spotlight,"Highlight",target,TutorialHighlightStyle.Outline);
 var bounds=(Rectangle)Call(spotlight,"GetTargetBounds")!;
 Check(bounds.X==30&&bounds.Y==60&&bounds.Width==50&&bounds.Height==10,"Halo ignores ancestor clipping");
 Check(!spotlight.MouseInputEnabled&&!spotlight.KeyboardInputEnabled,"Spotlight consumes gameplay input");
 Call(spotlight,"Render",skin);Check(renderer.Rectangles>4,"Spotlight did not render");
 target.SetPosition(10,80);Check(Call(spotlight,"GetTargetBounds")==null,"Off-scroll target still highlighted");
 target.Hide();Check(Call(spotlight,"GetTargetBounds")==null,"Hidden target still highlighted");groups++;
 target.Parent=null;parent.Parent=null;spotlight.Parent=null;
}
{
 using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
 var canvas=new Canvas(skin){Size=new Point(640,480)};
 var guide=(Base)Activator.CreateInstance(windowType,hidden,null,[canvas,new Action<TutorialRequestPacket>(_=>{})],null)!;
 var spotlight=Field<Base>(guide,"_spotlight");var disposed=0;var worldDisposed=0;
 Field<Base>(guide,"_worldMarker").Disposed+=(_,_)=>worldDisposed++;
 spotlight.Disposed+=(_,_)=>disposed++;
 canvas.Dispose();Check(disposed==1&&worldDisposed==1,"Canvas teardown did not dispose overlays exactly once");groups++;
}
groups+=WorldGuidanceChecks.Run();
Console.WriteLine($"PASS: {groups} tutorial UI groups (synthetic metrics; no live gameplay claim).");

sealed class TestSkin:SkinBase
{
 public TestSkin(RendererBase renderer):base(renderer){DefaultFont=new TestFont();DefaultFontSize=12;Colors.Label.Normal=Colors.Label.Disabled=Color.White;}
}
sealed class MetricsRenderer:RendererBase
{
 public int Rectangles;
 public override void DrawFilledRect(Rectangle rectangle)=>Rectangles++;
 public override Point MeasureText(IFont? font,int fontSize,string? text,float scale=1f)=>new((int)Math.Ceiling((text?.Length??0)*fontSize*.6f*scale),(int)Math.Ceiling((fontSize+3)*scale));
}
sealed class TestFont:IFont
{
 public string Name=>"tutorial-test-metrics";
 public ICollection<int> SupportedSizes{get;}=new[]{10,11,12,14};
 public int PickBestMatchFor(int size)=>size;
 public int GetNextFontSize(int startSize,int direction,int limit=0)=>startSize+direction;
}
public class TutorialLogProxy:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_Logger"
  ?Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
  :method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
}
