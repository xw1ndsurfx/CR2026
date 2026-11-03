using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Intersect.Editor.Forms.EditorHub;

internal static class HubText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = Read<Dictionary<string, Dictionary<string, string>>>("Texts.json");
    internal static T Read<T>(string resource)
    {
        using var stream = typeof(HubText).Assembly.GetManifestResourceStream("EditorHub." + resource)
            ?? throw new InvalidOperationException("Missing hub resource: " + resource);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    internal static string Localize(IReadOnlyDictionary<string, string> text, string? language = null) =>
        text.GetValueOrDefault(language ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? text.GetValueOrDefault("en") ?? "";
    internal static string T(string key) => Texts.TryGetValue(key, out var text) ? Localize(text) : key;
}

internal sealed class HubDefinition
{
    public string Id { get; set; } = "";
    public string OldMenu { get; set; } = "";
    public string Category { get; set; } = "Tools";
    public string Subcategory { get; set; } = "Configuration";
    public string[] Tags { get; set; } = [];
    public Dictionary<string, string> Title { get; set; } = [];
    public Dictionary<string, string> Description { get; set; } = [];
    public string Art { get; set; } = "";
    public string Keywords { get; set; } = "";
    internal string Name => HubText.Localize(Title);
    internal string Detail => HubText.Localize(Description);
}

internal sealed record HubEntry(HubDefinition Definition, Action Execute, Func<bool> Available, ToolStripItem? Source = null)
{
    internal string Id => Definition.Id;
}

// Menus remain the command authority. The hub registers their leaf commands rather
// than duplicating network requests or constructing alternative editor instances.
internal sealed class HubRegistry : IDisposable
{
    internal static readonly HubDefinition[] Definitions = HubText.Read<HubDefinition[]>("Catalog.json");
    private readonly Dictionary<string, HubEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<ToolStripItem, EventHandler> _tracked = [];
    private readonly List<ToolStripDropDown> _watched = [];
    private readonly ToolStripMenuItem[] _roots;
    private readonly HashSet<ToolStripDropDown> _activeMenus = [];
    private readonly Action<string> _record;
    internal IEnumerable<HubEntry> Entries => _entries.Values;
    internal event Action? Changed;

    internal HubRegistry(IEnumerable<ToolStripMenuItem> roots, Action<string> record)
    { _roots = roots.ToArray(); _record = record; Refresh(); }

    internal void Register(HubEntry entry) { _entries[entry.Id] = entry; Changed?.Invoke(); }
    internal HubEntry? Find(string id) => _entries.GetValueOrDefault(id);
    internal bool Open(string id)
    {
        if (Find(id) is not { } entry || !entry.Available()) return false;
        entry.Execute();
        if (entry.Source == null) _record(id); // Menu invocations are recorded by the shared Click handler.
        return true;
    }
    internal void Refresh()
    {
        foreach (var id in _entries.Where(pair => pair.Value.Source != null).Select(pair => pair.Key).ToArray()) _entries.Remove(id);
        _activeMenus.Clear();
        foreach (var root in _roots) Scan(root, root.Name);
        var active = _entries.Values.Select(entry => entry.Source).OfType<ToolStripItem>().ToHashSet();
        foreach (var item in _tracked.Keys.Where(item => !active.Contains(item)).ToArray())
        { item.Click -= _tracked[item]; _tracked.Remove(item); }
        foreach (var menu in _watched.Where(menu => !_activeMenus.Contains(menu)).ToArray())
        { menu.ItemAdded -= OnItemAdded; menu.ItemRemoved -= OnItemRemoved; _watched.Remove(menu); }
        Changed?.Invoke();
    }
    private void Scan(ToolStripMenuItem menu, string root)
    {
        _activeMenus.Add(menu.DropDown);
        if (!_watched.Contains(menu.DropDown))
        { _watched.Add(menu.DropDown); menu.DropDown.ItemAdded += OnItemAdded; menu.DropDown.ItemRemoved += OnItemRemoved; }
        foreach (var item in menu.DropDownItems.OfType<ToolStripMenuItem>())
        {
            if (item.HasDropDownItems) { Scan(item, root); continue; }
            var id = item.Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                // User-installed tools use their relative executable path as identity.
                var identity = item.Tag is string path ? Path.GetRelativePath("tools", path) : root + "/" + item.Text;
                id = "external." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToLowerInvariant())))[..16];
            }
            var definition = Definitions.FirstOrDefault(d => d.Id == id) ?? new HubDefinition
            {
                Id = id, OldMenu = root, Category = "Tools", Tags = ["Advanced"], Art = "packageUpdateToolStripMenuItem.png",
                Title = new() { ["en"] = item.Text.Replace("&", "").TrimEnd('.') },
                Description = new() { ["en"] = item.ToolTipText }, Keywords = item.Text,
            };
            _entries[id] = new(definition, item.PerformClick, () => !item.IsDisposed && item.Enabled, item);
            if (_tracked.ContainsKey(item)) continue;
            var captured = id;
            EventHandler handler = (_, _) => _record(captured);
            _tracked.Add(item, handler); item.Click += handler;
        }
    }
    private void OnItemAdded(object? sender, ToolStripItemEventArgs e) => Refresh();
    private void OnItemRemoved(object? sender, ToolStripItemEventArgs e) => Refresh();

    internal HubEntry[] Search(string query, string category = "Home", string tag = "All", ISet<string>? favorites = null)
    {
        var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Entries.Where(e => (category is "Home" or "All" || category == "Favorites" && favorites?.Contains(e.Id) == true || e.Definition.Category == category)
                && (tag == "All" || e.Definition.Tags.Contains(tag)))
            .Select(e => (Entry: e, Score: Score(e.Definition, words)))
            .Where(e => e.Score >= 0).OrderByDescending(e => e.Score).ThenBy(e => e.Entry.Definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => e.Entry).ToArray();
    }
    private static int Score(HubDefinition definition, string[] words)
    {
        var names = Normalize(string.Join(" ", definition.Title.Values));
        var text = Normalize(names + " " + string.Join(" ", definition.Description.Values) + " " + definition.Keywords + " " + definition.Category + " " +
            HubText.T(definition.Category) + " " + HubText.T(definition.Subcategory) + " " + string.Join(" ", definition.Tags.Select(HubText.T)));
        if (!words.All(text.Contains)) return -1;
        return words.Sum(word => names.StartsWith(word) ? 8 : names.Contains(word) ? 4 : 1);
    }
    internal static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var clean = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return clean.Replace("pnj", "npc").Replace("objets", "items").Replace("objet", "item").Replace("sorts", "spells").Replace("sort ", "spell ");
    }
    public void Dispose()
    {
        foreach (var pair in _tracked) pair.Key.Click -= pair.Value;
        foreach (var menu in _watched) { menu.ItemAdded -= OnItemAdded; menu.ItemRemoved -= OnItemRemoved; }
        _tracked.Clear(); _watched.Clear(); _entries.Clear();
    }
}
