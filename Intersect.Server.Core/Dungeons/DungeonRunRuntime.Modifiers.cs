using Intersect.Enums;
using Intersect.Framework.Core.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;

namespace Intersect.Server.Dungeons;
internal static partial class DungeonRunRuntime
{
    private static IEnumerable<Npc> ModifierNpcs(Run run) => GetDungeonMapIds(run.Dungeon, run.EntryMapId)
        .SelectMany(id => MapController.TryGetInstanceFromMap(id, run.MapInstanceId, out var map) ? map.GetEntities().OfType<Npc>() : []);

    internal static void ConfigureDungeonNpc(Npc npc)
    {
        if (npc.MapInstanceId == Guid.Empty || npc.PetOwner != null || npc.IsDead ||
            !RunsByInstance.TryGetValue(npc.MapInstanceId, out var run) || run.Finished || run.CompletionStarted ||
            !IsDungeonMap(run, npc.MapId) || run.Modifiers.Names.Length == 0 || !GetParticipants(run).Any(npc.CanPlayerAttack)) return;
        lock (npc.EntityLock)
        {
            if (npc.DungeonModifierRunId == run.Id) return;
            ResetNpcModifiers(npc);
            npc.DungeonBaseHealth = npc.GetMaxVital(Vital.Health);
            npc.DungeonModifierRunId = run.Id;
            npc.DungeonDamagePercent = run.Modifiers.DamagePercent;
            var health = DungeonModifierSnapshot.Scale(npc.DungeonBaseHealth, run.Modifiers.HealthPercent);
            var current = DungeonModifierSnapshot.Scale(npc.GetVital(Vital.Health), run.Modifiers.HealthPercent);
            npc.SetMaxVital(Vital.Health, health); npc.SetVital(Vital.Health, Math.Min(health, current));
        }
    }
    private static void ClearRunModifiers(Run run)
    {
        foreach (var npc in ModifierNpcs(run))
            lock (npc.EntityLock)
                if (npc.DungeonModifierRunId == run.Id) ResetNpcModifiers(npc);
    }
    private static void ResetNpcModifiers(Npc npc)
    {
        if (npc.DungeonModifierRunId == Guid.Empty) return;
        var health = npc.GetMaxVital(Vital.Health);
        var current = health > 0 ? (long)Math.Min(npc.DungeonBaseHealth, (decimal)npc.GetVital(Vital.Health) * npc.DungeonBaseHealth / health) : 0;
        npc.SetMaxVital(Vital.Health, npc.DungeonBaseHealth); npc.SetVital(Vital.Health, current);
        npc.DungeonModifierRunId = Guid.Empty; npc.DungeonDamagePercent = 100; npc.DungeonBaseHealth = 0;
    }
    internal static long ScaleNpcDungeonDamage(Entity attacker, long damage) => attacker is Npc npc && npc.DungeonModifierRunId != Guid.Empty
        ? DungeonModifierSnapshot.Scale(damage, npc.DungeonDamagePercent) : damage;
}
