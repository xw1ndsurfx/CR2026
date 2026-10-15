using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Maps;

internal static class EntityAnchorTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }

    private static void RunCore()
    {
        var body = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity)); GC.SuppressFinalize(body);
        var map = DispatchProxy.Create<IMapInstance, EventInteractionMapFixture>();
        body.MapId = ((EventInteractionMapFixture)(object)map).MapId; body.LatestMap = map;
        typeof(Entity).GetField("TileWidth", Hidden)!.SetValue(body, Options.Instance.Map.TileWidth);
        typeof(Entity).GetField("TileHeight", Hidden)!.SetValue(body, Options.Instance.Map.TileHeight);
        typeof(Entity).GetField("mOrigin", Hidden)!.SetValue(body, new Vector2(116, 232));
        var texture = DispatchProxy.Create<IGameTexture, DirectionalBodyFixture>();
        var empty = DispatchProxy.Create<IGameTexture, CpuFrameTexture>();
        ((CpuFrameTexture)(object)empty).Right = 0;
        var destinationMethod = typeof(Entity).GetMethod("SpriteDestination", Hidden)!;
        var include = typeof(Entity).GetMethod("IncludeVisibleGraphicTop", Hidden)!;
        var equipment = typeof(Entity).GetMethod("IncludeEquipmentGraphicTop", Hidden)!;
        var graphicTop = typeof(Entity).GetProperty("RenderedGraphicTop", Hidden)!;
        for (var direction = 0; direction < 4; direction++)
        {
            var source = new FloatRect(0, direction * 128, 128, 128);
            var destination = (FloatRect)destinationMethod.Invoke(body, [texture, source])!;
            Check(Math.Abs(destination.X + 64 - 116) <= .5f && destination.Y + 80 - direction == 232,
                "Visible player feet must match the event/collision tile in all four directions");
            graphicTop.SetValue(body, null); include.Invoke(body, [texture, source, destination]);
            var top = body.GetTop();
            Check(top == destination.Y + 40 + direction, "Labels must follow the visible directional body");
            equipment.Invoke(body, [empty, new FloatRect(0, 0, 64, 64), new FloatRect(0, -1000, 64, 64)]);
            Check(body.GetTop() == top, "Empty upward-facing equipment must not lift the label stack");
            equipment.Invoke(body, [texture, source, new FloatRect(0, -1000, 128, 128)]);
            Check(body.GetTop() == top, "Raised weapons must not lift the name away from the character's head");
        }

        var resource = (Resource)RuntimeHelpers.GetUninitializedObject(typeof(Resource)); GC.SuppressFinalize(resource);
        resource.X = 3; resource.Y = 4;
        var resourceType = typeof(Resource);
        resourceType.GetField("_tileWidth", Hidden)!.SetValue(resource, Options.Instance.Map.TileWidth);
        resourceType.GetField("_tileHeight", Hidden)!.SetValue(resource, Options.Instance.Map.TileHeight);
        resourceType.GetField("_renderBoundsSrc", Hidden)!.SetValue(resource, new FloatRect(0, 0, 128, 128));
        resourceType.GetField("_renderBoundsDest", Hidden)!.SetValue(resource, new FloatRect(0, 0, 128, 128));
        resourceType.GetMethod("AlignResourceGraphic", Hidden)!.Invoke(resource, [map, texture]);
        var drawn = (FloatRect)resourceType.GetField("_renderBoundsDest", Hidden)!.GetValue(resource)!;
        Check(drawn.X + 64 == 1000 + 3 * Options.Instance.Map.TileWidth + Options.Instance.Map.TileWidth / 2f &&
            drawn.Y + 80 == -640 + 5 * Options.Instance.Map.TileHeight,
            "Resource graphic and harvest tile must share the player's visible-foot anchor");
        System.Console.WriteLine("PASS shared anchors: four player directions, empty equipment labels and resource harvest placement");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

public class DirectionalBodyFixture : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "get_Width") return 128;
        if (method?.Name == "get_Height") return 512;
        if (method?.Name == "GetPixel")
        {
            var x = (int)args![0]!; var y = (int)args[1]!; var row = y / 128; var localY = y % 128;
            return new Color(x >= 55 && x < 73 && localY >= 40 + row && localY < 80 - row ? 255 : 0, 255, 255, 255);
        }
        return null;
    }
}
