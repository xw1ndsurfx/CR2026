using System.ComponentModel;
using System.Globalization;
using Intersect.Framework.Core.Tutorials;

namespace Intersect.Editor.Forms.Tutorials;

internal sealed class TutorialStepProperties(TutorialStep step):CustomTypeDescriptor
{
    public override object GetPropertyOwner(PropertyDescriptor? property)=>step;
    public override PropertyDescriptorCollection GetProperties()=>GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)=>new(
        TypeDescriptor.GetProperties(step,attributes).Cast<PropertyDescriptor>().Where(p=>p.IsBrowsable)
            .Select(p=>(PropertyDescriptor)new HighlightProperty(p,step)).ToArray());
    internal static string EnumCaption(object value)=>FrmTutorial.T(value.ToString() switch
    {
        "WorldMap"=>"World map", "Hotbar"=>"Quickbar", "WorldNpc"=>"World NPC", "WorldTile"=>"World tile",
        "ReachMap"=>"Reach map", "ReachTile"=>"Reach tile", "CombatDodge"=>"Dodge", "CombatPerfectParry"=>"Perfect parry",
        "CombatCounterattack"=>"Counterattack", "CombatFinisher"=>"Finisher", _=>value.ToString()!,
    });
    private sealed class HighlightProperty(PropertyDescriptor inner,TutorialStep owner):PropertyDescriptor(inner)
    {
        public override string DisplayName=>FrmTutorial.T(inner.Name switch
        {
            "HighlightTarget"=>"Highlight target", "HighlightStyle"=>"Highlight style", "MapId"=>"Target map",
            "TargetX"=>"Target X", "TargetY"=>"Target Y", "TargetRadius"=>"Arrival radius", "TargetNpcId"=>"Target NPC",
            "VariableId"=>"Player variable", "VariableValue"=>"Required value", "CinematicEventId"=>"Cinematic common event", _=>inner.Name,
        });
        public override string Category=>FrmTutorial.T("Objective");
        public override string Description=>DisplayName;
        public override TypeConverter Converter=>inner.PropertyType.IsEnum?new HighlightEnum(inner.PropertyType):inner.Converter;
        public override Type ComponentType=>inner.ComponentType;
        public override Type PropertyType=>inner.PropertyType;
        public override bool IsReadOnly=>inner.IsReadOnly;
        public override object? GetValue(object? component)=>inner.GetValue(owner);
        public override void SetValue(object? component,object? value)=>inner.SetValue(owner,value);
        public override bool CanResetValue(object component)=>inner.CanResetValue(owner);
        public override void ResetValue(object component)=>inner.ResetValue(owner);
        public override bool ShouldSerializeValue(object component)=>inner.ShouldSerializeValue(owner);
    }
    private sealed class HighlightEnum(Type type):EnumConverter(type)
    {
        public override object? ConvertTo(ITypeDescriptorContext? context,CultureInfo? culture,object? value,Type destinationType)=>
            destinationType==typeof(string)&&value!=null?EnumCaption(value):base.ConvertTo(context,culture,value,destinationType);
        public override object? ConvertFrom(ITypeDescriptorContext? context,CultureInfo? culture,object value)
        {
            if(value is string text)
                foreach(var candidate in Enum.GetValues(type))if(EnumCaption(candidate)==text)return candidate;
            return base.ConvertFrom(context,culture,value);
        }
    }
}
