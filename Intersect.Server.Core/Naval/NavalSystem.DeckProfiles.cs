using Intersect.Framework.Core.Naval;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static ShipDeckKind DeckKind(Vessel vessel)=>ShipDeck.Kind(vessel.Definition.Graphics);
    private static int MountedCannons(Vessel vessel,bool starboard)=>Math.Min(_settings.Gunnery.CannonsPerSide,
        NavalShipModels.Find(vessel.Definition.Graphics)?.CannonCount(starboard)??_settings.Gunnery.CannonsPerSide);
    private static bool ValidGunKey(Vessel vessel,int key)=>key>=0 && key<_settings.Gunnery.CannonsPerSide*2 &&
        key%_settings.Gunnery.CannonsPerSide<MountedCannons(vessel,key>=_settings.Gunnery.CannonsPerSide);
    private static double CannonHeading(Vessel vessel,bool starboard)=>ShipDeck.CannonHeading(vessel.State.Heading,starboard,DeckKind(vessel));
    private static DeckPoint CannonBeam(Vessel vessel,bool starboard)
    {
        var angle=CannonHeading(vessel,starboard)*Math.PI/180;return new(Math.Sin(angle),-Math.Cos(angle));
    }
}
