using Intersect.Framework.Core.Exportation;
using Intersect.Server.Exportation;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    // Only the native naval server calls these callbacks. A client cannot
    // submit a forged embarkation or disembarkation packet to satisfy delivery.
    internal void ExportationBoarded(Guid shipId)
    {
        lock (EntityLock)
        {
            var original = ReadExportation();
            if (original is not { Stage: DeliveryStage.InTransit } ||
                ExportationRuntime.Find(original.RouteId) is not { } route) return;
            // Never accept a vessel that is not in this player's owned fleet,
            // even though this callback is emitted by the native naval server.
            if (!NavalFleet.Any(ship => ship.Id == shipId && ship.Health > 0 && !ship.Sunk)) return;
            var draft = JsonConvert.DeserializeObject<ExportationRun>(
                JsonConvert.SerializeObject(original))!;
            if (ExportationRules.TryEmbark(route, draft,
                new ExportationActor(MapId, route.SourceNpcId, shipId, true, true, true)))
                SaveExportation(original, draft);
        }
    }

    internal void ExportationDocked(Guid shipId)
    {
        lock (EntityLock)
        {
            var original = ReadExportation();
            if (original is not { Stage: DeliveryStage.InTransit } ||
                ExportationRuntime.Find(original.RouteId) is not { } route) return;
            var draft = JsonConvert.DeserializeObject<ExportationRun>(
                JsonConvert.SerializeObject(original))!;
            if (ExportationRules.TryDock(route, draft,
                new ExportationActor(MapId, route.RecipientNpcId, shipId, true, true, false)))
                SaveExportation(original, draft);
        }
    }

    internal void ExportationDied()
    {
        lock (EntityLock)
        {
            _exportSession = null;
            var original = ReadExportation();
            if (original is not { Stage: DeliveryStage.InTransit }) return;
            var draft = JsonConvert.DeserializeObject<ExportationRun>(
                JsonConvert.SerializeObject(original))!;
            if (ExportationRules.TryLose(draft, CargoFailure.Death))
                SaveExportation(original, draft);
        }
    }

    // Runs after the naval update, outside the global naval lock. Ship damage
    // is authoritative and already persisted, including across a crash/relogin.
    internal void UpdateExportationSinking()
    {
        var original = ReadExportation();
        if (original is not { Stage: DeliveryStage.InTransit, ArrivedByShip: false } ||
            !NavalFleet.Any(ship => ship.Id == original.ShipId && ship.Sunk)) return;
        lock (EntityLock)
        {
            original = ReadExportation();
            if (original is not { Stage: DeliveryStage.InTransit, ArrivedByShip: false } ||
                !NavalFleet.Any(ship => ship.Id == original.ShipId && ship.Sunk)) return;
            var draft = JsonConvert.DeserializeObject<ExportationRun>(
                JsonConvert.SerializeObject(original))!;
            if (ExportationRules.TryLose(draft, CargoFailure.ShipSunk, original.ShipId) &&
                SaveExportation(original, draft))
                ExportationNotice("Cargo lost — return to the merchant");
        }
    }
}
