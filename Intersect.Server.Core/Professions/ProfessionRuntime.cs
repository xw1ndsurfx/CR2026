using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.Jousting;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Achievements;
using Intersect.Server.Networking;

namespace Intersect.Server.Professions;

internal static class ProfessionRuntime
{
    private static long Raw(Player player, Guid id) => Math.Max(0L, player.GetVariable(id)?.Value?.Integer ?? 0L);
    private static long Total(Player player, Guid id) => Math.Max(0L, Raw(player, id) - 1L);

    internal static bool IsLearned(Player player, Guid id) => NativeProfessions.IsNative(id) || Raw(player, id) > 0;

    internal static int GetLevel(Player player, Guid id)
    {
        if (JoustProfession.IsNative(id)) return JoustProfession.Progress(GetExperience(player, id)).Level;
        if(NavalProfessions.IsNative(id))return Naval.NavalSystem.CareerProgress(player,id).Level;
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        return definition == null || !IsLearned(player, id) ? 0 : definition.LevelForExperience(GetExperience(player, id));
    }

    internal static long GetExperience(Player player, Guid id)
    {
        if (JoustProfession.IsNative(id)) return Math.Clamp(player.PetCollection.Jousting?.Experience ?? 0L, 0L, 1_000_000_000L);
        if(NavalProfessions.IsNative(id))return Naval.NavalSystem.CareerExperience(player,id);
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        return definition == null || !IsLearned(player, id)
            ? 0L
            : definition.ClampExperience(Total(player, id));
    }

    internal static long GetExperienceToNextLevel(Player player, Guid id)
    {
        if (JoustProfession.IsNative(id))
        {
            var joust = JoustProfession.Progress(GetExperience(player, id));
            return joust.Required == 0 ? -1 : joust.Required - joust.Experience;
        }
        if(NavalProfessions.IsNative(id)){var progress=Naval.NavalSystem.CareerProgress(player,id);return progress.Required<=0?-1:progress.Required-progress.Experience;}
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null || !IsLearned(player, id)) return -1;

        var total = GetExperience(player, id);
        var level = definition.LevelForExperience(total);
        if (level >= definition.MaximumLevel) return -1;

        return Math.Max(0L, definition.ExperienceToReachLevel(level + 1) - total);
    }

    internal static (long Experience, long Required) LevelProgress(Player player, Guid id)
    {
        if (JoustProfession.IsNative(id))
        {
            var joust = JoustProfession.Progress(GetExperience(player, id));
            return (joust.Experience, joust.Required);
        }
        if (NavalProfessions.IsNative(id))
        {
            var progress=Naval.NavalSystem.CareerProgress(player,id);
            return (progress.Experience,progress.Required);
        }
        var definition=ProfessionConfigurationRuntime.Current.Find(id);
        if (definition==null || !IsLearned(player,id)) return (0,0);
        var level=GetLevel(player,id);
        var floor=definition.ExperienceToReachLevel(level);
        return (Math.Max(0,GetExperience(player,id)-floor),level>=definition.MaximumLevel?0:definition.ExperienceToReachLevel(level+1)-floor);
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
        if(NativeProfessions.IsNative(id))return;
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null) return;
        player.SetVariableValue(id, 0L);
        PacketSender.SendChatMsg(player, $"[{definition.Name}] Profession forgotten.", ChatMessageType.Notice);
    }

    internal static void SetLevel(Player player, Guid id, int level)
    {
        if(NativeProfessions.IsNative(id))return;
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
        if(NativeProfessions.IsNative(id))return;
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null) return;

        if (!IsLearned(player, id)) Learn(player, id);

        var oldLevel = GetLevel(player, id);
        var total = definition.ClampExperience(experience);
        player.SetVariableValue(id, total + 1L);
        var newLevel = definition.LevelForExperience(total);
        AchievementRuntime.SetAbsoluteProgress(
            player,
            Intersect.Framework.Core.Achievements.AchievementObjectiveType.ProfessionLevel,
            newLevel,
            id
        );

        if (newLevel > oldLevel)
        {
            PacketSender.SendChatMsg(player, $"[{definition.Name}] Level {newLevel}!", ChatMessageType.Notice);
            GrantLevelRewards(player, definition, oldLevel, newLevel);
        }
    }

    internal static void AddExperience(Player player, Guid id, long amount)
    {
        if(NativeProfessions.IsNative(id))return;
        var definition = ProfessionConfigurationRuntime.Current.Find(id);
        if (definition == null || amount == 0) return;

        if (!IsLearned(player, id))
        {
            if (amount < 0) return;
            Learn(player, id);
        }

        var oldTotal = GetExperience(player, id);
        var baseAmount = amount;
        var eventPercent = amount > 0 ? player.ActivityExperienceBonus : 0;
        var eventBonus = Intersect.Framework.Core.WorldEvents.WorldActivitiesConfiguration.AwardBonus(amount, eventPercent);
        if (amount > 0) amount = (long)Math.Min(long.MaxValue, (decimal)amount + eventBonus);
        var oldLevel = definition.LevelForExperience(oldTotal);
        var proposedTotal = amount > 0 && oldTotal > long.MaxValue - amount
            ? long.MaxValue
            : Math.Max(0L, oldTotal + amount);
        var newTotal = definition.ClampExperience(proposedTotal);
        if (Raw(player, id) != newTotal + 1L)
            player.SetVariableValue(id, newTotal + 1L);
        var newLevel = definition.LevelForExperience(newTotal);
        AchievementRuntime.SetAbsoluteProgress(
            player,
            Intersect.Framework.Core.Achievements.AchievementObjectiveType.ProfessionLevel,
            newLevel,
            id
        );

        var experienceGranted = Math.Max(0L, newTotal - oldTotal);
        Intersect.Server.WorldEvents.WorldActivitiesRuntime.NotifyGain(player, definition.Name, baseAmount, experienceGranted, eventPercent);
        if (experienceGranted > 0)
            PacketSender.SendChatMsg(player, $"[{definition.Name}] +{experienceGranted:N0} profession XP.", ChatMessageType.Notice);

        if (newLevel > oldLevel)
        {
            PacketSender.SendChatMsg(player, $"[{definition.Name}] Level {newLevel}!", ChatMessageType.Notice);
            GrantLevelRewards(player, definition, oldLevel, newLevel);
        }
    }

    internal static void AwardPotionCompletion(Player player, long experience)
    {
        if (ProfessionConfigurationRuntime.Current.FindPotionProfession() is { } profession)
            AwardActivity(player, profession.Id, experience);
    }

    internal static void AwardActivity(Player player, Guid professionId, long amount)
    {
        var definition = ProfessionConfigurationRuntime.Current.Find(professionId);
        if (definition == null || amount <= 0) return;

        var oldLevel = GetLevel(player, professionId);
        var previousExperience = GetExperience(player, professionId);
        AddExperience(player, professionId, amount);
        var experienceGranted = Math.Max(0L, GetExperience(player, professionId) - previousExperience);
        SendHarvestProgress(player, definition, experienceGranted, oldLevel);
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
        var oldLevel = GetLevel(player, profession.Id);
        var previousExperience = GetExperience(player, profession.Id);

        if (link.Experience > 0)
            AddExperience(player, profession.Id, link.Experience);
        else if (!IsLearned(player, profession.Id))
            Learn(player, profession.Id);

        var experienceGranted = Math.Max(0L, GetExperience(player, profession.Id) - previousExperience);
        SendHarvestProgress(player, profession, experienceGranted, oldLevel);
    }

    private static void SendHarvestProgress(
        Player player,
        ProfessionDefinition profession,
        long experienceGained,
        int oldLevel
    )
    {
        if (!IsLearned(player, profession.Id))
            return;

        var totalExperience = GetExperience(player, profession.Id);
        var level = profession.LevelForExperience(totalExperience);
        var maximumLevelReached = level >= profession.MaximumLevel;

        long experienceIntoLevel;
        long experienceRequiredForLevel;
        long experienceToNextLevel;
        int percentage;

        if (maximumLevelReached)
        {
            experienceIntoLevel = 0;
            experienceRequiredForLevel = 0;
            experienceToNextLevel = 0;
            percentage = 100;
        }
        else
        {
            var currentLevelStart = profession.ExperienceToReachLevel(level);
            var nextLevelStart = profession.ExperienceToReachLevel(level + 1);

            experienceIntoLevel = Math.Max(0L, totalExperience - currentLevelStart);
            experienceRequiredForLevel = Math.Max(1L, nextLevelStart - currentLevelStart);
            experienceToNextLevel = Math.Max(0L, nextLevelStart - totalExperience);

            var progress = (decimal)experienceIntoLevel * 100m / experienceRequiredForLevel;
            percentage = (int)Math.Clamp(
                Math.Round(progress, MidpointRounding.AwayFromZero),
                0m,
                100m
            );
        }

        player.SendPacket(
            new ProfessionProgressPacket
            {
                ProfessionId = profession.Id,
                ProfessionName = profession.Name,
                Level = level,
                MaximumLevel = profession.MaximumLevel,
                ExperienceGained = Math.Max(0L, experienceGained),
                ExperienceIntoLevel = experienceIntoLevel,
                ExperienceRequiredForLevel = experienceRequiredForLevel,
                ExperienceToNextLevel = experienceToNextLevel,
                Percentage = percentage,
                LeveledUp = level > oldLevel,
                MaximumLevelReached = maximumLevelReached,
            }
        );
    }

    /// <summary>
    /// Normalize experience stored as cumulative XP + 1 in player variables.
    /// The limit is the XP required for the maximum profession level, not zero.
    /// </summary>
    internal static void ClampPlayerExperienceAtCaps(Player player, IEnumerable<ProfessionDefinition>? definitions = null)
    {
        foreach (var definition in definitions ?? ProfessionConfigurationRuntime.Current.Professions)
        {
            var variable = player.GetVariable(definition.Id);
            if (variable == null || variable.Value.Integer <= 0)
                continue;

            var current = Math.Max(0L, variable.Value.Integer - 1L);
            var capped = definition.ClampExperience(current);
            if (capped != current)
                player.SetVariableValue(definition.Id, capped + 1L);
        }
    }

    /// <summary>
    /// Repair already-banked profession XP on inactive characters as well.
    /// Run at startup and before raising a profession cap in the editor.
    /// </summary>
    internal static int ClampStoredExperienceAtCaps(IEnumerable<ProfessionDefinition> definitions)
    {
        var byId = definitions.ToDictionary(definition => definition.Id);
        if (byId.Count == 0)
            return 0;

        const int batchSize = 500;
        var professionIds = byId.Keys.ToArray();
        var processed = 0;
        var updated = 0;

        while (true)
        {
            using var context = DbInterface.CreatePlayerContext(
                readOnly: false, queryTrackingBehavior: QueryTrackingBehavior.TrackAll
            );
            var batch = context.Player_Variables
                .Where(StoredProfessionVariableFilter(professionIds))
                .OrderBy(variable => variable.Id)
                .Skip(processed)
                .Take(batchSize)
                .ToList();
            if (batch.Count == 0)
                break;

            foreach (var variable in batch)
            {
                var currentRaw = variable.Value.Integer;
                if (currentRaw <= 0)
                    continue;

                var definition = byId[variable.VariableId];
                var current = currentRaw - 1L;
                var capped = definition.ClampExperience(current);
                if (capped == current)
                    continue;

                variable.Value.Integer = capped + 1L;
                context.Entry(variable).Property(entry => entry.Json).IsModified = true;
                updated++;
            }

            context.SaveChanges();
            processed += batch.Count;
        }

        // Keep online in-memory characters synchronized with persisted data.
        foreach (var player in Player.OnlinePlayersSnapshot())
            ClampPlayerExperienceAtCaps(player, byId.Values);

        return updated;
    }

    private static Expression<Func<PlayerVariable, bool>> StoredProfessionVariableFilter(Guid[] professionIds) =>
        // C# 14 can bind array.Contains to MemoryExtensions.Contains (ReadOnlySpan),
        // which EF Core 8 cannot interpret. Keep the translatable Enumerable overload explicit.
        variable => Enumerable.Contains(professionIds, variable.VariableId);

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
