using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Entities;
using Intersect.Server.Networking;

namespace Intersect.Server.Professions;

internal static class ProfessionRuntime
{
    private static long Raw(Player player, Guid id) => Math.Max(0L, player.GetVariable(id)?.Value?.Integer ?? 0L);
    private static long Total(Player player, Guid id) => Math.Max(0L, Raw(player, id) - 1L);

    internal static bool IsLearned(Player player, Guid id) => Raw(player, id) > 0;

    internal static int GetLevel(Player player, Guid id)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        return definition == null || !IsLearned(player, id) ? 0 : definition.LevelForExperience(Total(player, id));
    }

    internal static long GetExperience(Player player, Guid id) =>
        IsLearned(player, id) ? Total(player, id) : 0L;

    internal static long GetExperienceToNextLevel(Player player, Guid id)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null || !IsLearned(player, id)) return -1;

        var total = Total(player, id);
        var level = definition.LevelForExperience(total);
        if (level >= definition.MaximumLevel) return -1;

        return Math.Max(0L, definition.ExperienceToReachLevel(level + 1) - total);
    }

    internal static void Learn(Player player, Guid id)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null || IsLearned(player, id)) return;
        player.SetVariableValue(id, 1L);
        PacketSender.SendChatMsg(player, $"[{definition.Name}] Profession learned.", ChatMessageType.Notice);
    }

    internal static void Forget(Player player, Guid id)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null) return;
        player.SetVariableValue(id, 0L);
        PacketSender.SendChatMsg(player, $"[{definition.Name}] Profession forgotten.", ChatMessageType.Notice);
    }

    internal static void SetLevel(Player player, Guid id, int level)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null) return;
        if (level <= 0) { Forget(player, id); return; }

        var oldLevel = GetLevel(player, id);
        var target = Math.Clamp(level, 1, definition.MaximumLevel);
        var total = definition.ExperienceToReachLevel(target);
        player.SetVariableValue(id, total == long.MaxValue ? long.MaxValue : total + 1L);
        if (target > oldLevel) GrantLevelRewards(player, definition, oldLevel, target);
    }

    internal static void SetExperience(Player player, Guid id, long experience)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null) return;

        if (!IsLearned(player, id)) Learn(player, id);

        var oldLevel = GetLevel(player, id);
        var total = Math.Max(0L, experience);
        player.SetVariableValue(id, total == long.MaxValue ? long.MaxValue : total + 1L);
        var newLevel = definition.LevelForExperience(total);

        if (newLevel > oldLevel)
        {
            PacketSender.SendChatMsg(player, $"[{definition.Name}] Level {newLevel}!", ChatMessageType.Notice);
            GrantLevelRewards(player, definition, oldLevel, newLevel);
        }
    }

    internal static void AddExperience(Player player, Guid id, long amount)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null || amount == 0) return;

        if (!IsLearned(player, id))
        {
            if (amount < 0) return;
            Learn(player, id);
        }

        var oldTotal = Total(player, id);
        var oldLevel = definition.LevelForExperience(oldTotal);
        var newTotal = amount > 0 && oldTotal > long.MaxValue - amount ? long.MaxValue : Math.Max(0L, oldTotal + amount);
        player.SetVariableValue(id, newTotal == long.MaxValue ? long.MaxValue : newTotal + 1L);
        var newLevel = definition.LevelForExperience(newTotal);

        if (amount > 0)
            PacketSender.SendChatMsg(player, $"[{definition.Name}] +{amount:N0} profession XP.", ChatMessageType.Notice);

        if (newLevel > oldLevel)
        {
            PacketSender.SendChatMsg(player, $"[{definition.Name}] Level {newLevel}!", ChatMessageType.Notice);
            GrantLevelRewards(player, definition, oldLevel, newLevel);
        }
    }

    internal static bool CanHarvest(Player player, ResourceDescriptor resource, out string error)
    {
        error = string.Empty;
        var match = ProfessionConfigurationRuntime.Current.FindResource(resource.Id);
        if (match == null) return true;

        var (profession, link) = match.Value;
        var level = GetLevel(player, profession.Id);
        if (level == 0 && link.RequiredLevel <= 1) return true;
        if (level >= link.RequiredLevel) return true;

        error = $"{profession.Name} level {link.RequiredLevel} is required to harvest {resource.Name}. Your level: {level}.";
        return false;
    }

    internal static void AwardHarvest(Player player, Guid resourceId)
    {
        var match = ProfessionConfigurationRuntime.Current.FindResource(resourceId);
        if (match == null) return;
        var (profession, link) = match.Value;
        if (link.Experience > 0) AddExperience(player, profession.Id, link.Experience);
        else if (!IsLearned(player, profession.Id)) Learn(player, profession.Id);
    }

    private static void GrantLevelRewards(Player player, ProfessionDefinition definition, int oldLevel, int newLevel)
    {
        foreach (var reward in definition.LevelRewards.Where(r => r.Level > oldLevel && r.Level <= newLevel).OrderBy(r => r.Level))
        {
            if (reward.ItemId != Guid.Empty && reward.Quantity > 0)
                player.TryGiveItem(reward.ItemId, reward.Quantity);

            if (reward.EventId != Guid.Empty &&
                Intersect.Framework.Core.GameObjects.Events.EventDescriptor.Get(reward.EventId) is { CommonEvent: true } evt)
                player.EnqueueStartCommonEvent(evt);
        }
    }
}
