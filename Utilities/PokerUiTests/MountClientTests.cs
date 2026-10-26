using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Maps;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Core;
using Intersect.Framework.Core.Mounts;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class MountClientTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var context = ApplicationContext.Context.Value; var oldContent = Globals.ContentManager;
        var renderer = Intersect.Client.Core.Graphics.Renderer;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        try
        {
            Intersect.Client.Core.Graphics.Renderer = null!;
            var content = new TestContentManager(); Globals.ContentManager = content;
            var config = MountConfiguration.Prototype(); var mount = config.Mounts[0]; mount.Bob = 0;
            var animal = DispatchProxy.Create<IGameTexture, MountTextureFixture>();
            content.AddImageTexture(mount.Idle, animal); content.AddImageTexture(mount.Loop, animal);
            var body = DispatchProxy.Create<IGameTexture, MountTextureFixture>();
            var bodyFixture = (MountTextureFixture)(object)body;
            bodyFixture.Width = 128; bodyFixture.Height = 256; bodyFixture.FrameWidth = 32; bodyFixture.FrameHeight = 64;
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(player);
            var map = DispatchProxy.Create<IMapInstance, EventInteractionMapFixture>();
            player.LatestMap = map;
            typeof(Entity).GetField("mOrigin", Hidden)!.SetValue(player, new Vector2(116, 232));
            var apply = typeof(Player).GetMethod("ApplyMountJson", Hidden)!;
            apply.Invoke(player, [config.ToJson()]);
            Check(player.ActiveMount?.Id == mount.Id, "An authoritative entity snapshot installs the remote player's mount.");
            var begin = typeof(Entity).GetMethod("BeginMountDraw", Hidden)!;
            var geometry = typeof(Entity).GetMethod("RiderLayerGeometry", Hidden)!;
            var src = new FloatRect(0, 0, 32, 64); var dst = new FloatRect(100, 200, 32, 64);
            begin.Invoke(player, [body, src, dst, Color.White]);
            var first = ((FloatRect Source, FloatRect Destination))geometry.Invoke(player, [src, dst])!;
            var clothing = ((FloatRect Source, FloatRect Destination))geometry.Invoke(player, [src, dst])!;
            Check(first.Destination.Equals(clothing.Destination) && first.Source.Equals(clothing.Source),
                "The body and its clothing share the same rider placement and clipping plane.");
            var hat = ((FloatRect Source, FloatRect Destination))geometry.Invoke(player, [new FloatRect(0, 0, 64, 64), new FloatRect(84, 200, 64, 64)])!;
            Check(Math.Abs((hat.Destination.X + 32) - (first.Destination.X + 16)) < .01,
                "Different paperdoll canvas widths remain centered on the body.");
            Check(first.Destination.Height < 64 && first.Source.Height < 64, "Lower-leg masking crops source and destination proportionally.");
            var emptyBody = DispatchProxy.Create<IGameTexture, MountTextureFixture>();
            var emptyFixture = (MountTextureFixture)(object)emptyBody; emptyFixture.Empty = true;
            begin.Invoke(player, [emptyBody, src, dst, Color.White]);
            var empty = ((FloatRect Source, FloatRect Destination))geometry.Invoke(player, [src, dst])!;
            Check(float.IsFinite(empty.Destination.X) && float.IsFinite(empty.Destination.Y), "An empty body frame keeps a finite canvas anchor for paperdolls.");
            apply.Invoke(player, [""]); begin.Invoke(player, [body, src, dst, Color.White]);
            var walking = ((FloatRect Source, FloatRect Destination))geometry.Invoke(player, [src, dst])!;
            Check(walking.Source.Equals(src) && walking.Destination.Equals(dst), "Unmounted drawing is unchanged.");
            apply.Invoke(player, ["invalid"]); Check(player.ActiveMount == null, "Invalid mount snapshots fall back to the ordinary player renderer.");
            using var metrics = new MetricsRenderer(); using var skin = new TestSkin(metrics); using var canvas = new Canvas(skin);
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.MountsWindow", true)!;
            var window = (ControlBase)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [canvas], null)!;
            var state = new MountStatePacket { ConfigurationJson = config.ToJson(), Owned = [mount.Id] };
            type.GetMethod("Apply", Hidden)!.Invoke(window, [state]);
            Check(Descendants(window).Any(c => c.Name == "MountRide" + mount.Id && !c.IsDisabled), "Unlocked cards offer a ride action.");
            state.Owned = []; type.GetMethod("Apply", Hidden)!.Invoke(window, [state]);
            Check(Descendants(window).Any(c => c.Name == "MountRide" + mount.Id && c.IsDisabled), "Locked cards cannot submit a ride action.");
            System.Console.WriteLine("PASS mounts client: authoritative snapshots, shared body/paperdoll geometry, padded layers, clipping, unmounted fallback and collection actions");
            var joustType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.JoustWindow", true)!;
            var joust = (ControlBase)Activator.CreateInstance(joustType, Hidden, null, [canvas], null)!;
            foreach (var phase in new[] { Intersect.Framework.Core.Jousting.JoustPhase.Charge, Intersect.Framework.Core.Jousting.JoustPhase.Aim,
                Intersect.Framework.Core.Jousting.JoustPhase.Impact, Intersect.Framework.Core.Jousting.JoustPhase.Results })
            {
                var view = new Intersect.Framework.Core.Jousting.JoustView { MatchId = mount.Id, Phase = phase, Pass = 1, Passes = 3 };
                joustType.GetMethod("Apply", Hidden)!.Invoke(joust, [new JoustStatePacket { ViewJson = System.Text.Json.JsonSerializer.Serialize(view) }]);
                var viewport = (ControlBase)joustType.GetField("_view", Hidden)!.GetValue(joust)!;
                Check(viewport.IsHidden == (phase == Intersect.Framework.Core.Jousting.JoustPhase.Charge),
                    "The map remains visible during charge and the impact animation remains visible in the aim viewport.");
            }
            var offer = new Intersect.Framework.Core.Jousting.JoustOffer { ItemId = Guid.NewGuid() };
            var joustConfig = new Intersect.Framework.Core.Jousting.JoustConfiguration { Economy = new() { Offers = [offer] } };
            joustType.GetMethod("Apply", Hidden)!.Invoke(joust, [new JoustStatePacket {
                ConfigurationJson = joustConfig.ToJson(), CareerJson = System.Text.Json.JsonSerializer.Serialize(new Intersect.Framework.Core.Jousting.JoustCareer { Medals = 42, Wins = 2, Matches = 3 }),
                ViewJson = System.Text.Json.JsonSerializer.Serialize(new Intersect.Framework.Core.Jousting.JoustView { Phase = Intersect.Framework.Core.Jousting.JoustPhase.Finished }) }]);
            var shop = (ScrollControl)joustType.GetField("_shop", Hidden)!.GetValue(joust)!;
            var career = (Label)joustType.GetField("_career", Hidden)!.GetValue(joust)!;
            Check(!shop.IsHidden && career.Text.Contains("42") && Descendants(shop).Any(c => c.Name == "Offer" + offer.Id), "Finished duels show the authoritative career and configured medal shop.");
        }
        finally { ApplicationContext.Context.Value = context; Globals.ContentManager = oldContent; Intersect.Client.Core.Graphics.Renderer = renderer; }
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase parent)
    { foreach (var child in parent.Children) { yield return child; foreach (var c in Descendants(child)) yield return c; } }
}
public class MountTextureFixture : DispatchProxy
{
    public bool Empty;
    public int Width = 512, Height = 512, FrameWidth = 128, FrameHeight = 128;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "get_Width") return Width;
        if (method?.Name == "get_Height") return Height;
        if (method?.Name == "GetPixel")
        {
            var x = (int)args![0]! % FrameWidth; var y = (int)args[1]! % FrameHeight;
            return new Color(!Empty && x >= FrameWidth / 4 && x < FrameWidth * 3 / 4 && y >= FrameHeight / 4 && y < FrameHeight * 3 / 4 ? 255 : 0, 255, 255, 255);
        }
        return null;
    }
}
