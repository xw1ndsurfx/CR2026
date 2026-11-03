using Intersect.Editor.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors;

internal static class FishingEditorState
{
    private static readonly System.Windows.Forms.Timer _refreshTimeout = new() { Interval = 15_000 };

    static FishingEditorState()
    {
        _refreshTimeout.Tick += (_, _) =>
        {
            if (IsRefreshing)
                ReportError(FishingEditorText.Get("The server did not respond while loading the active fishing configuration. Refresh to try again."));
        };
    }

    internal static FishingConfiguration Configuration { get; private set; } = new();
    internal static bool HasConfiguration { get; private set; }
    internal static bool IsRefreshing { get; private set; }
    internal static string? LastError { get; private set; }
    internal static event Action? Changed;

    internal static void Apply(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var next = FishingConfiguration.FromJson(json);
        FishingRules.Validate(next);
        Configuration = next;
        HasConfiguration = true;
        FinishRefresh();
        LastError = null;
        Changed?.Invoke();
    }

    internal static void ReportError(string message)
    {
        // A failed read or application must never overwrite the last server-confirmed snapshot.
        FinishRefresh();
        LastError = message;
        Changed?.Invoke();
    }

    internal static void Request()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        LastError = null;
        _refreshTimeout.Start();
        Changed?.Invoke();
        if (Networking.Network.EditorLidgrenNetwork is null)
        {
            ReportRequestFailure();
            return;
        }
        try
        {
            Networking.Network.SendPacket(new FishingRequestPacket { Action = FishingAction.GetConfiguration });
        }
        catch (Exception)
        {
            // The shared network sender throws a plain Exception when sending fails.
            ReportRequestFailure();
        }
    }

    private static void ReportRequestFailure() => ReportError(FishingEditorText.Get(
        "The active fishing configuration could not be requested. Check the server connection and refresh to try again."));

    private static void FinishRefresh()
    {
        _refreshTimeout.Stop();
        IsRefreshing = false;
    }

    internal static void RequestDraft() => Networking.Network.SendPacket(new FishingRequestPacket
    { Action = FishingAction.GetDraftConfiguration });
}
