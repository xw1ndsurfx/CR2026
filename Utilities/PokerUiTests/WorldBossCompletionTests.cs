using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Network.Packets.WorldEvents;
using MessagePack;

internal static class WorldBossCompletionTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        try { Verify(); } catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { Intersect.Core.ApplicationContext.Context.Value = previous; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Verify()
    {
        _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
        var ui = (GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(GameInterface).GetField("<GameCanvas>k__BackingField", flags)!.SetValue(ui, canvas);
        typeof(GameInterface).GetField("_worldBossStatuses", flags)!.SetValue(ui, new Dictionary<Guid, WorldBossStatusPacket>());
        typeof(GameInterface).GetField("_finishedWorldBossNpcs", flags)!.SetValue(ui, new Dictionary<Guid, long>());
        var status = new WorldBossStatusPacket { Active = true, BossId = Guid.NewGuid(), NpcId = Guid.NewGuid(), Health = 40, MaxHealth = 861, ExpiresAtUnixMilliseconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() };
        status = MessagePackSerializer.Deserialize<WorldBossStatusPacket>(MessagePackSerializer.Serialize(status));
        ui.UpdateWorldBossStatus(status);
        var window = (Intersect.Client.Framework.Gwen.Control.Base)typeof(GameInterface).GetField("_worldBossStatusWindow", flags)!.GetValue(ui)!;
        Check(!window.IsHidden, "Live boss status is visible.");
        ui.OnWorldBossNpcDied(Guid.NewGuid()); Check(!window.IsHidden, "Unrelated NPC death leaves boss status intact.");
        ui.OnWorldBossNpcDied(status.NpcId); Check(window.IsHidden, "The exact boss death hides the HUD even before the final status arrives.");
        ui.UpdateWorldBossStatus(status); Check(window.IsHidden, "Delayed active snapshots never reopen a defeated boss.");
        status.NpcId = Guid.NewGuid(); ui.UpdateWorldBossStatus(status); Check(!window.IsHidden, "A new spawn of the same boss can be displayed.");
        ui.UpdateWorldBossStatus(new WorldBossStatusPacket { BossId = status.BossId, NpcId = status.NpcId });
        ui.UpdateWorldBossStatus(status); Check(window.IsHidden, "Terminal status also suppresses delayed active snapshots.");
        System.Console.WriteLine("PASS world boss completion: exact NPC identity, immediate death closure, delayed packets and fresh respawn");
    }
}
