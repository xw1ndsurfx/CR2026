using System.ComponentModel;
using System.Drawing.Design;
using System.Runtime.CompilerServices;
using System.Windows.Forms.Design;
using Intersect.Editor.Maps;

namespace Intersect.Editor.Forms.Styling;

public sealed class ResourcePropertyEditor : UITypeEditor
{
    private static readonly ConditionalWeakTable<PropertyDescriptor, List<object>> Recent = new();

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.Modal;

    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (context?.PropertyDescriptor is not { } property ||
            provider.GetService(typeof(IWindowsFormsEditorService)) is not IWindowsFormsEditorService service)
            return value;
        using var source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        var values = property.Converter.GetStandardValues(context);
        if (values != null) foreach (var entry in values) source.Items.Add(entry);
        if (value != null && !source.Items.Contains(value)) source.Items.Add(value);
        source.SelectedItem = value;
        var directory = property.Converter switch
        {
            MapMusicProperty => Path.Combine("resources", "music"),
            MapSoundProperty => Path.Combine("resources", "sounds"),
            MapFogProperty => Path.Combine("resources", "fogs"),
            MapImageProperty => Path.Combine("resources", "images"),
            _ => null,
        };
        var recent = Recent.GetValue(property, _ => []);
        using var picker = new EditorChoicePicker(source, SearchableChoices.Snapshot(source), directory, recent);
        if (service.ShowDialog(picker) != DialogResult.OK || picker.SelectedChoice is not { } selected) return value;
        recent.Remove(selected.Item); recent.Insert(0, selected.Item);
        if (recent.Count > 20) recent.RemoveAt(recent.Count - 1);
        return selected.Item;
    }
}
