using System.Text.Json;
using Intersect.Editor.Core;

namespace Intersect.Editor.Forms.EditorHub;

internal sealed record HubRecent(string Id, DateTimeOffset Opened);
internal sealed class HubPreferences
{
    private const string Key = "EditorHub.v1";
    private readonly Action<string, string> _save;
    public HashSet<string> Favorites { get; set; } = new(StringComparer.Ordinal);
    public List<HubRecent> Recent { get; set; } = [];
    public bool Collapsed { get; set; }
    internal event Action? Changed;
    internal string? SaveError { get; private set; }
    internal HubPreferences(Func<string, string>? load = null, Action<string, string>? save = null)
    {
        _save = save ?? Preferences.SavePreference;
        try
        {
            var json = (load ?? Preferences.LoadPreference)(Key);
            if (string.IsNullOrWhiteSpace(json) || json.Length > 100_000) return;
            var data = JsonSerializer.Deserialize<Data>(json);
            Favorites = (data?.Favorites ?? []).Where(id => !string.IsNullOrEmpty(id) && id.Length <= 160).ToHashSet(StringComparer.Ordinal);
            Recent = (data?.Recent ?? []).Where(r => r != null && !string.IsNullOrEmpty(r.Id) && r.Id.Length <= 160).DistinctBy(r => r.Id).Take(12).ToList();
            Collapsed = data?.Collapsed == true;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) { }
    }
    private sealed record Data(string[] Favorites, HubRecent[] Recent, bool Collapsed);
    internal void ToggleFavorite(string id) { if (!Favorites.Add(id)) Favorites.Remove(id); Persist(); }
    internal void Record(string id)
    { Recent.RemoveAll(r => r.Id == id); Recent.Insert(0, new(id, DateTimeOffset.UtcNow)); Recent = Recent.Take(12).ToList(); Persist(); }
    internal void ToggleSidebar() { Collapsed = !Collapsed; Persist(); }
    private void Persist()
    {
        try { _save(Key, JsonSerializer.Serialize(new Data(Favorites.Order(StringComparer.Ordinal).ToArray(), Recent.ToArray(), Collapsed))); SaveError = null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) { SaveError = e.Message; }
        Changed?.Invoke();
    }
}
