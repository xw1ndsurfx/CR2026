using Rectangle=Intersect.Client.Framework.GenericClasses.Rectangle;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game.Hotbar;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using NUnit.Framework;
using ControlBase=Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase=Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase=Intersect.Client.Framework.Gwen.Skin.Base;
namespace Intersect.Tests.Client;

[TestFixture,NonParallelizable]
public sealed class NavalCargoWindowTests
{
    private object? _previousContent;
    private TestContent _content=null!;
    private static readonly System.Reflection.FieldInfo CurrentContent=typeof(GameContentManager).GetField("_current",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
    [SetUp] public void SetUp() {_previousContent=CurrentContent.GetValue(null);_content=new TestContent();}
    [TearDown] public void TearDown() {CurrentContent.SetValue(null,_previousContent);}

    [Test]
    public void PerspectiveTextureIsDrawnWithWhiteTintAndRestoresTheIncomingRendererColor()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin) {Size=new Point(800,600)};var window=new NavalCannonViewWindow(canvas);
        window.Update(new(){Id=Guid.NewGuid(),CaptainId=Guid.NewGuid()},true);
        var viewport=Descendants(window).Single(c=>c.Name=="CannonPerspectiveViewport");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var texture=System.Reflection.DispatchProxy.Create<IGameRenderTexture,TextureProxy>();
        viewport.GetType().GetField("_texture",flags)!.SetValue(viewport,texture);
        viewport.GetType().GetField("_nextFrame",flags)!.SetValue(viewport,long.MaxValue);
        var previous=new Color(255,27,40,53);renderer.DrawColor=previous;
        viewport.GetType().GetMethod("Render",flags)!.Invoke(viewport,[skin]);
        Assert.That(renderer.TexturedTints.Single(),Is.EqualTo(Color.White));
        Assert.That(renderer.DrawColor,Is.EqualTo(previous));
    }
    public class TextureProxy:System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? method,object?[]? args)=>
            method?.ReturnType==typeof(bool)?false:method?.ReturnType==typeof(int)?640:null;
    }

    [Test]
    public void CrewViewportUsesMouseCoordinatesForAimWithoutDuplicatingTheNativeAmmoAndProgressBars()
    {
        var previous=Intersect.Client.General.Globals.Me;
        var me=(Intersect.Client.Entities.Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));
        GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        try
        {
            Intersect.Client.General.Globals.Me=me;Intersect.Client.Naval.NavalWorld.Clear();
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            using var canvas=new Canvas(skin) {Size=new Point(800,600)};
            var window=new NavalCannonViewWindow(canvas);
            var ship=new ShipSnapshot {Id=Guid.NewGuid(),CaptainId=Guid.NewGuid(),CrewIds=[me.Id]};
            window.Update(ship,true);
            var viewport=Descendants(window).Single(c=>c.Name=="CannonPerspectiveViewport");
            var position=viewport.ToCanvas(viewport.Width*3/4,viewport.Height*3/4);
            viewport.GetType().GetMethod("OnMouseMoved",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(viewport,[position.X,position.Y,1,1]);
            Assert.That(Intersect.Client.Naval.NavalWorld.GunYaw,Is.GreaterThan(0));
            Assert.That(Intersect.Client.Naval.NavalWorld.GunElevation,Is.LessThan(0));
            Assert.That(Descendants(window).Single(c=>c.Name=="CannonAmmunition").IsHidden,Is.True);
            Assert.That(Descendants(window).Single(c=>c.Name=="GunneryExperience").IsHidden,Is.True);
        }
        finally {Intersect.Client.Naval.NavalWorld.Clear();Intersect.Client.General.Globals.Me=previous;}
    }

    [TestCase(1920,1080)]
    [TestCase(640,480)]
    [TestCase(800,600)]
    [TestCase(2560,1080)]
    public void CrewPerspectiveCoversTheEntireCanvasKeepsHudsAboveAndClosesWhenTheShipSinks(int width,int height)
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin) {Size=new Point(width,height)};
        var window=new NavalCannonViewWindow(canvas);
        var ship=new ShipSnapshot {Id=Guid.NewGuid(),CaptainId=Guid.NewGuid(),CannonsEnabled=true};
        window.Update(ship,true);
        Assert.That(window.IsHidden,Is.False);
        Assert.That(window.Bounds,Is.EqualTo(new Rectangle(0,0,width,height)));
        Assert.That(window.Titlebar.IsHidden,Is.True);
        var viewport=Descendants(window).Single(c=>c.Name=="CannonPerspectiveViewport");
        Assert.That(viewport.Bounds,Is.EqualTo(new Rectangle(0,0,width,height)));
        window.GetType().GetMethod("Layout",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,[skin]);
        Assert.That(viewport.ToCanvas(0,0),Is.EqualTo(new Point(0,0)));
        // The fullscreen control stays behind the actual hotbar/HUD, even after touching the world image.
        var hud=new ControlBase(canvas,"TestNavalHud");hud.SetBounds(0,0,100,100);window.Touch();
        Assert.That(canvas.Children.ToList().IndexOf(window),Is.LessThan(canvas.Children.ToList().IndexOf(hud)));
        Assert.That(canvas.GetComponentAt(10,10),Is.SameAs(hud),"HUD clicks must win over the fullscreen scene.");
        Assert.That(canvas.GetComponentAt(width*3/4,height/2),Is.SameAs(viewport),"World clicks must reach the aiming viewport.");
        viewport.GetType().GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(viewport,[skin]);
        Assert.That(renderer.Fills.Any(f=>f.Rect==new Rectangle(0,0,width,height) && f.Color.A==255),Is.True,"Opaque cover must remain present even without a ready map or GPU scene.");
        canvas.SetSize(width/2,height/2);window.Update(ship,true);
        Assert.That(window.Width,Is.EqualTo(width/2));Assert.That(viewport.Height,Is.EqualTo(height/2));
        foreach(var control in window.Children.Where(c=>c.Name is "CannonPerspectiveViewport" or "FireBroadside" or "PortView" or "StarboardView"))
            Assert.That(control.X>=0 && control.Y>=0 && control.Right<=window.Width && control.Bottom<=window.Height,Is.True,control.Name);
        Assert.That(Descendants(window).OfType<Button>().Single(b=>b.Name=="FireBroadside").IsDisabled,Is.True);
        ship.Sinking=true;window.Update(ship,true);
        Assert.That(window.IsHidden,Is.True);
        Assert.That(Intersect.Client.Naval.NavalWorld.CannonViewOpen,Is.False);
    }

    [Test]
    public void CaptainKeepsTheCompactPreviewAndCloseReturnsToNormalView()
    {
        var previous=Intersect.Client.General.Globals.Me;
        var me=(Intersect.Client.Entities.Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));
        GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        try
        {
            Intersect.Client.General.Globals.Me=me;
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            using var canvas=new Canvas(skin) {Size=new Point(800,600)};var window=new NavalCannonViewWindow(canvas);
            window.Update(new(){Id=Guid.NewGuid(),CaptainId=me.Id},true);
            Assert.That(window.Width,Is.LessThan(800));Assert.That(window.Height,Is.LessThan(600));
            Assert.That(window.Titlebar.IsHidden,Is.False);
            Intersect.Client.Naval.NavalWorld.CannonViewOpen=true;
            var close=Descendants(window).OfType<Button>().Single(b=>b.Name=="CloseCannonView");
            typeof(ControlBase).GetMethod("EmitVirtualClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(close,
                [Intersect.Client.Framework.Gwen.Control.EventArguments.MouseButtonState.Empty]);
            Assert.That(Intersect.Client.Naval.NavalWorld.CannonViewOpen,Is.False);
            window.Update(null,false);Assert.That(window.IsHidden,Is.True);
        }
        finally {Intersect.Client.Naval.NavalWorld.Clear();Intersect.Client.General.Globals.Me=previous;}
    }

    [TestCase(1920,1080)]
    [TestCase(800,600)]
    [TestCase(640,480)]
    public void NativeManifestScrollsAllLotsAndKeepsItsRowsAndScrollOnRepeatedSnapshots(int width,int height)
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin) {Size=new Point(width,height)};
        var window=new NavalCargoWindow(canvas);
        var item=new ItemDescriptor(Guid.NewGuid()) {Name="Aureons",Icon="aureons.png"};ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            var ship=new ShipSnapshot {Id=Guid.NewGuid(),CargoCapacity=20,
                Cargo=Enumerable.Range(0,20).Select(i=>new NavalCargoParcel {ItemId=item.Id,Quantity=1000000,Quality=i%5+1}).ToArray()};
            window.Update(ship,true);
            Assert.That(window.Width,Is.LessThan(width));Assert.That(window.Height,Is.LessThan(height));
            var scroll=Descendants(window).OfType<ScrollControl>().Single();
            Assert.That(scroll.Children.Count,Is.EqualTo(20));
            Assert.That(scroll.VerticalScrollBar.ContentSize,Is.GreaterThan(scroll.Height));
            Assert.That(scroll.HorizontalScrollBar.IsHidden,Is.True);
            Assert.That(Descendants(window).OfType<ImagePanel>().Count(),Is.EqualTo(20));
            Assert.That(_content.Requests.Count(r=>r==(TextureType.Item,"aureons.png")),Is.EqualTo(20));
            Assert.That(Descendants(window).Any(c=>c.Name is "Previous" or "Next"),Is.False);
            Assert.That(Descendants(window).OfType<Label>().Where(c=>!c.IsHidden).All(c=>!(c.Text??string.Empty).Contains('\n')),Is.True);
            foreach(var card in scroll.Children)
                foreach(var label in Descendants(card).OfType<Label>())
                    Assert.That(label.X>=0 && label.Right<=card.Width && label.Width>0,Is.True,label.Name);
            scroll.VerticalScrollBar.ScrollAmount=.6f;var first=scroll.Children[0];
            // A new network array with unchanged values must not recreate widgets at every snapshot.
            ship.Cargo=ship.Cargo.Select(p=>new NavalCargoParcel {Id=p.Id,ItemId=p.ItemId,Quantity=p.Quantity,Quality=p.Quality,Kind=p.Kind}).ToArray();
            window.Update(ship,true);
            Assert.That(scroll.Children[0],Is.SameAs(first));Assert.That(scroll.VerticalScrollBar.ScrollAmount,Is.EqualTo(.6f).Within(.01));
            ship.Cargo[0].Quantity++;window.Update(ship,true);
            Assert.That(scroll.VerticalScrollBar.ScrollAmount,Is.EqualTo(.6f).Within(.01));
            ship.Id=Guid.NewGuid();window.Update(ship,true);Assert.That(scroll.VerticalScrollBar.ScrollAmount,Is.Zero);
            ship.Sinking=true;window.Update(ship,true);Assert.That(window.IsHidden,Is.True);
        }
        finally {ItemDescriptor.Lookup.Delete(item);}
    }
    [Test]
    public void EmptyAndUnknownItemsRemainReadableWithoutBreakingTheManifest()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin) {Size=new Point(800,600)};var window=new NavalCargoWindow(canvas);
        var ship=new ShipSnapshot {Id=Guid.NewGuid(),CargoCapacity=20};window.Update(ship,true);
        Assert.That(Descendants(window).Single(c=>c.Name=="CargoEmpty").IsHidden,Is.False);
        ship.Cargo=[new(){ItemId=Guid.NewGuid(),Quantity=3,Quality=5}];window.Update(ship,true);
        Assert.That(Descendants(window).Single(c=>c.Name=="CargoEmpty").IsHidden,Is.True);
        Assert.That(Descendants(window).OfType<Label>().Single(c=>c.Name=="CargoItemName").Text,Does.Contain("Unknown item"));
        window.Update(null,true);Assert.That(window.IsHidden,Is.True);
    }
    [Test]
    public void CrewRosterAndDockOffersUseRealScrollControlsWithoutRecreatingIdenticalRows()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin) {Size=new Point(640,480)};var window=new NavalCrewWindow(canvas);
        var state=new ShipCrewState {AtDock=true,PersonalCargoCount=2,Offers=Enumerable.Range(0,5).Select(i=>new ShipCrewOffer {
            ShipId=Guid.NewGuid(),Name="Royal ship "+i,CaptainName="Captain "+i,Occupied=1,Capacity=4}).ToArray()};
        window.Apply(new HarbourStatePacket {View=HarbourView.Crew,Crew=state});
        var scroll=Descendants(window).OfType<ScrollControl>().Single();
        Assert.That(Descendants(scroll).OfType<Button>().Count(),Is.EqualTo(5));
        var first=scroll.Children[0];window.Update(state);Assert.That(scroll.Children[0],Is.SameAs(first));
        state.ShipId=Guid.NewGuid();state.Members=[new(){PlayerId=Guid.NewGuid(),Name="Captain",Captain=true},new(){PlayerId=Guid.NewGuid(),Name="Sailor"}];
        window.Update(state);Assert.That(scroll.Children.Count,Is.EqualTo(2));
        foreach(var button in Descendants(window).OfType<Button>())
            Assert.That(button.X>=0 && button.Y>=0 && button.Right<=button.Parent!.Width,Is.True,button.Name);
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase parent)
    {foreach(var child in parent.Children) {yield return child;foreach(var descendant in Descendants(child)) yield return descendant;}}
    private sealed class TestContent:GameContentManager
    {
        internal List<(TextureType,string)> Requests=[];
        internal TestContent() {mFontDict["sourcesanspro"]=new TestFont();mFontDict["sourcesansproblack"]=new TestFont();}
        public override IGameTexture? GetTexture(TextureType type,string name) {Requests.Add((type,name));return null;}
        public override void LoadTilesets(string[] names)=>throw new InvalidOperationException();
        public override void LoadTexturePacks()=>throw new InvalidOperationException();
        public override void LoadItems()=>throw new InvalidOperationException();
        public override void LoadEntities()=>throw new InvalidOperationException();
        public override void LoadSpells()=>throw new InvalidOperationException();
        public override void LoadAnimations()=>throw new InvalidOperationException();
        public override void LoadFaces()=>throw new InvalidOperationException();
        public override void LoadImages()=>throw new InvalidOperationException();
        public override void LoadDungeons()=>throw new InvalidOperationException();
        public override void LoadFogs()=>throw new InvalidOperationException();
        public override void LoadResources()=>throw new InvalidOperationException();
        public override void LoadPaperdolls()=>throw new InvalidOperationException();
        public override void LoadGui()=>throw new InvalidOperationException();
        public override void LoadMisc()=>throw new InvalidOperationException();
        public override void LoadFonts()=>throw new InvalidOperationException();
        public override void LoadShaders()=>throw new InvalidOperationException();
        public override void LoadSounds()=>throw new InvalidOperationException();
        public override void LoadMusic()=>throw new InvalidOperationException();
        protected override TAsset Load<TAsset>(Dictionary<string,IAsset> lookup,ContentType type,string name,Func<Stream> stream)=>throw new InvalidOperationException();
    }
    private sealed class TestSkin:SkinBase
    {
        internal TestSkin(RendererBase renderer):base(renderer)
        {DefaultFont=new TestFont();DefaultFontSize=12;Colors.Label.Normal=Colors.Label.Disabled=Color.White;}
    }
    private sealed class MetricsRenderer:RendererBase
    {
        internal List<(Rectangle Rect,Color Color)> Fills {get;}=[];
        public override void DrawFilledRect(Rectangle rect)=>Fills.Add((rect,DrawColor));
        internal List<Color> TexturedTints {get;}=[];
        public override void DrawTexturedRect(IGameTexture? texture,Rectangle bounds,Color color,float u1=0,float v1=0,float u2=1,float v2=1)=>TexturedTints.Add(DrawColor);
        public override Point MeasureText(IFont? font,int size,string? text,float scale=1f)=>new((int)Math.Ceiling((text?.Length??0)*size*.6*scale),(int)Math.Ceiling((size+3)*scale));
    }
    private sealed class TestFont:IFont
    {
        public string Name=>"naval-cargo-test";
        public ICollection<int> SupportedSizes {get;}=new[]{10,12,18,22};
        public int PickBestMatchFor(int size)=>size;
        public int GetNextFontSize(int startSize,int direction,int limit=0)=>startSize+direction;
    }
}
