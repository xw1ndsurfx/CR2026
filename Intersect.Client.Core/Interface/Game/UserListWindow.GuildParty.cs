using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class UserListWindow
{
    private void DrawGuild()
    {
        var player = Globals.Me;
        if (player == null || string.IsNullOrWhiteSpace(player.Guild))
        {
            AddLabel(_body, "GuildHeading", "FIND A GUILD", 0, 40, 459, 45,
                19, Gold, true);
            AddLabel(_body, "GuildInviteOnly",
                "Joining a guild currently requires a guild invitation.\n" +
                "The in-game server doesn't provide a public guild directory\n" +
                "or an application/join-by-name packet yet.",
                24, 145, 412, 90, 12, Cream, true);
            _body.SetInnerSize(466, 426);
            AddAction(_footer, "FindGuildUnavailable", "FIND A GUILD",
                153, 7, 180, 49, null,
                "Guild search and public joining are not available in the current game protocol.");
            return;
        }

        GuildMember[] members = player.GuildMembers ?? [];
        var ranked = members
            .OrderBy(member => member.Rank)
            .ThenByDescending(member => member.Online)
            .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AddLabel(_body, "GuildName", Shorten(player.Guild, 31), 17, 7,
            435, 34, 22, Cream, true);
        AddLabel(_body, "GuildSummary",
            ranked.Length + " MEMBERS  /  " + ranked.Count(member => member.Online) + " ONLINE",
            13, 43, 445, 22, 11, Gold, true);

        AddLabel(_body, "GuildTableHead", "NAME          LV     RANK          ONLINE",
            13, 70, 444, 24, 12, Gold, true);

        var y = 101;
        foreach (var member in ranked)
        {
            var statusColor = member.Online ? Cream : Muted;
            AdmurinGuiArt.Add(_body, "GuildBadge" + member.Id, "userlist_2",
                13, y + 4, 20, 20, member.Online ? 0 : 16, 0, 16, 16);

            var name = AddLabel(_body, "GuildName" + member.Id, Shorten(member.Name, 17),
                38, y, 167, 26, 11, statusColor);
            if (member.Online)
            {
                name.MouseInputEnabled = true;
                name.SetToolTipText("View Character Information");
                name.Clicked += (_, _) => PacketSender.SendRequestPlayerProfile(member.Id, openWindow: true);
            }
            AddLabel(_body, "GuildLevel" + member.Id, member.Level.ToString(),
                207, y, 42, 26, 11, statusColor, true);

            var ranks = Options.Instance.Guild.Ranks;
            var title = member.Rank >= 0 && member.Rank < ranks.Length
                ? ranks[member.Rank].Title
                : "Member";
            AddLabel(_body, "GuildRank" + member.Id, Shorten(title, 13),
                252, y, 129, 26, 10, statusColor, true);

            if (member.Online)
            {
                AdmurinGuiArt.Add(_body, "OnlineBadge" + member.Id, "userlist_4",
                    394, y + 2, 43, 24, 0, 0, 28, 17);
            }
            else
            {
                AddLabel(_body, "OfflineStatus" + member.Id, "OFF",
                    390, y, 59, 25, 10, Muted, true);
            }
            y += 35;
        }

        if (ranked.Length == 0)
            AddLabel(_body, "NoGuildRoster", "No guild members are available yet.",
                10, 165, 449, 35, 12, Muted, true);

        _body.SetInnerSize(468, Math.Max(426, y + 10));

        var canInvite = player.GuildRank != null &&
                        (player.Rank == 0 || player.GuildRank.Permissions.Invite);
        AddAction(_footer, "GuildInvite", "INVITE", 6, 13, 104, 42,
            canInvite ? InviteGuildMember : null,
            canInvite ? null : "Your guild rank cannot invite members.");
        AddAction(_footer, "GuildManage", "MANAGE", 119, 13, 126, 42,
            _openGuildManagement, "Open the existing guild administration menu.");
        AddAction(_footer, "GuildLeave", "LEAVE", 254, 13, 112, 42,
            player.Rank == 0 ? null : LeaveGuild,
            player.Rank == 0 ? "The guild owner must transfer ownership before leaving." : null);
    }

    private static void InviteGuildMember()
    {
        _ = new InputBox(
            title: "Invite Guild Member",
            prompt: "Enter the character name to invite into your guild.",
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox ||
                    args.Value is not Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent.StringSubmissionValue value)
                    return;
                var name = value.Value?.Trim();
                if (name is { Length: >= 3 })
                    PacketSender.SendInviteGuild(name);
            });
    }

    private static void LeaveGuild()
    {
        _ = new InputBox(
            title: "Leave Guild",
            prompt: "Are you sure you want to leave your guild?",
            inputType: InputType.YesNo,
            onSubmit: (_, _) => PacketSender.SendLeaveGuild());
    }

    private void DrawParty()
    {
        var player = Globals.Me;
        var members = player?.Party;
        var count = members?.Count ?? 0;

        AddLabel(_body, "PartyHeader",
            count > 0 ? "PARTY   " + count + " / " + Options.Instance.Party.MaximumMembers
                      : "NO PARTY FORMED",
            7, 4, 446, 26, 13, Gold, true);

        if (player != null && members != null)
        {
            var leader = count > 0 && members[0].Id == player.Id;
            for (var index = 0; index < count; index++)
            {
                var member = members[index];
                var y = 33 + index * 86;

                AdmurinGuiArt.Add(_body, "PartyBadge" + index, "userlist_2",
                    12, y + 3, 23, 22, 0, 0, 16, 16);

                var nameButton = new Button(_body, "PartyMemberName" + index)
                {
                    Text = string.Empty,
                };
                nameButton.SetBounds(37, y, 286, 31);
                AddLabel(nameButton, "Name",
                    Shorten(member.Name, 23) + " - Lv. " + member.Level,
                    0, 0, 281, 27, 12, Cream);
                nameButton.SetToolTipText("View Character Information: " + member.Name);
                nameButton.Clicked += (_, _) =>
                    PacketSender.SendRequestPlayerProfile(member.Id, openWindow: true);

                if (leader && index > 0)
                {
                    var targetId = member.Id;
                    AddAction(_body, "PartyKick" + index, "KICK",
                        367, y, 84, 27, () => PacketSender.SendPartyKick(targetId));
                }
                else
                {
                    AddLabel(_body, "PartyRole" + index,
                        index == 0 ? "LEADER" : member.Id == player.Id ? "YOU" : "MEMBER",
                        348, y, 104, 26, 11, Gold, true);
                }

                var hpMax = member.MaxVital.Length > (int)Vital.Health
                    ? member.MaxVital[(int)Vital.Health] : 0L;
                var hp = member.Vital.Length > (int)Vital.Health
                    ? member.Vital[(int)Vital.Health] : 0L;
                var mpMax = member.MaxVital.Length > (int)Vital.Mana
                    ? member.MaxVital[(int)Vital.Mana] : 0L;
                var mp = member.Vital.Length > (int)Vital.Mana
                    ? member.Vital[(int)Vital.Mana] : 0L;

                var healthBar = new VitalBar(_body, "PartyHP" + index, hpMax > 0 ? (double)hp / hpMax : 0,
                    new Color(a: 255, r: 225, g: 56, b: 77),
                    new Color(a: 255, r: 88, g: 18, b: 31));
                healthBar.SetBounds(14, y + 33, 214, 21);
                healthBar.SetToolTipText("HP: " + hp.ToString("N0") + " / " + hpMax.ToString("N0"));

                var manaBar = new VitalBar(_body, "PartyMP" + index, mpMax > 0 ? (double)mp / mpMax : 0,
                    new Color(a: 255, r: 74, g: 204, b: 220),
                    new Color(a: 255, r: 19, g: 75, b: 82));
                manaBar.SetBounds(236, y + 33, 215, 21);
                manaBar.SetToolTipText("MP: " + mp.ToString("N0") + " / " + mpMax.ToString("N0"));

                AddLabel(_body, "HpCaption" + index, hp.ToString("N0") + " / " + hpMax.ToString("N0"),
                    14, y + 59, 213, 17, 9, Muted, true);
                AddLabel(_body, "MpCaption" + index, mp.ToString("N0") + " / " + mpMax.ToString("N0"),
                    236, y + 59, 215, 17, 9, Muted, true);
            }
        }

        if (count == 0)
            AddLabel(_body, "EmptyParty",
                "Invite a character to start a party with friends.",
                12, 146, 442, 58, 12, Cream, true);

        _body.SetInnerSize(468, Math.Max(426, 38 + count * 86));

        AddAction(_footer, "InviteParty", "INVITE", 100, 12, 131, 43,
            InvitePartyMember);
        AddAction(_footer, "LeaveParty", "LEAVE PARTY", 241, 12, 139, 43,
            count > 0 ? LeaveParty : null,
            count > 0 ? null : "You aren't currently in a party.");
    }

    private static void InvitePartyMember()
    {
        _ = new InputBox(
            title: "Invite to Party",
            prompt: "Enter the character name to invite to your party.",
            inputType: InputType.TextInput,
            onSubmit: (control, args) =>
            {
                if (control is not InputBox ||
                    args.Value is not Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent.StringSubmissionValue value)
                    return;
                var name = value.Value?.Trim();
                if (name is { Length: >= 3 })
                    PacketSender.SendPartyInvite(name);
            });
    }

    private static void LeaveParty()
    {
        _ = new InputBox(
            title: "Leave Party",
            prompt: "Are you sure you want to leave the party?",
            inputType: InputType.YesNo,
            onSubmit: (_, _) => PacketSender.SendPartyLeave());
    }

    private sealed class VitalBar : Base
    {
        private readonly double _ratio;
        private readonly Color _fill;
        private readonly Color _empty;

        public VitalBar(Base parent, string name, double ratio, Color fill, Color empty)
            : base(parent, name)
        {
            _ratio = Math.Clamp(ratio, 0, 1);
            _fill = fill;
            _empty = empty;
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            skin.Renderer.DrawColor = new Color(a: 255, r: 17, g: 16, b: 15);
            skin.Renderer.DrawFilledRect(rect);
            var inner = new UiRectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
            skin.Renderer.DrawColor = _empty;
            skin.Renderer.DrawFilledRect(inner);
            var width = (int)Math.Round(inner.Width * _ratio);
            if (width > 0)
            {
                skin.Renderer.DrawColor = _fill;
                skin.Renderer.DrawFilledRect(new UiRectangle(inner.X, inner.Y, width, inner.Height));
            }
        }
    }
}
