using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Newtonsoft.Json;
namespace Intersect.Framework.Core.GameObjects.Items;
public sealed class ItemPresentationSettings
{
    public bool ShowValue {get;set;}=true;
    public bool ShowRequiredLevel {get;set;}=true;
    public int NavalAmmoKind {get;set;}=-1;
    public string NavalAmmoIcon {get;set;}="";
}
public partial class ItemDescriptor
{
    [NotMapped] public ItemPresentationSettings Presentation {get;set;}=new();
    [Column("Presentation"),JsonIgnore]
    public string JsonPresentation
    {
        get=>JsonConvert.SerializeObject(Presentation);
        set=>Presentation=string.IsNullOrWhiteSpace(value)?new():JsonConvert.DeserializeObject<ItemPresentationSettings>(value)??new();
    }
    [NotMapped,JsonIgnore] public string DisplayIcon=>ItemType==ItemType.Ammo && !string.IsNullOrWhiteSpace(Presentation.NavalAmmoIcon)?Presentation.NavalAmmoIcon:Icon;
    [NotMapped,JsonIgnore] public bool UsesSpellIcon=>ItemType==ItemType.Ammo && !string.IsNullOrWhiteSpace(Presentation.NavalAmmoIcon);
    [NotMapped,JsonIgnore] public int MinimumLevel=>UsageRequirements?.Lists.Count>0?UsageRequirements.Lists.Min(list=>
        list.Conditions.OfType<LevelOrStatCondition>().Where(condition=>condition.ComparingLevel && !condition.Negated)
            .Select(condition=>condition.Comparator switch {
                VariableComparator.GreaterOrEqual or VariableComparator.Equal=>Math.Max(1,condition.Value),
                VariableComparator.Greater=>Math.Max(1,condition.Value+1),_=>1}).DefaultIfEmpty(1).Max()):1;
}
