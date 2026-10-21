using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Leaderboards;
using Intersect.Server.MiniGames.Lockpicking;
using Intersect.Server.Professions;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestPlayerProfilePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } requester)
            return;

        Player? target = null;

        if (packet.PlayerId != Guid.Empty)
            target = Player.FindOnline(packet.PlayerId);

        if (target == null && !string.IsNullOrWhiteSpace(packet.PlayerName))
            target = Player.FindOnline(packet.PlayerName);

        if (target == null)
        {
            var offline = packet.PlayerId != Guid.Empty ? Player.Find(packet.PlayerId)
                : !string.IsNullOrWhiteSpace(packet.PlayerName) ? Player.Find(packet.PlayerName) : null;
            if (offline != null)
            {
                client.Send(new PlayerProfilePacket { Found = true, OpenWindow = packet.OpenWindow, PlayerId = offline.Id,
                    Name = offline.Name, OfflineHouseProfile = true, PublicHouse = Intersect.Server.Housing.HousingRuntime.PublicHouse(offline) });
                return;
            }
            client.Send(
                new PlayerProfilePacket
                {
                    Found = false,
                    Message = string.IsNullOrWhiteSpace(packet.PlayerName)
                        ? "That character is not online."
                        : $"{packet.PlayerName} is not online. Character information is available while the player is online.",
                    OpenWindow = packet.OpenWindow,
                }
            );
            return;
        }

        var stats = new int[Enum.GetValues<Stat>().Length];
        for (var index = 0; index < stats.Length; ++index)
            stats[index] = target.Stat[index].Value();

        var equipment = new List<PlayerEquipmentProfilePacket>();
        for (var slotIndex = 0; slotIndex < Options.Instance.Equipment.Slots.Count; ++slotIndex)
        {
            var slotName = Options.Instance.Equipment.Slots[slotIndex];
            var itemId = Guid.Empty;
            var itemName = "Empty";

            if (target.TryGetEquippedItem(slotIndex, out var item) && item.Descriptor != null)
            {
                itemId = item.ItemId;
                itemName = item.Descriptor.Name;
            }

            equipment.Add(
                new PlayerEquipmentProfilePacket
                {
                    SlotIndex = slotIndex,
                    SlotName = slotName,
                    ItemId = itemId,
                    ItemName = itemName,
                    Properties = target.TryGetEquippedItem(slotIndex, out var equippedItem)
                        ? new ItemProperties(equippedItem.Properties)
                        : null,
                }
            );
        }

        var professions = new List<PlayerProfessionProfilePacket>();
        foreach (var definition in ProfessionConfigurationRuntime.Current.Professions.OrderBy(x => x.Name))
        {
            if (!ProfessionRuntime.IsLearned(target, definition.Id))
                continue;

            var lockStats = LockpickingStatsRuntime.Get(target, definition.Id);
            professions.Add(
                new PlayerProfessionProfilePacket
                {
                    ProfessionId = definition.Id,
                    Name = definition.Name,
                    Level = ProfessionRuntime.GetLevel(target, definition.Id),
                    MaximumLevel = Intersect.Framework.Core.Naval.NavalProfessions.IsNative(definition.Id)?Naval.NavalSystem.CareerMaximum(definition.Id):definition.MaximumLevel,
                    Experience = ProfessionRuntime.GetExperience(target, definition.Id),
                    ExperienceToNextLevel = ProfessionRuntime.GetExperienceToNextLevel(target, definition.Id),
                    LevelExperience = ProfessionRuntime.LevelProgress(target, definition.Id).Experience,
                    LevelExperienceRequired = ProfessionRuntime.LevelProgress(target, definition.Id).Required,
                    LocksPicked = lockStats.LocksPicked,
                    PerfectPicks = lockStats.PerfectPicks,
                    PicksBroken = lockStats.PicksBroken,
                    HighestLockDifficulty = lockStats.HighestDifficulty,
                    FastestLockpickMilliseconds = lockStats.FastestPickMilliseconds,
                    FastestDifficultyFiveLockpickMilliseconds = lockStats.FastestDifficultyFiveMilliseconds,
                }
            );
        }

        var miniGames=new Intersect.Server.MiniGames.Progression.SqliteMiniGameProgressStore(Path.Combine("resources","minigames-test.db"));
        foreach(var (key,name,id) in new[]{("potions","Alchemy",1),("poker","Poker",2),("blackjack","Blackjack",3),("roulette","Roulette",4)})
        {
            var progress=miniGames.Load(target.Id,key);
            if(key!="potions")
            {
                try
                {
                    var ledger=Intersect.Server.MiniGames.Currency.PokerInventoryBridge.Ledger;
                    var funded=key=="poker"?ledger.Profile(target.Id):key=="blackjack"?ledger.BlackjackProfile(target.Id):ledger.RouletteProfile(target.Id);
                    if(funded.Experience>progress.Experience)progress=funded;
                }
                catch(NotSupportedException) { /* Unfunded mini-game progression still exists on other database providers. */ }
            }
            var level=progress.Level;
            var floor=Intersect.Framework.Core.MiniGames.MiniGameProgression.ExperienceAtLevel(level);
            var required=level>=Intersect.Framework.Core.MiniGames.MiniGameProgression.MaximumLevel?0:Intersect.Framework.Core.MiniGames.MiniGameProgression.ExperienceAtLevel(level+1)-floor;
            professions.Add(new PlayerProfessionProfilePacket {
                ProfessionId=new Guid($"c1026000-0000-4000-8000-0000000001{id:00}"),Name=name,Level=level,
                MaximumLevel=Intersect.Framework.Core.MiniGames.MiniGameProgression.MaximumLevel,Experience=progress.Experience,
                ExperienceToNextLevel=required<=0?-1:required-(progress.Experience-floor),LevelExperience=progress.Experience-floor,LevelExperienceRequired=required});
        }
        // Use the same directional layer order as the Character Window.
        // Sending the resolved sprites avoids relying on other players'
        // inventory state being available to the observing client.
        var paperdollSprites = Options.Instance.Equipment.Paperdoll.Directions[1]
            .Select(slot =>
            {
                var equipmentSlot = Options.Instance.Equipment.Slots.IndexOf(slot);
                if (equipmentSlot < 0 ||
                    !target.TryGetEquippedItem(equipmentSlot, out var equipped) ||
                    equipped.Descriptor == null)
                    return string.Empty;

                return ((int)target.Gender == 0
                    ? equipped.Descriptor.MalePaperdoll
                    : equipped.Descriptor.FemalePaperdoll) ?? string.Empty;
            })
            .ToArray();

        var rankingTitle = LeaderboardTitleRuntime.GetStatus(target.Id);

        client.Send(
            new PlayerProfilePacket
            {
                Found = true,
                OpenWindow = packet.OpenWindow,
                PlayerId = target.Id,
                Name = target.Name,
                Level = target.Level,
                ClassName = ClassDescriptor.GetName(target.ClassId),
                GuildName = target.Guild?.Name ?? string.Empty,
                MapName = target.MapName,
                Experience = target.Exp,
                ExperienceToNextLevel = target.ExperienceToNextLevel,
                Stats = stats,
                Equipment = equipment.ToArray(),
                Professions = professions.ToArray(),
                IsSelf = requester.Id == target.Id,
                PublicHouse = Intersect.Server.Housing.HousingRuntime.PublicHouse(target),
                PvpGrade=Intersect.Server.Pvp.PvpRuntime.Grade(target),PvpPoints=Intersect.Server.Pvp.PvpRuntime.Points(target),
                RankingTitle = rankingTitle?.Title ?? string.Empty,
                RankingExperienceBonusPercent = rankingTitle?.ExperienceBonusPercent ?? 0,
                RankingPosition = rankingTitle?.Rank ?? 0,
                RankingConsecutiveDays = rankingTitle?.ConsecutiveDays ?? 0,
                Sprite = target.Sprite ?? string.Empty,
                PaperdollSprites = paperdollSprites,
                LevelExperienceRequired = target.ExperienceToNextLevel,
            }
        );
    }
}
