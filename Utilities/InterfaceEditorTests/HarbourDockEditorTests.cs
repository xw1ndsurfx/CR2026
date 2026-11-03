using System.Reflection;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

internal static class HarbourDockEditorTests
{
    internal static void Run()
    {
        using var layers = new FrmMapLayers();
        var field = typeof(FrmMapLayers).GetField("AttributeTypeStrings", BindingFlags.Static | BindingFlags.NonPublic)!;
        var names = (Dictionary<MapAttributeType,string>)field.GetValue(null)!;
        if (!names.TryGetValue(MapAttributeType.Dock, out var name) || name != Strings.Naval.Dock.ToString())
            throw new InvalidOperationException("Dock must be selectable in map attributes.");
        if (layers.CreateAttribute(MapAttributeType.Dock) is not MapDockAttribute)
            throw new InvalidOperationException("Painting a quay must create a saved Dock attribute.");
        if (layers.CreateAttribute(MapAttributeType.Blocked) is not MapBlockedAttribute)
            throw new InvalidOperationException("Existing blocked brush must remain unchanged.");
        if (!names.ContainsKey(MapAttributeType.RainPuddle) || layers.CreateAttribute(MapAttributeType.RainPuddle) is not MapRainPuddleAttribute)
            throw new InvalidOperationException("Rain puddle must be selectable and paintable without changing existing attributes.");
        DockLandingPickerTests.Run();
        Console.WriteLine("PASS Quai Q is selectable and paintable; existing blocked brush preserved");
    }
}
