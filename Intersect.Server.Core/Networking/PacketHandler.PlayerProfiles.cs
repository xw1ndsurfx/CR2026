using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
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

            professions.Add(
                new PlayerProfessionProfilePacket
                {
                    ProfessionId = definition.Id,
                    Name = definition.Name,
                    Level = ProfessionRuntime.GetLevel(target, definition.Id),
                    MaximumLevel = definition.MaximumLevel,
                    Experience = ProfessionRuntime.GetExperience(target, definition.Id),
                    ExperienceToNextLevel = ProfessionRuntime.GetExperienceToNextLevel(target, definition.Id),
                }
            );
        }

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
            }
        );
    }
}
