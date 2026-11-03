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
// Every new activity uses the same acceptance, activityTarget, expiry and quantity boundaries.
foreach(var objective in Enum.GetValues<RoyalContractObjective>().Where(o => (int)o >= 3))
{
    var activityTarget=Guid.NewGuid();
    var activity=new RoyalContract { Id=Guid.NewGuid(), Template=new() { Objective=objective,TargetId=activityTarget }, Quantity=3, ExpiresAt=now.AddHours(1) };
    var activityProgress=new RoyalContractProgress();
    Check(!RoyalContractRules.Advance(activity,activityProgress,objective,activityTarget,1,now),"Unaccepted activity cannot activityProgress: "+objective);
    activityProgress.Accepted=true;
    Check(!RoyalContractRules.Advance(activity,activityProgress,objective,Guid.NewGuid(),1,now),"Wrong activity activityTarget rejected: "+objective);
    Check(RoyalContractRules.Advance(activity,activityProgress,objective,activityTarget,2,now) && activityProgress.Quantity==2,"Authoritative activity amount: "+objective);
    Check(RoyalContractRules.Advance(activity,activityProgress,objective,activityTarget,int.MaxValue,now) && activityProgress.Quantity==3,"Overflow-safe activity cap: "+objective);
    Check(!RoyalContractRules.Advance(activity,activityProgress,objective,activityTarget,1,now),"Completed activity cannot increment twice: "+objective);
    Check(RoyalContractRules.CanClaim(activity,activityProgress,3,now),"Activity reward can be claimed: "+objective);
    foreach(var language in GameTextCatalog.Languages)
        Check(RoyalContractsText.Get(RoyalContractRules.ObjectiveLabel(objective),language).Length>0,"Localized activity: "+objective+"/"+language);
}
Check((int)RoyalContractAction.Claim==3 && (int)RoyalContractObjective.ExploreMaps==2,"Existing action and objective IDs preserved");
Check((int)new OpenFurnitureShopCommand().Type==1092 && (int)new OpenHouseDirectoryCommand().Type==1093,"Housing command IDs are distinct and stable");
var lobbyConfig=new Intersect.Framework.Core.LobbyConfiguration { Activities=[new() {Activity=Intersect.Network.Packets.PartyLobbies.LobbyActivity.Dungeon,MaximumMembers=4,Image="dungeon.png"}] };
lobbyConfig.Validate();
lobbyConfig.Activities[0].Image="../escape.png"; Reject(lobbyConfig.Validate,"Lobby image traversal rejected");
lobbyConfig.Activities[0].Image="dungeon.png";lobbyConfig.Activities[0].MaximumMembers=0;Reject(lobbyConfig.Validate,"Zero lobby capacity rejected");
lobbyConfig.Activities[0].MaximumMembers=101;Reject(lobbyConfig.Validate,"Excessive lobby capacity rejected");
var validPng=TestPng(1,1,[0,255,0,0,255]);
Check(Intersect.Framework.Core.GuildEmblemPng.Sanitize(validPng).SequenceEqual(validPng),"Valid RGBA logo accepted");
Reject(()=>Intersect.Framework.Core.GuildEmblemPng.Sanitize(TestPng(129,1,[])),"Oversized logo rejected before decode");
Reject(()=>Intersect.Framework.Core.GuildEmblemPng.Sanitize(TestPng(1,1,[0,0,0,0,0,0])),"PNG decompression overrun rejected");
Reject(()=>Intersect.Framework.Core.GuildEmblemPng.Sanitize(validPng[..^1]),"Truncated logo rejected");
var corrupt=validPng.ToArray();corrupt[20]^=1;Reject(()=>Intersect.Framework.Core.GuildEmblemPng.Sanitize(corrupt),"Bad logo CRC rejected");
Reject(()=>Intersect.Framework.Core.GuildEmblemPng.Sanitize(new byte[65537]),"Oversized upload rejected");
var emblem=new Intersect.Network.Packets.Guilds.GuildEmblemRequestPacket { Action=Intersect.Network.Packets.Guilds.GuildEmblemAction.Submit,Image=validPng,Revision=Guid.NewGuid() };
var emblemCopy=MessagePackSerializer.Deserialize<Intersect.Network.Packets.Guilds.GuildEmblemRequestPacket>(MessagePackSerializer.Serialize(emblem));
Check(emblemCopy.Image.SequenceEqual(validPng) && emblemCopy.Revision==emblem.Revision,"Emblem packet revision and image round trip");

static byte[] TestPng(int width,int height,byte[] raw)
{
    using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
    void Chunk(string name,byte[] data)
    {
        var length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);output.Write(length);
        var tag=System.Text.Encoding.ASCII.GetBytes(name);output.Write(tag);output.Write(data);uint crc=uint.MaxValue;
        foreach(var b in tag.Concat(data)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^(0xedb88320u & (uint)-(int)(crc&1));}
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);output.Write(length);
    }
    var header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header,width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=6;Chunk("IHDR",header);
    using var compressed=new MemoryStream();using(var zlib=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionLevel.Fastest,true))zlib.Write(raw);
    Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);return output.ToArray();
}
foreach(var (source,versions) in Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Catalog.Entries)
    foreach(var language in GameTextCatalog.Languages)
        if(!versions.TryGetValue(language,out var translated) || !GameTextTokens.Compatible(source,translated)) throw new Exception("QOL locale coverage/placeholder mismatch: "+source+"/"+language);

var sealItem=new ItemDescriptor(Guid.NewGuid()){Name="Seal shop reward",ItemType=ItemType.Currency,MaxInventoryStack=10};
ItemDescriptor.Lookup.Set(sealItem.Id,sealItem);
try
{
    var sealBuyer=new Player();GC.SuppressFinalize(sealBuyer);sealBuyer.PetCollection.RoyalContracts.Seals=100;
    var sealOffer=new Intersect.GameObjects.ShopItemDescriptor(sealItem.Id,Guid.Empty,25);
    var savedPurchase=RoyalSealShopRuntime.CommitPurchase(sealBuyer,sealOffer,3,()=>
    {
        Check(sealBuyer.PetCollection.RoyalContracts.Seals==25,"Seals debit is staged in the item save");
        Check(sealBuyer.Items.Where(i=>i.ItemId==sealItem.Id).Sum(i=>i.Quantity)==3,"Purchased items staged before save");return true;
    });
    Check(savedPurchase=="" && sealBuyer.PetCollection.RoyalContracts.Seals==25,"Successful purchase spends exactly the authoritative seal price");
    var called=false;
    Check(RoyalSealShopRuntime.CommitPurchase(sealBuyer,sealOffer,2,()=>{called=true;return true;})=="Not enough Royal Seals." && !called,"Insufficient seals cannot save or grant items");
    sealBuyer.PetCollection.RoyalContracts.Seals=100;
    foreach(var throws in new[]{false,true})
    {
        var failure=RoyalSealShopRuntime.CommitPurchase(sealBuyer,sealOffer,1,()=>{if(throws)throw new IOException();return false;});
        Check(failure.Length>0 && sealBuyer.PetCollection.RoyalContracts.Seals==100 && sealBuyer.Items.Where(i=>i.ItemId==sealItem.Id).Sum(i=>i.Quantity)==3,"Failed or throwing saves restore seals and inventory together");
    }
    Check(RoyalSealShopRuntime.CommitPurchase(sealBuyer,sealOffer,0,()=>true).Length>0,"Zero purchase quantity rejected");
    foreach(var slot in Enumerable.Range(0,Options.Instance.Player.MaxInventory))sealBuyer.Items[slot].Set(new Item(sealItem.Id,10));
    Check(RoyalSealShopRuntime.CommitPurchase(sealBuyer,sealOffer,1,()=>true)=="Make room in your inventory." && sealBuyer.PetCollection.RoyalContracts.Seals==100,"Full inventory never consumes Royal Seals");
    var equipment=new ItemDescriptor(Guid.NewGuid()){Name="Seal equipment",ItemType=ItemType.Equipment,Stackable=false};
    ItemDescriptor.Lookup.Set(equipment.Id,equipment);
    try
    {
        var empty=Enumerable.Range(0,4).Select(_=>Item.None).ToArray();
        Check(ShopInventoryPlan.Grant(empty,equipment,3,out var equipmentPlan)==ShopPlanError.None && equipmentPlan.Count(i=>i.ItemId==equipment.Id)==3,"Seal rewards support independent non-stackable equipment copies");
        Check(equipmentPlan.Where(i=>i.ItemId==equipment.Id).All(i=>i.Quantity==1),"Equipment quantity never becomes an invalid stack");
    }
    finally {ItemDescriptor.Lookup.DeleteAt(equipment.Id);}
    var sealShop=new Intersect.GameObjects.ShopDescriptor(Guid.NewGuid()){ConfigurationKind=Intersect.GameObjects.ShopConfigurationKind.RoyalSeals,SellingItems=[sealOffer]};
    var restoredShop=new Intersect.GameObjects.ShopDescriptor{JsonBuyingItems=sealShop.JsonBuyingItems,JsonSellingItems=sealShop.JsonSellingItems};
    Check(restoredShop.ConfigurationKind==Intersect.GameObjects.ShopConfigurationKind.RoyalSeals && restoredShop.SellingItems.Single().CostItemQuantity==25,"Specialized seal shop and prices survive native persistence");
    var request=new Intersect.Network.Packets.RoyalContracts.RoyalSealShopRequestPacket{Action=Intersect.Network.Packets.RoyalContracts.RoyalSealShopAction.Buy,ShopId=sealShop.Id,Slot=0,Quantity=3,ExpectedPrice=25,ExpectedItemId=sealItem.Id};
    var copy=MessagePackSerializer.Deserialize<Intersect.Network.Packets.RoyalContracts.RoyalSealShopRequestPacket>(MessagePackSerializer.Serialize(request));
    Check(copy.ShopId==request.ShopId && copy.Quantity==3 && copy.ExpectedPrice==25 && copy.ExpectedItemId==sealItem.Id,"Seal purchase quote round trip");
}
finally {ItemDescriptor.Lookup.DeleteAt(sealItem.Id);}
Console.WriteLine($"Royal Contracts: {count} checks passed.");
