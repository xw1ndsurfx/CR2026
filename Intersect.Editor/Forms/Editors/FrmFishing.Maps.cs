using Intersect.Editor.General;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Maps.MapList;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmFishing
{
    private void BuildZoneMaps(TableLayoutPanel table, FishingZoneDefinition zone)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 4 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var search = new TextBox { Name = "FishingMapSearch", Dock = DockStyle.Fill,
            PlaceholderText = T("Search maps"), Text = _mapFilter };
        var maps = new CheckedListBox { Name = "FishingMapChoices", Dock = DockStyle.Fill,
            CheckOnClick = true, Height = 180, IntegralHeight = false };
        var count = new Label { Name = "FishingMapCount", AutoSize = true, MaximumSize = new(600, 0) };
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        panel.Controls.Add(search); panel.Controls.Add(maps); panel.Controls.Add(actions); panel.Controls.Add(count);
        var refreshing = false;
        void UpdateCount() => count.Text = string.Format(T("{0} map(s) selected. Filtering does not change the selection."), zone.MapIds.Count);
        void RefreshMaps(Guid selected = default)
        {
            if (selected == Guid.Empty && maps.SelectedItem is Choice current) selected = current.Id;
            refreshing = true; maps.BeginUpdate();
            try
            {
                maps.Items.Clear();
                var choices = MapList.OrderedMaps.Where(map => map.MapId != Guid.Empty)
                    .DistinctBy(map => map.MapId).Select(map => new Choice(map.MapId, map.Name)).ToList();
                foreach (var id in zone.MapIds.Where(id => choices.All(choice => choice.Id != id)))
                    choices.Add(new Choice(id, string.Format(T("Missing map: {0}"), id)));
                foreach (var choice in choices.Where(choice => choice.Caption.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)))
                {
                    var index = maps.Items.Add(choice, zone.MapIds.Contains(choice.Id));
                    if (choice.Id == selected) maps.SelectedIndex = index;
                }
                UpdateCount();
            }
            finally { maps.EndUpdate(); refreshing = false; }
        }
        search.TextChanged += (_, _) => { _mapFilter = search.Text; RefreshMaps(); };
        maps.ItemCheck += (_, e) => {
            if (refreshing || _loading || maps.Items[e.Index] is not Choice choice) return;
            // Mutate this ID only: checked items outside the current filter must survive.
            if (e.NewValue == CheckState.Checked)
            {
                if (!zone.MapIds.Contains(choice.Id) && zone.MapIds.Count >= FishingRules.MaximumMapsPerZone)
                { e.NewValue = CheckState.Unchecked; _status.Text = string.Format(T("A zone can include at most {0} maps."), FishingRules.MaximumMapsPerZone); return; }
                if (!zone.MapIds.Contains(choice.Id)) zone.MapIds.Add(choice.Id);
            }
            else zone.MapIds.Remove(choice.Id);
            UpdateCount(); Changed();
        };
        AddButton(actions, "Add current map", () => {
            if (Globals.CurrentMap is not { } map) { _status.Text = T("Open a map before adding the current map."); return; }
            if (!zone.MapIds.Contains(map.Id) && zone.MapIds.Count >= FishingRules.MaximumMapsPerZone)
            { _status.Text = string.Format(T("A zone can include at most {0} maps."), FishingRules.MaximumMapsPerZone); return; }
            if (!zone.MapIds.Contains(map.Id)) { zone.MapIds.Add(map.Id); Changed(); }
            search.Text = ""; RefreshMaps(map.Id);
        });
        AddButton(actions, "Remove selected map", () => {
            if (maps.SelectedItem is not Choice choice || !zone.MapIds.Remove(choice.Id)) return;
            Changed(); RefreshMaps(choice.Id);
        });
        RefreshMaps(); Row(table, "Fishing maps", panel);
    }
}
