using Intersect.Core;
using Microsoft.Extensions.Logging;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Newtonsoft.Json;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static readonly Dictionary<Guid,Dictionary<NavalTaskKind,long>> QuestUpdates=[];
    private static void QueueNavalTask(Guid actor,NavalTaskKind kind,long amount)
    {if(actor==Guid.Empty || amount<=0)return;if(!QuestUpdates.TryGetValue(actor,out var changes))QuestUpdates[actor]=changes=[];changes[kind]=Math.Min(200000000000000L,changes.GetValueOrDefault(kind)+amount);}
    private static bool SaveManagement(Player p)
    {
        try{return p.User?.Save(force:true)==UserSaveResult.Completed;}
        catch(Exception error){ApplicationContext.Context.Value?.Logger.LogError(error,"Could not save naval activity for {PlayerId}",p.Id);return false;}
    }
    internal static bool CommitCrewForfeit(Player p,Func<bool> save)=>p.NavalCrewPassage==null || CommitNavalForfeit(p,p.NavalCrewPassage.Ship.Id,save);
    internal static bool CommitNavalForfeit(Player p,Guid ship,Func<bool> save)
    {
        var holds=RewardHolds(p).Where(h=>h.Id==ship).ToArray();if(holds.Length==0)return true;
        var before=p.NavalFleetJson;var ok=false;
        try{foreach(var hold in holds)hold.Cargo.Clear();ok=save();return ok;}
        finally{if(!ok)p.NavalFleetJson=before;}
    }
    private sealed class RepairRound {internal Guid Token=Guid.NewGuid(),Ship;internal NavalRepairKind Kind;internal long Start,Last;internal int Hits,Target;}
    private static readonly Dictionary<Guid,RepairRound> Repairs=[];
    private static readonly Dictionary<Guid,long> RepairCooldown=[];
    private static readonly Dictionary<Guid,long> RepairRewards=[];
    private static readonly Dictionary<Guid,List<(Guid Player,int Hull)>> RepairRequests=[];
    internal static int CareerMaximum(Guid id){lock(Gate){_=IsEnabled();return id==NavalProfessions.Sailing?_settings.Sailing.MaximumLevel:id==NavalProfessions.Gunnery?_settings.Gunnery.MaximumLevel:_settings.Repair.MaximumLevel;}}
    internal static SailingProgress CareerProgress(Player p,Guid id)
    {lock(Gate){_=IsEnabled();return id==NavalProfessions.Sailing?_settings.Sailing.Progress(p.SailingExperience):id==NavalProfessions.Gunnery?_settings.Gunnery.Curve().Progress(p.GunneryExperience):_settings.Repair.Curve().Progress(p.NavalCareer.RepairExperience);}}
    internal static long CareerExperience(Player p,Guid id)=>id==NavalProfessions.Sailing?p.SailingExperience:id==NavalProfessions.Gunnery?p.GunneryExperience:p.NavalCareer.RepairExperience;
    private static Guid Driver(Vessel v)=>v.HelmsmanId==Guid.Empty?v.State.CaptainId:v.HelmsmanId;
    private static int AmmoCount(Player p,Guid item)=>p.NavalCareer.Ammo.FirstOrDefault(a=>a.ItemId==item)?.Quantity??0;
    private static bool AmmoAdd(Player p,Guid item,int amount)
    {
        var stack=p.NavalCareer.Ammo.FirstOrDefault(a=>a.ItemId==item);
        var value=(long)(stack?.Quantity??0)+amount;
        if(item==Guid.Empty || value<0 || value>1000000000 || stack==null && (amount<=0 || p.NavalCareer.Ammo.Count>=512)) return false;
        if(value==0) p.NavalCareer.Ammo.Remove(stack);
        else if(stack==null) p.NavalCareer.Ammo.Add(new(){ItemId=item,Quantity=(int)value});else stack.Quantity=(int)value;
        return true;
    }
    internal static bool CommitAmmo(Player p,Guid item,int amount,Func<bool> save)
    {
        var before=p.NavalFleetJson;var ok=false;
        try {if(!AmmoAdd(p,item,amount)) return false;ok=save();return ok;}
        finally {if(!ok)p.NavalFleetJson=before;}
    }
    private static ItemDescriptor NavalCurrency(Guid id,string fallback)=>Currency(new HarbourOffer {CurrencyItemId=id,Currency=fallback});
    private static bool ShopValid(Player p,long now)=>p.NavalShipId==Guid.Empty && HarbourAccessRules.ShopSessionValid(p.MapId,p.X,p.Y,p.NavalShopMap,p.NavalShopX,p.NavalShopY,now,p.NavalShopExpires,_settings.ShopInteractionRange);
    private static bool SkillShopValid(Player p,long now)
    {
        if(p.NavalAmmoShop || !ShopValid(p,now))return false;
        try{return ShopCatalogue(p).Enabled;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException){return false;}
    }
    internal static void OpenAmmoShopFromEvent(Player p,Entities.Events.Event instance,Guid shopId=default)
    {
        lock(p.EntityLock)
        {
            var now=Timing.Global.Milliseconds;
            lock(Gate)
            {
                if(!IsEnabled() || !p.InGame || p.IsDead || p.HasPendingDeath || p.NavalShipId!=Guid.Empty || shopId!=Guid.Empty && Intersect.GameObjects.ShopDescriptor.Get(shopId)?.ConfigurationKind!=Intersect.GameObjects.ShopConfigurationKind.NavalAmmunition) return;
                var map=instance.MapId==Guid.Empty?p.MapId:instance.MapId;
                var x=instance.MapId==Guid.Empty?p.X:instance.X;var y=instance.MapId==Guid.Empty?p.Y:instance.Y;
                if(!HarbourAccessRules.ShopSessionValid(p.MapId,p.X,p.Y,map,x,y,0,1,_settings.ShopInteractionRange))return;
                p.NavalShopMap=map;p.NavalShopX=x;p.NavalShopY=y;p.NavalShopExpires=now+_settings.ShopSessionSeconds*1000L;p.NavalAmmoShop=true;p.NavalShopId=shopId;
                SendManagement(p,NavalManageAction.AmmoShop,now);
            }
        }
    }
    private static (Guid Currency,int Price,bool Allowed) AmmoTrade(Player p,CannonAmmunition ammo,bool sell)
    {
        if(p.NavalShopId==Guid.Empty)return (ammo.ShopCurrencyItemId,sell?ammo.ShopPrice/2:ammo.ShopPrice,ammo.ForSale && ammo.RequiresItem);
        var shop=Intersect.GameObjects.ShopDescriptor.Get(p.NavalShopId);
        if(shop?.ConfigurationKind!=Intersect.GameObjects.ShopConfigurationKind.NavalAmmunition)return(default,0,false);
        var entry=(sell?shop.BuyingItems:shop.SellingItems).FirstOrDefault(i=>i.ItemId==ammo.ItemId);
        if(entry!=null)return (entry.CostItemId==Guid.Empty?shop.DefaultCurrencyId:entry.CostItemId,sell?shop.BuybackPrice(entry.CostItemQuantity):entry.CostItemQuantity,entry.CostItemQuantity>=0);
        if(sell && !shop.BuyingWhitelist && ItemDescriptor.Get(ammo.ItemId) is {} item)return(shop.DefaultCurrencyId,shop.BuybackPrice(item.Price),true);
        return(default,0,false);
    }
    private static void SendManagement(Player p,NavalManageAction mode,long now,string message="")
    {
        Ships.TryGetValue(p.NavalShipId,out var v);Repairs.TryGetValue(p.Id,out var round);
        var state=new NavalManagementState {Mode=mode,Message=message,SkillShop=SkillShopValid(p,now),Shop=mode==NavalManageAction.AmmoShop && p.NavalAmmoShop && ShopValid(p,now),
            ShipId=v?.State.Id??Guid.Empty,OwnerId=v?.State.CaptainId??Guid.Empty,HelmsmanId=v==null?Guid.Empty:Driver(v),
            HelmOfferId=v?.HelmOfferTo==p.Id && v.HelmOfferUntil>now?v.HelmOfferToken:Guid.Empty,
            SailingLevel=_settings.Sailing.Progress(p.SailingExperience).Level,GunneryLevel=_settings.Gunnery.Curve().Progress(p.GunneryExperience).Level,
            Shipwright=_settings.Repair.Curve().Progress(p.NavalCareer.RepairExperience),SelectedAmmo=p.NavalSelectedAmmo};
        state.Ammo=_settings.Ammunition.Select(a=>new NavalAmmoOffer {Kind=a.Kind,ItemId=a.ItemId,Quantity=a.RequiresItem?AmmoCount(p,a.ItemId):-1,
            MainQuantity=a.RequiresItem && a.ItemId!=Guid.Empty?p.FindInventoryItemQuantity(a.ItemId):0,ForSale=AmmoTrade(p,a,false).Allowed,
            Price=AmmoTrade(p,a,false).Price,Currency=NavalCurrency(AmmoTrade(p,a,false).Currency,"Aureons")?.Name??"",
            CanBuy=AmmoTrade(p,a,false).Allowed && NavalCurrency(AmmoTrade(p,a,false).Currency,"Aureons") is {} money && p.CountItems(money.Id)>=AmmoTrade(p,a,false).Price,
            SellPrice=AmmoTrade(p,a,true).Allowed?AmmoTrade(p,a,true).Price:-1,SellCurrency=NavalCurrency(AmmoTrade(p,a,true).Currency,"Aureons")?.Name??"",SailingLevel=a.RequiredSailingLevel,GunneryLevel=a.RequiredLevel,
            Icon=string.IsNullOrEmpty(a.Icon)?ItemDescriptor.Get(a.ItemId)?.Icon??"":a.Icon,
            Available=(v!=null && Driver(v)==p.Id?state.SailingLevel>=a.RequiredSailingLevel:state.GunneryLevel>=a.RequiredLevel)}).ToArray();
        state.Skills=_settings.Skills.Select(d=>new NavalSkillView {Definition=d,Owned=p.NavalCareer.Skills.Contains(d.Kind),
            CooldownMilliseconds=v!=null && v.SkillReady.TryGetValue(d.Kind,out var ready)?(int)Math.Clamp(ready-now,0,3600000):0}).ToArray();
        if(round!=null) {state.RepairToken=round.Token;state.RepairKind=round.Kind;state.RepairTarget=round.Target;state.RepairHits=round.Hits;
            state.RepairRequiredHits=_settings.Repair.RequiredHits;state.RepairRemainingMilliseconds=(int)Math.Max(0,round.Start+_settings.Repair.RoundSeconds*1000-now);
            state.RepairElapsedMilliseconds=(int)Math.Max(0,now-round.Start);}
        p.SendPacket(new NavalManagementPacket {Json=JsonConvert.SerializeObject(state)});
    }
    internal static void Manage(Player p,NavalManagePacket packet,long now)
    {
        lock(p.EntityLock)
        {
            if(!Enum.IsDefined(packet.Action) || !p.InGame || p.IsDead || p.HasPendingDeath || p.User==null || p.Z!=0)return;
            if(now-p.LastNavalManageRequest<100)return;p.LastNavalManageRequest=now;
            lock(Gate)if(!IsEnabled())return;
            var mode=packet.Action;var message="";
            if(mode==NavalManageAction.AmmoShop){lock(Gate)if(!p.NavalAmmoShop || !ShopValid(p,now))return;}
            if(mode==NavalManageAction.RepairMenu){lock(Gate) {if(!Ships.TryGetValue(p.NavalShipId,out var repairShip) || Driver(repairShip)==p.Id)return;}lock(Gate)SendManagement(p,NavalManageAction.Repair,now);return;}

            if(mode==NavalManageAction.Deposit)return;
            if(mode is NavalManageAction.Withdraw or NavalManageAction.BuyAmmo or NavalManageAction.SellAmmo)
            {
                var a=_settings.Ammunition.FirstOrDefault(a=>a.Kind==packet.Ammo);var count=packet.Quantity;
                if(a==null || !a.RequiresItem || count is <1 or >10000 || ItemDescriptor.Get(a.ItemId)?.IsStackable!=true)return;
                var json=p.NavalFleetJson;var inventory=SnapshotCargoInventory(p);var ok=false;
                try
                {
                    if(mode==NavalManageAction.Withdraw)
                    {
                        var planned=PlanCargoInventory(inventory,[new NavalCargoParcel {ItemId=a.ItemId,Quantity=count}]);
                        ok=planned!=null && AmmoAdd(p,a.ItemId,-count);
                        if(ok)for(var i=0;i<planned.Length;i++)p.Items[i].Set(planned[i]);
                    }
                    else
                    {
                        var sell=mode==NavalManageAction.SellAmmo;var trade=AmmoTrade(p,a,sell);
                        lock(Gate)ok=p.NavalAmmoShop && ShopValid(p,now) && trade.Allowed;
                        var currency=NavalCurrency(trade.Currency,"Aureons");var cost=(long)trade.Price*count;
                        if(sell)
                        {
                            var fromBag=Math.Min(count,AmmoCount(p,a.ItemId));
                            ok=ok && currency!=null && cost>0 && cost<=int.MaxValue;
                            if(ok)ok=(fromBag==0 || AmmoAdd(p,a.ItemId,-fromBag)) && (count==fromBag || p.TryTakeItem(a.ItemId,count-fromBag,sendUpdate:false));
                            var planned=ok?PlanCargoInventory(SnapshotCargoInventory(p),CurrencyParcels(currency.Id,cost)):null;
                            ok=ok && planned!=null;
                            if(ok)for(var i=0;i<planned.Length;i++)p.Items[i].Set(planned[i]);
                        }
                        else ok=ok && currency!=null && cost>=0 && cost<=int.MaxValue && (cost==0 || p.TryTakeItem(currency.Id,(int)cost,sendUpdate:false)) && AmmoAdd(p,a.ItemId,count);
                    }
                    if(ok)ok=SaveManagement(p);
                }
                finally {if(!ok){p.NavalFleetJson=json;for(var i=0;i<inventory.Length;i++)p.Items[i].Set(inventory[i]);}}
                message=ok?"Ammunition updated.":"Transaction failed. Nothing was changed.";mode=mode is NavalManageAction.BuyAmmo or NavalManageAction.SellAmmo?NavalManageAction.AmmoShop:NavalManageAction.Ammo;PacketSender.SendInventory(p);if(ok)p.RefreshNavalCargoQuests([a.ItemId]);
            }
            else if(mode==NavalManageAction.SelectAmmo)
            {
                var selected=_settings.Ammunition.FirstOrDefault(a=>a.Kind==packet.Ammo);
                if(selected==null)return;
                if(selected.RequiresItem && AmmoCount(p,selected.ItemId)<1000000000 && p.FindInventoryItemQuantity(selected.ItemId)>0)
                {
                    var json=p.NavalFleetJson;var items=SnapshotCargoInventory(p);var quantity=Math.Min(1000000000-AmmoCount(p,selected.ItemId),Math.Min(10000,p.FindInventoryItemQuantity(selected.ItemId)));var saved=false;
                    try {saved=p.TryTakeItem(selected.ItemId,quantity,sendUpdate:false) && AmmoAdd(p,selected.ItemId,quantity) && SaveManagement(p);}
                    finally {if(!saved){p.NavalFleetJson=json;for(var i=0;i<items.Length;i++)p.Items[i].Set(items[i]);}}
                    PacketSender.SendInventory(p);if(!saved)return;
                }
                p.NavalSelectedAmmo=packet.Ammo;
                if(packet.Value==1)return;
                mode=NavalManageAction.Ammo;
            }
            else if(mode==NavalManageAction.BuySkill)
            {
                var d=ShopSkills(p).FirstOrDefault(d=>d.Kind==packet.Skill);
                bool valid;lock(Gate)valid=SkillShopValid(p,now);
                var currency=d==null?null:NavalCurrency(d.CurrencyItemId,"Auragems");
                if(!valid || d==null || currency==null || p.NavalCareer.Skills.Contains(d.Kind) || _settings.Sailing.Progress(p.SailingExperience).Level<d.RequiredLevel)return;
                var json=p.NavalFleetJson;var inventory=SnapshotCargoInventory(p);var ok=false;
                try {ok=(d.Price==0 || p.TryTakeItem(currency.Id,d.Price,sendUpdate:false)) && p.NavalCareer.Skills.Add(d.Kind);if(ok)ok=SaveManagement(p);}
                finally {if(!ok){p.NavalFleetJson=json;for(var i=0;i<inventory.Length;i++)p.Items[i].Set(inventory[i]);}}
                message=ok?"Skill learned.":"Transaction failed. Nothing was changed.";PacketSender.SendInventory(p);SendHarbour(p,ShopCatalogue(p),message,Intersect.Server.LogiCoins.LogiCoinPurchaseRuntime.HasActivePremium(p),HarbourView.Shop);return;
            }
            else
            {
                lock(Gate)
                {
                    Ships.TryGetValue(p.NavalShipId,out var v);
                    if(mode==NavalManageAction.UseSkill)
                    {
                        var d=_settings.Skills.FirstOrDefault(s=>s.Kind==packet.Skill);
                        if(v==null || Driver(v)!=p.Id || d==null || v.Health<=0 || v.SunkUntil>0 || !p.NavalCareer.Skills.Contains(d.Kind) ||
                            _settings.Sailing.Progress(p.SailingExperience).Level<d.RequiredLevel || v.SkillReady.GetValueOrDefault(d.Kind)>now)return;
                        if(d.Kind==NavalSkillKind.RammingSpeed)v.RamTargets.Clear();
                        v.SkillReady[d.Kind]=now+d.CooldownSeconds*1000L;v.SkillUntil[d.Kind]=now+d.DurationSeconds*1000L;mode=NavalManageAction.Skills;
                    }
                    else if(mode==NavalManageAction.Repair)
                    {
                        if(v==null || !_settings.Repair.Enabled || !Enum.IsDefined(packet.Repair) || v.Health is <=0 or >=100 || v.SunkUntil>0 || (Driver(v)==p.Id) ||
                            RepairCooldown.GetValueOrDefault(p.Id)>now || Repairs.ContainsKey(p.Id) || p.NavalBusy)
                        {SendManagement(p,NavalManageAction.Repair,now,"Repair unavailable. Check hull, role and cooldown.");return;}
                        foreach(var gun in v.Guns.Values.Where(g=>g.Owner==p.Id))gun.Owner=gun.Lease=Guid.Empty;
                        Repairs[p.Id]=new(){Ship=v.State.Id,Kind=packet.Repair,Start=now,Target=Random.Shared.Next(3)};
                    }
                    else if(mode==NavalManageAction.CancelRepair) {Repairs.Remove(p.Id);RepairCooldown[p.Id]=now+1000;}
                    else if(mode==NavalManageAction.RepairInput)
                    {
                        if(!Repairs.TryGetValue(p.Id,out var r) || r.Token!=packet.Token)return;
                        if(v==null || v.State.Id!=r.Ship || v.Health is <=0 or >=100 || (Driver(v)==p.Id) || now-r.Start>_settings.Repair.RoundSeconds*1000)
                        {Repairs.Remove(p.Id);message="Repair cancelled.";}
                        else if(now-r.Last>=400)
                        {
                            r.Last=now;
                            var correct=NavalRepairRules.Matches(r.Kind,r.Target,packet.Value,now-r.Start);
                            if(correct)r.Hits=Math.Min(_settings.Repair.RequiredHits,r.Hits+1);else r.Hits=Math.Max(0,r.Hits-1);
                            r.Target=r.Kind==NavalRepairKind.Pumping?(r.Target==0?2:0):Random.Shared.Next(3);
                            if(NavalRepairRules.Complete(_settings.Repair,r.Hits,now-r.Start))
                            {
                                Repairs.Remove(p.Id);RepairCooldown[p.Id]=now+_settings.Repair.CooldownSeconds*1000L;
                                if(!RepairRequests.TryGetValue(v.State.Id,out var requests))RepairRequests[v.State.Id]=requests=[];
                                requests.Add((p.Id,_settings.Repair.HullPerSuccess));message="Repair completed.";
                            }
                        }
                        mode=NavalManageAction.Repair;
                    }
                    else if(mode==NavalManageAction.OfferHelm)
                    {
                        if(packet.TargetId==Guid.Empty){SendManagement(p,mode,now,"Choose a crew member to offer the helm.");return;}
                        if(v==null || v.State.CaptainId!=p.Id || !v.Crew.TryGetValue(packet.TargetId,out var crew) || now-crew.Seen>1500 || !p.Party.Any(member=>member.Id==packet.TargetId))return;
                        v.HelmOfferTo=packet.TargetId;v.HelmOfferToken=Guid.NewGuid();v.HelmOfferUntil=now+30000;message="Helm offered for 30 seconds.";
                    }
                    else if(mode==NavalManageAction.AcceptHelm)
                    {
                        if(v==null || v.HelmOfferTo!=p.Id || v.HelmOfferToken!=packet.Token || v.HelmOfferUntil<=now || !p.Party.Any(member=>member.Id==v.State.CaptainId))return;
                        Repairs.Remove(p.Id);v.HelmsmanId=p.Id;v.HelmOfferTo=v.HelmOfferToken=Guid.Empty;v.Controls.Reset();
                        foreach(var g in v.Guns.Values)g.Owner=g.Lease=Guid.Empty;
                    }
                    else if(mode==NavalManageAction.ReclaimHelm)
                    {
                        if(v==null || v.State.CaptainId!=p.Id && Driver(v)!=p.Id)return;
                        Repairs.Remove(v.State.CaptainId);v.HelmsmanId=v.HelmOfferTo=v.HelmOfferToken=Guid.Empty;v.Controls.Reset();
                        foreach(var g in v.Guns.Values)g.Owner=g.Lease=Guid.Empty;
                    }
                    else if(mode==NavalManageAction.Dock)
                    {if(v==null || v.State.CaptainId!=p.Id || !ByCrewDock(v))return;v.DockRequested=true;v.Anchored=true;v.Controls.Reset();}
                }
                if(mode==NavalManageAction.Dock)return;
            }
            lock(Gate)SendManagement(p,mode,now,message);
        }
    }
    private static void TryRam(Vessel v,Vessel target,long now)
    {
        var power=SkillPower(v,NavalSkillKind.RammingSpeed,now);
        if(power==0 || target.Enemy==null || target.Health<=0 || target.SunkUntil>0 || Math.Abs(v.State.Speed)<1 || !v.RamTargets.Add(target.State.Id))return;
        var damage=Math.Min(target.Health,Math.Max(1,_settings.CannonDamage*(100+power)/100));
        target.Contributions[Driver(v)]=target.Contributions.GetValueOrDefault(Driver(v))+damage;
        target.Health-=damage;target.State.DamageState=1;Engage(v,target,now);
        QueueNavalTask(Driver(v),NavalTaskKind.HullDamage,damage);
        Emit(NavalEffectKind.HullImpact,target.State.MapId,target.State.X,target.State.Y,v.State.Heading,now);
        if(target.Health==0)
        {
            CreditEnemySink(target,v.State.Id,v.State.CaptainId);target.State.Speed=target.State.Throttle=0;target.Controls.Reset();
            target.SunkUntil=now+Math.Max(2000,target.Definition.FrameMilliseconds*4);target.SinkingGraphics=target.Definition.SinkingGraphics;
            Emit(NavalEffectKind.Sinking,target.State.MapId,target.State.X,target.State.Y,target.State.Heading,now);
        }
    }
    private static string IdleGraphic(ShipDefinition d)
    {
        if(!string.IsNullOrEmpty(d.IdleGraphics))return d.IdleGraphics;
        return d.Graphics.ToLowerInvariant() switch {"ship_corpsroyaux_002.png"=>"Ship_CorpsRoyaux_idle.png","ship_herbonia_001.png" or "ship_herbonia_002.png"=>"Ship_Herbonia_idle.png",_=>d.Graphics};
    }
    private static ShipDefinition MovementDefinition(Vessel v,long now)
    {
        var bonus=Math.Max(SkillPower(v,NavalSkillKind.FullSail,now),SkillPower(v,NavalSkillKind.RammingSpeed,now));
        if(bonus==0 && v.SlowUntil<=now)return v.Definition;
        var d=JsonConvert.DeserializeObject<ShipDefinition>(JsonConvert.SerializeObject(v.Definition));
        var factor=(100+bonus)/100d*(v.SlowUntil>now?(100-v.SlowPercent)/100d:1);
        d.MaxSpeed=Math.Min(20,d.MaxSpeed*factor);d.Acceleration*=Math.Max(1,factor);return d;
    }
    private static int SkillPower(Vessel v,NavalSkillKind kind,long now)=>v.SkillUntil.GetValueOrDefault(kind)>now?_settings.Skills.FirstOrDefault(s=>s.Kind==kind)?.PowerPercent??0:0;
    private static void UpdateManagement(Player p,long now)
    {
        if(p.NavalPartyExitRequested && p.CaptainedShipId!=Guid.Empty)
        {
            var id=p.CaptainedShipId;var forfeitedShip=p.NavalFleet.FirstOrDefault(s=>s.Id==id);
            lock(Gate)if(Ships.TryGetValue(id,out var vessel)){vessel.Anchored=true;vessel.Controls.Reset();}
            if(!ApplyPendingExperience(p,now,force:true) || !ApplyPendingDamage(p,now,force:true) || !CommitNavalForfeit(p,id,()=>SaveManagement(p)))return;
            lock(Gate){ReleaseLocked(p);Repairs.Remove(p.Id);if(PendingSailingRewards.TryGetValue(p.Id,out var pending))PendingSailingRewards[p.Id]=pending with {Cargo=[]};}
            p.NavalPartyExitRequested=false;if(forfeitedShip!=null)ReturnToHarbour(p,forfeitedShip);else p.WarpToSpawn();SaveManagement(p);
        }
        else if(p.NavalShipId==Guid.Empty)p.NavalPartyExitRequested=false;

        Dictionary<NavalTaskKind,long> changes;lock(Gate){QuestUpdates.Remove(p.Id,out changes);}
        if(changes!=null)foreach(var pair in changes)p.UpdateNavalQuestTasks(pair.Key,pair.Value);
        if(p.Quests.Count>0){p.UpdateNavalQuestTasks(NavalTaskKind.SailingLevel,_settings.Sailing.Progress(p.SailingExperience).Level);
            p.UpdateNavalQuestTasks(NavalTaskKind.GunneryLevel,_settings.Gunnery.Curve().Progress(p.GunneryExperience).Level);
            p.UpdateNavalQuestTasks(NavalTaskKind.ShipwrightLevel,_settings.Repair.Curve().Progress(p.NavalCareer.RepairExperience).Level);}
        List<(Guid Player,int Hull)> requests=null;Guid ship=Guid.Empty;int hull=0;
        lock(Gate)
        {
            if(Ships.TryGetValue(p.CaptainedShipId,out var v))
            {
                if(v.HelmsmanId!=Guid.Empty && (!v.Crew.TryGetValue(v.HelmsmanId,out var crew) || now-crew.Seen>1500 || !p.Party.Any(member=>member.Id==v.HelmsmanId)))
                {v.HelmsmanId=Guid.Empty;v.Controls.Reset();}
                if(RepairRequests.Remove(v.State.Id,out requests) && v.Health>0 && v.SunkUntil==0)
                {ship=v.State.Id;hull=Math.Min(100-v.Health,requests.Sum(r=>r.Hull)*(100+SkillPower(v,NavalSkillKind.RepairBonus,now))/100);}
            }
            if(Repairs.TryGetValue(p.Id,out var r) && (r.Ship!=p.NavalShipId || !Ships.TryGetValue(r.Ship,out var active) || active.Health is <=0 or >=100 || Driver(active)==p.Id || now-r.Start>_settings.Repair.RoundSeconds*1000))Repairs.Remove(p.Id);
        }
        if(hull>0 && p.NavalFleet.FirstOrDefault(s=>s.Id==ship) is {} owned)
        {
            var before=owned.Health;owned.Health=Math.Min(100,before+hull);var saved=false;
            try {saved=SaveManagement(p);}finally {if(!saved)owned.Health=before;}
            lock(Gate)
            {
                if(saved && Ships.TryGetValue(ship,out var v) && v.Health>0 && v.SunkUntil==0)
                {
                    v.Health=owned.Health;v.State.DamageState=v.Health<100?1:0;
                    foreach(var actor in requests.Select(r=>r.Player).Distinct()) {if(Ships.TryGetValue(v.CombatTargetId,out var target))target.Contributions[actor]=target.Contributions.GetValueOrDefault(actor)+Math.Max(1,hull/requests.Count);QueueNavalTask(actor,NavalTaskKind.Repairs,1);RepairRewards[actor]=Math.Min(200000000000000L,RepairRewards.GetValueOrDefault(actor)+_settings.Repair.ExperiencePerSuccess);}
                }
                else if(!saved){if(RepairRequests.TryGetValue(ship,out var waiting))requests.AddRange(waiting);RepairRequests[ship]=requests;}
            }
        }
        long xp;lock(Gate){xp=RepairRewards.GetValueOrDefault(p.Id);RepairRewards.Remove(p.Id);}
        if(xp>0)
        {
            var before=p.NavalCareer.RepairExperience;p.NavalCareer.RepairExperience=before>=_settings.Repair.Curve().Cap?before:Math.Min(_settings.Repair.Curve().Cap,before+xp);
            if(!SaveManagement(p)){p.NavalCareer.RepairExperience=before;lock(Gate)RepairRewards[p.Id]=RepairRewards.GetValueOrDefault(p.Id)+xp;}
            else EmitLevelUp(p,_settings.Repair.Curve().Progress(before).Level,_settings.Repair.Curve().Progress(p.NavalCareer.RepairExperience).Level,now);
        }
    }
}
