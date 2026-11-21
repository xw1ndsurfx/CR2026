using Android.Content;
using Intersect.Client.Framework.Input;
using Intersect.Client.Android;

namespace Intersect.Client.MonoGame.Input;

public partial class MonoClipboard : GameClipboard
{
    private static ClipboardManager? Manager => GameActivity.Current?.GetSystemService(Context.ClipboardService) as ClipboardManager;
    public override void SetText(string data)
    {
        if (Manager is { } manager) manager.PrimaryClip = ClipData.NewPlainText(string.Empty, data);
    }
    public override string GetText() => Manager?.PrimaryClip?.GetItemAt(0)?.Text ?? string.Empty;
    public override bool IsEmpty => string.IsNullOrEmpty(GetText());
    public override bool IsEnabled => Manager != null;
}
