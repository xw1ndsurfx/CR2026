namespace Intersect.Server.Entities;

public partial class Player
{
    internal void RequestCinematicVisualSkip(Guid eventId,long version)
    {
        lock (mEventLock)
            foreach (var entry in EventLookup.Values.Where(e=>e.Descriptor.Id == eventId && e.CinematicActive).ToArray())
                entry.RequestCinematicVisualSkip(version);
    }
}
