using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Server.Entities;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.Invasions;

internal static class InvasionDefenseTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Verify()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var runtime = typeof(Npc).Assembly.GetType("Intersect.Server.WorldEvents.Invasions.InvasionRuntime", true)!;
        var sessionType = runtime.GetNestedType("Session", BindingFlags.NonPublic)!;
        var definition = new InvasionDefinition { TargetMapId = Guid.NewGuid(), TargetX = 10, TargetY = 10, TargetHealth = 100 };
        Check(definition.EnvironmentLeadSeconds == 0, "Default ambience begins at the first warning");
        var create = runtime.GetMethod("CreateDefenseTargets", flags)!;
        var targets = (IList)create.Invoke(null, [definition])!;
        Check(targets.Count == 1, "Legacy invasions retain exactly their original objective");
        definition.AdditionalTargets = [new() { Name = "Gate", MapId = definition.TargetMapId, X = 20, Y = 10, Health = 200 }];
        targets = (IList)create.Invoke(null, [definition])!;
        var session = RuntimeHelpers.GetUninitializedObject(sessionType);
        sessionType.GetProperty("Definition")!.SetValue(session, definition);
        sessionType.GetProperty("Objectives")!.SetValue(session, targets);
        var damage = runtime.GetMethod("DamageDefenseTarget", flags)!;
        var assign = runtime.GetMethod("AssignDefenseTarget", flags)!;
        var oldOptions = Options.Instance;
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            var npc = new Npc(new NPCDescriptor(Guid.NewGuid())); GC.SuppressFinalize(npc);
            npc.MapId = definition.TargetMapId; npc.X = 19; npc.Y = 10;
            Check(ReferenceEquals(assign.Invoke(null, [npc, session]), targets[1]), "Invaders select a nearby living objective");
            Check(!(bool)damage.Invoke(null, [session, targets[1], 250])!, "Losing one target does not end all-target defense");
            Check((int)sessionType.GetProperty("ObjectiveHealth")!.GetValue(session)! == 100, "Damage belongs to the selected target and aggregate HP updates");
            Check(ReferenceEquals(assign.Invoke(null, [npc, session]), targets[0]) && npc.InvasionTargetX == 10, "Invaders redirect to a surviving target");
            Check((bool)damage.Invoke(null, [session, targets[0], 100])!, "Losing every target causes defeat");
            definition.DefeatOnAnyTargetDestroyed = true;
            targets = (IList)create.Invoke(null, [definition])!;
            sessionType.GetProperty("Objectives")!.SetValue(session, targets);
            Check((bool)damage.Invoke(null, [session, targets[0], 100])!, "Optional any-target rule ends defense immediately");
        }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, oldOptions); }
        System.Console.WriteLine("PASS invasion legacy objective, individual HP, nearest target, retargeting and both defeat rules");
    }
}
