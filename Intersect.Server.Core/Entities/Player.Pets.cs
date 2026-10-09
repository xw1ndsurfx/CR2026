using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public sealed class PetCollection
{
    public Guid ActivePetId { get; set; }
    public Dictionary<Guid, PetProgress> OwnedPets { get; set; } = [];
}

public sealed class PetProgress
{
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public bool AutoLoot { get; set; } = true;
    public long ExperienceToNextLevel => 100L * Math.Max(1, Level) * Math.Max(1, Level);
}

public partial class Player
{
    [JsonIgnore, Column("Pets")]
    public string PetsJson
    {
        get => JsonConvert.SerializeObject(PetCollection);
        set => PetCollection = string.IsNullOrWhiteSpace(value) ? new PetCollection() :
            JsonConvert.DeserializeObject<PetCollection>(value) ?? new PetCollection();
    }

    [NotMapped, JsonIgnore]
    public PetCollection PetCollection { get; set; } = new();

    [NotMapped, JsonIgnore]
    public Npc? ActivePet { get; private set; }

    private void PetNotice(string message) =>
        PacketSender.SendChatMsg(this, message, ChatMessageType.Notice);

    public bool TryActivatePetItem(Guid itemId)
    {
        if (itemId == Guid.Empty) return false;
        var descriptor = NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
            .FirstOrDefault(npc => npc.IsPet && npc.PetSummonItemId == itemId);
        if (descriptor == null) return false;
        PetCollection.OwnedPets ??= [];
        PetCollection.OwnedPets.TryAdd(descriptor.Id, new PetProgress());
        SummonPet(descriptor.Id);
        return true;
    }

    private void SummonPet(Guid npcId)
    {
        PetCollection.OwnedPets ??= [];
        if (!PetCollection.OwnedPets.ContainsKey(npcId) ||
            !NPCDescriptor.TryGet(npcId, out var descriptor) || !descriptor.IsPet)
        {
            PetNotice("Ce familier n'est pas debloque.");
            return;
        }
        if (ActivePet != null && PetCollection.ActivePetId == npcId)
        {
            PetCollection.ActivePetId = Guid.Empty;
            DismissPetRuntime();
            PetNotice("Familier range.");
            SendPetState();
            return;
        }
        DismissPetRuntime();
        PetCollection.ActivePetId = npcId;
        SpawnPetRuntime(descriptor);
        PetNotice(descriptor.Name + " est maintenant ton familier !");
        SendPetState();
    }

    private void SpawnPetRuntime(NPCDescriptor? descriptor = null)
    {
        if (!InGame || IsDead || Client == null || MapId == Guid.Empty) return;
        PetCollection.OwnedPets ??= [];
        descriptor ??= NPCDescriptor.Get(PetCollection.ActivePetId);
        if (descriptor == null || !descriptor.IsPet ||
            !PetCollection.OwnedPets.TryGetValue(descriptor.Id, out var progress) ||
            !MapController.TryGetInstanceFromMap(MapId, MapInstanceId, out var instance)) return;

        var pet = new Npc(descriptor, despawnable: true)
        {
            PetOwner = this, MapId = MapId, MapInstanceId = MapInstanceId,
            X = X, Y = Y, Z = Z, Dir = Dir, Passable = true
        };
        pet.ApplyPetLevel(progress.Level);
        ActivePet = pet;
        instance.AddEntity(pet);
        PacketSender.SendEntityDataToProximity(pet);
    }

    private void DismissPetRuntime()
    {
        var pet = ActivePet;
        ActivePet = null;
        if (pet == null) return;
        pet.PetOwner = null;
        if (MapController.TryGetInstanceFromMap(pet.MapId, pet.MapInstanceId, out var instance))
            instance.RemoveEntity(pet);
        PacketSender.SendEntityLeave(pet);
        pet.Dispose();
    }

    public void OnPetDied(Npc pet)
    {
        if (ActivePet != pet) return;
        ActivePet = null;
        PetCollection.ActivePetId = Guid.Empty;
        PetNotice("Ton familier est tombe au combat. Utilise /pet summon pour le rappeler.");
        SendPetState();
    }

    private void UpdateActivePet(long timeMs)
    {
        if (Client == null || !InGame || IsDead || MapId == Guid.Empty) return;
        if (PetCollection.ActivePetId == Guid.Empty)
        {
            if (ActivePet != null) DismissPetRuntime();
            return;
        }
        if (ActivePet == null)
        {
            SpawnPetRuntime();
            return;
        }
        if (ActivePet.MapId != MapId || ActivePet.MapInstanceId != MapInstanceId)
        {
            DismissPetRuntime();
            SpawnPetRuntime();
        }
    }

    public void AwardPetExperience(long npcExperience)
    {
        PetCollection.OwnedPets ??= [];
        if (npcExperience <= 0 || PetCollection.ActivePetId == Guid.Empty ||
            !PetCollection.OwnedPets.TryGetValue(PetCollection.ActivePetId, out var progress) ||
            !NPCDescriptor.TryGet(PetCollection.ActivePetId, out var descriptor) || !descriptor.IsPet) return;

        var maximumLevel = Math.Clamp(descriptor.PetMaxLevel, 1, 200);
        if (progress.Level >= maximumLevel) return;
        var gain = Math.Max(1L, npcExperience / 4L);
        progress.Experience = Math.Min(long.MaxValue - gain, Math.Max(0, progress.Experience)) + gain;
        var increased = false;
        while (progress.Level < maximumLevel && progress.Experience >= progress.ExperienceToNextLevel)
        {
            progress.Experience -= progress.ExperienceToNextLevel;
            progress.Level++;
            increased = true;
        }
        if (increased)
        {
            ActivePet?.ApplyPetLevel(progress.Level);
            if (ActivePet != null) PacketSender.SendEntityDataToProximity(ActivePet);
            PetNotice(descriptor.Name + " atteint le niveau " + progress.Level + " !");
        }
        SendPetState();
    }

    public void HandlePetCommand(string arguments)
    {
        PetCollection.OwnedPets ??= [];
        var parts = (arguments ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length == 0 ? "help" : parts[0].ToLowerInvariant();
        switch (command)
        {
            case "dismiss":
            case "ranger":
                PetCollection.ActivePetId = Guid.Empty;
                DismissPetRuntime();
                PetNotice("Familier range.");
                break;
            case "summon":
            case "invoquer":
                var name = string.Join(" ", parts.Skip(1));
                var matching = PetCollection.OwnedPets.Keys.Select(NPCDescriptor.Get)
                    .FirstOrDefault(npc => npc != null &&
                        (npc.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) ||
                         npc.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
                if (matching == null) PetNotice("Familier inconnu. Utilise /pet list.");
                else SummonPet(matching.Id);
                break;
            case "loot":
                if (PetCollection.ActivePetId == Guid.Empty ||
                    !PetCollection.OwnedPets.TryGetValue(PetCollection.ActivePetId, out var progress))
                {
                    PetNotice("Invoque d'abord un familier.");
                    break;
                }
                if (parts.Length > 1 &&
                    (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                    progress.AutoLoot = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                PetNotice("Ramassage automatique : " + (progress.AutoLoot ? "ON" : "OFF"));
                break;
            case "list":
                var names = PetCollection.OwnedPets.Keys.Select(NPCDescriptor.Get)
                    .Where(npc => npc != null).Select(npc => npc!.Name).ToArray();
                PetNotice("Familiers : " + (names.Length == 0 ? "aucun" : string.Join(", ", names)));
                break;
            case "status":
            case "info":
                if (PetCollection.ActivePetId == Guid.Empty ||
                    !PetCollection.OwnedPets.TryGetValue(PetCollection.ActivePetId, out var state))
                {
                    PetNotice("Aucun familier actif.");
                    break;
                }
                var current = NPCDescriptor.Get(PetCollection.ActivePetId);
                PetNotice((current?.Name ?? "Familier") + " - Niv. " + state.Level +
                    " EXP " + state.Experience + "/" + state.ExperienceToNextLevel +
                    " Loot " + (state.AutoLoot ? "ON" : "OFF"));
                break;
            default:
                PetNotice("/pet list | /pet summon NOM | /pet dismiss | /pet status | /pet loot on/off");
                break;
        }
        SendPetState();
    }
}
