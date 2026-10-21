using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Interface.Menu;
using Intersect.Client.Localization;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Graphics;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Network.Packets.Metalworking;
using Intersect.Network.Packets.Pvp;
internal static class CombatPvpMetalworkUiTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run(){try{Verify();}catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}}
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static void Verify()
    {
        var content=new TestContentManager();var context=Intersect.Core.ApplicationContext.Context.Value;var database=Globals.Database;
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,ActivitiesLogProxy>();
        var menuConfig=ClientConfiguration.Instance.MenuAnimations;
        var music=typeof(GameInterface).Assembly.GetType("Intersect.Client.WorldEvents.WorldMusic",true)!;var property=music.GetProperty("Configuration",BindingFlags.Static|BindingFlags.NonPublic)!;var previous=property.GetValue(null);
        try
        {
            var prefs=new InterfaceRefreshTests.MemoryPreferences();prefs.LoadPreferences();Check(prefs.ShowPvpGrades,"PvP grade defaults visible");prefs.ShowPvpGrades=false;prefs.SavePreferences();var restored=new InterfaceRefreshTests.MemoryPreferences(prefs.Values);restored.LoadPreferences();Check(!restored.ShowPvpGrades,"PvP grade preference survives reload");Globals.Database=prefs;
            var recipe=new MetalworkRecipe {Name="Iron ingot",Enabled=true,Ingredients=[new(){ItemId=Guid.NewGuid(),Quantity=3}],OutputItemId=Guid.NewGuid()};property.SetValue(null,new WorldActivitiesConfiguration {Metalworking=new(){Recipes=[recipe]}});
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            foreach(var size in new[]{new Point(640,480),new Point(1280,800)})
            {
                using var canvas=new Canvas(skin){Size=size};var assembly=typeof(GameInterface).Assembly;
                var pvpType=assembly.GetType("Intersect.Client.Interface.Game.PvpWindow",true)!;var pvp=(Base)Activator.CreateInstance(pvpType,Hidden,null,[canvas],null)!;
                pvpType.GetMethod("Apply",Hidden)!.Invoke(pvp,[new PvpStatePacket {Grade="Captain",Points=1500,Arenas=[new(){Id=Guid.NewGuid(),Name="Castle arena",Players=4,Minutes=10,MinimumLevel=5}]}]);
                Check(pvp.Width<=canvas.Width && pvp.Height<=canvas.Height,"PvP fits narrow screen");
                var metalType=assembly.GetType("Intersect.Client.Interface.Game.MetalworkWindow",true)!;var metal=(Base)Activator.CreateInstance(metalType,Hidden,null,[canvas],null)!;var apply=metalType.GetMethod("Apply",Hidden)!;var id=Guid.NewGuid();
                apply.Invoke(metal,[new MetalworkStatePacket {SessionId=id,Sequence=1,RecipeId=recipe.Id,Kind=MetalworkKind.Smelting,Running=true}]);
                Check(!metal.IsHidden && metal.Width<=canvas.Width && metal.Height<=canvas.Height,"Foundry opens at narrow screen without game data textures");
                var start=(Button)metalType.GetField("_start",Hidden)!.GetValue(metal)!;var pump=(Button)metalType.GetField("_pump",Hidden)!.GetValue(metal)!;Check(start.IsDisabled && !pump.IsHidden && pump.Right<metal.Width,"Active recipe locks selection and keeps bellows visible");
                apply.Invoke(metal,[new MetalworkStatePacket {SessionId=id,Sequence=3,RecipeId=recipe.Id,Running=true,Stage=MetalworkStage.Pouring}]);
                apply.Invoke(metal,[new MetalworkStatePacket {SessionId=id,Sequence=2,RecipeId=recipe.Id,Running=true,Stage=MetalworkStage.Heating}]);Check(pump.IsHidden,"Stale packets cannot rewind stage");
                apply.Invoke(metal,[new MetalworkStatePacket {SessionId=id,Sequence=4,Closed=true}]);apply.Invoke(metal,[new MetalworkStatePacket {SessionId=id,Sequence=3}]);Check(metal.IsHidden,"Late packets cannot reopen closed station");
            }
            using(var canvas=new Canvas(skin){Size=new Point(800,600)})
            {
                content.AddAnimationTexture("menu-fixture",DispatchProxy.Create<IGameTexture,CpuFrameTexture>());
                ClientConfiguration.Instance.MenuAnimations=[new(){XPercent=50,YPercent=50,ScalePercent=200,TransparencyPercent=50,Lower=new AnimationLayer {Sprite="menu-fixture",XFrames=1,YFrames=1,FrameCount=1,FrameSpeed=100,ScalePercent=100,OffsetX=10,TransparencyPercent=20}}];
                var menu=(MainMenu)RuntimeHelpers.GetUninitializedObject(typeof(MainMenu));typeof(MainMenu).GetField("_menuCanvas",Hidden)!.SetValue(menu,canvas);var list=typeof(MainMenu).GetField("_menuAnimations",Hidden)!;list.SetValue(menu,Activator.CreateInstance(list.FieldType));var weatherList=typeof(MainMenu).GetField("_weatherLayers",Hidden)!;weatherList.SetValue(menu,Activator.CreateInstance(weatherList.FieldType));typeof(MainMenu).GetMethod("UpdateMenuAnimations",Hidden)!.Invoke(menu,null);
                var panel=canvas.Children.OfType<ImagePanel>().Single();Check(panel.Width==128 && panel.X==356 && panel.Y==236 && panel.RenderColor.A==102 && !panel.MouseInputEnabled,"Offline menu snapshots apply position, scale, offsets, alpha and pass input through");
            }
        }
        finally{ClientConfiguration.Instance.MenuAnimations=menuConfig;property.SetValue(null,previous);Globals.Database=database;Intersect.Core.ApplicationContext.Context.Value=context;}
        System.Console.WriteLine("PASS adaptive PvP/foundry UI, grade preference, stale packet rejection and offline main menu animation snapshots");
    }
}
