using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities.Events;
using Intersect.Server.Exportation;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    /// <summary>
    /// Invoked by an actual event character. Merchant and recipient are
    /// EventDescriptor IDs, not client-provided NPC or map identifiers.
    /// </summary>
    internal void OpenExportationFromEvent(Guid routeId, Event source)
    {
        lock (EntityLock)
        {
            if (source?.Descriptor is null || source.CallStack.Count == 0 ||
                Client is not { IsEditor: false } || !InGame || IsDead || HasPendingDeath ||
                NavalShipId != Guid.Empty || InstanceType != MapInstanceType.Overworld ||
                source.MapInstanceId != MapInstanceId ||
                (X - source.X) * (X - source.X) + (Y - source.Y) * (Y - source.Y) > 16)
                return;
            var route = ExportationRuntime.Find(routeId);
            var config = ExportationRuntime.Current;
            if (route is null || config.ProfessionId == Guid.Empty ||
                ProfessionConfigurationRuntime.Current.Find(config.ProfessionId) is null ||
                QuestDescriptor.Get(route.QuestId) is not { Tasks.Count: 1 } quest)
                return;
            var sourceNpc = route.SourceNpcId == source.Descriptor.Id && MapId == route.SourceMapId;
            var recipientNpc = route.RecipientNpcId == source.Descriptor.Id && MapId == route.RecipientMapId;
            if (!sourceNpc && !recipientNpc) return;

            var old = ReadExportation();
            var run = old;
            if (run is { Stage: DeliveryStage.Delivered or DeliveryStage.Abandoned }) run = null;
            if (run is not null && run.RouteId != route.Id)
            {
                ExportationNotice("Finish your current Exportation delivery first.");
                return;
            }
            if (run is null)
            {
                if (!sourceNpc || !CanStartQuest(quest) ||
                    !ExportationRules.TryStart(route, ExportationActor(route, false),
                        ProfessionRuntime.GetLevel(this, config.ProfessionId), out run))
                {
                    ExportationNotice("A usable ship and the required Exportation level are needed.");
                    return;
                }
                if (!SaveExportation(old, run!))
                {
                    ExportationNotice("Exportation could not be saved.");
                    return;
                }
            }
            if (run is null) return;
            if (run.Stage == DeliveryStage.Lost && sourceNpc)
            {
                old = ReadExportation();
                run = JsonConvert.DeserializeObject<ExportationRun>(JsonConvert.SerializeObject(old))!;
                if (!ExportationRules.TryRestart(route, run, ExportationActor(route, false)) ||
                    !SaveExportation(old, run))
                {
                    ExportationNotice("Repair or obtain a ship to prepare the cargo again.");
                    return;
                }
            }
            if (recipientNpc && run.Stage != DeliveryStage.InTransit) return;
            if (sourceNpc && run.Stage == DeliveryStage.InTransit)
            {
                ExportationNotice("Deliver your sealed cargo to the recipient.");
                return;
            }
            _exportSession = new ExportSession(run.Id, route.Id, source.Descriptor.Id,
                MapId, MapInstanceId, X, Y, recipientNpc);
            _exportSequence = _exportLastRequest = 0;
            ExportationState(route, run, open: true);
        }
    }

    internal void HandleExportationRequest(ExportationRequestPacket request)
    {
        lock (EntityLock)
        {
            var session = _exportSession;
            if (session is null || request.RunId != session.Run ||
                request.Sequence <= _exportSequence || request.Sequence <= 0 ||
                !Enum.IsDefined(request.Action)) return;
            _exportSequence = request.Sequence;
            if (request.Action == ExportationAction.Leave) { _exportSession = null; return; }
            var now = Environment.TickCount64;
            if (now - _exportLastRequest < 60) return;
            _exportLastRequest = now;
            var route = ExportationRuntime.Find(session.Route);
            var old = ReadExportation();
            if (route is null || old is null || !ExportationSessionValid(route, old))
            {
                _exportSession = null;
                SendPacket(new ExportationStatePacket { Close = true });
                return;
            }
            if (request.Action == ExportationAction.Refresh)
            { ExportationState(route, old); return; }

            var run = JsonConvert.DeserializeObject<ExportationRun>(JsonConvert.SerializeObject(old))!;
            var actor = ExportationActor(route, session.Recipient);
            var changed = false;
            switch (request.Action)
            {
                case ExportationAction.Pack when !session.Recipient && request.QuantityChange is 1 or -1:
                    changed = ExportationRules.TryPack(route, run, actor, request.ItemId,
                        request.Group, request.QuantityChange);
                    break;
                case ExportationAction.Restart when !session.Recipient:
                    changed = ExportationRules.TryRestart(route, run, actor);
                    break;
                case ExportationAction.Seal when !session.Recipient:
                    ExportationSeal(route, old, run, actor);
                    return;
                case ExportationAction.Deliver when session.Recipient:
                    ExportationDeliver(route, old, run, actor);
                    return;
            }
            if (changed && !SaveExportation(old, run))
                ExportationNotice("Exportation could not be saved.");
            ExportationState(route, ReadExportation() ?? old);
        }
    }

    private void ExportationSeal(ExportationRoute route, ExportationRun old,
        ExportationRun run, ExportationActor actor)
    {
        var quest = QuestDescriptor.Get(route.QuestId);
        if (quest is not { Tasks.Count: 1 } || !CanStartQuest(quest) ||
            !ExportationRules.TrySeal(route, run, actor)) return;

        var existing = FindQuest(quest.Id);
        var previous = existing is null ? (Guid.Empty, 0, false) :
            (existing.TaskId, existing.TaskProgress, existing.Completed);
        StartQuest(quest);
        if (!SaveExportation(old, run))
        {
            if (existing is null) Quests.RemoveAll(q => q.QuestId == quest.Id);
            else
            {
                existing.TaskId = previous.Item1;
                existing.TaskProgress = previous.Item2;
                existing.Completed = previous.Item3;
            }
            PacketSender.SendQuestsProgress(this);
            ExportationNotice("Exportation could not be saved.");
        }
        ExportationState(route, ReadExportation() ?? old);
    }
}
