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
            .Select(p=>p.Name is nameof(TutorialStep.HighlightTarget) or nameof(TutorialStep.HighlightStyle)
                ?(PropertyDescriptor)new HighlightProperty(p,step):p).ToArray());
    private sealed class HighlightProperty(PropertyDescriptor inner,TutorialStep owner):PropertyDescriptor(inner)
    {
        public override string DisplayName=>FrmTutorial.T(inner.Name==nameof(TutorialStep.HighlightTarget)?"Highlight target":"Highlight style");
        public override TypeConverter Converter=>new HighlightEnum(inner.PropertyType);
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
        private static string Caption(object value)=>FrmTutorial.T(value.ToString() switch
        {
            "WorldMap"=>"World map", "Hotbar"=>"Quickbar", "Outline"=>"Outline", "Circle"=>"Circle",
            _=>value.ToString()!,
        });
        public override object? ConvertTo(ITypeDescriptorContext? context,CultureInfo? culture,object? value,Type destinationType)=>
            destinationType==typeof(string)&&value!=null?Caption(value):base.ConvertTo(context,culture,value,destinationType);
        public override object? ConvertFrom(ITypeDescriptorContext? context,CultureInfo? culture,object value)
        {
            if(value is string text)
                foreach(var candidate in Enum.GetValues(type))if(Caption(candidate)==text)return candidate;
            return base.ConvertFrom(context,culture,value);
        }
    }
}
