using System.Globalization;
using Intersect.Models;
using System.Runtime.CompilerServices;

namespace Intersect.Editor.Forms.Styling;

internal sealed record EditorChoice(int Index, object Item, string Text)
{
    internal IDatabaseObject? Descriptor { get; init; }
    internal string Extension { get; } = Path.GetExtension(Text).ToLowerInvariant();
}

internal static class SearchableChoices
{
    private sealed class State
    {
        internal bool Enabled = true;
        internal bool Pending;
        internal string? Directory;
        internal readonly Dictionary<int, (object Item, IDatabaseObject Descriptor)> Descriptors = [];
        internal readonly List<object> Recent = [];
    }

    private static readonly ConditionalWeakTable<ComboBox, State> States = new();
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".wav", ".ogg", ".mp3", ".flac", ".opus" };

    internal static void Attach(ComboBox box)
    {
        if (States.TryGetValue(box, out _)) return;
        var state = new State();
        States.Add(box, state);
        box.DropDown += (_, _) =>
        {
            if (!state.Enabled || state.Pending || box.IsDisposed || !box.IsHandleCreated) return;
            state.Pending = true;
            // Run after existing DropDown handlers refresh their asset lists.
            box.BeginInvoke((Action)(() =>
            {
                try
                {
                    if (box.IsDisposed || !box.Enabled || !box.DroppedDown || !ShouldSearch(box)) return;
                    box.DroppedDown = false;
                    using var picker = new EditorChoicePicker(box, Snapshot(box), state.Directory, state.Recent);
                    if (picker.ShowDialog(box.FindForm()) == DialogResult.OK && picker.SelectedChoice is { } choice)
                        TryCommit(box, choice);
                }
                finally { state.Pending = false; }
            }));
        };
    }

    internal static void SetEnabled(ComboBox box, bool enabled)
    {
        Attach(box); States.GetValue(box, _ => new State()).Enabled = enabled;
    }

    internal static void SetResourceDirectory(ComboBox box, string directory)
    {
        Attach(box); States.GetValue(box, _ => new State()).Directory = directory;
    }

    internal static void SetDescriptor(ComboBox box, int index, IDatabaseObject descriptor)
    {
        Attach(box);
        States.GetValue(box, _ => new State()).Descriptors[index] = (box.Items[index], descriptor);
    }

    internal static void AddNames<T>(ComboBox box) where T : DatabaseObject<T>
    {
        Attach(box);
        var state = States.GetValue(box, _ => new State());
        if (box.Items.Count <= state.Descriptors.Keys.DefaultIfEmpty(-1).Max()) state.Descriptors.Clear();
        foreach (var pair in DatabaseObject<T>.Lookup.OrderBy(pair => pair.Value?.Name))
        {
            var index = box.Items.Add(pair.Value?.Name ?? DatabaseObject<T>.Deleted);
            if (pair.Value != null) state.Descriptors[index] = (box.Items[index], pair.Value);
        }
    }

    internal static void AddTextureNames(ComboBox box, Content.GameContentManager.TextureType type)
    {
        var folder = type switch
        {
            Content.GameContentManager.TextureType.Entity => "entities",
            Content.GameContentManager.TextureType.Misc => "misc",
            Content.GameContentManager.TextureType.Gui => "gui",
            _ => type.ToString().ToLowerInvariant() + "s",
        };
        SetResourceDirectory(box, Path.Combine("resources", folder));
        box.Items.AddRange(Content.GameContentManager.GetSmartSortedTextureNames(type));
    }

    internal static bool ShouldSearch(ComboBox box) =>
        States.TryGetValue(box, out var state) && state.Enabled &&
        (state.Directory != null || state.Descriptors.Count > 0 || box.Items.Count >= 12 || Snapshot(box).Any(entry => MediaExtensions.Contains(entry.Extension)));

    internal static EditorChoice[] Snapshot(ComboBox box) => Enumerable.Range(0, box.Items.Count)
        .Select(index => new EditorChoice(index, box.Items[index], box.GetItemText(box.Items[index]) ?? string.Empty)
        {
            Descriptor = box.Items[index] as IDatabaseObject ??
                (States.TryGetValue(box, out var state) && state.Descriptors.TryGetValue(index, out var captured) &&
                 ReferenceEquals(captured.Item, box.Items[index]) ? captured.Descriptor : null),
        }).ToArray();

    internal static EditorChoice[] Filter(IEnumerable<EditorChoice> entries, string query, string? extension, int sort, bool recentOnly, IReadOnlyCollection<object> recent)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = entries.Where(entry => (string.IsNullOrEmpty(extension) || entry.Extension == extension) &&
            (!recentOnly || recent.Contains(entry.Item)) && words.All(word =>
                CultureInfo.InvariantCulture.CompareInfo.IndexOf(entry.Text, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0));
        return (sort switch
        {
            1 => matches.OrderBy(entry => entry.Text, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Index),
            2 => matches.OrderByDescending(entry => entry.Text, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Index),
            _ => matches.OrderBy(entry => entry.Index),
        }).ToArray();
    }

    internal static bool TryCommit(ComboBox box, EditorChoice choice)
    {
        if (box.IsDisposed || !box.Enabled || choice.Index < 0 || choice.Index >= box.Items.Count) return false;
        var item = box.Items[choice.Index];
        if (choice.Descriptor != null && Snapshot(box)[choice.Index].Descriptor?.Id != choice.Descriptor.Id) return false;
        // Never select another item after a refresh, even when display names are duplicated.
        if (!ReferenceEquals(item, choice.Item) && !(item is string text && choice.Item is string captured && text == captured)) return false;
        Attach(box);
        var state = States.GetValue(box, _ => new State());
        state.Recent.Remove(choice.Item); state.Recent.Insert(0, choice.Item);
        if (state.Recent.Count > 20) state.Recent.RemoveAt(state.Recent.Count - 1);
        box.SelectedIndex = choice.Index;
        return true;
    }
}
