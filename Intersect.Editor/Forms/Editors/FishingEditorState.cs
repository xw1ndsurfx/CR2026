using Intersect.Framework.Core.Fishing;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors;

internal static class FishingEditorState
{
    internal static FishingConfiguration Configuration { get; private set; } = new();
    internal static event Action? Changed;
    internal static void Apply(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var next = FishingConfiguration.FromJson(json);
        FishingRules.Validate(next);
        Configuration = next;
        Changed?.Invoke();
    }
    internal static void Request() => Networking.Network.SendPacket(new FishingRequestPacket
    { Action = FishingAction.GetConfiguration });
}
