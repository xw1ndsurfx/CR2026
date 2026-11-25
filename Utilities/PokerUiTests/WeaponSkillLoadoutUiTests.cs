using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Server;
using Base = Intersect.Client.Framework.Gwen.Control.Base;

internal static class WeaponSkillLoadoutUiTests
{
    [ModuleInitializer] internal static void Run()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.WeaponMasteriesWindow", true)!;
        var window = (WindowControl)Activator.CreateInstance(type, hidden, null, [canvas], null)!;
        var config = new RoyalCombatConfiguration(); var category = config.Masteries.Single(c => c.Key == "swords");
        var skill = category.Talents.First(t => t.Skill.Enabled);
        var slots = Enumerable.Repeat("", 10).ToArray(); slots[7] = skill.Key;
        var state = new ArcheryStatePacket { Open = true, ConfigurationJson = config.ToJson(),
            MasteriesJson = JsonSerializer.Serialize(new Dictionary<string,long>{{category.Key,500}}),
            MasteryRanksJson = JsonSerializer.Serialize(new Dictionary<string,Dictionary<string,int>>{{category.Key,new(){{skill.Key,1}}}}),
            WeaponSkillSlotsJson = JsonSerializer.Serialize(new Dictionary<string,string[]>{{category.Key,slots}}) };
        void Apply() => type.GetMethod("Apply", hidden)!.Invoke(window, [state]);
        void Click(string name) => typeof(Button).GetMethod("OnKeyReturn", hidden)!.Invoke(All(window).OfType<Button>().Single(b => b.Name == name), [false]);
        Apply(); Click("MasteryCategoryswords"); Click("WeaponQuickbarSlot7");
        var assign = All(window).OfType<Button>().Where(b => b.Name == "AssignWeaponSkill").ToArray();
        Check(assign.Length == 3 && assign.Count(b => !b.IsDisabled) == 1 && assign.All(b => b.Text.Contains("8")),
            "Selecting a quickbar slot offers learned active skills only, for that numbered slot");
        Check(All(window).Single(b => b.Name == "WeaponQuickbarSlot7").TooltipText.Contains(skill.Name), "The slot identifies its saved skill");
        state.MasteriesReadOnly = true; Apply();
        Check(All(window).OfType<Button>().Where(b => b.Name == "AssignWeaponSkill").All(b => b.IsDisabled), "Inspection cannot edit another character's quickbar");
        Check(All(window).Single(b => b.Name == "ClearWeaponQuickbarSlot").IsHidden, "Inspection hides quickbar editing controls");
        state.MasteriesJson = ""; Apply();
        Check(All(window).Where(b => b.Name.StartsWith("WeaponQuickbarSlot")).All(b => b.IsHidden), "Unavailable inspection removes stale quickbar contents");
        System.Console.WriteLine("PASS weapon quickbar selection UI: numbered slots, learned-skill eligibility, saved layout and inspection isolation");
    }
    private static IEnumerable<Base> All(Base root)
    {
        yield return root;
        foreach (var child in root.Children) foreach (var descendant in All(child)) yield return descendant;
    }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
}
