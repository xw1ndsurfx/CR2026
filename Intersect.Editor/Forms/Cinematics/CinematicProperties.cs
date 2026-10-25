using System.ComponentModel;
using System.Globalization;
using Intersect.Framework.Core.Cinematics;
namespace Intersect.Editor.Forms.Cinematics;

internal sealed class CinematicProperties(object value) : CustomTypeDescriptor
{
    internal object Value => value;
    public override object GetPropertyOwner(PropertyDescriptor? pd) => value;
    public override PropertyDescriptorCollection GetProperties() => GetProperties([]);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var visible = value is CinematicClip clip ? clip.Track switch
        {
            CinematicTrack.Camera => new[] { "X", "Y", "Zoom", "Ease", "FollowActor", "ActorId", "OffsetX", "OffsetY" },
            CinematicTrack.Presentation => ["HideHud", "Letterbox", "FadeOpacity", "ShakePixels", "Ease"],
            CinematicTrack.Movement => ["ActorId", "Speed"],
            CinematicTrack.Animation => ["ResourceId", "X", "Y", "AttachToActor", "ActorId", "AnimationOffsetX", "AnimationOffsetY"],
            CinematicTrack.Dialogue => ["Text", "Face"],
            CinematicTrack.Choice => ["Text", "Face", "Option1", "Option2", "Option3", "Option4", "ChoiceEvent1", "ChoiceEvent2", "ChoiceEvent3", "ChoiceEvent4"],
            CinematicTrack.Sound => ["AudioFile", "VoiceSound"],
            CinematicTrack.Music => ["AudioFile"],
            CinematicTrack.Event => ["ResourceId", "AllowPlayerControl"],
            _ => [],
        } : null;
        var descriptors = TypeDescriptor.GetProperties(value, attributes ?? []).Cast<PropertyDescriptor>()
            .Where(p => p.IsBrowsable && (visible == null || visible.Contains(p.Name) || p.Name is "Name" or "Track" or "StartMs" or "DurationMs"))
            .Select(p => (PropertyDescriptor)new LocalizedProperty(p, value)).ToArray();
        return new(descriptors, true);
    }
    private sealed class LocalizedProperty(PropertyDescriptor inner, object value) : PropertyDescriptor(inner)
    {
        public override Type ComponentType => inner.ComponentType;
        public override bool IsReadOnly => inner.IsReadOnly || Name is "MapId" or "Version" or "Track" or "TargetId";
        public override Type PropertyType => inner.PropertyType;
        public override TypeConverter Converter => PropertyType.IsEnum ? new LocalizedEnum(PropertyType) : base.Converter;
        public override string DisplayName => CinematicText.T(Name);
        public override bool CanResetValue(object component) => inner.CanResetValue(value);
        public override object? GetValue(object? component) => inner.GetValue(value);
        public override void ResetValue(object component) => inner.ResetValue(value);
        public override void SetValue(object? component, object? newValue) => inner.SetValue(value, newValue);
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(value);
    }
    private sealed class LocalizedEnum(Type type) : EnumConverter(type)
    {
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
            destinationType == typeof(string) && value != null ? CinematicText.T(value.ToString()!) : base.ConvertTo(context, culture, value, destinationType);
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string text)
                foreach (var name in Enum.GetNames(type))
                    if (CinematicText.T(name) == text) return Enum.Parse(type, name);
            return base.ConvertFrom(context, culture, value);
        }
    }
}
