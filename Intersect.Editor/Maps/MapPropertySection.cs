using System.ComponentModel;
using System.Reflection;
using Intersect.Framework.Core.GameObjects.Maps;
using Newtonsoft.Json.Linq;

namespace Intersect.Editor.Maps;

/// <summary>A detached section snapshot; copies only its allowlisted settings, never map identity/content.</summary>
internal sealed class MapPropertySection
{
    internal string Category {get;}
    private readonly Dictionary<string,JToken> _values;
    private MapPropertySection(string category, Dictionary<string,JToken> values) {Category=category;_values=values;}

    private static string[] Fields(string category)
    {
        var names=TypeDescriptor.GetProperties(typeof(MapProperties)).Cast<PropertyDescriptor>()
            .Where(p=>p.Attributes[typeof(CategoryAttribute)] is CustomCategory c && c.Key==category && p.IsBrowsable && !p.IsReadOnly)
            .Select(p=>p.Name switch { "WeatherAlpha"=>nameof(MapDescriptor.WeatherIntensity),"FogAlpha"=>nameof(MapDescriptor.FogTransparency),_=>p.Name }).ToHashSet();
        if(category=="weather") { names.Add(nameof(MapDescriptor.WeatherEffects));names.Add(nameof(MapDescriptor.WeatherAnimationId)); }
        return names.Where(name=>typeof(MapDescriptor).GetProperty(name)?.GetSetMethod(true)!=null).ToArray();
    }
    internal static MapPropertySection? Capture(MapDescriptor map, string category)
    {
        var key=TypeDescriptor.GetProperties(typeof(MapProperties)).Cast<PropertyDescriptor>()
            .Select(p=>p.Attributes[typeof(CategoryAttribute)]).OfType<CustomCategory>()
            .FirstOrDefault(c=>c.Key==category || c.Category==category)?.Key;
        if(key==null)return null;
        var values=Fields(key).ToDictionary(name=>name,name=>
        {
            var value=typeof(MapDescriptor).GetProperty(name)!.GetValue(map);
            return value==null?JValue.CreateNull():JToken.FromObject(value);
        });
        return new(key,values);
    }
    internal void Apply(MapDescriptor map)
    {
        // Deserialize before applying so each destination owns its own mutable objects.
        var values=Fields(Category).Where(_values.ContainsKey).Select(name=>
        {
            var property=typeof(MapDescriptor).GetProperty(name)!;
            return (Property:property,Value:_values[name].ToObject(property.PropertyType));
        }).ToArray();
        foreach(var entry in values)entry.Property.SetValue(map,entry.Value);
    }
}

partial class MapProperties
{
    internal MapPropertySection? CopySection(string category)=>MapPropertySection.Capture(mMyMap,category);
    internal void PasteSection(MapPropertySection section)
    {
        General.Globals.MapEditorWindow?.PrepUndoState();
        section.Apply(mMyMap);
        Core.Graphics.TilePreviewUpdated=true;
        General.Globals.MapEditorWindow?.AddUndoState();
    }
}
