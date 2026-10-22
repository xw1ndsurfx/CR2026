using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Naval;
using ControlBase=Intersect.Client.Framework.Gwen.Control.Base;
internal static class NavalAmmoSelectionTests
{
    [ModuleInitializer] internal static void Run()
    {
        try
        {
            _=new TestContentManager();
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            using var canvas=new Canvas(skin){Size=new Point(1024,768)};
            var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.NavalManagementWindow",true)!;
            var panel=(ControlBase)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,[canvas,NavalManageAction.Ammo],null)!;
            var state=new NavalManagementState{Mode=NavalManageAction.Ammo,SelectedAmmo=CannonAmmoKind.Standard,AmmoCapacity=1000,Ammo=[
                new(){Kind=CannonAmmoKind.Standard,Quantity=-1,Available=true},
                new(){Kind=CannonAmmoKind.Heavy,Quantity=100,Available=true}]};
            void Apply()=>type.GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,[state]);
            ControlBase Find(string name)=>Descendants(panel).Single(c=>c.Name==name);
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
            Apply();
            Check(Find("AmmoInUse").Parent!.Name=="AmmoSlotStandard","Initial equipped badge missing");
            typeof(Button).GetMethod("OnKeyReturn",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Find("AmmoSlotHeavy"),[false]);
            Check(state.SelectedAmmo==CannonAmmoKind.Standard,"Browsing must not equip ammunition");
            Check(Find("AmmoInUse").Parent!.Name=="AmmoSlotStandard","Browsing moved equipped badge");
            Check(!Find("AmmoUse").IsDisabled,"Use must remain available for a different round");
            state.SelectedAmmo=CannonAmmoKind.Heavy;Apply();
            Check(Find("AmmoInUse").Parent!.Name=="AmmoSlotHeavy","Server acknowledgement must move badge");
            Check(Find("AmmoUse").IsDisabled,"Equipped round must show the active state");
            var world=typeof(GameInterface).Assembly.GetType("Intersect.Client.Naval.NavalWorld",true)!;
            Check((CannonAmmoKind)world.GetProperty("SelectedAmmo",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)! ==CannonAmmoKind.Heavy,"Confirmed selection must also drive cannon shots");
            state.SelectedAmmo=CannonAmmoKind.Standard;Apply();
            System.Console.WriteLine("PASS naval Ammo: browsing, authoritative equipped badge, use state and cannon selection");
        }
        catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase parent)=>parent.Children.SelectMany(child=>new[]{child}.Concat(Descendants(child)));
}
