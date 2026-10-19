using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;

public partial class Player
{
    internal static readonly Guid ExperienceBoostPercentId = new("c1026000-0000-4000-8000-000000000001");
    internal static readonly Guid ExperienceBoostUntilId = new("c1026000-0000-4000-8000-000000000002");
    [NotMapped, JsonIgnore] internal int PersonalExperienceBonus => PersonalBonusAt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    internal int PersonalBonusAt(long now) => (GetVariable(ExperienceBoostUntilId)?.Value.Integer ?? 0) > now ? (int)Math.Clamp(GetVariable(ExperienceBoostPercentId)?.Value.Integer ?? 0, 0, 1000) : 0;
    internal bool ActivateExperienceBoost(int slot, int percent, long seconds)
    {
        if (percent is < 1 or > 1000 || seconds is < 1 or > 604800)
            return false;
        lock (EntityLock)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var active = PersonalBonusAt(now);
            if (active > percent)
                return false;
            var previousEnd = GetVariable(ExperienceBoostUntilId)?.Value.Integer ?? now;
            var end = active == percent ? Math.Max(now, previousEnd) + seconds * 1000L : now + seconds * 1000L;
            // Reject the whole dose if it cannot fit; never consume a potion for a tiny clipped extension.
            if (end > now + 604800000L || end <= previousEnd && active == percent)
                return false;
            if (slot < 0 || slot >= Items.Count || !TryTakeItem(Items[slot], 1))
                return false;
            SetVariableValue(ExperienceBoostPercentId, (long)percent);
            SetVariableValue(ExperienceBoostUntilId, end);
            SendExperienceBoostStatus();
            return true;
        }
    }
    internal void SendExperienceBoostStatus()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SendPacket(new ExperienceBoostStatusPacket { BonusPercent = PersonalBonusAt(now), RemainingMs = Math.Max(0, (GetVariable(ExperienceBoostUntilId)?.Value.Integer ?? 0) - now) });
    }
    internal int ActivityExperienceBonus => PersonalExperienceBonus + Intersect.Server.WorldEvents.WorldActivitiesRuntime.Current.BonusAt(DateTimeOffset.Now);
}
