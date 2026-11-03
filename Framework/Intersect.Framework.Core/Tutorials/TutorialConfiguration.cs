using Intersect.Localization;
using Newtonsoft.Json;
namespace Intersect.Framework.Core.Tutorials;

public enum TutorialObjective { Read, Move, ReachMap, Variable }
public sealed class TutorialStep
{
    [System.ComponentModel.Browsable(false)] public Guid Id { get; set; } = Guid.NewGuid();
    [System.ComponentModel.Browsable(false)] public string Title { get; set; } = "";
    [System.ComponentModel.Browsable(false)] public string Text { get; set; } = "";
    public TutorialObjective Objective { get; set; }
    public Guid MapId { get; set; }
    public Guid VariableId { get; set; }
    public long VariableValue { get; set; } = 1;
    // Select a common event compiled by the existing cinematic director.
    public Guid CinematicEventId { get; set; }
}
public sealed class TutorialConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool AutoStart { get; set; } = true;
    public List<TutorialStep> Steps { get; set; } = [];
    public GameTextCatalog Translations { get; set; } = new();
    public bool IsValid => Steps is { Count: > 0 and <= 100 } && Translations?.Entries != null &&
        Steps.All(s => s != null && s.Id != Guid.Empty && !string.IsNullOrWhiteSpace(s.Title) &&
            !string.IsNullOrWhiteSpace(s.Text) && s.Title.Length <= 200 && s.Text.Length <= 6000 &&
            Enum.IsDefined(s.Objective) && (s.Objective != TutorialObjective.ReachMap || s.MapId != Guid.Empty) &&
            (s.Objective != TutorialObjective.Variable || s.VariableId != Guid.Empty) &&
            new[] { s.Title, s.Text }.All(source => GameTextCatalog.Languages.All(lang =>
                Translations.Entries.TryGetValue(GameTextCatalog.NormalizeText(source), out var versions) && versions != null &&
                versions.TryGetValue(lang, out var value) && !string.IsNullOrWhiteSpace(value) && GameTextTokens.Compatible(source, value)))) &&
        Steps.Select(s => s.Id).Distinct().Count() == Steps.Count;
    public static TutorialConfiguration CreateDefault()
    {
        var result = new TutorialConfiguration { Translations = GameTextCatalog.FromJson(TutorialText.Catalog.ToJson()) };
        var titles = new[] { "Welcome to Corps Royaux", "Your first steps", "Your interface", "Inventory and equipment", "Combat and quests", "Professions and campfires", "Mini-games", "Naval adventures", "Continue your adventure" };
        var texts = new[] { "Discover the kingdom at your own pace. You can pause this guide and reopen it with the Tutorial button.",
            "Move your character a few tiles to discover the world. The guide will detect your movement.",
            "Your HUD shows your health, mana and experience. Use the menu buttons to explore your character, inventory, quests and map.",
            "Open your inventory to inspect your items and equip suitable gear. Ammunition includes arrows and cannonballs; keep supplies for your activities.",
            "Talk to NPCs to discover quests. Follow your objectives and use your equipped weapons and spells during combat.",
            "Gather resources and practise professions to progress. Discover crafting and campfires as you explore the kingdom.",
            "Discover activities such as cards, cooking, potion brewing, lockpicking, archery and jousting. Each activity introduces its own rules when opened.",
            "Visit a harbour to discover ships, crews, cannons and ammunition. Prepare your ship before sailing and learn navigation with your crew.",
            "You are ready to explore. You can replay this guide at any time. Enjoy Corps Royaux!" };
        for (var i = 0; i < titles.Length; i++) result.Steps.Add(new() { Id = Guid.Parse($"c1026029-0000-4000-8000-{i+1:000000000000}"), Title = titles[i], Text = texts[i], Objective = i == 1 ? TutorialObjective.Move : TutorialObjective.Read });
        return result;
    }
}
public sealed class TutorialProgress
{
    public HashSet<Guid> Completed { get; set; } = [];
    public bool Paused { get; set; }
    public bool Finished { get; set; }
    public TutorialStep? Current(TutorialConfiguration config) => Finished ? null : config.Steps.FirstOrDefault(s => !Completed.Contains(s.Id));
}
public static class TutorialText
{
    public static GameTextCatalog Catalog { get; } = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(TutorialText).Assembly.GetManifestResourceStream("Intersect.Tutorial.Text")!;
        using var reader = new StreamReader(stream); return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string source, string language) => Catalog.Get(source, language);
}
