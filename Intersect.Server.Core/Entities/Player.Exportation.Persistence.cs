using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Exportation;
using Intersect.Server.Professions;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace Intersect.Server.Entities;

/// <summary>
/// Durable cargo is stored as a namespaced extension of NavalFleetJson.
/// The existing native account transaction persists it with quests, items
/// and profession variables, so no EF migration is needed.
/// </summary>
public partial class Player
{
    private const string ExportKey = "Logiklik.Exportation.V1";
    private sealed record ExportSession(Guid Run, Guid Route, Guid Event, Guid Map,
        Guid Instance, float X, float Y, bool Recipient);
    private ExportSession? _exportSession;
    private long _exportSequence, _exportLastRequest;

    private ExportationRun? ReadExportation()
    {
        if (_navalExtra?.TryGetValue(ExportKey, out var token) != true) return null;
        try
        {
            var run = token.ToObject<ExportationRun>();
            return run is not null && run.Id != Guid.Empty && run.RouteId != Guid.Empty ? run : null;
        }
        catch (Newtonsoft.Json.JsonException) { return null; }
        catch (ArgumentException) { return null; }
    }

    private void WriteExportation(ExportationRun? run)
    {
        _navalExtra ??= new Dictionary<string, JToken>(StringComparer.Ordinal);
        if (run is null) _navalExtra.Remove(ExportKey);
        else _navalExtra[ExportKey] = JToken.FromObject(run);
    }

    private bool SaveExportation(ExportationRun? oldRun, ExportationRun newRun)
    {
        WriteExportation(newRun);
        try
        {
            if (User?.Save(force: true) == UserSaveResult.Completed) return true;
        }
        catch (Exception ex)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(
                ex, "Could not persist Exportation run for {CharacterId}", Id);
        }
        WriteExportation(oldRun);
        return false;
    }

    // A merchant checks fleet ownership and seaworthiness, NOT dock location.
    // The real vessel is selected only by the authenticated naval embarkation.
    private OwnedShip? ExportationBoat() => NavalFleet.FirstOrDefault(ship =>
        ship.Health > 0 && !ship.Sunk);

    private ExportationActor ExportationActor(ExportationRoute route, bool recipient)
    {
        var ship = ExportationBoat();
        return new(MapId, recipient ? route.RecipientNpcId : route.SourceNpcId,
            ship?.Id ?? Guid.Empty, InGame && !IsDead && !HasPendingDeath,
            ship != null, NavalShipId != Guid.Empty);
    }

    private bool ExportationSessionValid(ExportationRoute route, ExportationRun run)
    {
        var session = _exportSession;
        if (session is null || session.Run != run.Id || session.Route != route.Id ||
            Client is not { IsEditor: false } || !InGame || IsDead || HasPendingDeath ||
            MapId != session.Map || MapInstanceId != session.Instance ||
            (X - session.X) * (X - session.X) + (Y - session.Y) * (Y - session.Y) > 16)
            return false;
        return session.Recipient
            ? session.Map == route.RecipientMapId && session.Event == route.RecipientNpcId
            : session.Map == route.SourceMapId && session.Event == route.SourceNpcId;
    }

    private void ExportationNotice(string source) => SendPacket(new ExportationStatePacket
    {
        Message = ExportationText.Get(source, Client?.LanguageCode ?? "en"),
    });

    private void ExportationState(ExportationRoute route, ExportationRun run, bool open = false)
    {
        var profession = ProfessionConfigurationRuntime.Current.Find(ExportationRuntime.Current.ProfessionId);
        var professionExperience = profession is null ? 0L :
            ProfessionRuntime.GetExperience(this, profession.Id);
        var professionLevel = profession is null ? 0 : profession.LevelForExperience(professionExperience);
        var maxLevel = profession is not null && professionLevel >= profession.MaximumLevel;
        var needed = profession is null || maxLevel ? 0L : Math.Max(1L, profession.ExperienceForNextLevel(professionLevel));
        var into = profession is null || maxLevel ? 0L :
            Math.Clamp(professionExperience - profession.ExperienceToReachLevel(professionLevel), 0L, needed);
        SendPacket(new ExportationStatePacket
        {
            Open = open,
            RouteJson = System.Text.Json.JsonSerializer.Serialize(route),
            RunJson = System.Text.Json.JsonSerializer.Serialize(run),
            MerchantName = EventDescriptor.Get(route.SourceNpcId)?.Name ?? route.Name,
            RecipientName = EventDescriptor.Get(route.RecipientNpcId)?.Name ?? route.Name,
            SourceMapName = MapDescriptor.Get(route.SourceMapId)?.Name ?? "",
            DestinationMapName = MapDescriptor.Get(route.RecipientMapId)?.Name ?? "",
            HasUsableBoat = ExportationBoat() is not null,
            ProfessionLevel = professionLevel,
            ProfessionExperienceInLevel = into,
            ProfessionExperienceRequired = needed,
            ProfessionMaximumLevel = maxLevel,
        });
    }
}
