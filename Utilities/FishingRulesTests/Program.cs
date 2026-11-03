using System.Text.Json;
using System.Text.RegularExpressions;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.MiniGames;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
void Invalid(Action action, string name)
{
    try { action(); } catch (InvalidDataException) { checks++; return; }
    throw new Exception(name);
}
var rod = Guid.NewGuid(); var cage = Guid.NewGuid(); var map = Guid.NewGuid(); var requirement = Guid.NewGuid();
var fish = new FishingCatchDefinition { ItemId = Guid.NewGuid(), ToolItemIds = [rod], Weight = 1 };
var lobster = new FishingCatchDefinition { ItemId = Guid.NewGuid(), ToolItemIds = [cage], Weight = 99 };
var rare = new FishingCatchDefinition { ItemId = Guid.NewGuid(), ToolItemIds = [rod], Legendary = true, Weight = 1000000 };
var zone = new FishingZoneDefinition { Name = "Test", MapIds = [map], UseTileRadius = true, X = 10, Y = 20, Radius = 2, Catches = [fish, lobster, rare] };
var config = new FishingConfiguration { Tools = [new() { ItemId = rod }, new() { ItemId = cage, Type = FishingToolType.Cage }], Zones = [zone] };
FishingRules.Validate(config); checks++;
Check(zone.AutomaticFailureChance == .50 && zone.AutomaticExperienceMultiplier == .20,
    "New zones default to 50% idle failures and 20% of normal XP");
Check(!FishingRules.AutomaticCatchSucceeds(zone, 0) && !FishingRules.AutomaticCatchSucceeds(zone, .499999) &&
    FishingRules.AutomaticCatchSucceeds(zone, .5) && FishingRules.AutomaticCatchSucceeds(zone, .999999),
    "Automatic catch probability respects the exact configured threshold");
Check(!FishingRules.AutomaticCatchSucceeds(zone, double.NaN) && !FishingRules.AutomaticCatchSucceeds(zone, 1),
    "Invalid and out-of-range random rolls cannot grant an automatic catch");
Check(FishingRules.AutomaticExperience(zone, 25) == 5 && FishingRules.AutomaticExperience(zone, 1) == 0 &&
    FishingRules.AutomaticExperience(zone, 0) == 0,
    "A 25 XP catch grants only 5 XP in auto; tiny values round down");
zone.AutomaticFailureChance = 1;
Check(!FishingRules.AutomaticCatchSucceeds(zone, .999999), "100% automatic failure never rewards fish");
zone.AutomaticFailureChance = 0;
Check(FishingRules.AutomaticCatchSucceeds(zone, 0), "0% automatic failure always succeeds");
zone.AutomaticFailureChance = .5;
zone.AutomaticExperienceMultiplier = 0;
Check(FishingRules.AutomaticExperience(zone, 25) == 0, "Zero idle XP factor grants no XP");
zone.AutomaticExperienceMultiplier = 1;
Check(FishingRules.AutomaticExperience(zone, 25) == 25, "Full idle XP factor is configurable");
zone.AutomaticExperienceMultiplier = .20;
zone.AutomaticFailureChance = -.1; Invalid(() => FishingRules.Validate(config), "Negative idle failure probability rejected");
zone.AutomaticFailureChance = 1.01; Invalid(() => FishingRules.Validate(config), "Idle failure probability above 100% rejected");
zone.AutomaticFailureChance = double.NaN; Invalid(() => FishingRules.ValidateDraft(config), "Nonfinite idle failure rejected in drafts");
zone.AutomaticFailureChance = .50;
zone.AutomaticExperienceMultiplier = double.PositiveInfinity; Invalid(() => FishingRules.Validate(config), "Nonfinite idle XP multiplier rejected");
zone.AutomaticExperienceMultiplier = -0.01; Invalid(() => FishingRules.ValidateDraft(config), "Negative idle XP rejected in drafts");
zone.AutomaticExperienceMultiplier = .20;
var legacyAutomaticZone = JsonSerializer.Deserialize<FishingZoneDefinition>("{\"Name\":\"Legacy idle\",\"MapIds\":[]}")!;
Check(legacyAutomaticZone.AutomaticFailureChance == .5 && legacyAutomaticZone.AutomaticExperienceMultiplier == .2,
    "Old saved fishing zone JSON receives safe idle balancing defaults");
Check(FishingRules.WithinZone(zone, map, 12, 22), "Zone includes its boundary");
Check(!FishingRules.WithinZone(zone, map, 13, 20), "Zone rejects outside position");
Check(!FishingRules.WithinZone(zone, Guid.NewGuid(), 10, 20), "Zone rejects another map");
Check(!FishingRules.WithinZone(zone, map, double.NaN, 20), "Zone rejects invalid position");
Check(FishingRules.Eligible(zone, cage, 1, 12, 25, false, _ => 0).Single() == lobster, "Cage only selects compatible lobster");
Check(FishingRules.Eligible(zone, Guid.Empty, 1, 12, 25, false, _ => 0).Length == 0, "No equipped tool grants nothing");
fish.MinimumLevel = 5;
Check(!FishingRules.Eligible(zone, rod, 4, 12, 25, false, _ => 0).Contains(fish), "Profession level gates catch");
fish.MinimumLevel = 1; fish.RequiredVariableId = requirement;
Check(!FishingRules.Eligible(zone, rod, 1, 12, 25, false, _ => 0).Contains(fish), "Player condition gates catch");
Check(FishingRules.Eligible(zone, rod, 1, 12, 25, false, _ => 1).Contains(fish), "Player condition permits catch");
fish.RequiredVariableId = Guid.Empty; fish.MinimumDepth = 50;
Check(!FishingRules.Eligible(zone, rod, 1, 12, 25, false, _ => 0).Contains(fish), "Depth gates catch");
Check(FishingRules.Eligible(zone, rod, 1, 12, 50, false, _ => 0).Contains(fish), "Depth boundary permits catch");
fish.MinimumDepth = 0;
Check(FishingRules.DuringHours(22, 4, 23) && FishingRules.DuringHours(22, 4, 2) && !FishingRules.DuringHours(22, 4, 12), "UTC overnight window");
Check(FishingRules.DuringHours(0, 24, 0) && FishingRules.DuringHours(0, 24, 23), "UTC all-day window");
Check(FishingRules.Select([fish, lobster], 0) == fish, "Weighted first boundary");
Check(FishingRules.Select([fish, lobster], .5) == lobster, "Weighted selection");
Check(FishingRules.SelectEncounter([rare], .02, .9, .1) == null, "Legendary-only table remains rare");
Check(FishingRules.SelectEncounter([fish, rare], 0, 0, 0) == fish, "Zero legendary chance never selects legendary");
Check(FishingRules.SelectEncounter([fish, rare], 1, .9, 0) == rare, "Guaranteed encounter still uses eligible legendary pool");
zone.LegendaryDuringAutomatic = false;
Check(!FishingRules.Eligible(zone, rod, 1, 12, 25, true, _ => 0).Contains(rare), "Automatic encounter policy");
zone.LegendaryDuringAutomatic = true;
fish.Weight = double.NaN; Invalid(() => FishingRules.Validate(config), "NaN weight rejected"); fish.Weight = 1;
fish.MaximumQuantity = 0; Invalid(() => FishingRules.Validate(config), "Invalid quantity range rejected"); fish.MaximumQuantity = 1;
zone.LegendaryChance = double.PositiveInfinity; Invalid(() => FishingRules.Validate(config), "Invalid rarity rejected"); zone.LegendaryChance = .02;
config.Tools.Add(config.Tools[0]); Invalid(() => FishingRules.Validate(config), "Duplicate tool IDs rejected"); config.Tools.RemoveAt(2);
Check(!FishingRules.SafeAssetPath("../outside.png", ".png") && !FishingRules.SafeAssetPath("C:/outside.png", ".png"), "Asset traversal rejected");
Check(FishingRules.SafeAssetPath("fishing/backgrounds/Water_Background4.png", ".png"), "Original asset path accepted");
var restored = FishingConfiguration.FromJson(config.ToJson());
FishingRules.Validate(restored);
Check(restored.Zones[0].Id == zone.Id && restored.Tools[0].ItemId == rod, "Configuration round-trip keeps GUIDs");

var secondMap = Guid.NewGuid();
var multipleMaps = new FishingZoneDefinition
{
    Name = "Several whole maps", MapIds = [map, secondMap], X = 10, Y = 20, Radius = 2, Catches = [fish],
};
var mapConfig = new FishingConfiguration { Tools = config.Tools, Zones = [multipleMaps] };
FishingRules.Validate(mapConfig); checks++;
Check(!new FishingZoneDefinition().UseTileRadius && new FishingZoneDefinition().MapIds.Count == 0,
    "New zones default to whole maps without inventing a map selection");
Check(FishingRules.WithinZone(multipleMaps, map, 0, 0) && FishingRules.WithinZone(multipleMaps, secondMap, 31, 31),
    "Every selected map is eligible beyond the inactive radius");
Check(!FishingRules.WithinZone(multipleMaps, Guid.NewGuid(), 10, 20) && !FishingRules.WithinZone(multipleMaps, Guid.Empty, 10, 20),
    "Whole-map scope cannot authorize unselected or empty map identities");
Check(!FishingRules.WithinZone(multipleMaps, secondMap, double.PositiveInfinity, 20) &&
    !FishingRules.WithinZone(multipleMaps, secondMap, -1, 20), "Whole-map scope still rejects invalid authoritative positions");
multipleMaps.Enabled = false;
Check(!FishingRules.WithinZone(multipleMaps, map, 10, 20), "A disabled multi-map zone cannot start fishing");
multipleMaps.Enabled = true; multipleMaps.UseTileRadius = true;
Check(FishingRules.WithinZone(multipleMaps, map, 12, 22) && FishingRules.WithinZone(multipleMaps, secondMap, 12, 22),
    "Explicit radius mode keeps its inclusive boundary on each selected map");
Check(!FishingRules.WithinZone(multipleMaps, map, 13, 20) && !FishingRules.WithinZone(multipleMaps, secondMap, 13, 20),
    "Explicit radius mode cannot expand to the rest of any selected map");
multipleMaps.UseTileRadius = false;
multipleMaps.MapIds = [map, map];
Invalid(() => FishingRules.Validate(mapConfig), "Duplicate selected maps are rejected");
multipleMaps.MapIds = [map, Guid.Empty];
Invalid(() => FishingRules.Validate(mapConfig), "Empty selected map identities are rejected");
multipleMaps.MapIds = null!;
Invalid(() => FishingRules.Validate(mapConfig), "Null map collections are rejected");
Check(!FishingRules.WithinZone(multipleMaps, map, 10, 20), "Malformed null map scope fails closed");
multipleMaps.MapIds = [];
Invalid(() => FishingRules.Validate(mapConfig), "Active zones require at least one selected map");
multipleMaps.Enabled = false;
FishingRules.Validate(mapConfig); checks++;
Check(!FishingRules.WithinZone(multipleMaps, map, 10, 20), "An empty disabled zone remains unavailable");
multipleMaps.Enabled = true;
multipleMaps.MapIds = Enumerable.Range(0, FishingRules.MaximumMapsPerZone + 1).Select(_ => Guid.NewGuid()).ToList();
Invalid(() => FishingRules.Validate(mapConfig), "Selected maps respect the bounded configuration size");
multipleMaps.MapIds.RemoveAt(multipleMaps.MapIds.Count - 1);
FishingRules.Validate(mapConfig); checks++;
multipleMaps.MapIds = [map, secondMap];
var multipleMapsCopy = FishingConfiguration.FromJson(mapConfig.ToJson()).Zones.Single();
Check(multipleMapsCopy.MapIds.SequenceEqual([map, secondMap]) && !multipleMapsCopy.UseTileRadius,
    "Saving and loading a multi-map zone preserves its full-map mode and ordered selection");
var duplicatedZone = JsonSerializer.Deserialize<FishingZoneDefinition>(JsonSerializer.Serialize(multipleMaps, FishingConfiguration.JsonOptions), FishingConfiguration.JsonOptions)!;
duplicatedZone.Id = Guid.NewGuid(); duplicatedZone.MapIds.Remove(map); duplicatedZone.Catches[0].Weight = 7;
Check(duplicatedZone.Id != multipleMaps.Id && duplicatedZone.MapIds.SequenceEqual([secondMap]) &&
    multipleMaps.MapIds.SequenceEqual([map, secondMap]) && multipleMaps.Catches[0].Weight == 1,
    "The editor's JSON duplication produces independent map selections and catches");

var legacyJson = JsonSerializer.Serialize(new
{
    Version = 1, Tools = config.Tools,
    Zones = new[] { new { Id = zone.Id, Name = "Legacy point", MapId = map, X = 10, Y = 20, Radius = 2, Catches = zone.Catches } },
});
var migrated = FishingConfiguration.FromJson(legacyJson);
FishingRules.Validate(migrated); checks++;
var migratedZone = migrated.Zones.Single();
Check(migratedZone.MapIds.SequenceEqual([map]) && migratedZone.MapId == Guid.Empty && migratedZone.UseTileRadius,
    "Legacy MapId migrates to a canonical single-map radius without widening the area");
Check(FishingRules.WithinZone(migratedZone, map, 12, 22) && !FishingRules.WithinZone(migratedZone, map, 13, 20) &&
    !FishingRules.WithinZone(migratedZone, secondMap, 10, 20), "Legacy eligibility preserves both tile and map boundaries");
using (var canonical = JsonDocument.Parse(migrated.ToJson()))
{
    var savedZone = canonical.RootElement.GetProperty("Zones")[0];
    Check(!savedZone.TryGetProperty("MapId", out _) && savedZone.GetProperty("MapIds").GetArrayLength() == 1 &&
        savedZone.GetProperty("UseTileRadius").GetBoolean(), "Canonical saves retire the legacy field and make radius mode explicit");
}
var remigrated = FishingConfiguration.FromJson(migrated.ToJson()).Zones.Single();
Check(remigrated.MapIds.SequenceEqual([map]) && remigrated.UseTileRadius && remigrated.X == 10 && remigrated.Y == 20 && remigrated.Radius == 2,
    "Repeated save and load preserves the migrated rectangle exactly");
var lowerCaseLegacy = FishingConfiguration.FromJson(legacyJson.Replace("\"MapId\"", "\"mApId\""));
Check(lowerCaseLegacy.Zones[0].MapIds.SequenceEqual([map]) && lowerCaseLegacy.Zones[0].UseTileRadius,
    "Legacy map migration honors case-insensitive configuration properties");
var conservativeLegacy = FishingConfiguration.FromJson(legacyJson.Replace("\"Radius\":2", "\"UseTileRadius\":false,\"Radius\":2"));
Check(conservativeLegacy.Zones[0].UseTileRadius, "A legacy-only map definition cannot silently become a whole-map zone");
var explicitEmpty = FishingConfiguration.FromJson(legacyJson.Replace("\"MapId\":", "\"MapIds\":[],\"MapId\":"));
Check(explicitEmpty.Zones[0].MapIds.Count == 0 && explicitEmpty.Zones[0].MapId == Guid.Empty,
    "Explicitly unchecking all maps never restores a stale legacy map");
Invalid(() => FishingRules.Validate(explicitEmpty), "An explicitly empty active selection is rejected after migration");
explicitEmpty.Zones[0].Enabled = false;
FishingRules.Validate(explicitEmpty); checks++;
var explicitNull = FishingConfiguration.FromJson(legacyJson.Replace("\"MapId\":", "\"MapIds\":null,\"MapId\":"));
Invalid(() => FishingRules.Validate(explicitNull), "A null modern selection cannot hide behind a valid legacy map");
var explicitModern = FishingConfiguration.FromJson(legacyJson.Replace("\"MapId\":", "\"MapIds\":[\"" + secondMap + "\"],\"MapId\":"));
Check(explicitModern.Zones[0].MapIds.SequenceEqual([secondMap]) && !explicitModern.Zones[0].UseTileRadius &&
    !FishingRules.WithinZone(explicitModern.Zones[0], map, 10, 20), "Modern map selections take precedence over stale legacy data");
var legacyZoneJson = JsonSerializer.Serialize(new { Name = "Legacy duplicate", MapId = map, X = 10, Y = 20, Radius = 2 });
var legacyZoneCopy = JsonSerializer.Deserialize<FishingZoneDefinition>(legacyZoneJson, FishingConfiguration.JsonOptions)!;
Check(legacyZoneCopy.MapIds.SequenceEqual([map]) && legacyZoneCopy.UseTileRadius,
    "Direct zone deserialization also migrates legacy data for editor duplication");

var unfinishedCatch = new FishingCatchDefinition { ItemId = Guid.Empty, ToolItemIds = [] };
var unfinishedZone = new FishingZoneDefinition { Name = "Unfinished lagoon", Catches = [unfinishedCatch] };
var draft = new FishingConfiguration { Tools = [new(), new()], Zones = [unfinishedZone] };
var draftSnapshot = draft.ToJson();
FishingRules.ValidateDraft(draft); checks++;
Check(draft.ToJson() == draftSnapshot && draft.Tools.All(t => t.ItemId == Guid.Empty && t.BaitItemId == Guid.Empty),
    "Draft validation preserves multiple unassigned tools and empty bait without changing authored data");
Invalid(() => FishingRules.Validate(draft), "An incomplete draft cannot pass active configuration validation");
var unfinishedRoundTrip = FishingConfiguration.FromJson(draftSnapshot);
FishingRules.ValidateDraft(unfinishedRoundTrip); checks++;
Check(unfinishedRoundTrip.Tools.Count == 2 && unfinishedRoundTrip.Zones[0].Enabled && unfinishedRoundTrip.Zones[0].MapIds.Count == 0 &&
    unfinishedRoundTrip.Zones[0].Catches[0].ItemId == Guid.Empty && unfinishedRoundTrip.Zones[0].Catches[0].ToolItemIds.Count == 0,
    "Draft JSON retains unfinished tools, catches and an intended active zone without dropping them");
var unresolvedConfig = new FishingConfiguration
{
    Tools = [new() { ItemId = rod }],
    Zones = [new() { Name = "Unresolved references", MapIds = [map],
        Catches = [new() { ItemId = Guid.NewGuid(), ToolItemIds = [Guid.NewGuid()] }] }],
};
FishingRules.ValidateDraft(unresolvedConfig); checks++;
Invalid(() => FishingRules.Validate(unresolvedConfig), "Unresolved tool references remain blocked from activation");
var emptyActiveZone = new FishingConfiguration { Zones = [new() { Name = "Planned active zone" }] };
FishingRules.ValidateDraft(emptyActiveZone); checks++;
Invalid(() => FishingRules.Validate(emptyActiveZone), "An intended active zone without maps or catches is draft-only");
draft.Tools[0].ItemId = rod; draft.Tools[1].ItemId = rod;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject duplicate assigned tool identities");
draft.Tools[1].ItemId = Guid.Empty; draft.Tools[0].BaitItemId = rod;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts cannot configure a tool as its own bait");
draft.Tools[0].ItemId = Guid.Empty; draft.Tools[0].BaitItemId = Guid.Empty;
draft.Tools[0].Power = double.NaN;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts preserve finite physics bounds");
draft.Tools[0].Power = 1;
unfinishedCatch.ToolItemIds = [Guid.Empty];
Invalid(() => FishingRules.ValidateDraft(draft), "An empty tool selection is allowed but an empty identity inside it is not");
unfinishedCatch.ToolItemIds = null!;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject null catch selections");
unfinishedCatch.ToolItemIds = [];
unfinishedZone.MapIds = [map, map];
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject duplicate map selections");
unfinishedZone.MapIds = null!;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject null map selections");
unfinishedZone.MapIds = [];
unfinishedZone.Assets.Background = "../outside.png";
Invalid(() => FishingRules.ValidateDraft(draft), "Incomplete authoring never permits unsafe asset paths");
unfinishedZone.Assets.Background = new FishingAssets().Background;
draft.Zones.Add(unfinishedZone);
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts retain unique zone identities");
draft.Zones.RemoveAt(1); unfinishedZone.Catches.Add(unfinishedCatch);
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts retain unique catch identities within a zone");
unfinishedZone.Catches.RemoveAt(1); unfinishedZone.Catches = null!;
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject null catch collections");
unfinishedZone.Catches = [unfinishedCatch];
draft.Tools = Enumerable.Range(0, 129).Select(_ => new FishingToolDefinition()).ToList();
Invalid(() => FishingRules.ValidateDraft(draft), "Unassigned tool profiles still respect the total count limit");
draft.Tools = [null!];
Invalid(() => FishingRules.ValidateDraft(draft), "Drafts still reject null tool entries");
Invalid(() => FishingRules.ValidateDraft(null!), "Null draft configurations are rejected consistently");

var held = new FishingFight(0, 10, 1);
for (var time = 100; time <= 30000 && !held.Broken; time += 100) held.Advance(time, true);
Check(held.Broken && !held.Complete, "Holding pressure permanently breaks the line");
var balanced = new FishingFight(0, 3, 1);
for (var time = 100; time <= 60000 && !balanced.Complete && !balanced.Broken; time += 100)
    balanced.Advance(time, balanced.Tension < 65);
Check(balanced.Complete && !balanced.Broken, "Pressure and release captures normal fish");
var elite = new FishingFight(0, 10, 1);
for (var time = 100; time <= 60000 && !elite.Complete && !elite.Broken; time += 100)
    elite.Advance(time, elite.Tension < 65);
Check(elite.Complete && !elite.Broken, "Highest difficulty remains playable");
var stalled = new FishingFight(0, 3, 1); stalled.Advance(60000, true);
Check(stalled.Progress < 15, "Server stall cannot fast-forward a reward");
var prior = stalled.Progress; stalled.Advance(1, true);
Check(stalled.Progress == prior, "Backdated time is ignored");
var pulled = new FishingFight(0, 3, 1);
var pull = FishingSkillDefinition.Defaults()[1]; pulled.UseSkill(pull, 0);
Check(pulled.Progress == 12 && pulled.Tension == 35, "Traction has progress benefit and tension cost");
Check(!pulled.UseSkill(FishingSkillDefinition.Defaults()[3], 0), "Diving cannot affect an active fight");
Check(!pulled.UseSkill(FishingSkillDefinition.Defaults()[2], 0), "Repair cannot exceed full line condition");
var stabilised = new FishingFight(0, 3, 1); var unstabilised = new FishingFight(0, 3, 1);
stabilised.UseSkill(FishingSkillDefinition.Defaults()[0], 0);
stabilised.Advance(1000, true); unstabilised.Advance(1000, true);
Check(stabilised.Tension < unstabilised.Tension, "Stabilizing reduces pressure gain");
Check(MiniGameProgression.Level(0) == 1 && MiniGameProgression.Level(long.MaxValue) == 25, "Fallback profession respects level caps");

var legendaryState = new FishingState { Automatic = true, Reeling = true };
var legendary = new FishingLegendaryEncounter(legendaryState, 0, 5000, 2, 800);
Check(!legendaryState.Automatic && legendaryState.AutomaticResumePending && !legendaryState.Reeling && legendaryState.Phase == FishingPhase.LegendaryPrompt, "Legendary encounter pauses automatic fishing and declares pending resumption");
Check(legendary.Accept(1000) && legendaryState.Phase == FishingPhase.Fighting && !legendaryState.Automatic && !legendaryState.AutomaticResumePending, "Legendary acceptance explicitly enters manual fishing and clears pending resumption");
Check(!legendary.Accept(1001), "Legendary prompt cannot be accepted twice");
Check(legendary.BeginChallenge(2000, () => 2), "Legendary challenge begins during manual fight");
Check(legendary.Input(2, 2050, () => 1) == FishingChallengeResult.Ignored, "Challenge rejects instant precomputed input");
Check(legendary.Input(2, 2200, () => 1) == FishingChallengeResult.Advanced && legendaryState.ChallengeExpectedInput == 1, "Correct challenge advances one server-selected step");
Check(legendary.Input(1, 2210, () => 0) == FishingChallengeResult.Ignored, "Same-tick challenge spam cannot complete a sequence");
Check(legendary.Input(1, 2400, () => 0) == FishingChallengeResult.Completed && legendary.Completed, "Completed challenge permits legendary capture");
Check(legendaryState.Phase == FishingPhase.Fighting && !legendaryState.Automatic, "Completed challenge resumes manual mode, never automatic");
Check(!legendary.BeginChallenge(2500, () => 0), "Completed challenge cannot restart");
var missedState = new FishingState { Automatic = true };
var missed = new FishingLegendaryEncounter(missedState, 0, 5000, 1, 800);
missed.Advance(4999);
Check(!missed.Expired && missedState.Phase == FishingPhase.LegendaryPrompt && !missedState.Automatic, "Automatic invitation stays paused before its exact deadline");
Check(!missed.Accept(5000) && missed.Expired && missed.ResumeAutomaticAfterTimeout && missedState.Phase == FishingPhase.Failed && missedState.Automatic && !missedState.AutomaticResumePending,
    "Ignored automatic invitation expires at exact deadline and schedules automatic resumption");
Check(!missed.Completed && missedState.ChallengeStep == 0 && !missedState.Reeling, "Ignored legendary invitation grants no challenge completion or capture");
Check(!FishingRules.ShouldAutoCast(missedState, 6499, 6500) && FishingRules.ShouldAutoCast(missedState, 6500, 6500),
    "Automatic fishing resumes only after the 1500 ms post-failure delay");
missed.Advance(9000);
Check(missedState.Automatic && missed.ResumeAutomaticAfterTimeout && !missed.Accept(9000), "Late invitation actions cannot re-enter the expired legendary or cancel automatic resumption");
var manualMissedState = new FishingState { Automatic = false };
var manualMissed = new FishingLegendaryEncounter(manualMissedState, 0, 5000, 1, 800);
manualMissed.Advance(5000);
Check(manualMissed.Expired && !manualMissed.ResumeAutomaticAfterTimeout && !manualMissedState.Automatic && !FishingRules.ShouldAutoCast(manualMissedState, 10000, 6500),
    "Ignored manual invitation remains manual after expiry");
var cancelledState = new FishingState { Automatic = true };
var cancelledResume = new FishingLegendaryEncounter(cancelledState, 0, 5000, 1, 800);
Check(cancelledState.AutomaticResumePending && cancelledResume.CancelAutomaticResume() && !cancelledState.AutomaticResumePending && !cancelledState.Automatic,
    "Stopping auto during a legendary invitation cancels pending resumption");
cancelledResume.Advance(5000);
Check(cancelledResume.Expired && !cancelledResume.ResumeAutomaticAfterTimeout && !cancelledState.Automatic && !FishingRules.ShouldAutoCast(cancelledState, 6500, 6500),
    "Explicitly cancelled automatic resumption stays stopped after invitation timeout");
var cancelledThenAcceptedState = new FishingState { Automatic = true };
var cancelledThenAccepted = new FishingLegendaryEncounter(cancelledThenAcceptedState, 0, 5000, 1, 800);
cancelledThenAccepted.CancelAutomaticResume();
Check(cancelledThenAccepted.Accept(1000) && cancelledThenAcceptedState.Phase == FishingPhase.Fighting && !cancelledThenAcceptedState.Automatic && !cancelledThenAcceptedState.AutomaticResumePending,
    "Cancelling auto resumption still permits manually accepting the creature");
var lateState = new FishingState { Automatic = true }; var late = new FishingLegendaryEncounter(lateState, 0, 5000, 1, 800);
late.Accept(1000); late.BeginChallenge(2000, () => 0);
Check(late.Input(0, 2800, () => 0) == FishingChallengeResult.Failed && !late.Completed && !lateState.Automatic && !late.ResumeAutomaticAfterTimeout,
    "An accepted automatic invitation becomes manual permanently even when its challenge times out");
var wrongState = new FishingState { Automatic = true }; var wrong = new FishingLegendaryEncounter(wrongState, 0, 5000, 1, 800);
wrong.Accept(1000); wrong.BeginChallenge(2000, () => 0);
Check(wrong.Input(1, 2200, () => 0) == FishingChallengeResult.Failed && !wrongState.Automatic && !wrong.ResumeAutomaticAfterTimeout,
    "An accepted automatic invitation stays manual after a wrong challenge input");

config.Sounds.Cues["cast"] = new() { File = "global.wav" };
zone.Sounds.Cues["cast"] = new() { File = "zone.wav" };
config.Tools[0].Sounds.Cues["cast"] = new() { File = "tool.wav" };
fish.Sounds.Cues["cast"] = new() { File = "" };
Check(FishingSoundCatalog.Resolve(config, zone, config.Tools[0], fish, "cast").File == "", "Explicit silence overrides inherited sound");
fish.Sounds.Cues.Clear();
Check(FishingSoundCatalog.Resolve(config, zone, config.Tools[0], fish, "cast").File == "tool.wav", "Tool sound overrides zone and global");
Check(FishingSoundCatalog.Default("cast", FishingToolType.Cage).File.Contains("cast_cage"), "Tool-specific default cast sound");
var netSoundTool = new FishingToolDefinition { ItemId = Guid.NewGuid(), Type = FishingToolType.Net };
netSoundTool.Sounds.Cues["cast_net"] = new() { File = "specific-net.wav" };
Check(FishingSoundCatalog.Resolve(config, zone, netSoundTool, fish, "cast").File == "specific-net.wav", "Generic cast resolves the configured net event");
config.Sounds.Cues["legendary"] = new() { File = "legendary-family.wav" };
Check(FishingSoundCatalog.Resolve(config, zone, netSoundTool, fish, "legendary_hooked").File == "legendary-family.wav", "Precise legendary hook inherits the configured family");
fish.Sounds.Cues["legendary_hooked"] = new() { File = "" };
Check(FishingSoundCatalog.Resolve(config, zone, netSoundTool, fish, "legendary_hooked").File == "", "Exact empty cue silences a global family override");
var reservedSound = new FishingConfiguration();
reservedSound.Sounds.Cues["challenge_timing_prompt"] = new() { File = "reserved.wav" };
Check(FishingSoundCatalog.Resolve(reservedSound, null, null, null, "challenge").File != "reserved.wav", "Unimplemented challenge hook cannot alter the implemented challenge");
var netProfile = FishingWorldProfile.Net(); netProfile.Validate(); checks++;
var netJson = JsonSerializer.Serialize(netProfile, FishingConfiguration.JsonOptions);
var netCopy = JsonSerializer.Deserialize<FishingWorldProfile>(netJson, FishingConfiguration.JsonOptions)!;
netCopy.Validate();
var netFiles = netCopy.Clips.Values.SelectMany(clip => new[] { clip.Front, clip.Back }).Where(path => path.Length > 0).ToArray();
Check(netFiles.Distinct().Count() == 9 && netFiles.SequenceEqual(netProfile.Clips.Values.SelectMany(clip => new[] { clip.Front, clip.Back }).Where(path => path.Length > 0)), "Net profile round-trip preserves all nine original asset paths");
Check(netCopy.FrameWidth == 128 && netCopy.FrameHeight == 128 && netCopy.Clips[FishingWorldAction.Throw].Frames == 6 && netCopy.Clips[FishingWorldAction.Fishing].Frames == 4,
    "Net atlas dimensions and clip frame counts survive JSON");

using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Text.json")));
foreach (var entry in document.RootElement.GetProperty("Entries").EnumerateObject())
{
    foreach (var language in new[] { "en", "fr", "es" })
    {
        Check(entry.Value.TryGetProperty(language, out var text) && !string.IsNullOrWhiteSpace(text.GetString()), "Missing language: " + entry.Name + "/" + language);
        var expected = Regex.Matches(entry.Name, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
        var actual = Regex.Matches(text.GetString()!, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
        Check(expected.SequenceEqual(actual), "Localization placeholders changed: " + entry.Name);
    }
    Check(entry.Value.GetProperty("en").GetString() == entry.Name, "English fallback differs from source");
}
#if FISHING_SERVER_TESTS
ServerRewardChecks.Run(Check);
ServerDraftChecks.Run(Check);
ServerSessionChecks.Run(Check);
#endif
Console.WriteLine($"Fishing rules: {checks} checks passed.");
