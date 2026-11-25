using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Matchmaking;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal static NavalExpeditionDefinition[] MatchmakingDefinitions(bool includeDisabled = false)
    {
        lock (Gate)
        {
            if (!IsEnabled() && !includeDisabled) return [];
            return JsonConvert.DeserializeObject<NavalExpeditionDefinition[]>(JsonConvert.SerializeObject(_settings.Expeditions))!;
        }
    }

    internal static MatchMember? MatchmakingMember(Player player, Guid definitionId, IReadOnlySet<Guid> cohort)
    {
        lock (Gate)
        {
            if (!IsEnabled() || !LoadExpeditions(ExpeditionNow) || ExpeditionFor(player.Id) != null ||
                player.NavalBusy || player.NavalPartyExitRequested ||
                _settings.Expeditions.FirstOrDefault(e => e.Enabled && e.Id == definitionId) is not {} definition ||
                _settings.Sailing.Progress(player.SailingExperience).Level < definition.RequiredSailingLevel) return null;
            if (player.NavalShipId == Guid.Empty)
                return new(player.Id, player.Name, player.Level, player.MapId);
            if (!Ships.TryGetValue(player.NavalShipId, out var ship) || !NearCrewDock(ship) ||
                Math.Abs(ship.State.Speed) >= .1 || ship.EnemyBoardingEncounter != Guid.Empty || ship.Health <= 0 || ship.SunkUntil > 0 ||
                player.MapId != ship.State.MapId || !cohort.Contains(ship.State.CaptainId) ||
                ship.Crew.Keys.Any(id => !cohort.Contains(id)) ||
                player.Id != ship.State.CaptainId && !ship.Crew.ContainsKey(player.Id)) return null;
            return new(player.Id, player.Name, player.Level, player.MapId, ship.State.Id, ship.State.CaptainId, VesselCapacity(ship), ship.Name);
        }
    }

    // Called only with the recorded, unanimous ready-check membership, never from a packet.
    internal static bool StartMatchmaking(Player leader, Guid definitionId, IReadOnlySet<Guid> consent)
    {
        lock (Gate)
        {
            if (!IsEnabled() || !LoadExpeditions(ExpeditionNow)) return false;
            return ProposeExpedition(leader, definitionId, ExpeditionNow, consent) == "";
        }
    }

    internal static void RollbackMatchmakingBoarding(Player player)
    { ReleaseCrew(player, true); }
}
