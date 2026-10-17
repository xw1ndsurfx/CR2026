using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Core.Sounds;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;
using Intersect.Network.Packets.Server;
using MessagePack;
using Newtonsoft.Json;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
internal static class HousingSoundTests
{
    [ModuleInitializer] internal static void Run() { try { Test(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static void Check(bool value,string text) { if(!value)throw new Exception(text); }
    private static IEnumerable<ControlBase> Desc(ControlBase root) { foreach(var child in root.Children) { yield return child; foreach(var nested in Desc(child))yield return nested; } }
    private sealed class TestSound : ISound
    {
        public bool Loaded { get; set; } = true;
        public bool Loop { get; set; }
        public bool Playing = true;
        public bool Stopped;
        public bool Update() => Playing;
        public void Stop() { Stopped = true; Playing = false; }
    }
    private static void Test()
    {
        var assembly = typeof(GameInterface).Assembly;
        var type=assembly.GetType("Intersect.Client.Interface.Game.SoundSequencePlayer",true)!;
        var sounds=new List<(string File,TestSound Sound)>();
        Func<string,ISound> create = file => { var sound=new TestSound { Loaded=file!="missing.wav" };sounds.Add((file,sound));return sound; };
        var sequence=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[create],null)!;
        var play=type.GetMethod("Play")!;var update=type.GetMethod("Update")!;
        play.Invoke(sequence,["first.wav","second.wav"]);update.Invoke(sequence,null);
        Check(sounds.Count==1&&sounds[0].File=="first.wav","Second invasion sound must wait for the first to finish");
        sounds[0].Sound.Playing=false;update.Invoke(sequence,null);
        Check(sounds.Count==2&&sounds[1].File=="second.wav"&&sounds[0].Sound.Stopped,"Sequence advances only after actual playback completion");
        type.GetMethod("Stop")!.Invoke(sequence,null);Check(sounds[1].Sound.Stopped,"Leaving the game stops pending invasion audio");
        sounds.Clear();play.Invoke(sequence,["missing.wav","second.wav"]);Check(sounds.Count==2,"Missing first audio cannot stall the second sound");
        type.GetMethod("Stop")!.Invoke(sequence,null);
        var packet=MessagePackSerializer.Deserialize<PlaySoundSequencePacket>(MessagePackSerializer.Serialize(new PlaySoundSequencePacket { First="first.wav",Second="second.wav" }));
        Check(packet.First=="first.wav"&&packet.Second=="second.wav","Loss sound sequence preserves ordering across the protocol");
        _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
        skin.Colors.Button.Normal=skin.Colors.Button.Disabled=skin.Colors.Button.Active=skin.Colors.Button.Hover=Color.White;
        using var canvas=new Canvas(skin){Size=new Point(1280,720)};
        var furniture=new FurnitureDefinition { Name="Chair",Tileset="test.png",CurrencyItemId=Guid.NewGuid(),Months="2026-10" };
        var view=new HouseView { OwnerId=Guid.NewGuid(),OwnerName="Owner",Enabled=true,Inside=true,IsOwner=true,Month="2026-10",Catalog=[furniture] };
        view.House.Furniture[furniture.Id]=new(){Definition=furniture,Quantity=2};
        view.House.PlaceFurniture(furniture.Id,Guid.Empty,2,3,1,32,26,100);
        view.House.Pets.Add(new HousePet { Id=Guid.NewGuid(),Name="Buddy" });
        var windowType=assembly.GetType("Intersect.Client.Interface.Game.HousingWindow",true)!;
        var window=(WindowControl)Activator.CreateInstance(windowType,[canvas])!;
        windowType.GetMethod("Apply")!.Invoke(window,[view,"House saved."]);window.Show();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var housingWorld = assembly.GetType("Intersect.Client.Interface.Game.HousingWorld", true)!;
        var worldView = housingWorld.GetProperty("View", BindingFlags.Static | BindingFlags.NonPublic)!;
        var preview = housingWorld.GetProperty("Preview", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldView = worldView.GetValue(null); var oldPreview = preview.GetValue(null);
        try
        {
            worldView.SetValue(null, view);
            var overlayType = typeof(GameInterface).GetNestedType("HousePlacementOverlay", BindingFlags.NonPublic)!;
            using var overlay = (ControlBase)Activator.CreateInstance(overlayType, [canvas])!;
            overlayType.GetMethod("Begin")!.Invoke(overlay, [furniture.Id, Guid.Empty]);
            Check(!overlay.IsHidden && preview.GetValue(null) != null, "Choosing Place activates the furniture preview");
            overlayType.GetMethod("OnMouseWheeled", flags)!.Invoke(overlay, [120]);
            var active = preview.GetValue(null)!;
            Check((int)active.GetType().GetProperty("Rotation")!.GetValue(active)! == 1, "Mouse wheel rotates the live placement preview");
            overlayType.GetMethod("OnMouseClicked", flags)!.Invoke(overlay, [Intersect.Client.Framework.Input.MouseButton.Right, new Point(), true]);
            Check(overlay.IsHidden && preview.GetValue(null) == null, "Right click cancels both placement and its ghost");
            var placed = view.House.Placements.Single();
            overlayType.GetMethod("Begin")!.Invoke(overlay, [furniture.Id, placed.Id]);
            active = preview.GetValue(null)!;
            Check((int)active.GetType().GetProperty("Rotation")!.GetValue(active)! == placed.Rotation, "Moving furniture starts at its existing rotation");
            view.IsOwner = false;
            overlayType.GetMethod("Begin")!.Invoke(overlay, [furniture.Id, Guid.Empty]);
            Check(overlay.IsHidden && preview.GetValue(null) == null, "Visitors cannot activate a house placement preview");
            view.IsOwner = true;
            canvas.RemoveChild(overlay, false); // The using scope disposes this overlay before the canvas.
        }
        finally { worldView.SetValue(null, oldView); preview.SetValue(null, oldPreview); }

        var layout=typeof(ControlBase).GetMethod("RecurseLayout",flags)!;
        for(var pass=0;pass<4;pass++)layout.Invoke(window,[skin]);
        var origin=window.ToCanvas(0,0);
        foreach(var button in window.Children.OfType<Button>())
        {
            var point=button.ToCanvas(0,0)-origin;
            Check(point.X>=0&&point.Y>=0&&point.X+button.Width<=window.Width&&point.Y+button.Height<=window.Height,$"House action {button.Text} at {point.X},{point.Y} ({button.Width}x{button.Height}) must fit {window.Width}x{window.Height}");
        }
        Check(Desc(window).OfType<Button>().Any(b=>b.Text=="Buy"),"Monthly catalog exposes the purchase action");
        view.IsOwner=false;view.Catalog=[];
        windowType.GetMethod("Apply")!.Invoke(window,[view,""]);
        var tabs=(Button[])windowType.GetField("_tabs",flags)!.GetValue(window)!;
        Check(tabs[0].IsHidden&&tabs[1].IsHidden&&!Desc(window).OfType<Button>().Any(b=>b.Text=="Move"||b.Text=="Store"),"Visitors have no purchase or decoration controls for another house");
        var state=MessagePackSerializer.Deserialize<HouseStatePacket>(MessagePackSerializer.Serialize(new HouseStatePacket { Json=JsonConvert.SerializeObject(view),WorldJson=JsonConvert.SerializeObject(view) }));
        Check(JsonConvert.DeserializeObject<HouseView>(state.WorldJson)!.House.Placements.Count==1,"Placed furniture survives house snapshot transport");
        var petsType=assembly.GetType("Intersect.Client.Interface.Game.PetsWindow",true)!;
        var pets=(WindowControl)Activator.CreateInstance(petsType,[canvas,false])!;
        var pet=new PetProfileEntry { PetId=Guid.NewGuid(),Name="Buddy",RecoverySeconds=300,MaximumHealth=10,MaximumMana=1,Stats=new int[6] };
        petsType.GetMethod("Apply")!.Invoke(pets,[new PetStatePacket { Pets=[pet] }]);
        var summon=Desc(pets).OfType<Button>().Single(b=>b.Name=="PetSummon"+pet.PetId);
        Check(summon.IsDisabled&&Desc(pets).OfType<Button>().Any(b=>b.Name=="PetHome"+pet.PetId),"Recovering pets cannot be summoned and owned pets can be sent home");
        var home=Desc(pets).OfType<Button>().Single(b=>b.Name=="PetHome"+pet.PetId);
        var homeCaption=home.Children.OfType<Label>().Single();
        Check(homeCaption.Font!=null&&homeCaption.FontSize>=10&&homeCaption.Text.Length>0&&renderer.MeasureText(homeCaption.Font,homeCaption.FontSize,homeCaption.Text).X<=homeCaption.Width,"Send pet home has an explicit readable font and enough caption width");
        System.Console.WriteLine("PASS house UI ownership controls, native layout, snapshot persistence, pet recovery controls and actual sequential sound completion");
    }
}
