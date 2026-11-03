using System.ComponentModel;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

internal sealed class FarmEditorProperties(FarmConfiguration value) : CustomTypeDescriptor
{
    private static string T(string source) => QualityOfLifeText.Get(source, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public override object GetPropertyOwner(PropertyDescriptor? property) => value;
    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => new(
        TypeDescriptor.GetProperties(value, attributes).Cast<PropertyDescriptor>()
            .Where(p => p.IsBrowsable && p.Name != nameof(FarmConfiguration.Crops))
            .Select(p => (PropertyDescriptor)new FarmProperty(p, value)).ToArray());

    private sealed class FarmProperty(PropertyDescriptor inner, FarmConfiguration owner) : PropertyDescriptor(inner)
    {
        public override string DisplayName => T(inner.Name switch
        {
            "Enabled" => "Enable farming", "ExteriorMapId" => "Exterior map", "EntryX" => "Entry X", "EntryY" => "Entry Y",
            "FieldX" => "Field X", "FieldY" => "Field Y", "MaximumWorkers" => "Maximum workers", "StorageCapacity" => "Granary capacity",
            "CurrencyItemId" => "Salary currency", "HiringPrice" => "Hiring price", "WagePerCycle" => "Wage per cycle", "WorkerSprite" => "Worker sprite",
            _ => inner.DisplayName,
        });
        public override Type ComponentType => inner.ComponentType;
        public override Type PropertyType => inner.PropertyType;
        public override bool IsReadOnly => inner.IsReadOnly;
        public override bool CanResetValue(object component) => inner.CanResetValue(owner);
        public override object? GetValue(object? component) => inner.GetValue(owner);
        public override void SetValue(object? component, object? next) => inner.SetValue(owner, next);
        public override void ResetValue(object component) => inner.ResetValue(owner);
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(owner);
    }
}
