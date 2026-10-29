using System.Text.Json;
using Intersect;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Localization;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.RoyalContracts;
using MessagePack;
using Console = System.Console;

var count = 0;
void Check(bool pass, string message) { if (!pass) throw new Exception(message); count++; }
void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { count++; return; } throw new Exception(message); }
var now = DateTimeOffset.Parse("2026-10-29T04:59:59Z");
var config = RoyalContractsConfiguration.CreateDefault();
Check(RoyalContractRules.PeriodStart(now, RoyalContractPeriod.Daily, 5) == DateTimeOffset.Parse("2026-10-28T05:00:00Z"), "Daily UTC boundary before reset");
Check(RoyalContractRules.PeriodStart(now.AddSeconds(1), RoyalContractPeriod.Daily, 5) == DateTimeOffset.Parse("2026-10-29T05:00:00Z"), "Daily reset at exact boundary");
Check(RoyalContractRules.PeriodStart(DateTimeOffset.Parse("2026-11-02T04:59:59Z"), RoyalContractPeriod.Weekly, 5) == DateTimeOffset.Parse("2026-10-26T05:00:00Z"), "Monday before reset remains previous week");
Check(RoyalContractRules.PeriodStart(DateTimeOffset.Parse("2026-11-02T05:00:00Z"), RoyalContractPeriod.Weekly, 5) == DateTimeOffset.Parse("2026-11-02T05:00:00Z"), "Monday weekly boundary");
Check(RoyalContractRules.PeriodStart(now.ToOffset(TimeSpan.FromHours(-4)), RoyalContractPeriod.Daily, 5) == RoyalContractRules.PeriodStart(now, RoyalContractPeriod.Daily, 5), "Client timezone cannot change a period");

var rotation = new RoyalContractRotation(); Check(rotation.Refresh(config, now), "Initial rotation");
var serialized = RoyalContractRules.Json(rotation);
var restored = RoyalContractRules.Read<RoyalContractRotation>(serialized);
Check(!restored.Refresh(config, now), "Restart preserves current rotation");
Check(serialized == RoyalContractRules.Json(restored), "Restart preserves quantities and rewards");
var weeklyIds = rotation.Weekly.Select(c => c.Id).ToArray(); var dailyIds = rotation.Daily.Select(c => c.Id).ToArray();
config.Templates[0].Seals = 1000;
Check(!rotation.Refresh(config, now), "Editing templates cannot change posted contracts");
Check(rotation.Daily.Single(c => c.Template.Id == config.Templates[0].Id).Template.Seals != 1000, "Posted definitions are deep copies");
rotation.Refresh(config, now.AddSeconds(1));
Check(!rotation.Daily.Select(c => c.Id).Intersect(dailyIds).Any(), "New day uses new claim identities");
Check(rotation.Weekly.Select(c => c.Id).SequenceEqual(weeklyIds), "Daily reset preserves weekly missions");
var generated = RoyalContractRules.Generate(config, RoyalContractPeriod.Daily, rotation.DailyStart);
config.Templates.Reverse();
Check(RoyalContractRules.Json(generated) == RoyalContractRules.Json(RoyalContractRules.Generate(config, RoyalContractPeriod.Daily, rotation.DailyStart)), "Rotation independent of editor ordering");
Check(generated.Select(c => c.Template.Id).Distinct().Count() == generated.Count, "No duplicate templates in a rotation");
Check(generated.All(c => c.Quantity >= c.Template.MinimumQuantity && c.Quantity <= c.Template.MaximumQuantity), "Quantities bounded by configuration");
var bad = RoyalContractRules.Read<RoyalContractsConfiguration>(RoyalContractRules.Json(config)); bad.ResetHourUtc = 24; Reject(bad.Validate, "Invalid reset rejected");
bad = RoyalContractRules.Read<RoyalContractsConfiguration>(RoyalContractRules.Json(config)); bad.Templates[1].MaximumQuantity = 0; Reject(bad.Validate, "Invalid quantity rejected");
bad = RoyalContractRules.Read<RoyalContractsConfiguration>(RoyalContractRules.Json(config)); bad.Templates.Add(bad.Templates[0]); Reject(bad.Validate, "Duplicate template identity rejected");
bad = RoyalContractRules.Read<RoyalContractsConfiguration>(RoyalContractRules.Json(config)); bad.Templates[0].Objective = RoyalContractObjective.DeliverItems; bad.Templates[0].TargetId = Guid.Empty; Reject(bad.Validate, "Delivery requires an item");

var contract = new RoyalContract { Id = Guid.NewGuid(), Template = new() { Objective = RoyalContractObjective.NpcKills }, Quantity = 3, ExpiresAt = now.AddHours(1) };
var progress = new RoyalContractProgress(); var target = Guid.NewGuid();
Check(!RoyalContractRules.Advance(contract, progress, RoyalContractObjective.NpcKills, target, 1, now), "No retrospective or unaccepted kills");
progress.Accepted = true; contract.Template.TargetId = target;
Check(!RoyalContractRules.Advance(contract, progress, RoyalContractObjective.NpcKills, Guid.NewGuid(), 1, now), "Wrong NPC cannot advance");
Check(RoyalContractRules.Advance(contract, progress, RoyalContractObjective.NpcKills, target, 1, now), "Authoritative matching kill advances");
Check(!RoyalContractRules.CanClaim(contract, progress, 2, now), "Incomplete contract cannot claim");
RoyalContractRules.Advance(contract, progress, RoyalContractObjective.NpcKills, target, int.MaxValue, now);
Check(progress.Quantity == 3, "Overflow-safe progress cap");
Check(RoyalContractRules.CanClaim(contract, progress, 3, now), "Complete contract eligible"); progress.Claimed = true;
Check(!RoyalContractRules.CanClaim(contract, progress, 3, now), "Duplicate claims rejected");
progress.Claimed = false; Check(!RoyalContractRules.CanClaim(contract, progress, 3, contract.ExpiresAt), "Expired at exact boundary");
progress.Quantity = 0; contract.Template.Collective = true;
Check(!RoyalContractRules.CanClaim(contract, progress, 1000, now), "Collective completion cannot reward a non-contributor");
contract.Template.Objective = RoyalContractObjective.ExploreMaps; contract.Template.TargetId = Guid.Empty;
Check(RoyalContractRules.Advance(contract, progress, RoyalContractObjective.ExploreMaps, target, 1, now), "Explore first map");
Check(!RoyalContractRules.Advance(contract, progress, RoyalContractObjective.ExploreMaps, target, 1, now), "Repeated map/instance/reconnect not counted twice");
var clonedProgress = RoyalContractRules.Read<RoyalContractProgress>(RoyalContractRules.Json(progress));
Check(!RoyalContractRules.Advance(contract, clonedProgress, RoyalContractObjective.ExploreMaps, target, 1, now), "Visited-map dedup persists across restart");

Options.EnsureCreated();
var item = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Currency, MaxInventoryStack = 10 };
ItemDescriptor.Lookup.Set(item.Id, item);
try
{
    foreach (var throws in new[] { false, true })
    {
        var player = new Player(); GC.SuppressFinalize(player);
        player.Items[0].Set(new Item(item.Id, 2)); player.Items[1].Set(new Item(item.Id, 3));
        var t = new RoyalContract { Id = Guid.NewGuid(), Template = new() { Collective = true, Objective = RoyalContractObjective.DeliverItems, TargetId = item.Id }, Quantity = 4, ExpiresAt = now.AddHours(1) };
        var before = RoyalContractRules.Json(player.PetCollection.RoyalContracts);
        player.PetCollection.RoyalContracts.Contracts[t.Id] = new() { Accepted = true, Quantity = 4 };
        bool Save()
        {
            Check(player.Items.Where(s => s.ItemId == item.Id).Sum(s => s.Quantity) == 1, "Debit staged before native save");
            Check(player.GetVariable(RoyalContractRules.ContributionId(t.Id)).Value.Integer == 4, "Contribution in same save as debit");
            if (throws) throw new IOException("simulated failure"); return false;
        }
        try
        {
            var result = RoyalContractExchange.Commit(player, t, before, item.Id, -4, Save);
            Check(!throws && result == RoyalContractExchangeResult.SaveFailed, "Failed save reported");
        }
        catch (IOException) { Check(throws, "Throwing native save is surfaced"); }
        Check(player.Items[0].Quantity == 2 && player.Items[1].Quantity == 3, "Failed delivery restores each native stack");
        Check(!player.PetCollection.RoyalContracts.Contracts.ContainsKey(t.Id), "Failed save restores mission state");
        Check(player.GetVariable(RoyalContractRules.ContributionId(t.Id)) == null, "Failed delivery cannot inflate collective total");
    }
    var p = new Player(); GC.SuppressFinalize(p);
    var c = new RoyalContract { Id = Guid.NewGuid(), Quantity = 1, Template = new() { RewardItemId = item.Id, RewardQuantity = 12 } };
    var stateBefore = RoyalContractRules.Json(p.PetCollection.RoyalContracts);
    p.PetCollection.RoyalContracts.Seals = 25;
    p.PetCollection.RoyalContracts.Contracts[c.Id] = new() { Accepted = true, Claimed = true, Quantity = 1 };
    string saved = "";
    Check(RoyalContractExchange.Commit(p, c, stateBefore, item.Id, 12, () => { saved = p.PetsJson; return true; }) == RoyalContractExchangeResult.Success, "Successful native reward save");
    Check(p.Items.Where(s => s.ItemId == item.Id).Sum(s => s.Quantity) == 12, "Reward split across native max stacks");
    p.PetsJson = saved;
    Check(p.PetCollection.RoyalContracts.Contracts[c.Id].Claimed && p.PetCollection.RoyalContracts.Seals == 25, "Claim receipt and seals survive native player reload");
    foreach (var slot in p.Items) slot.Set(new Item(item.Id, 10));
    var old = RoyalContractRules.Json(p.PetCollection.RoyalContracts); p.PetCollection.RoyalContracts.Seals = 50;
    var called = false;
    Check(RoyalContractExchange.Commit(p, c, old, item.Id, 1, () => { called = true; return true; }) == RoyalContractExchangeResult.InventoryFull, "Full inventory rejects reward");
    Check(!called && p.PetCollection.RoyalContracts.Seals == 25, "Full inventory cannot consume claim or seals");
    p.RoyalContractOpenUntil = 0; RoyalContractsRuntime.Request(p, RoyalContractAction.Claim, c.Id);
    Check(p.PetCollection.RoyalContracts.Seals == 25, "Forged request without an event session ignored");
}
finally { ItemDescriptor.Lookup.DeleteAt(item.Id); }

var packet = new RoyalContractRequestPacket { Action = RoyalContractAction.Deliver, ContractId = contract.Id };
var decoded = MessagePackSerializer.Deserialize<RoyalContractRequestPacket>(MessagePackSerializer.Serialize(packet));
Check(decoded.Action == packet.Action && decoded.ContractId == packet.ContractId, "Native request round-trip");
var board = new RoyalContractBoardPacket { OpenWindow = true, StateJson = RoyalContractRules.Json(new RoyalContractBoardState { Seals = 120 }) };
var boardCopy = MessagePackSerializer.Deserialize<RoyalContractBoardPacket>(MessagePackSerializer.Serialize(board));
Check(boardCopy.OpenWindow && RoyalContractRules.Read<RoyalContractBoardState>(boardCopy.StateJson).Seals == 120, "Native board packet round-trip");
Check((int)new OpenRoyalContractsCommand().Type == 1090, "Stable event command ID");
foreach (var language in GameTextCatalog.Languages)
{
    var text = RoyalContractsText.Get("Royal seals: {0}", language);
    Check(text.Length > 0 && GameTextTokens.Compatible("Royal seals: {0}", text), "Locale placeholders " + language);
}
Check(RoyalContractsText.Get("Royal Contracts", "fr") == "Contrats royaux", "French catalog");
Check(RoyalContractsText.Get("Royal Contracts", "es") == "Contratos reales", "Spanish catalog");
Check(RoyalContractsText.Get("Royal Contracts", "unknown") == "Royal Contracts", "English fallback");
Check(RoyalContractsText.Get("Custom author text", "fr") == "Custom author text", "Untranslated content preserves source");
Console.WriteLine($"Royal Contracts: {count} checks passed.");
