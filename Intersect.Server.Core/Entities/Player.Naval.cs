using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Enums;
using Intersect.Server.Maps;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    [NotMapped, JsonIgnore] public Guid CaptainedShipId { get; internal set; }
    [NotMapped, JsonIgnore] internal long NavalSnapshotTime { get; set; }
    [NotMapped, JsonIgnore] internal bool NavalWarpInProgress { get; set; }
    [NotMapped, JsonIgnore] internal bool NavalBusy => IsBusy() || MoveRoute != null ||
        EventLookup.Values.Any(e => e.HoldingPlayer) ||
        CachedStatuses.Any(s => s.Type is SpellEffect.Stun or SpellEffect.Snare or SpellEffect.Sleep);
    internal bool NavalEventBlocked(MapController map, int x, int y) =>
        TryGetBlockerOnTile(map, x, y, Z, out _, out _, out _);
}
