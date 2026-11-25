using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Newtonsoft.Json;

internal static class NavalExpeditionEditorTests
{
    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run()
    {
        var culture = CultureInfo.CurrentUICulture; var maps = MapList.OrderedMaps.ToArray();
        var mapA = Guid.NewGuid(); var mapB = Guid.NewGuid();
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Expedition reward" };
        ItemDescriptor.Lookup.Set(item.Id, item);
        MapList.OrderedMaps.Clear(); MapList.OrderedMaps.AddRange([new() { MapId = mapA, Name = "Ocean A" }, new() { MapId = mapB, Name = "Ocean B" }]);
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                string T(string source) => NavalSetupText.Get(source, language);
                var patrol = new HerboniaPatrol { Enabled = true, Ship = new() { MapId = mapA, Name = "Pirate escort" } };
                var monster = new NavalMonsterDefinition { Enabled = true, MapId = mapB, Name = "Kraken", TentacleNpcId = Guid.NewGuid() };
                var definition = new NavalExpeditionDefinition { Enabled = true, Name = "Coastal convoy", Stages = [new() { MapId = mapA }, new() { MapId = mapA, Objective = NavalExpeditionObjective.DefeatPatrol, TargetId = patrol.Ship.Id }] };
                var configuration = new NavalConfiguration { Expeditions = [definition], Herbonia = [patrol], Monsters = [monster] };
                using var editor = new FrmNavalConfiguration(new() { NavigationJson = JsonConvert.SerializeObject(configuration), CatalogueJson = "{}", NavigationVersion = "expedition-v1" });
                editor.Size = new(850, 620);
                var tabs = editor.Controls.OfType<TabControl>().Single(); tabs.SelectedIndex = 11;
                var page = tabs.SelectedTab!; Check(page.Text == T("Naval expeditions"), "Missing translated expedition tab");
                var mode = All(page).OfType<ComboBox>().Single(c => c.Name == "Mode"); mode.SelectedIndex = (int)NavalExpeditionMode.Fleet;
                Check(mode.Text == T("Fleet"), "Mode is not translated");
                var reward = All(page).OfType<ComboBox>().Single(c => c.Name == "RewardItemId");
                reward.SelectedIndex = reward.Items.Cast<object>().Select((value, index) => (value, index)).Single(v => v.value.ToString() == item.Name).index;
                All(page).OfType<NumericUpDown>().Single(c => c.Name == "RewardQuantity").Value = 3;
                var list = All(page).OfType<ListBox>().Single(c => c.Name == "ExpeditionStageList"); list.SelectedIndex = 1;
                Check(All(page).OfType<ComboBox>().Single(c => c.Name == "ExpeditionTarget").Text == patrol.Ship.Name, "Patrol selection was lost");
                var map = All(page).OfType<ComboBox>().Single(c => c.Name == "MapId");
                map.SelectedIndex = map.Items.Cast<object>().Select((value, index) => (value, index)).Single(v => v.value.ToString() == "Ocean B").index;
                Check(All(page).OfType<ComboBox>().Single(c => c.Name == "ExpeditionTarget").SelectedIndex == 0, "Changing map retained an invalid target");
                All(page).OfType<ComboBox>().Single(c => c.Name == "ExpeditionObjective").SelectedIndex = (int)NavalExpeditionObjective.DefeatMonster;
                var target = All(page).OfType<ComboBox>().Single(c => c.Name == "ExpeditionTarget"); target.SelectedIndex = 1;
                Check(target.Text == "Kraken", "Monster choices do not follow map and objective");
                Click(All(page).OfType<Button>().Single(b => b.Text == T("Move up")));
                Check(list.SelectedIndex == 0, "Cannot reorder objective");
                Click(All(page).OfType<Button>().Single(b => b.Text == T("Add stage")));
                Check(list.Items.Count == 3, "Cannot add stage");
                Click(All(page).OfType<Button>().Single(b => b.Text == T("Remove") && b.Parent?.Parent is GroupBox));
                Check(list.Items.Count == 2, "Cannot remove stage");
                var request = (NavalConfigurationRequestPacket)typeof(FrmNavalConfiguration).GetMethod("PrepareSave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!;
                var saved = JsonConvert.DeserializeObject<NavalConfiguration>(request.ConfigurationJson)!.Expeditions.Single();
                Check(request.Section == 1 && request.ExpectedVersion == "expedition-v1", "Expedition must use the navigation revision");
                Check(saved.Id == definition.Id && saved.Mode == NavalExpeditionMode.Fleet && saved.RewardItemId == item.Id && saved.RewardQuantity == 3, "Expedition parameters not saved");
                Check(saved.Stages[0].Id == definition.Stages[1].Id && saved.Stages[0].TargetId == monster.Id && saved.Stages[0].MapId == mapB, "Reorder changed stable ids or target");
                Console.WriteLine("PASS expedition editor: modes, existing items, map-dependent targets, objective changes, stage ordering and navigation save / " + language);
            }
        }
        finally { CultureInfo.CurrentUICulture = culture; MapList.OrderedMaps.Clear(); MapList.OrderedMaps.AddRange(maps); ItemDescriptor.Lookup.Delete(item); }
    }
}
