using System.Reflection;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.Professions;
using Intersect.Localization;
using Newtonsoft.Json;

var assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
}
var crop = new FarmCrop { SeedItemId = Guid.NewGuid(), HarvestItemId = Guid.NewGuid(), Seconds = 60, Yield = 10, Experience = 20 };
var config = new FarmConfiguration { Enabled = true, ExteriorMapId = Guid.NewGuid(), CurrencyItemId = Guid.NewGuid(), WorkerSprite = "farmer.png", Crops = [crop], StorageCapacity = 100, WagePerCycle = 5 };
const long start = 1_000_000;
FarmProfile Fresh(int workers = 1) => new()
{
    Learned = true, Wallet = 100, CurrencyItemId = config.CurrencyItemId, Seeds = new() { [crop.SeedItemId] = 100 },
    Plots = Enumerable.Range(0, workers).Select(_ => new FarmPlot { CropId = crop.Id, Active = true }).ToList(),
};
Check(config.IsValid && new HousingConfiguration().IsValid, "Valid farm settings and legacy disabled houses");
var live = Fresh(); var offline = Fresh();
live.Advance(config, start); offline.Advance(config, start);
for (var t = start + 1000; t <= start + 180000; t += 1000) live.Advance(config, t);
offline = JsonConvert.DeserializeObject<FarmProfile>(JsonConvert.SerializeObject(offline))!;
offline.Advance(config, start + 180000);
Check(live.Harvested == 30 && live.Harvested == offline.Harvested && live.Wallet == offline.Wallet && live.Experience == offline.Experience, "Live and offline simulation agree across persistence");
Check(offline.Seeds[crop.SeedItemId] == 96 && offline.WagesPaid == 20 && offline.Plots[0].PendingQuantity == 10, "Every started cycle reserves exactly one seed, its wage and its harvest space");
var saved = JsonConvert.SerializeObject(offline);
offline.Advance(config, start + 180000);
Check(JsonConvert.SerializeObject(offline) == saved, "Same timestamp cannot grant twice");
offline.Advance(config, start);
Check(JsonConvert.SerializeObject(offline) == saved, "Clock rollback cannot rewind farming");
var full = Fresh(2); full.Advance(config, start); full.Advance(config, start + 10_000_000);
Check(full.Harvested == 100 && full.Plots.All(p => p.DueUtc == 0) && full.Wallet == 50 && full.Seeds[crop.SeedItemId] == 90, "Shared storage capacity includes all pending harvests and blocks before spending");
full.Advance(config, start + 20_000_000);
Check(full.Harvested == 100 && full.Wallet == 50, "Full granary does not charge idle wages");
full.Storage.Clear(); full.Advance(config, start + 20_000_000);
Check(full.Harvested == 100 && full.Plots.All(p => p.StartedUtc == start + 20_000_000), "Emptying storage starts work now without banking blocked time");
var broke = Fresh(); broke.Wallet = 4; broke.Advance(config, start); broke.Advance(config, start + 10000000);
Check(broke.Harvested == 0 && broke.Wallet == 4 && broke.Seeds[crop.SeedItemId] == 100, "No debt and no seeds consumed without salary");
broke.Wallet = 10; broke.Advance(config, start + 10000000);
Check(broke.Harvested == 0 && broke.Plots[0].StartedUtc == start + 10000000, "Replenishing salary cannot generate backdated crops");
var noSeeds = Fresh(); noSeeds.Seeds.Clear(); noSeeds.Advance(config, start); noSeeds.Advance(config, start + 10000000);
Check(noSeeds.Wallet == 100 && noSeeds.Harvested == 0, "No seeds means no wage charge");
var paused = Fresh(); paused.Advance(config, start); paused.Plots[0].Active = false; paused.Advance(config, start + 10000000);
Check(paused.Harvested == 10 && paused.Wallet == 95 && paused.Plots[0].DueUtc == 0, "Pause completes the prepaid cycle and then stops");
var expert = Fresh(); expert.Plots[0].Experienced = true; expert.Advance(config, start); expert.Advance(config, start + 48000);
Check(expert.Harvested == 12 && expert.WagesPaid == 20, "Expert has 20 percent faster cycles, improved yield and double wages");
var snapshot = Fresh(); snapshot.Advance(config, start); snapshot.Plots[0].Active = false;
var edited = JsonConvert.DeserializeObject<FarmConfiguration>(JsonConvert.SerializeObject(config))!;
edited.Crops[0].Yield = 999; edited.Crops[0].HarvestItemId = Guid.NewGuid(); snapshot.Advance(edited, start + 60000);
Check(snapshot.Storage.GetValueOrDefault(crop.HarvestItemId) == 10 && snapshot.Experience == 20, "Existing cycles retain their saved yield, output and experience");
var currencies = Fresh(); currencies.CurrencyItemId = Guid.NewGuid(); currencies.Advance(config, start);
Check(currencies.Wallet == 100 && currencies.Plots[0].DueUtc == 0, "Changing currency cannot convert salary funds");
var locked = Fresh(); var lockedConfig = JsonConvert.DeserializeObject<FarmConfiguration>(JsonConvert.SerializeObject(config))!;
lockedConfig.Crops[0].RequiredLevel = 2; locked.Advance(lockedConfig, start);
Check(locked.Wallet == 100 && locked.Status(locked.Plots[0], lockedConfig) == "Farming level too low", "Crop level gates are enforced in simulation");
var house = HouseProfile.Read(JsonConvert.SerializeObject(new HouseProfile { Farm = offline }));
var reenabled = Fresh(); reenabled.Advance(config, start);
var resumedConfig = JsonConvert.DeserializeObject<FarmConfiguration>(JsonConvert.SerializeObject(config))!;
resumedConfig.ProductionEpochUtc = start + 10_000_000;
reenabled.Advance(resumedConfig, start + 10_000_000);
Check(reenabled.Harvested == 10 && reenabled.Plots[0].StartedUtc == resumedConfig.ProductionEpochUtc, "Re-enabling farming cannot credit new cycles during its disabled interval");
Check(house.Farm.Experience == offline.Experience && house.Farm.Wallet == offline.Wallet && house.Farm.Plots[0].DueUtc == offline.Plots[0].DueUtc, "Existing Housing storage persists the complete farm without a migration");
var professions = new ProfessionConfiguration(); FarmingProfession.Include(professions); FarmingProfession.Include(professions);
Check(professions.Professions.Count(p => p.Id == FarmingProfession.Id) == 1 && professions.IsStructurallyValid, "Farmer profession registration is stable and idempotent");
using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ui.json")!;
using var reader = new StreamReader(stream);
var catalog = GameTextCatalog.FromJson(reader.ReadToEnd());
string[] captions = ["Farm", "Farmer", "Manage crops and hired farm workers.", "Wheat", "Carrots", "Potatoes", "Visit farm", "Deposit 100 for salaries", "Withdraw salary fund", "Hire novice ({0})", "Hire expert ({0})", "Deposit 10 seeds", "Pause after harvest", "Dismiss worker", "Collect harvest", "Expert", "Novice", "Working", "Paused", "Choose a crop", "Farming level too low", "Salary fund empty", "No seeds", "Granary full", "Farmer level {0} - EXP {1}", "Salary fund: {0} {1} - granary: {2}/{3}", "Harvested: {0} - wages paid: {1}", "{0}: {1} seeds", "Worker {0} ({1}) - {2}", "Crop: {0} - wage per cycle: {1}", "Grow {0} (level {1}, {2} min)", "Farm saved.", "Farming is not available yet.", "Only the owner can manage this farm.", "Farm worker not found.", "Not enough supplies.", "Farm worker limit reached.", "Pause the worker and wait for the current harvest.", "Invalid farm quantity.", "Wait for the current harvest before changing crops.", "Withdraw the previous salary currency first."];
foreach (var caption in captions)
{
    Check(catalog.Entries.TryGetValue(caption, out var versions) && GameTextCatalog.Languages.All(lang => versions.ContainsKey(lang) && GameTextTokens.Compatible(caption, versions[lang])), "Locale coverage and placeholders: " + caption);
    Check(catalog.Get(caption, "unknown") == caption, "Unknown locale falls back to English");
}
FarmingServerChecks.Run(Check, config);
Console.WriteLine($"PASS: {assertions} farming checks (offline persistence, conservation, storage, pauses, currencies, progression, server authorization/rollback and en/fr/es).");
