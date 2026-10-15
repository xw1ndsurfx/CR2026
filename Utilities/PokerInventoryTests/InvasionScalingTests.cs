using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Intersect;
using Intersect.Enums;
using Intersect.Server.Entities;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.Invasions;

internal static class InvasionScalingTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(Npc).Assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionRuntime", true)!;
        var online = (ConcurrentDictionary<Guid, Player>)typeof(Player).GetField("OnlinePlayersById", flags)!.GetValue(null)!;
        var players = new List<Player>(); var oldOptions = Options.Instance;
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            foreach (var (level, instance) in new[] { (5, Guid.Empty), (5, Guid.Empty), (50, Guid.Empty), (1000, Guid.NewGuid()) })
            {
                var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(player);
                player.Id = Guid.NewGuid(); player.Level = level; player.MapInstanceId = instance;
                players.Add(player); online[player.Id] = player;
            }
            var definition = new InvasionDefinition();
            var snapshot = type.GetMethod("CaptureScalingSnapshot", flags)!.Invoke(null, [definition])!;
            int Field(string name) => (int)snapshot.GetType().GetProperty(name)!.GetValue(snapshot)!;
            Check(Field("PlayerCount") == 3 && Field("TargetLevel") == 20,
                "Actual invasion snapshot excludes dungeon instances and uses overworld mean level");
            var descriptor = new NPCDescriptor(Guid.NewGuid()) { Level = 1, Damage = 10000 };
            descriptor.MaxVitals[(int)Vital.Health] = 1000000;
            for (var i = 0; i < descriptor.Stats.Length; i++) descriptor.Stats[i] = 10000;
            var npc = new Npc(descriptor); GC.SuppressFinalize(npc);
            type.GetMethod("ApplyAdaptiveScaling", flags)!.Invoke(null, [npc, definition, false, snapshot]);
            Check(npc.Level == 20 && npc.GetMaxVital(Vital.Health) < 600 && npc.BaseStats[(int)Stat.Attack] <= 31,
                "Overpowered source NPCs are limited to the weak horde preset");
            Check(npc.InvasionBaseDamageMultiplier * descriptor.Damage <= InvasionBalance.DamageLimit(20) &&
                npc.InvasionDamageMultiplier < 0.5, "Normal invaders have low base and final damage");
            Check(descriptor.Level == 1 && descriptor.Damage == 10000 && descriptor.MaxVitals[(int)Vital.Health] == 1000000,
                "Scaling never mutates the NPC shared by ordinary spawns");
            System.Console.WriteLine("PASS actual invasion snapshot and weak instance scaling");
        }
        finally
        {
            foreach (var player in players) online.TryRemove(player.Id, out _);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, oldOptions);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
