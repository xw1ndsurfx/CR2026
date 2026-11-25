using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Audio;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Framework.Input;
using Intersect.Client.Interface;
using Newtonsoft.Json.Linq;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Checkbox = Intersect.Client.Framework.Gwen.Control.Checkbox;
using ComboBox = Intersect.Client.Framework.Gwen.Control.ComboBox;
using TextBox = Intersect.Client.Framework.Gwen.Control.TextBox;
using TabControl = Intersect.Client.Framework.Gwen.Control.TabControl;
using Point = Intersect.Point;
using Console = System.Console;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;
    private static readonly List<ProbeInstance> Played = [];
    private static bool HoldVoices;
    private static void Check(bool result, string name) { if (!result) throw new Exception(name); _checks++; Console.WriteLine("PASS " + name); }
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Hidden)!.Invoke(target, args);
    private static void Flush(Canvas canvas) { Call(canvas, "RemoveAndDisposeDeadSounds"); Call(canvas, "CompleteInterfaceAudioFrame"); }
    private static void Pause() => Thread.Sleep(180);
    private static void Click(Base control, bool user = true)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = control;
        typeof(Base).GetMethod("InputMouseButtonState", Hidden)!.Invoke(control, [MouseButton.Left, new Point(1, 1), true, user]);
        typeof(Base).GetMethod("InputMouseButtonState", Hidden)!.Invoke(control, [MouseButton.Left, new Point(1, 1), false, user]);
    }
    [STAThread] private static int Main()
    {
        try { Run(); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager();
        var sounds = (Dictionary<string, IAsset>)typeof(GameContentManager).GetField("mSoundDict", Hidden)!.GetValue(content)!;
        foreach (var cue in Enum.GetValues<UiCue>())
            sounds[GameContentManager.RemoveExtension(UiAudioFiles.File(cue))] = new ProbeSource(UiAudioFiles.File(cue));
        QueueTests();
        using var renderer = new DrawingRenderer(1280, 720);
        using var skin = new EquipmentSkin(renderer);
        using (var canvas = new Canvas(skin) { Size = new Point(1280,720) })
        {
            var button = new Button(canvas, "TestButton"); button.Clicked += (_, _) => { };
            Click(button); Flush(canvas); Check(Played.Count == 0, "unconfigured canvas remains silent");
            InterfaceAudio.Configure(canvas);
            Click(button); Flush(canvas); Check(Last() == UiCue.Click, "missing legacy button files get one click fallback");
            var count=Played.Count; Flush(canvas); Check(Played.Count==count,"frame does not replay click");
            Pause(); Click(button, false); Flush(canvas); Check(Played.Count==count,"automated click stays silent");
            Pause(); button.IsDisabled=true; Click(button); Flush(canvas); Check(Played.Count==count,"disabled button stays silent"); button.IsDisabled=false;
            Pause(); Call(button,"OnKeyReturn",false); Flush(canvas); Check(Played.Count==count+1 && Last()==UiCue.Click,"keyboard activation has feedback");
            Pause(); Call(button,"OnMouseEntered"); Flush(canvas); Check(Last()==UiCue.Hover,"hover fallback");
            count=Played.Count; Call(button,"OnMouseEntered"); Flush(canvas); Check(Played.Count==count,"hover rate limit");
            Pause(); button.LoadJson(JObject.Parse("{\"DefaultInterfaceSounds\":false}")); Click(button); Flush(canvas); Check(Played.Count==count,"JSON silence opt out");
            Check(button.GetJson()![nameof(Base.DefaultInterfaceSounds)]!.Value<bool>()==false,"silence setting survives JSON roundtrip"); button.DefaultInterfaceSounds=true;
            foreach(var state in Enum.GetValues<ButtonSoundState>()) button.SetSound(state,null);
            Pause(); Click(button); Flush(canvas); Check(Played.Count==count,"explicitly cleared button sounds stay silent");
            var image=new ImagePanel(canvas,"InventoryItem"); image.Clicked+=(_,_)=>{};
            Pause(); Click(image); Flush(canvas); Check(Last()==UiCue.Select,"interactive image selection");
            count=Played.Count; var decoration=new ImagePanel(canvas,"Decor"); Pause(); Click(decoration); Call(decoration,"OnMouseEntered"); Flush(canvas); Check(Played.Count==count,"decorative images remain silent");
            var silentParent=new Base(canvas){DefaultInterfaceSounds=false};var silentChild=new Button(silentParent);silentChild.Clicked+=(_,_)=>{};
            Pause();Click(silentChild);Flush(canvas);Check(Played.Count==count,"parent opt out silences descendants");
            var checkbox=new Checkbox(canvas,"OptionsCheckbox"); Pause(); Click(checkbox); Flush(canvas); Check(Last()==UiCue.ToggleOn,"checkbox checked cue");
            Pause(); Click(checkbox); Flush(canvas); Check(Last()==UiCue.ToggleOff,"checkbox unchecked cue");
            var tab=new TabButton(canvas,"TestTab") { Page=new Base(canvas) }; Pause(); Click(tab); Flush(canvas); Check(Last()==UiCue.Tab,"tab selection cue");
            var tabs=new TabControl(canvas,"KeyboardTabs");var firstTab=tabs.AddPage("First");var secondTab=tabs.AddPage("Second");
            Pause();count=Played.Count;Call(firstTab,"OnKeyRight",true);Flush(canvas);Check(secondTab.IsTabActive && Played.Count==count+1 && Last()==UiCue.Tab,"next tab keyboard feedback");
            Pause();count=Played.Count;Call(secondTab,"OnKeyLeft",true);Flush(canvas);Check(firstTab.IsTabActive && Played.Count==count+1 && Last()==UiCue.Tab,"previous tab keyboard feedback");
            Pause();count=Played.Count;Call(firstTab,"OnKeyLeft",true);Flush(canvas);Check(Played.Count==count,"tab navigation boundary stays silent");
            var text=new TextBox(canvas,"TestText"); text.SubmitPressed+=(_,_)=>{}; Pause(); Call(text,"OnReturn"); Flush(canvas); Check(Last()==UiCue.Submit,"text submit without keystroke noise");
            var slider=new Slider(canvas);slider.SetRange(0,100);slider.Value=20;count=Played.Count;Pause();Flush(canvas);Check(Played.Count==count,"setting slider value programmatically stays silent");
            Pause();Call(slider,"OnKeyRight",true);Flush(canvas);Check(Last()==UiCue.Slider,"slider keyboard feedback");
            var combo=new ComboBox(canvas,"TestCombo"); count=Played.Count; Pause(); var item=combo.AddItem("One"); combo.SelectedItem=item; Flush(canvas); Check(Played.Count==count,"programmatic combo refresh stays silent");
            var custom=new ProbeSource("custom.wav"); sounds["custom"]=custom;
            button.SetSound(ButtonSoundState.MouseClicked,"custom.wav"); Pause(); Click(button); Flush(canvas); Check(Played.Last().Source==custom,"authored click retains priority");
            count=Played.Count; Flush(canvas); Check(Played.Count==count,"no duplicate after authored sound");
            custom.Muted=true; Pause(); Click(button); Flush(canvas); Check(Played.Count==count,"muted authored sound does not allocate voice or fallback");
            var source=(ProbeSource)sounds[GameContentManager.RemoveExtension(UiAudioFiles.File(UiCue.Select))]; source.Muted=true;
            Pause(); Click(image); Flush(canvas); Check(Played.Count==count,"effects mute also silences default sounds"); source.Muted=false;
            source.Throw=true; Pause(); Click(image); Flush(canvas); Check(Played.Count==count,"unavailable audio backend does not crash click"); source.Throw=false;
            WindowTests(canvas);
            CatalogTests(canvas);
            Pause(); Click(image); Flush(canvas);
            Check(Played.Last().Volume==52,"fallback uses modest volume");
            Check(Played.Last().Updated>0,"audio volume is set before playback");
            var active=Played.Last();var updates=active.Updated;source.Muted=true;active.Update();Check(active.Updated==updates+1,"mute immediately after starting a sound updates its volume");source.Muted=false;
        }
        Check(Played.All(p=>p.Disposed),"all canvas-owned voices released on disposal");
        using(var rendered=new Canvas(skin){Size=new Point(1280,720)})
        {
            InterfaceAudio.Configure(rendered);
            var window=new WindowControl(rendered,"Inventory",false,"InventoryWindow"); window.SetBounds(0,0,300,220); window.Hide();
            var before=Played.Count;
            rendered.RenderCanvas(TimeSpan.Zero,TimeSpan.Zero);Check(Played.Count==before,"constructing hidden windows produces no sound");
            Pause();window.Show();rendered.RenderCanvas(TimeSpan.Zero,TimeSpan.Zero);Check(Last()==UiCue.BagOpen,"actual canvas render detects visible window");
            before=Played.Count;Pause();rendered.RenderCanvas(TimeSpan.Zero,TimeSpan.Zero);Check(Played.Count==before,"actual canvas redraw remains silent");
            Pause();window.Hide();rendered.RenderCanvas(TimeSpan.Zero,TimeSpan.Zero);Check(Last()==UiCue.PanelClose,"actual canvas render detects close");
        }
        using(var bounded=new Canvas(skin){Size=new Point(1280,720)})
        {
            HoldVoices=true;
            InterfaceAudio.Configure(bounded); var button=new Button(bounded); button.Clicked+=(_,_)=>{};
            var before=Played.Count;
            for(var i=0;i<20;i++){Pause();Click(button);Flush(bounded);}
            Check(Played.Count-before==8,"default audio voices capped under rapid repeated input");
        }
        Console.WriteLine($"Interface audio: {_checks} checks passed.");
    }
    private static UiCue Last() => Enum.GetValues<UiCue>().Single(c=>UiAudioFiles.File(c)==Played.Last().Source.Name);
    private static void QueueTests()
    {
        long now=1000; var queue=new UiAudioQueue(()=>now);
        queue.Request(UiCue.Hover);queue.Request(UiCue.Click);queue.Request(UiCue.BookOpen);
        Check(queue.Take()==UiCue.BookOpen,"window cue coalesces hover and click");
        Check(queue.Take()==null,"queue consumed once"); now+=200;
        queue.Request(UiCue.Submit);queue.Request(UiCue.Error);Check(queue.Take()==UiCue.Error,"error wins over submit"); now+=200;
        queue.Request(UiCue.Click);queue.DedicatedSoundPlayed();Check(queue.Take()==null,"dedicated sound suppresses fallback"); now+=200;
        queue.Request(UiCue.Click);now+=500;Check(queue.Take()==null,"stale hidden-canvas feedback discarded");
        queue.Request(UiCue.Hover);Check(queue.Take()==UiCue.Hover,"hover after quiet period");now+=60;queue.Request(UiCue.Hover);Check(queue.Take()==null,"rapid hover suppression");
        now+=60;queue.Request(UiCue.Click);Check(queue.Take()==UiCue.Click,"click survives hover-only suppression");
        queue.Request(UiCue.Click);queue.Clear();Check(queue.Take()==null,"reset clears pending feedback");
    }
    private static void WindowTests(Canvas canvas)
    {
        long now=1000; var queue=new UiAudioQueue(()=>now);var tracker=new UiWindowAudioTracker();
        var window=new WindowControl(canvas,"Bank",false,"BankWindow");tracker.Observe(window,new(UiCue.ChestOpen,UiCue.ChestClose));tracker.Complete(queue);
        Check(queue.Take()==UiCue.ChestOpen,"first presented window opens once");now+=200;tracker.Complete(queue);Check(queue.Take()==null,"cached render and refresh do not repeat window cue");
        window.Hide();tracker.Complete(queue);Check(queue.Take()==UiCue.ChestClose,"hidden window closes once");now+=200;tracker.Complete(queue);Check(queue.Take()==null,"hidden windows stay silent");
        window.Show();tracker.Complete(queue);Check(queue.Take()==UiCue.ChestOpen,"keyboard-reopened window audible");now+=200;
        canvas.RemoveChild(window,true);Call(canvas,"ProcessDelayedDeletes");tracker.Complete(queue);Check(queue.Take()==UiCue.ChestClose,"removed window closes and releases tracking");
        now+=200;tracker.Complete(queue);Check(queue.Take()==null,"removed window not replayed");
        var parent=new Base(canvas);var child=new WindowControl(parent);tracker.Observe(child,new(UiCue.PanelOpen));tracker.Complete(queue);queue.Take();now+=200;
        parent.Hide();tracker.Complete(queue);Check(queue.Take()==UiCue.PanelClose,"parent visibility accounted for");
        parent.Show();tracker.Clear();now+=200;tracker.Complete(queue);Check(queue.Take()==null,"tracker cleared at teardown");canvas.RemoveChild(parent,true);
    }
    private static void CatalogTests(Canvas canvas)
    {
        var categories=new Dictionary<string,UiCue>{["MainMenuWindow"]=UiCue.MenuOpen,["LoginWindow"]=UiCue.MenuOpen,["SelectCharacterWindow"]=UiCue.MenuOpen,["InventoryWindow"]=UiCue.BagOpen,["BankWindow"]=UiCue.ChestOpen,["ShopWindow"]=UiCue.Coins,["QuestsWindow"]=UiCue.BookOpen,["SpellsWindow"]=UiCue.Magic,["GuildWindow"]=UiCue.Social,["EquipmentLoadoutWindow"]=UiCue.Armor,["NavalManagementWindow"]=UiCue.Map,["CraftingWindow"]=UiCue.Workshop,["DailyRewardWindow"]=UiCue.Reward,["DungeonAbandonConfirmationWindow"]=UiCue.Prompt,["NewFutureWindow"]=UiCue.PanelOpen};
        foreach(var (name,cue) in categories){var w=new WindowControl(canvas,name:name);Check(InterfaceAudio.ResolveWindow(w)?.Open==cue,"window category "+name);canvas.RemoveChild(w,true);}
        foreach(var name in new[]{"HotBarWindow","ChatboxWindow","DungeonRunHudWindow","ItemDescriptionWindow","DebugWindow"}) {var w=new WindowControl(canvas,name:name);Check(InterfaceAudio.ResolveWindow(w)==null,"persistent/decorative exclusion "+name);canvas.RemoveChild(w,true);}
    }
    public class LogProxy:DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_Logger"?Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance:method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
    }
    private sealed class ProbeSource(string name):GameAudioSource
    {
        public bool Muted,Throw;
        public override bool IsLoaded=>true;
        public override int TypeVolume=>Muted?0:100;
        public override GameAudioInstance CreateInstance(){if(Throw)throw new InvalidOperationException("No device");Name=name;return new ProbeInstance(this);}
        public override void ReleaseInstance(GameAudioInstance? instance){}
    }
    private sealed class ProbeInstance(GameAudioSource source):GameAudioInstance(source)
    {
        public bool Disposed;public int Updated;private AudioInstanceState _state=AudioInstanceState.Stopped;private long _started;
        public override AudioInstanceState State=>_state==AudioInstanceState.Playing&&!HoldVoices&&Environment.TickCount64-_started>20?AudioInstanceState.Stopped:_state;
        protected override void OnVolumeChanged(int oldVolume,int newVolume)=>Updated++;
        protected override void OnLoopingChanged(bool looping){}
        public override void Play(){_state=AudioInstanceState.Playing;_started=Environment.TickCount64;Played.Add(this);}
        public override void Pause()=>_state=AudioInstanceState.Paused;
        public override void Stop()=>_state=AudioInstanceState.Stopped;
        public override void Dispose(){Disposed=true;_state=AudioInstanceState.Disposed;}
    }
}
