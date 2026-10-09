using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;

namespace Intersect.Server.Entities;

public partial class Player
{
    private long _nextPetActionAt;

    /// <summary>Authoritative companion state, sent only to the owner.</summary>
    public void SendPetState(bool openWindow = false)
    {
        if (Client == null || !InGame)
            return;

        PetCollection.OwnedPets ??= [];

        var profiles = new List<PetProfileEntry>();
        foreach (var (petId, saved) in PetCollection.OwnedPets)
        {
            if (saved == null || !NPCDescriptor.TryGet(petId, out var descriptor) || !descriptor.IsPet)
                continue;

            var maximumLevel = Math.Clamp(descriptor.PetMaxLevel, 1, 200);
            var level = Math.Clamp(saved.Level, 1, maximumLevel);
            var active = ActivePet is { IsDead: false } pet && pet.Descriptor.Id == petId &&
                         pet.MapInstanceId == MapInstanceId;
            var runtime = active ? ActivePet : null;
            var maximumHealth = Math.Max(1L, descriptor.MaxVitals[(int)Vital.Health] +
                (long)(level - 1) * Math.Max(0, descriptor.PetHealthGrowth));
            var maximumMana = Math.Max(0L, descriptor.MaxVitals[(int)Vital.Mana]);

            var stats = runtime?.GetStatValues() ??
                descriptor.Stats.Select(stat => (int)Math.Min(int.MaxValue,
                    (long)stat + (long)(level - 1) * Math.Max(0, descriptor.PetStatGrowth))).ToArray();

            var interval = Math.Max(1, descriptor.PetSpellUnlockInterval);
            var abilities = descriptor.Spells
                .Select((spellId, index) => new { Spell = SpellDescriptor.Get(spellId), Index = index })
                .Where(entry => entry.Spell != null)
                .Select(entry => new PetAbilityEntry
                {
                    Name = entry.Spell!.Name,
                    Icon = entry.Spell.Icon ?? string.Empty,
                    RequiredLevel = 1 + entry.Index * interval,
                }).ToArray();

            profiles.Add(new PetProfileEntry
            {
                PetId = petId,
                Name = GetPetDisplayName(descriptor),
                SpeciesName = descriptor.Name,
                Sprite = descriptor.Sprite ?? string.Empty,
                Level = level,
                MaximumLevel = maximumLevel,
                Experience = Math.Max(0, saved.Experience),
                ExperienceToNextLevel = level >= maximumLevel ? 0 : saved.ExperienceToNextLevel,
                IsActive = active,
                AutoLoot = saved.AutoLoot,
                LootRadius = Math.Clamp(descriptor.PetLootRadius, 0, 8),
                Stats = stats,
                Health = runtime?.GetVital(Vital.Health) ?? maximumHealth,
                MaximumHealth = runtime?.GetMaxVital(Vital.Health) ?? maximumHealth,
                Mana = runtime?.GetVital(Vital.Mana) ?? maximumMana,
                MaximumMana = runtime?.GetMaxVital(Vital.Mana) ?? maximumMana,
                Abilities = abilities,
            });
        }

        SendPacket(new PetStatePacket
        {
            ActivePetId = PetCollection.ActivePetId,
            Pets = profiles.OrderBy(profile => profile.Name).ToArray(),
            OpenWindow = openWindow,
        });
    }

    public void HandlePetAction(PetActionKind action, Guid petId, string? requestedName = null)
    {
        if (!InGame || Client == null || IsDead)
            return;

        var time = Timing.Global.Milliseconds;
        if (time < _nextPetActionAt)
            return;

        _nextPetActionAt = time + 250;
        PetCollection.OwnedPets ??= [];

        switch (action)
        {
            case PetActionKind.Summon:
                if (petId != Guid.Empty && PetCollection.OwnedPets.ContainsKey(petId))
                    SummonPet(petId);
                break;

            case PetActionKind.Dismiss:
                if (ActivePet != null)
                {
                    PetCollection.ActivePetId = Guid.Empty;
                    DismissPetRuntime();
                }
                break;

            case PetActionKind.ToggleAutoLoot:
                if (petId != Guid.Empty && petId == PetCollection.ActivePetId &&
                    PetCollection.OwnedPets.TryGetValue(petId, out var progress) &&
                    progress != null)
                    progress.AutoLoot = !progress.AutoLoot;
                break;

            case PetActionKind.Rename:
                if (petId == Guid.Empty ||
                    !PetCollection.OwnedPets.TryGetValue(petId, out var renamedPet) ||
                    renamedPet == null ||
                    !NPCDescriptor.TryGet(petId, out var petDescriptor) || !petDescriptor.IsPet)
                    break;

                var newName = (requestedName ?? string.Empty).Trim().Normalize(
                    System.Text.NormalizationForm.FormC);

                // A blank name resets the nickname to the species' original name.
                // Validate on the server, never trust the client text field.
                if (newName.Length > 0 &&
                    (newName.Length < 2 || newName.Length > 24 ||
                     !newName.Any(char.IsLetterOrDigit) ||
                     newName.Any(character => !char.IsLetterOrDigit(character) &&
                         character != ' ' && character != '-' && character != '\'' &&
                         character != '’')))
                {
                    PetNotice("Nom invalide : 2 à 24 caractères (lettres, chiffres, espaces, tirets, apostrophes).");
                    break;
                }

                renamedPet.Nickname = newName;
                if (ActivePet is { } activeCompanion && activeCompanion.Descriptor.Id == petId)
                {
                    activeCompanion.RefreshPetDisplayName();
                    PacketSender.SendEntityDataToProximity(activeCompanion);
                }

                PetNotice(newName.Length == 0
                    ? "Nom du familier réinitialisé."
                    : "Ton familier s'appelle maintenant " + newName + " !");
                break;
        }

        SendPetState();
    }
}
