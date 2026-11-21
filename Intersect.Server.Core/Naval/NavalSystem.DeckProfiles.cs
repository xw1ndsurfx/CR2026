using Intersect.Framework.Core.Naval;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static ShipDeckKind DeckKind(Vessel vessel)=>ShipDeck.Kind(vessel.Definition.Graphics);
    private static int GunStride(Vessel vessel)=>DeckKind(vessel)==ShipDeckKind.ShipOfLine?6:_settings.Gunnery.CannonsPerSide;
    private static int MountedCannons(Vessel vessel,bool starboard)=>Math.Min(GunStride(vessel),
        NavalShipModels.Find(vessel.Definition.Graphics)?.CannonCount(starboard)??GunStride(vessel));
    private static bool ValidGunKey(Vessel vessel,int key)=>key>=0 && key<GunStride(vessel)*2 &&
        key%GunStride(vessel)<MountedCannons(vessel,key>=GunStride(vessel));
    private static double StationOffset(Vessel vessel,int index,bool starboard)=>DeckKind(vessel)==ShipDeckKind.Raft?0:
        GunneryAim.StationOffset(DeckKind(vessel)==ShipDeckKind.ShipOfLine?index%3:index,
            DeckKind(vessel)==ShipDeckKind.ShipOfLine?3:MountedCannons(vessel,starboard),vessel.Definition.Length);
    private static double CannonHeading(Vessel vessel,bool starboard)=>ShipDeck.CannonHeading(vessel.State.Heading,starboard,DeckKind(vessel));
    private static DeckPoint CannonBeam(Vessel vessel,bool starboard)
    {
        var angle=CannonHeading(vessel,starboard)*Math.PI/180;return new(Math.Sin(angle),-Math.Cos(angle));
    }
}
