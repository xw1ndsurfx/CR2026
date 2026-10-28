using Intersect.Framework.Core.Naval;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Maps;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal sealed record DockLanding(Guid MapId, int X, int Y, int DockX, int DockY);

    // Called under Gate. The current vessel map owns the destination, never its
    // launch definition or the saved emergency return point from an earlier port.
    private static DockLanding FindDockLanding(Player player, Vessel vessel)
    {
        var state = vessel.State;
        if (vessel.SunkUntil > 0 || vessel.Health <= 0 || !double.IsFinite(state.Speed) || Math.Abs(state.Speed) > .1) return null;
        var attributes = MapController.Get(state.MapId)?.Attributes;
        var ocean = OceanMapResolver.Maps();
        bool Clear(int x, int y) => ClearTile(player, state.MapId, x, y) &&
            !Ships.Values.Any(other => HullTiles(other, ocean, other.State.MapId,
                other.State.X, other.State.Y).Contains((state.MapId, x, y)));
        var marked = ShipDock.Tiles(attributes).Any();
        var exit = marked ? ShipDock.FindLanding(attributes, state.X, state.Y, 5, Clear)
            : ShipDisembarkation.Find(state, vessel.Definition, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight, Clear);
        if (exit is not { } landing) return null;
        var quay = ShipDock.Nearest(attributes, landing.X + .5, landing.Y + .5, 1.01)
            ?? (vessel.Definition.DockX, vessel.Definition.DockY);
        return new(state.MapId, landing.X, landing.Y, quay.Item1, quay.Item2);
    }

    internal static bool CommitDockingReturn(OwnedShip ship, DockLanding landing, Func<bool> save)
    {
        var before = (ship.ReturnMapId, ship.ReturnX, ship.ReturnY, ship.ReturnDockX, ship.ReturnDockY);
        var committed = false;
        try
        {
            ship.ReturnMapId = landing.MapId; ship.ReturnX = landing.X; ship.ReturnY = landing.Y;
            ship.ReturnDockX = landing.DockX; ship.ReturnDockY = landing.DockY;
            // Keep AtSea until native release/warp completes. If the process
            // stops here, login recovery safely uses this newly persisted quay.
            committed = save();
            return committed;
        }
        finally
        {
            if (!committed) (ship.ReturnMapId, ship.ReturnX, ship.ReturnY, ship.ReturnDockX, ship.ReturnDockY) = before;
        }
    }

    private static bool SaveDockingReturn(Player player, OwnedShip ship, DockLanding landing) =>
        CommitDockingReturn(ship, landing, () => player.User?.Save(force: true) == UserSaveResult.Completed);
}
