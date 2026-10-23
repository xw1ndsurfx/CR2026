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
    public void DungeonAbandonButtonFitsTheHudAndDisappearsWhenTheRunEnds()
    {
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(800, 600) };
        var hud = new DungeonRunHudWindow(canvas);
        var button = Descendants(hud).OfType<Button>().Single(b => b.Name == "DungeonRunAbandon");
        hud.Apply(new DungeonRunStatePacket { RunId = Guid.NewGuid(), Status = DungeonRunStatus.Active });
        Assert.That(button.IsHidden, Is.False);
        Assert.That(button.Bottom, Is.LessThan(hud.Height));
        Assert.That(button.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(button.Right, Is.LessThanOrEqualTo(hud.Width));
        Assert.That(hud.GetComponentAt(button.X + 2, button.Y + 2), Is.SameAs(button),
            "The HUD remains click-through while its abandon button receives input.");
        foreach (var status in new[] { DungeonRunStatus.Completed, DungeonRunStatus.Failed, DungeonRunStatus.Abandoned })
        {
            hud.Apply(new DungeonRunStatePacket { Status = status });
            Assert.That(button.IsHidden, Is.True);
        }
    }

    [Test]
    public void FleetCardsShowEachShipAppearanceWithoutOverlappingTheirControls()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1024,768)};
        var window=new HarbourWindow(canvas,HarbourView.Fleet);
        window.Apply(new(){Entries=[new(){ModelId="royal",Owned=true,Health=80,ShipName="Royal",Graphics="royal.png",Frames=4},
            new(){ModelId="herbonia",Owned=true,Health=100,ShipName="Herbonia",Graphics="herbonia.png",Frames=1}]});
        var previews=Descendants(window).OfType<NavalShipPreview>().ToArray();
        Assert.That(previews.Select(p=>p.Graphic),Is.EqualTo(new[]{"royal.png","herbonia.png"}));
        Assert.That(previews.Select(p=>p.Frames),Is.EqualTo(new[]{4,1}));
        foreach(var preview in previews)
        {
            var name=Descendants(window).OfType<Label>().Single(c=>c.Name=="Name"+preview.Name.Last());
            Assert.That(preview.Right,Is.LessThan(name.X));
            var launch=Descendants(window).OfType<Button>().Single(c=>c.Name=="Launch"+preview.Name.Last());
            Assert.That(preview.Right,Is.LessThan(launch.X));
        }
    }

    [Test]
    public void NavalAmmoQuantityAndShortcutUseSeparateSingleLineLabels()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var button=new NavalActionButton(canvas,"AmmoFixture") {Font=skin.DefaultFont,FontSize=8};
        button.SetBounds(0,0,36,36);
        Assert.That(NavalActionButton.AmmoQuantityCaption(-1),Is.EqualTo("-"));
        Assert.That(NavalActionButton.AmmoQuantityCaption(0),Is.EqualTo("0"));
        Assert.That(NavalActionButton.AmmoQuantityCaption(87),Is.EqualTo("87"));
        foreach(var quantity in new[]{"-","0","87","999K"})
        {
            button.SetCaption("4",quantity);
            typeof(ControlBase).GetMethod("RecurseLayout",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(canvas,[skin]);
            var count=button.Children.OfType<Label>().Single(c=>c.Name=="NavalCounter");
            Assert.That(button.Text,Is.EqualTo("4"));Assert.That(count.Text,Is.EqualTo(quantity));
            Assert.That(count.MouseInputEnabled,Is.False);Assert.That(count.Bottom,Is.LessThanOrEqualTo(button.TextY));
            Assert.That(Descendants(button).OfType<Label>().All(c=>!c.Text!.Contains('\n') && !c.Text.Contains('\r')),Is.True);
        }
        button.SetCaption("1","3",true);
        Assert.That(button.Text,Is.EqualTo("1"));
        Assert.That(button.Children.OfType<Label>().Single().Text,Is.EqualTo("3"));
        button.SetCaption("0");Assert.That(button.Children.OfType<Label>().Single().IsHidden,Is.True);
    }

    [Test]
    public void NavalTargetShowsHoverableSpellEffectsWrapsThemAndClearsThemAfterCombat()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(240,300)};
        Intersect.Client.Naval.NavalWorld.Clear();
        try
        {
            var enemy=new ShipSnapshot {Id=Guid.NewGuid(),Enemy=true,Health=100,MaximumHealth=100,
                CannonStatuses=Enum.GetValues<CannonAmmoKind>().Select(kind=>new NavalCannonStatus
                {Kind=kind,Icon="fire.png",RemainingMilliseconds=30000,DamagePerTick=5,TickSeconds=2}).ToArray()};
            Intersect.Client.Naval.NavalWorld.Apply(new(){Ships=[enemy]});
            var hud=new NavalTargetHud(canvas);hud.Update(enemy);
            var badges=hud.Children.OfType<Panel>().Where(c=>c.Name.StartsWith("CannonStatus")).ToArray();
            Assert.That(badges,Has.Length.EqualTo(8));
            Assert.That(badges.All(b=>b.MouseInputEnabled && !b.IsHidden),Is.True);
            Assert.That(badges.All(b=>b.X>=0 && b.Right<=hud.Width && b.Bottom<=hud.Height),Is.True);
            Assert.That(badges[0].TooltipText,Does.Contain("5"));
            Assert.That(_content.Requests.Count(r=>r==(TextureType.Spell,"fire.png")),Is.EqualTo(8));
            enemy.CannonStatuses=[];hud.Update(enemy);Assert.That(badges.All(b=>b.IsHidden),Is.True);
            hud.Update(null);Assert.That(hud.IsHidden,Is.True);
        }
        finally {Intersect.Client.Naval.NavalWorld.Clear();}
    }

    [Test]
    public void PremiumTimerSupportsMoreThanAWeekExpiresAndClearsBetweenCharacters()
    {
        var old=System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("fr");
            Intersect.Client.WorldEvents.PremiumState.Apply(new(){RemainingMilliseconds=40L*86400000});
            Assert.That(Intersect.Client.WorldEvents.PremiumState.Remaining,Is.GreaterThan(39L*86400000));
            Assert.That(ExperienceEventHud.FormatPremium(40L*86400000),Does.Contain("40"));
            Intersect.Client.WorldEvents.PremiumState.Apply(new(){RemainingMilliseconds=0});Assert.That(Intersect.Client.WorldEvents.PremiumState.Remaining,Is.Zero);
            Intersect.Client.WorldEvents.PremiumState.Apply(new(){RemainingMilliseconds=long.MaxValue});
            Assert.That(Intersect.Client.WorldEvents.PremiumState.Remaining,Is.LessThanOrEqualTo(3153600000000L));
            Intersect.Client.WorldEvents.PremiumState.Reset();Assert.That(Intersect.Client.WorldEvents.PremiumState.Remaining,Is.Zero);
        }
        finally{System.Globalization.CultureInfo.CurrentUICulture=old;Intersect.Client.WorldEvents.PremiumState.Reset();}
    }
    [Test]
    public void RepairWindowShowsPersonalAndCrewExperienceAndASeparateHullPuzzle()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1000,800)};
        var window=new NavalManagementWindow(canvas,NavalManageAction.Repair);
        window.Apply(new(){Mode=NavalManageAction.Repair,Shipwright=new(4,50,100),RepairToken=Guid.NewGuid(),RepairKind=NavalRepairKind.Hammering,
            RepairCells=NavalRepairPuzzle.Create(NavalRepairKind.Hammering,5),RepairParticipants=[new(){PlayerId=Guid.NewGuid(),Name="Shipwright Test",Progress=new(9,75,150)}]});
        var controls=Descendants(window).ToArray();
        Assert.That(controls.OfType<PlayerProfileWindow.ProfileProgressBar>().Count(),Is.EqualTo(2));
        Assert.That(controls.OfType<PlayerProfileWindow.ProfileProgressBar>().Select(p=>p.Progress),Is.All.EqualTo(.5f));
        Assert.That(controls.OfType<Label>().Any(l=>l.Text?.Contains("Shipwright Test")==true),Is.True);
        Assert.That(controls.OfType<RepairPuzzleBoard>().Count(),Is.EqualTo(1));
        Assert.That(controls.OfType<Button>().Any(b=>b.Name=="RotateRepairPiece"),Is.True);
    }
    [Test]
    public void NewsTitleMarqueeKeepsTheFullTitleMovesLeftAndReturnsFromTheRight()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var title="Corps Royaux Progress Update - A New Glimpse Awaits!";
        var button=new LogiklikNewsWindow.NewsTitleButton(canvas,"NewsTest"){Text=title};
        Assert.That(button.Text,Is.EqualTo(title));
        Assert.That(LogiklikNewsWindow.NewsTitleButton.Offset(200,400,0),Is.EqualTo(8));
        Assert.That(LogiklikNewsWindow.NewsTitleButton.Offset(200,400,1600),Is.EqualTo(-34));
        Assert.That(LogiklikNewsWindow.NewsTitleButton.Offset(200,400,10600),Is.GreaterThan(180));
        var loop=600L*1000/42;
        Assert.That(Math.Abs(LogiklikNewsWindow.NewsTitleButton.Offset(200,400,12000)-LogiklikNewsWindow.NewsTitleButton.Offset(200,400,12000+loop)),Is.LessThanOrEqualTo(1));
    }
    [TestCase(530,20)]
    [TestCase(560,24)]
    [TestCase(400,10)]
    public void ProfileExperienceBarsKeepAFullBackgroundAtZeroHalfAndFullExperience(int width,int height)
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var bar=new PlayerProfileWindow.ProfileProgressBar(canvas,"ProfessionExperience");bar.SetBounds(0,0,width,height);
        var draw=bar.GetType().GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        var previous=new Color(255,180,170,160);
        foreach(var progress in new[]{0f,.5f,1f})
        {
            renderer.Fills.Clear();renderer.DrawColor=previous;bar.Progress=progress;draw.Invoke(bar,[skin]);
            var border=height<12?1:2;
            Assert.That(renderer.Fills[0].Rect.Width,Is.EqualTo(width));Assert.That(renderer.Fills[0].Rect.Height,Is.EqualTo(height));
            Assert.That(renderer.Fills[1].Rect.Width,Is.EqualTo(width-border*2));
            Assert.That(renderer.Fills[1].Color,Is.EqualTo(new Color(255,70,50,37)));
            Assert.That(renderer.Fills.Count,Is.EqualTo(progress==0?2:3));
            if(progress>0)Assert.That(renderer.Fills[2].Rect.Width,Is.EqualTo((int)((width-border*2)*progress)));
            Assert.That(renderer.DrawColor,Is.EqualTo(previous));
        }
    }

    [Test]
    public void InventoryAmmoFooterDoesNotReserveTheButtonHeightTwice()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var inventory=new Intersect.Client.Interface.Game.Inventory.InventoryWindow(canvas);inventory.SetSize(240,340);inventory.Show();
        var items=Descendants(inventory).OfType<ScrollControl>().Single();items.Margin=new Intersect.Client.Framework.Gwen.Margin(3,5,7,38);
        inventory.ConfigureAmmoFooter();inventory.ConfigureAmmoFooter();
        typeof(ControlBase).GetMethod("RecurseLayout",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(canvas,[skin]);
        var ammo=Descendants(inventory).OfType<Button>().Single(button=>button.Name=="InventoryAmmo");
        Assert.That(items.Margin.Left,Is.EqualTo(3));Assert.That(items.Margin.Top,Is.EqualTo(5));Assert.That(items.Margin.Right,Is.EqualTo(7));
        Assert.That(items.Margin.Bottom,Is.Zero);Assert.That(ammo.Parent!.Y + ammo.Y - items.Bottom,Is.InRange(0,6));
        var campfire=Descendants(inventory).OfType<Button>().Single(button=>button.Name=="InventoryCampfire");
        Assert.That(campfire.Parent,Is.SameAs(ammo.Parent));
        Assert.That(campfire.Y,Is.EqualTo(ammo.Y));
        Assert.That(campfire.Height,Is.EqualTo(ammo.Height));
        Assert.That(campfire.X,Is.GreaterThanOrEqualTo(ammo.Right));
        Assert.That(campfire.Right,Is.LessThanOrEqualTo(ammo.Parent.Width));
        Assert.That(ammo.Height,Is.EqualTo(28));
    }

    [Test]
    public void SailingSkillsScrollbarCanReachTheLastOwnedSkill()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var window=new NavalManagementWindow(canvas,NavalManageAction.Skills);
        var entries=Enum.GetValues<NavalSkillKind>().Select(kind=>new NavalSkillView {Owned=true,Definition=new(){Kind=kind}}).ToArray();
        window.Apply(new(){Mode=NavalManageAction.Skills,Skills=entries});
        var scroll=Descendants(window).OfType<ScrollControl>().Single();
        var hosted=(IReadOnlyList<ControlBase>)typeof(ControlBase).GetField("_children",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(scroll)!;
        Assert.That(hosted.Contains(scroll.VerticalScrollBar),Is.True,"Rebuilding skills must keep the scrollbar in the actual rendered control tree");
        Assert.That(scroll.VerticalScrollBar.IsHidden,Is.False);
        Assert.That(scroll.VerticalScrollBar.ContentSize,Is.GreaterThan(scroll.Height));
        scroll.VerticalScrollBar.SetScrollAmount(1,true);scroll.UpdateScrollBars();
        Assert.That(scroll.VerticalScrollBar.ScrollAmount,Is.EqualTo(1));
        Assert.That(scroll.Children.Any(child=>child.Name=="SkillAction"+entries.Last().Definition.Kind),Is.True);
        var inner=scroll.InnerPanel;
        window.Apply(new(){Mode=NavalManageAction.Skills,Skills=entries});
        Assert.That(scroll.InnerPanel,Is.SameAs(inner));
        Assert.That(scroll.VerticalScrollBar.Parent,Is.SameAs(scroll));
        Assert.That(scroll.HorizontalScrollBar.Parent,Is.SameAs(scroll));
        Assert.That(scroll.Children.Count(child=>child.Name.StartsWith("SkillAction")),Is.EqualTo(entries.Length));
    }

    [Test]
    public void FocusedTextInputBlocksGameplayAndHiddenInputReleasesIt()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var input=new TextBox(canvas,"EventOpenedSearch");input.SetBounds(10,10,250,30);
        var previous=Intersect.Client.Framework.Gwen.Input.InputHandler.KeyboardFocus;
        try
        {
            Intersect.Client.Framework.Gwen.Input.InputHandler.KeyboardFocus=input;
            Assert.That(Intersect.Client.Interface.Interface.HasInputFocus(),Is.True);
            input.Hide();Assert.That(Intersect.Client.Interface.Interface.HasInputFocus(),Is.False);
            input.Show();Assert.That(Intersect.Client.Interface.Interface.HasInputFocus(),Is.True);
            Intersect.Client.Framework.Gwen.Input.InputHandler.KeyboardFocus=null;
            Assert.That(Intersect.Client.Interface.Interface.HasInputFocus(),Is.False);
        }
        finally {Intersect.Client.Framework.Gwen.Input.InputHandler.KeyboardFocus=previous;}

    }

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

    [Test]
    public void CampfireAndGiftControlsDetachBeforeDisposalAndDoNotCrashCanvasShutdown()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1000,700)};
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        var viewType=typeof(GameInterface).GetNestedType("FireView",System.Reflection.BindingFlags.NonPublic)!;
        var view=Activator.CreateInstance(viewType,true)!;var button=new Button(canvas,"ExpiredFireButton");
        viewType.GetField("Button",flags)!.SetValue(view,button);viewType.GetMethod("Dispose",flags)!.Invoke(view,null);
        Assert.That(canvas.Children.Contains(button),Is.False);
        var giftType=typeof(LogiCoinShopWindow).GetNestedType("GiftRecipientWindow",System.Reflection.BindingFlags.NonPublic)!;
        var gift=(ControlBase)Activator.CreateInstance(giftType,flags,null,[canvas,new Intersect.Framework.Core.LogiCoins.LogiCoinShopOffer{Name="Gift"}],null)!;
        giftType.GetMethod("Destroy",flags)!.Invoke(gift,null);giftType.GetMethod("Destroy",flags)!.Invoke(gift,null);
        Assert.That(canvas.Children.Contains(gift),Is.False);
    }

    [Test]
    public void LiveUiDesignerMovesActualControlsAndAppliesBoundsAndSoundProperties()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Name="DesignerFixture",Size=new Point(1000,700)};
        var panel=new ControlBase(canvas,"EditablePanel");panel.SetBounds(400,40,200,160);
        var image=new ImagePanel(panel,"EditableImage");image.SetBounds(20,20,40,40);
        var type=typeof(Intersect.Client.Interface.Shared.UiDesignerRuntime);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        var document=type.GetField("_document",flags)!;var dirty=type.GetField("_dirty",flags)!;
        var before=((Newtonsoft.Json.Linq.JObject)document.GetValue(null)!).DeepClone();var wasDirty=dirty.GetValue(null);
        try
        {
            var designer=Intersect.Client.Interface.Shared.UiDesignerRuntime.CreateDesigner(canvas);
            var methods=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            designer.GetType().GetMethod("OnMouseDown",methods)!.Invoke(designer,[Intersect.Client.Framework.Input.MouseButton.Left,new Point(430,70),true]);
            designer.GetType().GetMethod("OnMouseMoved",methods)!.Invoke(designer,[450,80,20,10]);
            designer.GetType().GetMethod("OnMouseUp",methods)!.Invoke(designer,[Intersect.Client.Framework.Input.MouseButton.Left,new Point(450,80),true]);
            Assert.That(image.X,Is.EqualTo(40));Assert.That(image.Y,Is.EqualTo(30));
            var properties=Newtonsoft.Json.Linq.JObject.Parse("{\"Bounds\":\"12,18,70,80\",\"HoverSound\":\"click.wav\"}");
            Intersect.Client.Interface.Shared.UiDesignerRuntime.ApplyControl(image,properties);
            Assert.That(image.Width,Is.EqualTo(70));Assert.That(image.HoverSound,Is.EqualTo("click.wav"));
            properties["HoverSound"]="";Intersect.Client.Interface.Shared.UiDesignerRuntime.ApplyControl(image,properties);
            Assert.That(image.HoverSound,Is.Empty);
            Assert.That(Intersect.Client.Interface.Shared.UiDesignerRuntime.Resolve(canvas,Intersect.Client.Interface.Shared.UiDesignerRuntime.ControlPath(image)),Is.SameAs(image));
            var sibling=new ImagePanel(panel,"EditableImage");
            Assert.That(Intersect.Client.Interface.Shared.UiDesignerRuntime.ControlPath(image),Is.Not.EqualTo(Intersect.Client.Interface.Shared.UiDesignerRuntime.ControlPath(sibling)));
        }
        finally{document.SetValue(null,before);dirty.SetValue(null,wasDirty);Intersect.Client.Framework.Gwen.Input.InputHandler.MouseFocus=null;}
    }
    [Test]
    public void ClothesGalleryFiltersAllowedStylesAndSelectionUpdatesTheExistingAppearanceControl()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1000,700)};
        var combo=new LabeledComboBox(canvas,"AllowedHair");combo.AddItem("First hair",userData:"Hair_1.png");combo.AddItem("Second hair",userData:"Hair_2.png");
        var gallery=new Intersect.Client.Interface.Shared.ClothesGallery(canvas,new(){["Hair"]=combo},()=>{});gallery.SetBounds(0,0,438,500);gallery.Refresh();
        var search=Descendants(gallery).OfType<TextBox>().Single();search.Text="Second";
        var card=Descendants(gallery).OfType<Button>().Single(button=>button.Name.StartsWith("ClothesCard"));
        typeof(ControlBase).GetMethod("EmitVirtualClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .Invoke(card,[Intersect.Client.Framework.Gwen.Control.EventArguments.MouseButtonState.Empty]);
        Assert.That(combo.SelectedItem!.UserData,Is.EqualTo("Hair_2.png"));
        Assert.That(Descendants(gallery).OfType<ImagePanel>().Single().Height,Is.GreaterThanOrEqualTo(100));
    }

    [Test]
    public void OrdinaryCargoRevealsOneLotThenUnlocksCollectionAfterTheSequence()
    {
        Intersect.Client.WorldEvents.PremiumState.Reset();
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        using var canvas=new Canvas(skin){Size=new Point(800,600)};
        var window=new NavalCargoWindow(canvas);
        var ship=new ShipSnapshot{Id=Guid.NewGuid(),Cargo=Enumerable.Range(0,5).Select(_=>new NavalCargoParcel{ItemId=Guid.NewGuid(),Quantity=1}).ToArray()};
        window.Update(ship,true);
        var manifest=Descendants(window).OfType<ScrollControl>().Single(c=>c.Name=="CargoManifest");
        var collect=Descendants(window).OfType<Button>().Single(c=>c.Name=="TakeAll");
        Assert.That(manifest.Children.Count,Is.EqualTo(1));Assert.That(collect.IsDisabled,Is.True);
        typeof(NavalCargoWindow).GetField("_revealStarted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .SetValue(window,Environment.TickCount64-10000);
        window.Update(ship,true);
        Assert.That(manifest.Children.Count,Is.EqualTo(5));Assert.That(collect.IsDisabled,Is.False);
        Assert.That(ship.Cargo.Length,Is.EqualTo(5),"Revealing changes presentation and must not consume rewards.");
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
        Intersect.Client.WorldEvents.PremiumState.Apply(new() { RemainingMilliseconds = 60000 });
        try
        {
            var ship=new ShipSnapshot {Id=Guid.NewGuid(),CargoCapacity=20,
                Cargo=Enumerable.Range(0,20).Select(i=>new NavalCargoParcel {ItemId=item.Id,Quantity=1000000,Quality=i%5+1}).ToArray()};
            window.Update(ship,true);
            Assert.That(window.Width,Is.LessThan(width));Assert.That(window.Height,Is.LessThan(height));
            var scroll=Descendants(window).OfType<ScrollControl>().Single(control=>control.Name=="CargoManifest");
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
        finally {ItemDescriptor.Lookup.Delete(item); Intersect.Client.WorldEvents.PremiumState.Reset();}
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
    [TestCase(640,480)]
    [TestCase(1920,1080)]
    public void AmmoAndRepairPanelsScrollUseReadableCaptionsAndDoNotModifyLandInventory(int width,int height)
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(width,height)};
        var window=new NavalManagementWindow(canvas);window.Apply(new(){Mode=NavalManageAction.Ammo,
            Ammo=Enum.GetValues<CannonAmmoKind>().Select(k=>new NavalAmmoOffer {Kind=k,Quantity=15,MainQuantity=5,Available=true}).ToArray()});
        var scroll=Descendants(window).OfType<ScrollControl>().Single();Assert.That(scroll.CanScrollV,Is.True);
        Assert.That(Descendants(window).Any(c=>c.Name.Contains("Deposit") || c.Name.StartsWith("NavalTab")),Is.False);
        foreach(var b in Descendants(scroll).OfType<Button>())
        {
            if(b.Name.StartsWith("AmmoSlot"))continue;
            Assert.That(b.Text,Is.Not.Empty);Assert.That(b.Height,Is.GreaterThanOrEqualTo(30));
        }
        window.Apply(new(){Mode=NavalManageAction.Repair,RepairToken=Guid.NewGuid(),RepairCells=NavalRepairPuzzle.Create(NavalRepairKind.Caulking,5),RepairKind=NavalRepairKind.Caulking,RepairRequiredHits=5});
        var targets=Descendants(window).OfType<Button>().Where(b=>b.Name.StartsWith("HullCell")).ToArray();
        Assert.That(targets.Length,Is.EqualTo(18));Assert.That(Descendants(window).OfType<RepairPuzzleBoard>().Count(),Is.EqualTo(1));
        Assert.That(window.Width,Is.LessThan(width));Assert.That(window.Height,Is.LessThan(height));
        window.Apply(new(){Mode=NavalManageAction.CancelRepair});Assert.That(window.IsHidden,Is.True);
    }
    [Test] public void CargoActionsHaveFullHeightReadableChildLabelsAndFormattedNumbersUseSupportedSpaces()
    {
        var previous=System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-CA");
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(800,600)};
            var window=new NavalCargoWindow(canvas);window.Update(new(){Id=Guid.NewGuid(),Cargo=[new(){ItemId=Guid.NewGuid(),Quantity=1000000}]},true);
            foreach(var button in Descendants(window).OfType<Button>().Where(b=>b.Name is "CargoUnload" or "CargoDiscard"))
            {
                var label=Descendants(button).OfType<Label>().Single(l=>l.Name=="CargoActionCaption");Assert.That(label.Height,Is.EqualTo(22));Assert.That(label.Text,Is.Not.Empty);
            }
            var qty=Descendants(window).OfType<Label>().Single(l=>l.Name=="CargoQuantity");Assert.That(qty.Text,Does.Not.Contain("\u00a0").And.Not.Contain("\u202f"));
        }
        finally{System.Globalization.CultureInfo.CurrentCulture=previous;}
    }

    [Test] public void SealedManifestHidesContentsDisablesUnloadingAndDisplaysMaximumCapacity()
    {
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(900,700)};
        var window=new NavalCargoWindow(canvas);window.Update(new(){Id=Guid.NewGuid(),CargoCapacity=1,Cargo=[new(){ItemId=Guid.Empty,Quantity=0,Quality=3}]},true);
        Assert.That(Descendants(window).OfType<Button>().Single(b=>b.Name=="CargoUnload").IsDisabled,Is.True);
        Assert.That(Descendants(window).OfType<Label>().Single(l=>l.Name=="CargoQuantity").IsHidden,Is.True);
        Assert.That(Descendants(window).OfType<Label>().Single(l=>l.Name=="CargoSummary").Text,Does.Contain("MAX"));
        Assert.That(_content.Requests.Any(r=>r.Item1==TextureType.Item),Is.False);
    }
    [TestCase(800,600)][TestCase(1366,768)]
    public void RepairControlFitsBesideTheBottomCannonButtons(int width,int height)
    {
        var previous=Intersect.Client.General.Globals.Me;
        var me=(Intersect.Client.Entities.Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        try
        {
            Intersect.Client.General.Globals.Me=me;Intersect.Client.Naval.NavalWorld.Clear();
            using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(width,height)};
            var window=new NavalCannonViewWindow(canvas);window.Update(new(){Id=Guid.NewGuid(),CaptainId=Guid.NewGuid(),CrewIds=[me.Id],RepairAvailable=true},true);
            var controls=Descendants(window).OfType<Button>().Where(b=>new[]{"PortView","StarboardView","FireBroadside","CannonRepair","CloseCannonView"}.Contains(b.Name)).OrderBy(b=>b.X).ToArray();
            Assert.That(controls.Length,Is.EqualTo(5));Assert.That(controls.Select(b=>b.Y).Distinct().Count(),Is.EqualTo(1));
            Assert.That(controls.First().X,Is.GreaterThanOrEqualTo(0));Assert.That(controls.Last().Right,Is.LessThanOrEqualTo(width));
            Assert.That(controls.Single(b=>b.Name=="CannonRepair").IsHidden,Is.False);
            Assert.That(controls.Single(b=>b.Name=="CannonRepair").IsDisabled,Is.False);
        }
        finally{Intersect.Client.Naval.NavalWorld.Clear();Intersect.Client.General.Globals.Me=previous;}
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
        {DefaultFont=new TestFont();DefaultFontSize=12;Colors.Label.Normal=Colors.Label.Disabled=Color.White;Colors.Button.Normal=Colors.Button.Hover=Colors.Button.Active=Colors.Button.Disabled=Color.White;}
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
