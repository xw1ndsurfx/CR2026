using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;

namespace Intersect.Server.Dungeons;

internal static partial class DungeonRunRuntime
{
    internal static bool TryAbandon(Player player, Guid runId)
    {
        lock (player.EntityLock)
        {
            Run run;
            bool lastParticipant;
            lock (Gate)
            {
                if (runId == Guid.Empty || !RunsByInstance.TryGetValue(player.MapInstanceId, out run!) ||
                    run.Id != runId || run.Finished || !IsDungeonMap(run, player.MapId) ||
                    !run.Participants.TryRemove(player.Id, out _))
                    return false;

                run.ChestLoot.Remove(player.Id);
                run.ChestClaimed.Remove(player.Id);
                run.ChestRetryAt.Remove(player.Id);
                lastParticipant = run.Participants.IsEmpty;
                if (lastParticipant)
                {
                    run.Finished = true;
                    RunsByInstance.Remove(run.MapInstanceId);
                }
            }

            DungeonStatisticsRuntime.RecordFailure(player, run.Dungeon.Id);
            if (run.Dungeon.FailureCommonEventId != Guid.Empty &&
                EventDescriptor.Get(run.Dungeon.FailureCommonEventId) is { } failureEvent)
                player.EnqueueStartCommonEvent(failureEvent);

            player.SendPacket(new DungeonChestPacket { RunId = run.MapInstanceId });
            player.SendPacket(CreatePacket(run, DungeonRunStatus.Abandoned,
                QualityOfLifeText.Get("You abandoned the dungeon.", player.Client?.LanguageCode ?? "en")));
            if (lastParticipant && run.NavalEncounterId != Guid.Empty)
                Naval.NavalSystem.FinishEnemyBoarding(run.NavalEncounterId, false);
            WarpToExit(player, run, reviveIfDead: player.IsDead);
            DungeonConfigurationRuntime.SendState(player, openWindow: false);
            return true;
        }
    }
}
