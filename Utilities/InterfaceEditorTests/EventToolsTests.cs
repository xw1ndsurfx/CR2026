using System.Reflection;
using Intersect;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.WorldEvents;
using Newtonsoft.Json;
internal static class EventToolsTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        // Reproduce the real copy/paste path, including nested guild branches and printer traversal.
        using var editor = new FrmEvent(new MapDescriptor(Guid.NewGuid()));
        var page = new EventPage();
        var root = page.CommandLists.Values.First();
        var condition = new ConditionalBranchCommand(page.CommandLists) { Condition = new PremiumStatusCondition() };
        var guild = new CreateGuildCommand(page.CommandLists);
        page.CommandLists[condition.BranchIds[0]].Add(guild);
        page.CommandLists[guild.BranchIds[0]].Add(new DisbandGuildCommand(page.CommandLists));
        root.Add(condition);
        editor.CurrentPage = page;
        editor.MyEvent = new EventDescriptor(Guid.NewGuid(), true) { Pages = [page] };
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var refresh = typeof(FrmEvent).GetMethod("ListPageCommands", flags)!;
        refresh.Invoke(editor, null);
        var list = (ListBox)typeof(FrmEvent).GetField("lstEventCommands", flags)!.GetValue(editor)!;
        list.SelectedIndex = 0;
        typeof(FrmEvent).GetMethod("btnCopy_Click", flags)!.Invoke(editor, [null!, EventArgs.Empty]);
        for (var copy = 0; copy < 3; copy++)
        {
            list.SelectedIndex = 0;
            typeof(FrmEvent).GetMethod("btnPaste_Click", flags)!.Invoke(editor, [null!, EventArgs.Empty]);
        }
        Check(root.Count == 4, "Repeated clipboard pastes must insert complete command trees");
        var all = root.Cast<ConditionalBranchCommand>().SelectMany(c => c.BranchIds).ToArray();
        Check(all.Distinct().Count() == all.Length && all.All(page.CommandLists.ContainsKey), "Each pasted condition owns independent branches");
        foreach (var branch in root.Cast<ConditionalBranchCommand>())
        {
            var copied = (CreateGuildCommand)page.CommandLists[branch.BranchIds[0]].Single();
            Check(copied.BranchIds.All(page.CommandLists.ContainsKey), "Pasted guild references must resolve after original branches are renamed");
        }
        // Persistence and backwards compatibility of emote permission (explicit opt-in).
        Check(!new EventDescriptor(Guid.NewGuid(), true) { Pages = [new()] }.IsChatEmote, "Common events are private by default");
        editor.MyEvent.Pages[0].ShowInChatEmoteMenu = true;
        Check(editor.MyEvent.IsChatEmote && JsonConvert.DeserializeObject<EventPage>(JsonConvert.SerializeObject(page, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto })!.ShowInChatEmoteMenu, "Chat permission survives existing page JSON storage");
        var legacyEmote = new EventDescriptor(Guid.NewGuid(), true) { Folder = "[SHOUTBOX] - Emoticons", Pages = [new()] };
        Check(legacyEmote.IsChatEmote && !legacyEmote.Pages[0].ShowInChatEmoteMenu, "Legacy SHOUTBOX folder is recognized without modifying pages");
        Check(JsonConvert.DeserializeObject<EventDescriptor>(JsonConvert.SerializeObject(legacyEmote))!.IsChatEmote, "Emote folder discovery survives event serialization");
        editor.MyEvent.CommonEvent = false;
        Check(!editor.MyEvent.IsChatEmote, "Map events cannot be executed from chat");
        var config = new WorldActivitiesConfiguration { CombatMusic = "old.ogg", BankOpeningCost = 3 };
        var stale = new WorldActivitiesConfiguration { CombatMusic = "stale.ogg", BankOpeningCost = 0, ExperienceEvents = [new() { Enabled = true }] };
        config.ApplySection(stale, WorldActivitiesSection.Experience);
        Check(config.CombatMusic == "old.ogg" && config.BankOpeningCost == 3 && config.ExperienceEvents.Count == 1, "Saving EXP never overwrites music or bank settings from a stale editor");
        stale.CombatMusic = "battle.ogg"; stale.ExperienceEvents = [];
        config.ApplySection(stale, WorldActivitiesSection.CombatMusic);
        Check(config.ExperienceEvents.Count == 1 && config.CombatMusic == "battle.ogg", "Saving combat music preserves EXP events");
        var overnight = new ExperienceEventDefinition { Enabled = true, StartHour = 23, StartMinute = 30, DurationMinutes = 60 };
        overnight.EndHour = 1; overnight.EndMinute = 15;
        var midnight = new DateTimeOffset(2026, 10, 17, 0, 30, 0, TimeSpan.Zero);
        Check(overnight.DurationMinutes == 105 && overnight.IsActive(midnight) && !overnight.IsActive(midnight.AddMinutes(45)), "Explicit end times cross midnight and stop at the exact end");
        var brush = typeof(Intersect.Editor.Forms.DockingElements.FrmMapLayers).Assembly.GetType("Intersect.Editor.Forms.DockingElements.ResourceBrush", true)!;
        var cells = ((IEnumerable<(int X, int Y)>)brush.GetMethod("Cells", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [0, 0, 8, 32, 26])!).ToArray();
        Check(cells.Length > 20 && cells.All(c => c.X >= 0 && c.Y >= 0 && c.X * c.X + c.Y * c.Y <= 64), "Resource brush clips at map edges and stays circular");
        System.Console.WriteLine("PASS actual nested guild copy/paste, emote persistence, independent editor saves, overnight XP end times and resource brush bounds");
    }
}
