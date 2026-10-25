using System.ComponentModel;
using System.Globalization;
using Intersect.Framework.Core.Cinematics;
namespace Intersect.Editor.Forms.Cinematics;

internal sealed class CinematicProperties(object value, bool vesselPoint = false, bool vesselMovement = false, CinematicScene? scene = null) : CustomTypeDescriptor
{
    internal object Value => value;
    public override object GetPropertyOwner(PropertyDescriptor? pd) => value;
    public override PropertyDescriptorCollection GetProperties() => GetProperties([]);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var visible = value is CinematicClip clip ? clip.Track switch
        {
            CinematicTrack.Camera => new[] { "X", "Y", "Zoom", "Ease", "FollowActor", "ActorId", "OffsetX", "OffsetY",
                "Projection", "Heading", "EyeHeight", "PitchDegrees", "FieldOfView" },
            CinematicTrack.Presentation => ["HideHud", "Letterbox", "FadeOpacity", "ShakePixels", "Ease"],
            CinematicTrack.Actors => ["ActorId", "ActorVisible"],
            CinematicTrack.Movement => vesselMovement ? ["ActorId", "Ease"] : ["ActorId", "Speed"],
            CinematicTrack.Naval => clip.VesselAction == CinematicVesselAction.VesselAppearance
                ? ["ActorId", "VesselAction", "VesselAppearance", "SynchronizedGroup"]
                : clip.VesselAction is CinematicVesselAction.FirePort or CinematicVesselAction.FireStarboard
                    ? ["ActorId", "VesselAction", "SynchronizedGroup"]
                    : clip.VesselAction == CinematicVesselAction.VesselHullImpact
                        ? ["ActorId", "VesselAction", "ResourceId", "AnimationOffsetX", "AnimationOffsetY", "SynchronizedGroup"]
                        : ["ActorId", "VesselAction", "X", "Y", "ResourceId"],
            CinematicTrack.Animation => ["ResourceId", "X", "Y", "AttachToActor", "ActorId", "AnimationOffsetX", "AnimationOffsetY", "SynchronizedGroup"],
            CinematicTrack.Projectile => ["ResourceId", "ImpactAnimationId", "ActorId", "X", "Y", "Ease", "ArcHeight", "RotateWithPath", "SynchronizedGroup"],
            CinematicTrack.Dialogue => ["Text", "Face"],
            CinematicTrack.Choice => ["Text", "Face", "Option1", "Option2", "Option3", "Option4", "ChoiceEvent1", "ChoiceEvent2", "ChoiceEvent3", "ChoiceEvent4"],
            CinematicTrack.Sound => ["AudioFile", "VoiceSound"],
            CinematicTrack.Music => ["AudioFile"],
            CinematicTrack.Event => ["ResourceId", "AllowPlayerControl"],
            CinematicTrack.Environment => ["RestoreEnvironment"],
            CinematicTrack.Gameplay => clip.GameplayAction == CinematicGameplayAction.CommonEvent
                ? ["GameplayAction", "ResourceId", "AllowPlayerControl"] : ["GameplayAction", "VariableId", "ExpectedValue"],
            CinematicTrack.Condition => clip.ConditionKind == CinematicConditionKind.PlayerFlag
                ? ["ConditionKind", "VariableId", "ExpectedValue", "ConditionTimeoutMs", "AllowPlayerControl"]
                : clip.ConditionKind is CinematicConditionKind.QuestTaskActive or CinematicConditionKind.QuestAfterTask
                    ? ["ConditionKind", "QuestId", "TaskId", "ConditionTimeoutMs", "AllowPlayerControl"]
                    : ["ConditionKind", "QuestId", "ConditionTimeoutMs", "AllowPlayerControl"],
            CinematicTrack.Quest => clip.QuestAction == CinematicQuestAction.StartOrOffer
                ? ["QuestAction", "QuestId", "OfferQuest"] : ["QuestAction", "QuestId", "TaskId"],
            _ => [],
        } : null;
        if (value is CinematicActor actor && actor.Kind == CinematicActorKind.PersonalEvent)
            visible = ["Name", "TargetId", "StartX", "StartY", "Kind", "GroupName"];
        else if (value is CinematicActor {Kind:CinematicActorKind.TemporaryNpc})
            visible = ["Name", "TargetId", "StartX", "StartY", "Kind", "NPCId", "VisibleAtStart", "HideName", "GroupName"];
        else if (value is CinematicActor {Kind:CinematicActorKind.TemporaryVessel})
            visible = ["Name", "TargetId", "StartX", "StartY", "Kind", "ShipModelKey", "StartHeading", "VesselAppearance", "VisibleAtStart", "HideName", "GroupName"];
        else if (value is CinematicPoint && vesselPoint) visible=["X", "Y"];
        if (value is CinematicClip { Track: CinematicTrack.Camera } camera)
            visible = visible!.Where(name =>
                (camera.FollowActor || name is not ("ActorId" or "OffsetX" or "OffsetY")) &&
                (camera.Projection == CinematicProjection.Perspective2_5D || name is not ("Heading" or "EyeHeight" or "PitchDegrees" or "FieldOfView"))).ToArray();
        var descriptors = TypeDescriptor.GetProperties(value, attributes ?? []).Cast<PropertyDescriptor>()
            .Where(p => p.IsBrowsable && p.Name is not ("Version" or "TargetId" or "Track") &&
                (visible == null || visible.Contains(p.Name) || p.Name is "Name" or "StartMs" or "DurationMs"))
            .Select(p => (PropertyDescriptor)new LocalizedProperty(p, value, scene)).ToArray();
        return new(descriptors, true);
    }
    private sealed class LocalizedProperty(PropertyDescriptor inner, object value, CinematicScene? scene) : PropertyDescriptor(inner,[RefreshPropertiesAttribute.All])
    {
        public override Type ComponentType => inner.ComponentType;
        public override bool IsReadOnly => inner.IsReadOnly || Name is "MapId" or "Version" or "Track" or "TargetId" or "Kind";
        public override Type PropertyType => inner.PropertyType;
        public override TypeConverter Converter => Name == "ActorId" && scene != null
            ? new ActorChoices(scene, value as CinematicClip) : PropertyType.IsEnum ? new LocalizedEnum(PropertyType) : base.Converter;
        public override string DisplayName => CinematicText.T(Name);
        public override bool CanResetValue(object component) => inner.CanResetValue(value);
        public override object? GetValue(object? component) => inner.GetValue(value);
        public override void ResetValue(object component) => inner.ResetValue(value);
        public override void SetValue(object? component, object? newValue) => inner.SetValue(value, newValue);
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(value);
    }
    private sealed class ActorChoices(CinematicScene scene, CinematicClip? clip) : GuidConverter
    {
        private CinematicActor[] Actors => scene.Actors.Where(a =>
            (clip?.Track != CinematicTrack.Actors || a.Kind != CinematicActorKind.PersonalEvent) &&
            (clip?.Track != CinematicTrack.Naval || a.Kind == CinematicActorKind.TemporaryVessel)).ToArray();
        private string Label(CinematicActor actor)
        {
            var label = string.IsNullOrWhiteSpace(actor.Name) ? CinematicText.T("Actor") : actor.Name;
            return Actors.Count(a => a.Name == actor.Name) > 1 ? label + " (" + (Array.IndexOf(Actors, actor) + 1) + ")" : label;
        }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Actors.Select(a => a.TargetId).ToArray());
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
            destinationType == typeof(string) && value is Guid id
                ? Actors.FirstOrDefault(a => a.TargetId == id) is { } actor ? Label(actor) : CinematicText.T("MissingActorChoice")
                : base.ConvertTo(context, culture, value, destinationType);
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
            value is string text && Actors.FirstOrDefault(a => Label(a) == text) is { } actor
                ? actor.TargetId : base.ConvertFrom(context, culture, value);
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
