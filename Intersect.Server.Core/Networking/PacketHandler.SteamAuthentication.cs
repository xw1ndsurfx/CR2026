using Intersect.Enums;
using Intersect.GameObjects;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Utilities;
using Intersect.Network.Packets.Client;
using Intersect.Server.Authentication;
using Intersect.Server.Database;
using Intersect.Server.Database.Logging.Entities;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Database.PlayerData.Security;
using Intersect.Server.Entities;
using Intersect.Server.General;
using Intersect.Server.Localization;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, SteamLoginPacket packet)
    {
        if (client.AccountAttempts > 3 && client.TimeoutMs > Timing.Global.Milliseconds)
        {
            PacketSender.SendError(client, Strings.Errors.ErrorTimeout, Strings.General.NoticeError);
            client.ResetTimeout();
            return;
        }

        client.ResetTimeout();

        if (Player.OnlinePlayers.Count >= Options.Instance.MaximumLoggedInUsers)
        {
            PacketSender.SendError(client, Strings.Networking.ServerFull, Strings.General.NoticeError);
            return;
        }

        var steamAuthentication = LogiklikSteamAuthService.Authenticate(packet.Ticket);
        if (!steamAuthentication.Ok)
        {
            UserActivityHistory.LogActivity(
                Guid.Empty,
                Guid.Empty,
                client.Ip,
                UserActivityHistory.PeerType.Client,
                UserActivityHistory.UserAction.FailedLogin,
                "steam"
            );
            client.FailedAttempt();
            PacketSender.SendError(
                client,
                string.IsNullOrWhiteSpace(steamAuthentication.Error)
                    ? "Steam authentication failed. You can still use the normal Logiklik login."
                    : steamAuthentication.Error,
                "Steam"
            );
            return;
        }

        var username = steamAuthentication.Username;
        if (!FieldChecking.IsValidUsername(username, Strings.Regex.Username))
        {
            PacketSender.SendError(client, "Steam returned an invalid Corps Royaux account.", "Steam");
            return;
        }

        List<TaskCompletionSource> logoutCompletionSources = [];
        lock (Client.GlobalLock)
        {
            foreach (var otherClient in Client.Instances.ToArray())
            {
                if (otherClient == null ||
                    otherClient == client ||
                    otherClient.IsEditor ||
                    !string.Equals(otherClient.Name, username, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                TaskCompletionSource source = new();
                otherClient.Disconnect(logoutCompletionSource: source);
                logoutCompletionSources.Add(source);
            }
        }

        if (logoutCompletionSources.Count > 0)
        {
            Task.WaitAll(logoutCompletionSources.Select(source => source.Task).ToArray());
        }

        if (!User.TryExternalLogin(username, out var user, out var failureReason))
        {
            UserActivityHistory.LogActivity(
                Guid.Empty,
                Guid.Empty,
                client.Ip,
                UserActivityHistory.PeerType.Client,
                UserActivityHistory.UserAction.FailedLogin,
                $"{username},steam,{failureReason.Type}"
            );
            PacketSender.SendError(client, Strings.Account.UnknownServerErrorRetryLogin, Strings.General.NoticeError);
            return;
        }

        client.SetUser(user);

        if (client.User != null)
        {
            client.PacketFloodingThresholds = Options.Instance.Security.Packets.PlayerThresholds;
            if (client.User.Power.IsAdmin || client.User.Power.IsModerator)
            {
                client.PacketFloodingThresholds = Options.Instance.Security.Packets.ModAdminThresholds;
            }
        }

        var isBanned = Ban.CheckBan(client.User, client.Ip);
        if (isBanned != null)
        {
            client.SetUser(null);
            client.Banned = true;
            PacketSender.SendError(client, isBanned, Strings.General.NoticeError);
            return;
        }

        if (Options.Instance.AdminOnly && client.Power == UserRights.None)
        {
            PacketSender.SendError(client, Strings.Account.AdminOnly, Strings.General.NoticeError);
            return;
        }

        Mute.FindMuteReason(client.User, client.Ip);

        UserActivityHistory.LogActivity(
            user?.Id ?? Guid.Empty,
            Guid.Empty,
            client.Ip,
            UserActivityHistory.PeerType.Client,
            UserActivityHistory.UserAction.Login,
            steamAuthentication.Created ? "steam:new-account" : "steam"
        );

        foreach (var character in client.Characters)
        {
            if (Player.FindOnline(character.Id) == null)
            {
                continue;
            }

            client.LoadCharacter(character);
            client.Entity.SetOnline();
            PacketSender.SendJoinGame(client);
            return;
        }

        if (client.Characters == default || client.Characters.Count < 1)
        {
            PacketSender.SendGameObjects(client, GameObjectType.Class);
            PacketSender.SendCreateCharacter(client, force: true);
            return;
        }

        if (Options.Instance.Player.MaxCharacters > 1 || !Options.Instance.Player.SkipCharacterSelect)
        {
            PacketSender.SendPlayerCharacters(client);
            return;
        }

        var selectedCharacter = DbInterface.GetUserCharacter(
            client.User,
            client.Characters.First().Id,
            explicitLoad: true
        );
        client.LoadCharacter(selectedCharacter);
        client.Entity.SetOnline();
        PacketSender.SendJoinGame(client);
    }
}
