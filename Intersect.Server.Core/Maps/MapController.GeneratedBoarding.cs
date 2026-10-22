using Intersect.Server.Entities;
namespace Intersect.Server.Maps;
public partial class MapController
{
    internal bool DisposeGeneratedBoardingDeck()
    {
        if(!GeneratedBoardingDeck || GetEntitiesOnAllInstances().OfType<Player>().Any())return false;
        DespawnAllInstances();
        foreach(var id in mInstances.Keys.ToArray())if(TryGetInstance(id,out _))DisposeInstanceWithId(id);
        return true;
    }
}
