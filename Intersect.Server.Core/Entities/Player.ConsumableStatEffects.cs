using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    private sealed class TimedConsumableModifier
    {
        public long ExpiresAt { get; init; }
        public int[] FlatStats { get; init; } = [];
        public int[] PercentageStats { get; init; } = [];
    }

    // Runtime-only modifiers. The existing item cooldown remains the reuse gate.
    private readonly ConcurrentDictionary<Guid, TimedConsumableModifier> _activeConsumableModifiers = new();

    private void ActivateConsumableStatEffects(ItemDescriptor item)
    {
        if (item.StatsGiven == null || item.PercentageStatsGiven == null ||
            !item.StatsGiven.Any(value => value != 0) &&
            !item.PercentageStatsGiven.Any(value => value != 0))
        {
            return;
        }

        // The item's normal cooldown is also the effect duration. A zero-cooldown
        // potion gets a short default; Grog uses its configured intoxication duration.
        var durationMs = item.Cooldown > 0
            ? Math.Clamp(item.Cooldown, 1000, 1800000)
            : item.Consumable.Type == ConsumableType.Grog
                ? (int)Math.Clamp(item.Consumable.Value == 0 ? 30 : item.Consumable.Value, 5, 120) * 1000
                : 30000;

        var flatStats = new int[Enum.GetValues<Stat>().Length];
        var percentageStats = new int[flatStats.Length];
        for (var i = 0; i < flatStats.Length; i++)
        {
            flatStats[i] = i < item.StatsGiven.Length ? Math.Clamp(item.StatsGiven[i], -1000, 1000) : 0;
            percentageStats[i] = i < item.PercentageStatsGiven.Length
                ? Math.Clamp(item.PercentageStatsGiven[i], -80, 200) : 0;
        }

        // Refresh rather than stack repeated doses of the same item.
        // Cap distinct active types so an inventory of potions cannot grow the set unbounded.
        if (!_activeConsumableModifiers.ContainsKey(item.Id) && _activeConsumableModifiers.Count >= 16)
        {
            var earliest = _activeConsumableModifiers.OrderBy(entry => entry.Value.ExpiresAt).FirstOrDefault();
            if (earliest.Key != Guid.Empty)
            {
                _activeConsumableModifiers.TryRemove(earliest.Key, out _);
            }
        }

        _activeConsumableModifiers[item.Id] = new TimedConsumableModifier
        {
            ExpiresAt = Environment.TickCount64 + durationMs,
            FlatStats = flatStats,
            PercentageStats = percentageStats,
        };

        PacketSender.SendEntityStats(this);
    }

    private Tuple<int, int> GetActiveConsumableStatBuffs(Stat stat)
    {
        var index = (int)stat;
        var now = Environment.TickCount64;
        var flat = 0;
        var percent = 0;
        foreach (var modifier in _activeConsumableModifiers.Values)
        {
            if (modifier.ExpiresAt <= now)
            {
                continue;
            }

            flat += modifier.FlatStats[index];
            percent += modifier.PercentageStats[index];
        }

        return new Tuple<int, int>(Math.Clamp(flat, -2000, 2000), Math.Clamp(percent, -80, 300));
    }

    private void UpdateConsumableStatEffects()
    {
        var now = Environment.TickCount64;
        var changed = false;
        foreach (var pair in _activeConsumableModifiers)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                changed |= _activeConsumableModifiers.TryRemove(pair.Key, out _);
            }
        }

        if (changed)
        {
            PacketSender.SendEntityStats(this);
        }
    }
}
