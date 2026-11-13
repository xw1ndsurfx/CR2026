using System.Collections.Concurrent;
using Android.App;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Intersect.Client.Framework.Input;
using Intersect.Client.Localization;
using KeyboardType = Intersect.Client.Framework.Input.KeyboardType;

namespace Intersect.Client.Android;

internal static class MobileKeyboard
{
    private static readonly ConcurrentQueue<Action> Completions = new();
    private static volatile bool _visible;
    internal static bool IsVisible => _visible;

    internal static void Drain()
    {
        while (Completions.TryDequeue(out var action)) action();
    }

    internal static void Show(KeyboardType type, Action<string?> completion, string description,
        string text, bool multiline, uint maxLength, string? submitCaption = null, bool preserveOnCancel = true)
    {
        if (_visible || GameActivity.Current is not { } activity) return;
        _visible = true;
        activity.RunOnUiThread(() =>
        {
            var input = new EditText(activity) { Text = text };
            input.InputType = type switch
            {
                KeyboardType.Password => InputTypes.ClassText | InputTypes.TextVariationPassword,
                KeyboardType.Email => InputTypes.ClassText | InputTypes.TextVariationEmailAddress,
                KeyboardType.Numeric => InputTypes.ClassNumber,
                KeyboardType.Pin => InputTypes.ClassNumber | InputTypes.NumberVariationPassword,
                _ => InputTypes.ClassText | (multiline ? InputTypes.TextFlagMultiLine : 0),
            };
            input.SetFilters([new InputFilterLengthFilter((int)Math.Min(maxLength, int.MaxValue))]);
            var submitted = false;
            var dialog = new AlertDialog.Builder(activity)!
                .SetTitle(description)!
                .SetView(input)!
                .SetPositiveButton(submitCaption ?? ClientLanguages.Text("Okay"), (_, _) =>
                {
                    if (submitted) return;
                    submitted = true;
                    var value = input.Text;
                    Completions.Enqueue(() => completion(value));
                })!
                .SetNegativeButton(ClientLanguages.Text("Cancel"), (_, _) => { })!
                .Create()!;
            dialog.DismissEvent += (_, _) =>
            {
                if (!submitted) Completions.Enqueue(() => completion(preserveOnCancel ? text : null));
                _visible = false;
            };
            dialog.Show();
            input.RequestFocus();
            dialog.Window?.SetSoftInputMode(SoftInput.StateAlwaysVisible);
            input.Post(() => (activity.GetSystemService(global::Android.Content.Context.InputMethodService) as InputMethodManager)
                ?.ShowSoftInput(input, ShowFlags.Implicit));
        });
    }
}
