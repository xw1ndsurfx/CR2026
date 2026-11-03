using Intersect.Framework.Core.Archery;

namespace Intersect.Editor.Forms.Editors;

internal static class RoyalCombatEditorState
{
    internal static RoyalCombatConfiguration? Current { get; private set; }
    internal static event Action? Changed;
    internal static void Apply(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        Current = RoyalCombatConfiguration.FromJson(json);
        Changed?.Invoke();
    }
}
