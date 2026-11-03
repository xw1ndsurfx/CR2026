using System.ComponentModel;
using System.Globalization;
using Intersect.Editor.General;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using EventDescriptor = Intersect.Framework.Core.GameObjects.Events.EventDescriptor;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Cinematics;

// Keep serialized identifiers in the source, but show authored names in the UI.
internal sealed class CinematicChapterProperties(object value, CinematicChapter chapter, Guid chapterEventId, bool advanced) : CustomTypeDescriptor
{
    internal sealed record Resource(Guid Id, string Name);
    private static string T(string key) => CinematicText.T(key);
    public override object GetPropertyOwner(PropertyDescriptor? pd) => value;
    public override PropertyDescriptorCollection GetProperties() => GetProperties([]);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var names = value is CinematicChapter
            ? new List<string> { "Name", "Project", "Saga", "AllowReplay", "StartQuestId" }
            : new List<string> { "Name", "MapId", "SceneEventId" };
        if (value is CinematicChapter && chapter.StartQuestId != Guid.Empty) names.Add("OfferQuest");
        if (advanced)
        {
            if (value is CinematicChapter)
            {
                names.AddRange(["UnlockVariableId", "CompletionVariableId", "CompletionEventId"]);
                if (chapter.UnlockVariableId != Guid.Empty) names.Add("UnlockValue");
            }
            else names.AddRange(["BeforeEventId", "AfterEventId"]);
        }
        return new(TypeDescriptor.GetProperties(value, attributes ?? []).Cast<PropertyDescriptor>()
            .Where(p => names.Contains(p.Name)).Select(p => (PropertyDescriptor)new NamedProperty(p, value, Choices(p.Name))).ToArray(), true);
    }

    private IEnumerable<Resource>? Choices(string property)
    {
        switch (property)
        {
            case "UnlockVariableId":
                return new[] { new Resource(Guid.Empty, T("ChapterNoUnlock")) }.Concat(Variables(chapter.CompletionVariableId));
            case "CompletionVariableId":
                return new[] { new Resource(Guid.Empty, T("ChapterAutomaticProgress")) }.Concat(Variables(chapter.UnlockVariableId));
            case "StartQuestId":
                return new[] { new Resource(Guid.Empty, T("ChapterNoQuest")) }.Concat(
                    QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>().Select(q => new Resource(q.Id, q.Name)));
            case "BeforeEventId": case "AfterEventId": case "CompletionEventId":
                return new[] { new Resource(Guid.Empty, T("ChapterNoEvent")) }.Concat(
                    EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent && e.Id != chapterEventId)
                        .Select(e => new Resource(e.Id, e.Name)));
            case "MapId":
                return MapList.OrderedMaps.Select(m => new Resource(m.MapId, m.Name)).Concat(
                    Globals.CurrentMap is { } map ? [new Resource(map.Id, map.Name)] : []);
            case "SceneEventId":
                return EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Concat(
                    Globals.CurrentMap?.LocalEvents.Values.AsEnumerable() ?? []).Where(e => e.Pages.Count == 1 && e.Pages[0].Cinematic != null)
                    .Select(e => new Resource(e.Id, e.Name));
            default: return null;
        }
    }

    private static IEnumerable<Resource> Variables(Guid excluded) => PlayerVariableDescriptor.Lookup.Values
        .OfType<PlayerVariableDescriptor>().Where(v => v.DataType == VariableDataType.Boolean && v.Id != excluded)
        .Select(v => new Resource(v.Id, v.Name));

    private sealed class NamedProperty(PropertyDescriptor inner, object owner, IEnumerable<Resource>? resources)
        : PropertyDescriptor(inner, [RefreshPropertiesAttribute.All])
    {
        public override Type ComponentType => inner.ComponentType;
        public override Type PropertyType => inner.PropertyType;
        public override bool IsReadOnly => inner.IsReadOnly || Name is "MapId" or "SceneEventId";
        public override string DisplayName => owner is CinematicChapterScene && Name == "Name" ? T("Name") : T("ChapterField" + Name);
        public override string Description => owner is CinematicChapterScene && Name == "Name" ? "" : T("ChapterHelp" + Name);
        public override TypeConverter Converter => resources != null ? new ResourceNames(resources) : base.Converter;
        public override bool CanResetValue(object component) => false;
        public override object? GetValue(object? component) => inner.GetValue(owner);
        public override void ResetValue(object component) { }
        public override void SetValue(object? component, object? newValue) => inner.SetValue(owner, newValue);
        public override bool ShouldSerializeValue(object component) => false;
    }

    internal sealed class ResourceNames(IEnumerable<Resource> resources) : GuidConverter
    {
        private readonly Resource[] _resources = resources.GroupBy(r => r.Id).Select(g => g.First())
            .OrderBy(r => r.Id != Guid.Empty).ThenBy(r => r.Name).ThenBy(r => r.Id).ToArray();
        private string Label(Resource resource)
        {
            var name = string.IsNullOrWhiteSpace(resource.Name) ? T("ChapterUnnamedResource") : resource.Name;
            return _resources.Count(r => r.Name == resource.Name) > 1
                ? name + " (" + (Array.IndexOf(_resources, resource) + 1) + ")" : name;
        }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(_resources.Select(r => r.Id).ToArray());
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? source, Type destinationType) =>
            destinationType == typeof(string) && source is Guid id
                ? _resources.FirstOrDefault(r => r.Id == id) is { } resource ? Label(resource) : T("ChapterMissingResource")
                : base.ConvertTo(context, culture, source, destinationType);
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object source)
        {
            if (source is string text && _resources.FirstOrDefault(r => Label(r) == text) is { } resource) return resource.Id;
            // A GUID typed or pasted into the main authoring UI is not a resource choice.
            throw new FormatException(T("ChapterChooseNamedResource"));
        }
    }
}
