using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.GameObjects.Events;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    [NotMapped, JsonIgnore] private long _nextMenuEvent;
    internal bool TryRunMenuEvent(EventDescriptor descriptor)
    {
        lock (EntityLock)
        {
            if (!InGame || IsDead || IsBusy() || descriptor is not { CommonEvent: true } ||
                _nextMenuEvent > Environment.TickCount64 || EventLookup.Values.Any(e => e.HoldingPlayer)) return false;
            _nextMenuEvent = Environment.TickCount64 + 1500;
            EnqueueStartCommonEvent(descriptor); return true;
        }
    }
}
