#if ANDROID
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Configuration;

namespace Intersect.Client.Interface.Game.Chat;

public partial class Chatbox
{
    private void OpenMobileChatKeyboard()
    {
        if (global::Intersect.Client.Android.MobileKeyboard.IsVisible) return;
        var field = mChatboxInput;
        var available = true;
        void FieldDisposed(Base _, EventArgs args) => available = false;
        field.Disposed += FieldDisposed;
        var draft = field.Text == GetDefaultInputText() ? string.Empty : field.Text;
        global::Intersect.Client.Android.MobileKeyboard.Show(KeyboardType.Normal, value =>
        {
            field.Disposed -= FieldDisposed;
            if (!available || Globals.GameState != GameStates.InGame) return;
            if (value != null)
            {
                field.Text = value;
                field.CursorPos = field.Text.Length;
                field.CursorEnd = field.Text.Length;
                TrySendMessage();
            }
            // Release chat focus after either Send or Cancel so mobile movement
            // controls return. Cancel leaves the original draft untouched.
            mChatboxMessages.Focus();
        }, Strings.Chatbox.Title, draft, multiline: false,
            maxLength: (uint)Math.Max(1, Options.Instance.Chat.MaxChatLength),
            submitCaption: Strings.Chatbox.Send, preserveOnCancel: false);
    }
}
#endif
