using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Models;
using Intersect.Utilities;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.NPCs;

public partial class NPCDescriptor : DatabaseObject<NPCDescriptor>, IFolderable
{
    private long[] _maxVitals = new long[Enum.GetValues<Vital>().Length];
    private int[] _stats = new int[Enum.GetValues<Stat>().Length];
    private long[] _vitalRegen = new long[Enum.GetValues<Vital>().Length];

    [NotMapped]
    public ConditionLists AttackOnSightConditions { get; set; } = new();

    [NotMapped]
    public List<Drop> Drops { get; set; }= [];

    [NotMapped, JsonIgnore]
    public long[] MaxVitals
    {
        get => _maxVitals;
        set => _maxVitals = value;
    }

    [JsonProperty(nameof(MaxVitals)), NotMapped]
    public IReadOnlyDictionary<Vital, long> MaxVitalsLookup
    {
        get =>
            MaxVitals.Select((value, index) => (value, index))
                .ToDictionary(t => (Vital)t.index, t => t.value)
                .AsReadOnly();
        set
        {
            foreach (var (key, val) in value)
            {
                MaxVitals[(int)key] = val;
            }
        }
    }

    [JsonProperty(nameof(VitalRegen)), NotMapped]
    public IReadOnlyDictionary<Vital, long> VitalRegenLookup
    {
        get =>
            VitalRegen.Select((value, index) => (value, index))
                .ToDictionary(t => (Vital)t.index, t => t.value)
                .AsReadOnly();
        set
        {
            foreach (var (key, val) in value)
            {
                VitalRegen[(int)key] = val;
            }
        }
    }

    [JsonProperty(nameof(Stats)), NotMapped]
    public IReadOnlyDictionary<Stat, int> StatsLookup
    {
        get =>
            Stats.Select((statValue, index) => (statValue, index))
                .ToDictionary(t => (Stat)t.index, t => t.statValue)
                .AsReadOnly();
        set
        {
            foreach (var (key, val) in value)
            {
                Stats[(int)key] = val;
            }
        }
    }

    [NotMapped]
    public ConditionLists PlayerCanAttackConditions { get; set; } = new();

    [NotMapped]
    public ConditionLists PlayerFriendConditions { get; set; } = new();

    [NotMapped, JsonIgnore]
    public int[] Stats
    {
        get => _stats;
        set => _stats = value;
    }

    [NotMapped, JsonIgnore]
    public long[] VitalRegen
    {
        get => _vitalRegen;
        set => _vitalRegen = value;
    }

    [NotMapped]
    public List<SpellEffect> Immunities { get; set; } = [];

    [JsonIgnore]
    [Column("Immunities")]
    public string ImmunitiesJson
    {
        get => JsonConvert.SerializeObject(Immunities);
        set
        {
            Immunities = JsonConvert.DeserializeObject<List<SpellEffect>>(value ?? "") ?? [];
        }
    }

    [JsonConstructor]
    public NPCDescriptor(Guid id) : base(id)
    {
        Name = "New Npc";
    }

    //Parameterless constructor for EF
    public NPCDescriptor()
    {
        Name = "New Npc";
    }

    [Column("AggroList")]
    [JsonIgnore]
    public string JsonAggroList
    {
        get => JsonConvert.SerializeObject(AggroList);
        set => AggroList = JsonConvert.DeserializeObject<List<Guid>>(value);
    }

    [NotMapped]
    public List<Guid> AggroList { get; set; } = [];

    public bool AttackAllies { get; set; }

    [Column("AttackAnimation")]
    public Guid AttackAnimationId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public AnimationDescriptor AttackAnimation
    {
        get => AnimationDescriptor.Get(AttackAnimationId);
        set => AttackAnimationId = value?.Id ?? Guid.Empty;
    }

    //Boss configuration
    public bool IsBoss { get; set; }

    [Column("BossAnimation")]
    public Guid BossAnimationId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public AnimationDescriptor BossAnimation
    {
        get => AnimationDescriptor.Get(BossAnimationId);
        set => BossAnimationId = value?.Id ?? Guid.Empty;
    }

    /// <summary>
    /// Vertical pixel offset for the looping boss marker/aura attached to the NPC.
    /// Negative values render the animation above the NPC.
    /// </summary>
    public int BossAnimationOffsetY { get; set; } = -48;

    [Column("DeathAnimation")]
    public Guid DeathAnimationId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public AnimationDescriptor DeathAnimation
    {
        get => AnimationDescriptor.Get(DeathAnimationId);
        set => DeathAnimationId = value?.Id ?? Guid.Empty;
    }

    // Player-owned companion settings. World NPCs are unchanged unless IsPet is enabled.
    public bool IsPet { get; set; }

    /// <summary>Inventory item that permanently unlocks and summons this companion.</summary>
    public Guid PetSummonItemId { get; set; }

    public int PetLootRadius { get; set; } = 2;

    public int PetMaxLevel { get; set; } = 50;

    public int PetStatGrowth { get; set; } = 1;

    public int PetHealthGrowth { get; set; } = 10;

    /// <summary>
    /// Legacy progression for pet spells without an individual level override.
    /// Ordinary NPC spells do not use these pet-only requirements.
    /// </summary>
    public int PetSpellUnlockInterval { get; set; } = 5;

    /// <summary>
    /// Indexed by the NPC's existing Spells list. Zero means use the legacy interval.
    /// This is deliberately separate from Spells so ordinary NPC behaviour is unchanged.
    /// </summary>
    [NotMapped]
    public List<int> PetSpellRequiredLevels { get; set; } = [];

    [JsonIgnore]
    [Column("PetSpellRequiredLevels")]
    public string PetSpellRequiredLevelsJson
    {
        get => JsonConvert.SerializeObject(PetSpellRequiredLevels ?? []);
        set => PetSpellRequiredLevels = string.IsNullOrWhiteSpace(value)
            ? []
            : JsonConvert.DeserializeObject<List<int>>(value) ?? [];
    }

    public int GetPetSpellRequiredLevel(int spellIndex)
    {
        if (spellIndex < 0)
        {
            return 1;
        }

        if (HasCustomPetSpellLevel(spellIndex))
        {
            return Math.Clamp(PetSpellRequiredLevels[spellIndex], 1, 200);
        }

        return (int)Math.Clamp(
            1L + (long)spellIndex * Math.Max(1, PetSpellUnlockInterval), 1L, 200L
        );
    }

    public bool HasCustomPetSpellLevel(int spellIndex) =>
        spellIndex >= 0 &&
        PetSpellRequiredLevels is { } levels &&
        spellIndex < levels.Count &&
        levels[spellIndex] > 0;

    /// <summary>
    /// Align saved pet requirements with the existing spell slots, preserving
    /// zero (automatic/legacy) values until the editor sets a specific level.
    /// </summary>
    public void EnsurePetSpellLevelSlots()
    {
        PetSpellRequiredLevels ??= [];
        var spellCount = Spells?.Count ?? 0;
        if (PetSpellRequiredLevels.Count > spellCount)
        {
            PetSpellRequiredLevels.RemoveRange(spellCount, PetSpellRequiredLevels.Count - spellCount);
        }

        while (PetSpellRequiredLevels.Count < spellCount)
        {
            PetSpellRequiredLevels.Add(0);
        }
    }

    public void SetPetSpellRequiredLevel(int spellIndex, int level)
    {
        if (spellIndex < 0 || spellIndex >= (Spells?.Count ?? 0))
        {
            return;
        }

        EnsurePetSpellLevelSlots();
        PetSpellRequiredLevels[spellIndex] = Math.Clamp(level, 1, 200);
    }

    public void ResetPetSpellRequiredLevel(int spellIndex)
    {
        if (spellIndex < 0 || spellIndex >= (Spells?.Count ?? 0))
        {
            return;
        }

        EnsurePetSpellLevelSlots();
        PetSpellRequiredLevels[spellIndex] = 0;
    }

    //Behavior
    public bool Aggressive { get; set; }

    /// <summary>
    /// Enables tactical positioning while this NPC has a combat target.
    /// </summary>
    public bool SmartCombatMovement { get; set; } = true;

    /// <summary>
    /// Controls how the NPC positions itself around combat targets.
    /// Auto infers the role from its offensive spells.
    /// </summary>
    public int CombatMovementMode { get; set; } = (int)NpcCombatMovementMode.Auto;

    /// <summary>
    /// Preferred distance in tiles. Zero lets Auto/role logic derive a range
    /// from configured spell cast/projectile ranges.
    /// </summary>
    public int PreferredCombatRange { get; set; }

    public byte Movement { get; set; }

    public bool Swarm { get; set; }

    public byte FleeHealthPercentage { get; set; }

    public bool FocusHighestDamageDealer { get; set; } = true;

    public int ResetRadius { get; set; }

    //Conditions
    [Column("PlayerFriendConditions")]
    [JsonIgnore]
    public string PlayerFriendConditionsJson
    {
        get => PlayerFriendConditions.Data();
        set => PlayerFriendConditions.Load(value);
    }

    [Column("AttackOnSightConditions")]
    [JsonIgnore]
    public string AttackOnSightConditionsJson
    {
        get => AttackOnSightConditions.Data();
        set => AttackOnSightConditions.Load(value);
    }

    [Column("PlayerCanAttackConditions")]
    [JsonIgnore]
    public string PlayerCanAttackConditionsJson
    {
        get => PlayerCanAttackConditions.Data();
        set => PlayerCanAttackConditions.Load(value);
    }

    //Combat
    public int Damage { get; set; } = 1;

    public int DamageType { get; set; }

    public int CritChance { get; set; }

    public double CritMultiplier { get; set; } = 1.5;

    public double Tenacity { get; set; } = 0.0;

    public int AttackSpeedModifier { get; set; }

    public int AttackSpeedValue { get; set; }

    //Common Events
    [Column("OnDeathEvent")]
    public Guid OnDeathEventId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public EventDescriptor OnDeathEvent
    {
        get => EventDescriptor.Get(OnDeathEventId);
        set => OnDeathEventId = value?.Id ?? Guid.Empty;
    }

    [Column("OnDeathPartyEvent")]
    public Guid OnDeathPartyEventId { get; set; }

    [NotMapped]
    [JsonIgnore]
    public EventDescriptor OnDeathPartyEvent
    {
        get => EventDescriptor.Get(OnDeathPartyEventId);
        set => OnDeathPartyEventId = value?.Id ?? Guid.Empty;
    }

    //Drops
    [Column("Drops")]
    [JsonIgnore]
    public string JsonDrops
    {
        get => JsonConvert.SerializeObject(Drops);
        set => Drops = JsonConvert.DeserializeObject<List<Drop>>(value);
    }

    /// <summary>
    /// If true this npc will drop individual loot for all of those who helped slay it.
    /// </summary>
    public bool IndividualizedLoot { get; set; }

    public long Experience { get; set; }

    public int Level { get; set; } = 1;

    //Vitals & Stats
    [Column("MaxVital")]
    [JsonIgnore]
    public string JsonMaxVital
    {
        get => DatabaseUtils.SaveLongArray(_maxVitals, Enum.GetValues<Vital>().Length);
        set => DatabaseUtils.LoadLongArray(ref _maxVitals, value, Enum.GetValues<Vital>().Length);
    }

    //NPC vs NPC Combat
    public bool NpcVsNpcEnabled { get; set; }

    public int Scaling { get; set; } = 100;

    public int ScalingStat { get; set; }

    public int SightRange { get; set; }

    //Basic Info
    public int SpawnDuration { get; set; }

    public int SpellFrequency { get; set; } = 2;

    //Spells
    [JsonIgnore]
    [Column("Spells")]
    public string CraftsJson
    {
        get => JsonConvert.SerializeObject(Spells, Formatting.None);
        protected set => Spells = JsonConvert.DeserializeObject<DbList<SpellDescriptor>>(value);
    }

    [NotMapped]
    public DbList<SpellDescriptor> Spells { get; set; } = [];

    public string Sprite { get; set; } = string.Empty;

    /// <summary>
    /// The database compatible version of <see cref="Color"/>
    /// </summary>
    [Column("Color")]
    [JsonIgnore]
    public string JsonColor
    {
        get => JsonConvert.SerializeObject(Color);
        set => Color = !string.IsNullOrWhiteSpace(value) ? JsonConvert.DeserializeObject<Color>(value) : Color.White;
    }

    /// <summary>
    /// Defines the ARGB color settings for this Npc.
    /// </summary>
    [NotMapped]
    public Color Color { get; set; } = new(255, 255, 255, 255);

    [Column("Stats")]
    [JsonIgnore]
    public string JsonStat
    {
        get => DatabaseUtils.SaveIntArray(_stats, Enum.GetValues<Stat>().Length);
        set => DatabaseUtils.LoadIntArray(ref _stats, value, Enum.GetValues<Stat>().Length);
    }

    //Vital Regen %
    [JsonIgnore]
    [Column("VitalRegen")]
    public string RegenJson
    {
        get => DatabaseUtils.SaveLongArray(_vitalRegen, Enum.GetValues<Vital>().Length);
        set => DatabaseUtils.LoadLongArray(ref _vitalRegen, value, Enum.GetValues<Vital>().Length);
    }

    /// <inheritdoc />
    public string Folder { get; set; } = string.Empty;

    public SpellDescriptor GetRandomSpell(Random random)
    {
        if (Spells == null || Spells.Count == 0)
        {
            return null;
        }

        var spellIndex = random.Next(0, Spells.Count);
        var spellId = Spells[spellIndex];

        return SpellDescriptor.Get(spellId);
    }
}
