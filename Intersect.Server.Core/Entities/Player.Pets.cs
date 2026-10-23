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
    public MountCollection Mounts { get; set; } = new();
    public Intersect.Framework.Core.QualityOfLife.PremiumConvenienceProfile Convenience {get;set;}=new();
    public Guid ActivePetId { get; set; }
    public Dictionary<Guid, PetProgress> OwnedPets { get; set; } = [];
}

public sealed class PetProgress
{
    public Guid DecorationId {get;set;}
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public bool AutoLoot { get; set; } = true;
    public long RecoveryUntilUtc { get; set; }
    public bool Housed { get; set; }

    /// <summary>Custom name per character; blank means the NPC's original name.</summary>
    public string Nickname { get; set; } = string.Empty;

    public long ExperienceToNextLevel => 100L * Math.Max(1, Level) * Math.Max(1, Level);
}

public partial class Player
{
    [JsonIgnore, Column("Pets")]
    public string PetsJson
    {
        get => JsonConvert.SerializeObject(PetCollection);
        set
        {
            PetCollection = string.IsNullOrWhiteSpace(value) ? new PetCollection() : JsonConvert.DeserializeObject<PetCollection>(value) ?? new PetCollection();
            PetCollection.Mounts ??= new(); PetCollection.Mounts.Owned ??= [];
        }
    }

    [NotMapped, JsonIgnore]
    public PetCollection PetCollection { get; set; } = new();

    [NotMapped, JsonIgnore]
    public Npc? ActivePet { get; private set; }
    internal long LastConvenienceRequest;

    private void PetNotice(string message) =>
        PacketSender.SendChatMsg(this, message, ChatMessageType.Notice);

    /// <summary>Resolve the nickname without modifying the shared NPC descriptor.</summary>
    public string GetPetDisplayName(NPCDescriptor descriptor)
    {
        if (PetCollection.OwnedPets != null &&
            PetCollection.OwnedPets.TryGetValue(descriptor.Id, out var progress) &&
            progress != null && !string.IsNullOrWhiteSpace(progress.Nickname))
            return progress.Nickname;

        return descriptor.Name;
    }

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
            PetNotice("This pet has not been unlocked.");
            return;
        }
        if (ActivePet != null && PetCollection.ActivePetId == npcId)
        {
            PetCollection.ActivePetId = Guid.Empty;
            DismissPetRuntime();
            PetNotice("Pet dismissed.");
            SendPetState();
            return;
        }
        var progress = PetCollection.OwnedPets[npcId];
        var remaining = Math.Max(0, progress.RecoveryUntilUtc - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (remaining > 0)
        {
            PetNotice("Your pet is recovering.");
            SendPetState(); return;
        }
        if (progress.Housed)
        {
            var resident = House.Pets.FirstOrDefault(p => p.Id == npcId);
            House.Pets.RemoveAll(p => p.Id == npcId); progress.Housed = false;
            if (User?.Save(force: true) != Intersect.Server.Database.PlayerData.UserSaveResult.Completed)
            {
                progress.Housed = true;
                if (resident != null) House.Pets.Add(resident);
                PetNotice("Your house could not be saved. Please try again."); SendPetState(); return;
            }
            Intersect.Server.Housing.HousingRuntime.BroadcastOwner(this);
        }
        DismissPetRuntime();
        PetCollection.ActivePetId = npcId;
        SpawnPetRuntime(descriptor);
        PetNotice(GetPetDisplayName(descriptor) + " is now your pet!");
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
        if (progress.Housed || progress.RecoveryUntilUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return;

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
        var id = PetCollection.ActivePetId;
        ActivePet = null;
        PetCollection.ActivePetId = Guid.Empty;
        if (PetCollection.OwnedPets.TryGetValue(id, out var progress))
            progress.RecoveryUntilUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Math.Clamp(Options.Instance.Npc.PetRecoverySeconds, 0, 86400) * 1000L;
        User?.Save(force: true);
        PetNotice("Your pet was defeated and needs time to recover.");
        SendPetState();
    }

    /// <summary>
    /// An explicit player warp (spell, event or admin transfer) should bring the
    /// companion to the destination. Natural grid-boundary walking never calls
    /// this method, so the pet crosses those boundaries using normal movement.
    /// </summary>
    private void RecallPetAfterOwnerWarp()
    {
        if (ActivePet == null || PetCollection.ActivePetId == Guid.Empty)
        {
            return;
        }

        DismissPetRuntime();
        SpawnPetRuntime();
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

        // A different MapId is normal while the companion walks across a
        // seamless border. Never destroy/resummon it merely for that reason.
        // Recall only if the player really changed instances or left the
        // adjacent maps the server pathfinder can traverse.
        if (!PetMapTraversal.CanWalkToOwner(this, ActivePet))
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
            PetNotice(GetPetDisplayName(descriptor) + " reached level " + progress.Level + "!");
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
                PetNotice("Pet dismissed.");
                break;
            case "summon":
            case "invoquer":
                var name = string.Join(" ", parts.Skip(1));
                var matching = PetCollection.OwnedPets.Keys.Select(NPCDescriptor.Get)
                    .FirstOrDefault(npc => npc != null &&
                        (npc.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) ||
                         npc.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                         GetPetDisplayName(npc).Equals(name, StringComparison.OrdinalIgnoreCase)));
                if (matching == null) PetNotice("Unknown pet. Use /pet list.");
                else SummonPet(matching.Id);
                break;
            case "loot":
                if (PetCollection.ActivePetId == Guid.Empty ||
                    !PetCollection.OwnedPets.TryGetValue(PetCollection.ActivePetId, out var progress))
                {
                    PetNotice("Summon a pet first.");
                    break;
                }
                if (parts.Length > 1 &&
                    (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                    progress.AutoLoot = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                PetNotice("Auto-loot: " + (progress.AutoLoot ? "ON" : "OFF"));
                break;
            case "list":
                var names = PetCollection.OwnedPets.Keys.Select(NPCDescriptor.Get)
                    .Where(npc => npc != null).Select(npc => GetPetDisplayName(npc!)).ToArray();
                PetNotice("Pets: " + (names.Length == 0 ? "none" : string.Join(", ", names)));
                break;
            case "status":
            case "info":
                if (PetCollection.ActivePetId == Guid.Empty ||
                    !PetCollection.OwnedPets.TryGetValue(PetCollection.ActivePetId, out var state))
                {
                    PetNotice("No active pet.");
                    break;
                }
                var current = NPCDescriptor.Get(PetCollection.ActivePetId);
                PetNotice((current != null ? GetPetDisplayName(current) : "Pet") + " - Lv. " + state.Level +
                    " EXP " + state.Experience + "/" + state.ExperienceToNextLevel +
                    " Loot " + (state.AutoLoot ? "ON" : "OFF"));
                break;
            default:
                PetNotice("/pet list | /pet summon NAME | /pet dismiss | /pet status | /pet loot on/off");
                break;
        }
        SendPetState();
    }
}
