namespace Intersect.Framework.Core.Naval;

public enum HarbourView : byte { Fleet, Shop }

public enum HarbourAction : byte { Open, Buy, Launch, Repair }

public sealed class OwnedShip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ModelId { get; set; } = "";
    public int Health { get; set; } = 100;
    public bool AtSea { get; set; }
    public Guid ReturnMapId { get; set; }
    public int ReturnDockX { get; set; }
    public int ReturnDockY { get; set; }
    public bool Sunk => Health == 0;
}

public static class ShipFleetRules
{
    public const int MaximumShips = 32;
    public static bool CanBuy(IEnumerable<OwnedShip> fleet, string model, bool requiresPremium, bool premium) =>
        (!requiresPremium || premium) && fleet.Count() < MaximumShips && !fleet.Any(s => s.ModelId == model);
    public static int AfterDamage(int health, int damage) => Math.Max(0, Math.Clamp(health, 0, 100) - Math.Max(0, damage));
}

public static class HarbourAccessRules
{
    public static bool Allows(HarbourView view, HarbourAction action) => view switch
    {
        HarbourView.Fleet => action is HarbourAction.Open or HarbourAction.Launch,
        HarbourView.Shop => action is HarbourAction.Open or HarbourAction.Buy or HarbourAction.Repair,
        _ => false,
    };
    public static bool ShopSessionValid(Guid map, double x, double y, Guid sessionMap, double anchorX,
        double anchorY, long now, long expires, double range) => map != Guid.Empty && map == sessionMap &&
        expires > now && double.IsFinite(range) && Math.Pow(x - anchorX, 2) + Math.Pow(y - anchorY, 2) <= range * range;
}
