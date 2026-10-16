using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Networking;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed partial class UserListWindow
{
    private bool _guildDirectoryOpen;
    private GuildDirectoryEntry[] _directoryEntries = [];
    private int _directoryTotal;
    private int _directoryPage;
    private string _directoryQuery = string.Empty;
    private Guid _selectedGuild;
    private bool _directoryRequestPending;
    private long _nextDirectoryRequestAt;

    public void ApplyGuildDirectory(GuildDirectoryPacket packet)
    {
        // Rapid pagination/search can leave prior requests in flight. Ignore
        // responses for any page other than the one the player now wants.
        if (!string.Equals(packet.Search, _directoryQuery, StringComparison.Ordinal) ||
            packet.Page != _directoryPage)
            return;

        _directoryEntries = packet.Entries ?? [];
        _directoryTotal = Math.Max(0, packet.Total);
        _directoryPage = Math.Max(0, packet.Page);
        if (_directoryEntries.All(entry => entry.Id != _selectedGuild))
            _selectedGuild = Guid.Empty;
        if (IsShowing(Page.Guild) && _guildDirectoryOpen)
            Refresh();
    }

    private void OpenGuildDirectory()
    {
        _directoryQuery = string.Empty;
        _directoryPage = 0;
        _directoryTotal = 0;
        _directoryEntries = [];
        _selectedGuild = Guid.Empty;
        _guildDirectoryOpen = true;
        Refresh();
        RequestGuildDirectory();
    }

    private void RequestGuildDirectory()
    {
        // Coalesce fast input so the last page/search is always delivered,
        // rather than dropping a click and leaving an empty panel.
        _directoryRequestPending = true;
        PumpGuildDirectory();
    }

    private void PumpGuildDirectory()
    {
        if (!_directoryRequestPending || !IsShowing(Page.Guild) ||
            !_guildDirectoryOpen || Environment.TickCount64 < _nextDirectoryRequestAt)
            return;

        _directoryRequestPending = false;
        _nextDirectoryRequestAt = Environment.TickCount64 + 400;
        PacketSender.SendRequestGuildDirectory(_directoryQuery, _directoryPage);
    }

    private void DrawGuildDirectory()
    {
        AddLabel(_body, "DirectoryHeading", "GUILD DIRECTORY", 9, 7, 448, 30, 17, Gold, true);
        AddLabel(_body, "DirectoryColumns", "NAME                     MEMBERS     INVITATION",
            7, 43, 452, 19, 10, Cream);

        for (var i = 0; i < _directoryEntries.Length; i++)
        {
            var entry = _directoryEntries[i];
            var isSelected = entry.Id == _selectedGuild;
            var y = 65 + i * 29;
            var button = new Button(_body, "GuildChoice" + i) { Text = string.Empty };
            button.SetBounds(5, y, 456, 27);

            var frame = new CopperButton(button, "GuildChoiceFrame");
            frame.SetBounds(0, 0, 456, 27);
            var label = AddLabel(button, "GuildChoiceCaption",
                (isSelected ? ">> " : "   ") + Shorten(entry.Name, 20),
                7, 0, 246, 25, 10, isSelected ? Gold : Cream);
            AddLabel(button, "MemberCount", entry.MemberCount.ToString("N0"),
                250, 0, 79, 25, 10, isSelected ? Cream : Muted, true);
            AddLabel(button, "JoinPolicy", "INVITE", 338, 0, 109, 25, 9, Muted, true);
            button.SetToolTipText(entry.Name + " - invitations are issued by officers");
            button.Clicked += (_, _) =>
            {
                _selectedGuild = entry.Id;
                Refresh();
            };
        }

        if (_directoryEntries.Length == 0)
            AddLabel(_body, "DirectoryEmpty",
                _directoryTotal > 0 ? "No more guilds on this page." : "No guilds found. Try another search.",
                15, 167, 430, 40, 12, Muted, true);

        var pageCount = Math.Max(1, (_directoryTotal + 11) / 12);
        AddLabel(_body, "DirectoryPages",
            "PAGE " + (_directoryPage + 1) + "/" + pageCount +
            "  /  " + _directoryTotal + " GUILDS" +
            (string.IsNullOrEmpty(_directoryQuery) ? "" : "  /  " + Shorten(_directoryQuery, 18)),
            7, 412, 452, 22, 10, Gold, true);
        _body.SetInnerSize(468, 438);

        AddAction(_footer, "GuildDirectorySearch", "SEARCH", 2, 14, 89, 41, SearchGuildDirectory);
        AddAction(_footer, "GuildDirectoryPrev", "<", 98, 14, 44, 41,
            _directoryPage > 0 ? () => ChangeGuildDirectoryPage(-1) : null);
        AddAction(_footer, "GuildDirectoryNext", ">", 147, 14, 44, 41,
            (_directoryPage + 1) * 12 < _directoryTotal ? () => ChangeGuildDirectoryPage(1) : null);
        AddAction(_footer, "GuildDirectoryJoin", "REQUEST INVITE", 200, 14, 180, 41,
            _selectedGuild != Guid.Empty ? RequestGuildInvitation : null,
            "An officer must accept and send a guild invitation.");
        AddAction(_footer, "GuildDirectoryBack", "BACK", 384, 14, 101, 41,
            () =>
            {
                _guildDirectoryOpen = false;
                Refresh();
            });
    }

    private void ChangeGuildDirectoryPage(int amount)
    {
        _directoryPage = Math.Max(0, _directoryPage + amount);
        _directoryEntries = [];
        _selectedGuild = Guid.Empty;
        Refresh();
        RequestGuildDirectory();
    }

    private void SearchGuildDirectory()
    {
        _ = new InputBox(
            title: Intersect.Client.Localization.ClientLanguages.Text("Find a Guild"),
            prompt: "Search guild names. Leave blank to list all guilds.",
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox || args.Value is not StringSubmissionValue entry)
                    return;

                _directoryQuery = (entry.Value ?? string.Empty).Trim();
                if (_directoryQuery.Length > 50)
                    _directoryQuery = _directoryQuery[..50];
                _directoryPage = 0;
                _directoryTotal = 0;
                _directoryEntries = [];
                _selectedGuild = Guid.Empty;
                Refresh();
                RequestGuildDirectory();
            });
    }

    private void RequestGuildInvitation()
    {
        if (_selectedGuild == Guid.Empty)
            return;
        PacketSender.SendRequestGuildJoin(_selectedGuild);
    }
}
