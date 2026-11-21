using Intersect.Framework.Core.Fishing;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmFishing
{
    private void BuildBonusLoot(TableLayoutPanel table, FishingCatchDefinition entry)
    {
        var actions = new FlowLayoutPanel { AutoSize = true };
        AddButton(actions, "Add bonus loot", () => {
            if (entry.BonusLoot.Count >= 16) return;
            entry.BonusLoot.Add(new()); Changed(); BuildSelectedPage();
        });
        Row(table, "Bonus loot", actions);
        Note(table, "Add currency at 100% and rare items at a lower chance. All loot is taken or discarded together.");
        foreach (var loot in entry.BonusLoot)
        {
            var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
            fields.ColumnStyles.Add(new(SizeType.Percent, 45));
            fields.ColumnStyles.Add(new(SizeType.Percent, 55));
            GuidField(fields, "Bonus item", loot.ItemId, ItemChoices(), id => loot.ItemId = id);
            Number(fields, "Drop chance (%)", loot.Chance * 100, 0, 100, value => loot.Chance = (double)value / 100, 2);
            Number(fields, "Minimum quantity", loot.MinimumQuantity, 1, 1000, value => loot.MinimumQuantity = (int)value);
            Number(fields, "Maximum quantity", loot.MaximumQuantity, 1, 1000, value => loot.MaximumQuantity = (int)value);
            var remove = new FlowLayoutPanel { AutoSize = true };
            AddButton(remove, "Remove bonus loot", () => { entry.BonusLoot.Remove(loot); Changed(); BuildSelectedPage(); });
            Row(fields, "", remove);
            Row(table, string.Format(T("Bonus loot #{0}"), entry.BonusLoot.IndexOf(loot) + 1), fields);
        }
    }
}
