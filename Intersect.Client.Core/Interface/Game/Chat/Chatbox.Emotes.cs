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
        _emotes.SetBounds(mChatboxWindow.X + mChatboxWindow.Width - 100, Math.Max(0, mChatboxWindow.Y - 26), 96, 23);
        _emotes.IsHidden = !mChatboxWindow.IsVisibleInTree;
    }
    public void ApplyEmotes(ChatEmoteMenuPacket packet)
    {
        UpdateEmotePicker();
        _emotes!.ClearItems();
        foreach (var entry in packet.Entries) _emotes.AddItem(ClientLanguages.ContentText(entry.Name), userData: entry.Id);
        if (packet.Entries.Length == 0) _emotes.AddItem(ClientLanguages.Text("No emotes configured"), userData: Guid.Empty);
        _emotes.Text = ClientLanguages.Text("Emotes");
    }
}
