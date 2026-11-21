namespace Intersect.Framework.Core.Naval;

/// <summary>Built-in stations follow the same faction and hull as the ship sprites.</summary>
public static class CannonStationCatalog
{
    public const string LegacyGraphic = "Ship_Crew_Cannon.png";
    public static string? ForShip(string? graphics)
    {
        var model = NavalShipModels.Find(graphics);
        return model == null || model.Activity == ShipDeckActivity.Fishing ? null
            : "Ship_Crew_" + model.Stem["Ship_".Length..] + "_Wide_v1.png";
    }

    public static string Resolve(string? graphics, string configured, bool matchShip) =>
        matchShip && string.Equals(configured, LegacyGraphic, StringComparison.OrdinalIgnoreCase)
            ? ForShip(graphics) ?? configured : configured;
}
