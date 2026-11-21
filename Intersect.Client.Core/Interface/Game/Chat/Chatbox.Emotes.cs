using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Network.Packets.ChatEmotes;
namespace Intersect.Client.Interface.Game.Chat;
public partial class Chatbox
{
    private ComboBox? _emotes;
    private long _emoteRefreshAt;
    private void UpdateEmotePicker()
    {
        if (_emotes == null)
        {
            _emotes = new ComboBox(mChatboxWindow.Parent, "ChatEmotes")
            {
                Text = ClientLanguages.Text("Emotes"), OpenMenuAbove = true, AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mChatboxWindow.Skin.DefaultFont, FontSize = 9
            };
            _emotes.ItemSelected += (_, args) =>
            {
                if (!args.Automated && args.SelectedUserData is Guid id && id != Guid.Empty) Intersect.Client.Networking.Network.SendPacket(new ChatEmoteRequestPacket { EventId = id });
                _emotes.Text = ClientLanguages.Text("Emotes");
            };
            _emotes.Clicked += (_, _) =>
            {
                if (Environment.TickCount64 < _emoteRefreshAt) return;
                _emoteRefreshAt = Environment.TickCount64 + 2000;
                Intersect.Client.Networking.Network.SendPacket(new ChatEmoteRequestPacket());
            };
            Intersect.Client.Networking.Network.SendPacket(new ChatEmoteRequestPacket());
        }
#if ANDROID
        var top = mChatboxMessages.IsHidden ? mChatboxWindow.Y + mChatboxInput.Y : mChatboxWindow.Y;
        var bounds = Android.TouchControls.FitHudControl(new Framework.GenericClasses.Rectangle(mChatboxWindow.X, top - 44, 136, 36));
        _emotes.FontSize = 12;
        _emotes.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
#else
        _emotes.SetBounds(mChatboxWindow.X + mChatboxWindow.Width - 100, Math.Max(0, mChatboxMessages.IsHidden?mChatboxWindow.Y+mChatboxInput.Y-26:mChatboxWindow.Y-26), 96, 23);
#endif
        _emotes.IsHidden = !mChatboxWindow.IsVisibleInTree;
    }
    public void ApplyEmotes(ChatEmoteMenuPacket packet)
    {
        UpdateEmotePicker();
        var wasOpen = _emotes!.IsOpen;
        _emotes!.ClearItems();
        foreach (var entry in packet.Entries) _emotes.AddItem(ClientLanguages.ContentText(entry.Name), userData: entry.Id);
        if (packet.Entries.Length == 0) _emotes.AddItem(ClientLanguages.Text("No emotes configured"), userData: Guid.Empty);
        _emotes.Text = ClientLanguages.Text("Emotes");
        // Adding the first entry selects it automatically, which closes Gwen's
        // menu. Preserve the state at packet arrival, never reopening a picker
        // the player has already dismissed while waiting for the response.
        if (wasOpen) _emotes.Open();
    }
}
