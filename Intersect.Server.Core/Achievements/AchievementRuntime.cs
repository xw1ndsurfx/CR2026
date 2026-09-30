using Intersect.Enums;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Server.Professions;

namespace Intersect.Server.Achievements;

internal static class AchievementRuntime
{
    private const byte ProgressSalt = 0x31;
    private const byte CompletedSalt = 0x57;
    private const byte ClaimedSalt = 0x79;
    private const byte CompletedAtSalt = 0x9B;

    private static Guid StateId(Guid achievementId, byte salt)
    {
        var bytes = achievementId.ToByteArray();
        bytes[0] ^= salt;
        bytes[5] ^= (byte)(salt * 3);
        bytes[10] ^= (byte)(salt * 5);
        bytes[15] ^= (byte)(salt * 7);
        return new Guid(bytes);
    }

    private static long GetRaw(Player player, Guid achievementId, byte salt) =>
        Math.Max(0L, player.GetVariable(StateId(achievementId, salt))?.Value?.Integer ?? 0L);

    private static void SetRaw(Player player, Guid achievementId, byte salt, long value) =>
        player.SetVariableValue(StateId(achievementId, salt), Math.Max(0L, value));

    internal static long GetProgress(Player player, Guid achievementId) =>
        GetRaw(player, achievementId, ProgressSalt);

    internal static bool IsCompleted(Player player, Guid achievementId) =>
        GetRaw(player, achievementId, CompletedSalt) > 0;

    internal static bool IsClaimed(Player player, Guid achievementId) =>
        GetRaw(player, achievementId, ClaimedSalt) > 0;

    internal static long CompletedAt(Player player, Guid achievementId) =>
        GetRaw(player, achievementId, CompletedAtSalt);

    internal static void RefreshAbsoluteObjectives(Player player)
    {
        foreach (var definition in AchievementConfigurationRuntime.Current.Achievements)
        {
            if (IsCompleted(player, definition.Id))
                continue;

            switch (definition.ObjectiveType)
            {
                case AchievementObjectiveType.PlayerLevel:
                    SetProgress(player, definition, player.Level, sendState: false);
                    break;

                case AchievementObjectiveType.ProfessionLevel:
                    SetProgress(
                        player,
                        definition,
                        ProfessionRuntime.GetLevel(player, definition.TargetId),
                        sendState: false
                    );
                    break;
            }
        }
    }

    internal static void AddProgress(
        Player player,
        AchievementObjectiveType objectiveType,
        long amount = 1,
        Guid targetId = default,
        string? targetKey = null
    )
    {
        if (amount <= 0)
            return;

        var changed = false;
        foreach (var definition in AchievementConfigurationRuntime.Current.Achievements)
        {
            if (definition.ObjectiveType != objectiveType || IsCompleted(player, definition.Id))
                continue;

            if (definition.TargetId != Guid.Empty && definition.TargetId != targetId)
                continue;

            if (!string.IsNullOrWhiteSpace(definition.TargetKey) &&
                !string.Equals(definition.TargetKey, targetKey ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                continue;

            var current = GetProgress(player, definition.Id);
            var next = current > long.MaxValue - amount ? long.MaxValue : current + amount;
            changed |= SetProgress(player, definition, next, sendState: false);
        }

        if (changed)
            SendState(player);
    }

    internal static void SetAbsoluteProgress(
        Player player,
        AchievementObjectiveType objectiveType,
        long value,
        Guid targetId = default,
        string? targetKey = null
    )
    {
        var changed = false;
        foreach (var definition in AchievementConfigurationRuntime.Current.Achievements)
        {
            if (definition.ObjectiveType != objectiveType || IsCompleted(player, definition.Id))
                continue;

            if (definition.TargetId != Guid.Empty && definition.TargetId != targetId)
                continue;

            if (!string.IsNullOrWhiteSpace(definition.TargetKey) &&
                !string.Equals(definition.TargetKey, targetKey ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                continue;

            changed |= SetProgress(player, definition, value, sendState: false);
        }

        if (changed)
            SendState(player);
    }

    private static bool SetProgress(
        Player player,
        AchievementDefinition definition,
        long value,
        bool sendState
    )
    {
        if (IsCompleted(player, definition.Id))
            return false;

        var bounded = Math.Clamp(value, 0L, definition.TargetAmount);
        var previous = GetProgress(player, definition.Id);
        if (previous == bounded && bounded < definition.TargetAmount)
            return false;

        SetRaw(player, definition.Id, ProgressSalt, bounded);

        if (bounded >= definition.TargetAmount)
        {
            SetRaw(player, definition.Id, CompletedSalt, 1);
            SetRaw(
                player,
                definition.Id,
                CompletedAtSalt,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            );

            PacketSender.SendChatMsg(
                player,
                $"Achievement completed: {definition.Name}",
                ChatMessageType.Notice
            );
        }

        if (sendState)
            SendState(player);

        return true;
    }

    internal static bool TryClaim(Player player, Guid achievementId, out string error)
    {
        error = string.Empty;

        var definition = AchievementConfigurationRuntime.Current.Find(achievementId);
        if (definition == null)
        {
            error = "Achievement not found.";
            return false;
        }

        if (!IsCompleted(player, achievementId))
        {
            error = "This achievement is not completed yet.";
            return false;
        }

        if (IsClaimed(player, achievementId))
        {
            error = "This achievement reward was already claimed.";
            return false;
        }

        var reward = definition.Reward;

        // Item rewards intentionally use Overflow so a full inventory cannot cause a
        // half-claimed reward or enable duplicate rewards on a retry.
        if (reward.ItemId != Guid.Empty && reward.ItemQuantity > 0)
            player.TryGiveItem(reward.ItemId, reward.ItemQuantity, ItemHandling.Overflow);

        if (reward.CurrencyItemId != Guid.Empty && reward.CurrencyQuantity > 0)
            player.TryGiveItem(reward.CurrencyItemId, reward.CurrencyQuantity, ItemHandling.Overflow);

        if (reward.Experience > 0)
            player.GiveExperience(reward.Experience);

        if (reward.CommonEventId != Guid.Empty &&
            EventDescriptor.Get(reward.CommonEventId) is { CommonEvent: true } commonEvent)
            player.EnqueueStartCommonEvent(commonEvent);

        SetRaw(player, achievementId, ClaimedSalt, 1);
        PacketSender.SendChatMsg(
            player,
            $"Achievement reward claimed: {definition.Name}",
            ChatMessageType.Notice
        );
        SendState(player);
        return true;
    }

    internal static AchievementProgressEntry[] BuildState(Player player)
    {
        RefreshAbsoluteObjectives(player);

        return AchievementConfigurationRuntime.Current.Achievements
            .Select(
                definition => new AchievementProgressEntry(
                    definition.Id,
                    GetProgress(player, definition.Id),
                    IsCompleted(player, definition.Id),
                    IsClaimed(player, definition.Id),
                    CompletedAt(player, definition.Id)
                )
            )
            .ToArray();
    }

    internal static void SendState(Player player, bool openWindow = false)
    {
        player.SendPacket(
            new AchievementStatePacket(
                AchievementConfigurationRuntime.Json,
                BuildState(player),
                openWindow
            )
        );
    }
}
