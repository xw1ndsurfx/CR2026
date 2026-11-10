using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static bool TryStartDeckRepair(Vessel ship,Player player,long now,bool reportMissing=false)
    {
        EnsureDeck(ship,now);
        var zone=ship.Deck.TryGetValue(player.Id,out var walker)?ShipDamageZone.Active(ship.Definition,ship.Health,ship.MaximumHealth)
            .Where(z=>z.Repairable && ShipDeck.Distance(walker.Position,new(z.X,z.Y))<=ShipDeck.UseDistance)
            .OrderBy(z=>ShipDeck.Distance(walker.Position,new(z.X,z.Y))).FirstOrDefault():null;
        if(zone==null)
        {if(reportMissing)SendManagement(player,NavalManageAction.Repair,now,"Move closer to a damaged area on the deck.");return false;}
        if(!_settings.Repair.Enabled || ship.SunkUntil>0 || ship.EnemyBoardingEncounter!=Guid.Empty || player.NavalBusy || player.IsDead || player.HasPendingDeath || Driver(ship)==player.Id || RepairCooldown.GetValueOrDefault(player.Id)>now ||
            Repairs.Values.Any(r=>r.Ship==ship.State.Id && r.ZoneId==zone.Id))
        {SendManagement(player,NavalManageAction.Repair,now,"Repair unavailable. Check hull, role and cooldown.");return true;}
        foreach(var gun in ship.Guns.Values.Where(g=>g.Owner==player.Id))gun.Owner=gun.Lease=Guid.Empty;
        Repairs[player.Id]=new(){Ship=ship.State.Id,ZoneId=zone.Id,Kind=zone.RepairKind,Start=now,Name=player.Name,
            Progress=_settings.Repair.Curve().Progress(player.NavalCareer.RepairExperience),Cells=NavalRepairPuzzle.Create(zone.RepairKind,_settings.Repair.RequiredHits)};
        player.RegisterAfkActivity();SendManagement(player,NavalManageAction.Repair,now);return true;
    }
}
