using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class LayoutMutationTests
{
    private static readonly MethodInfo Recurse = typeof(ControlBase).GetMethod("RecurseLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        foreach (var dock in new[] { Pos.None, Pos.Top, Pos.Fill })
        foreach (var dirty in new[] { false, true })
        foreach (var before in new[] { false, true })
        foreach (var operation in new[] { "reorder", "add", "remove", "reparent", "dispose" })
        {
            using var canvas = new Canvas(skin) { Size = new Point(800, 600) };
            var parent = new ControlBase(canvas) { Size = new Point(500, 400) }; var elsewhere = new ControlBase(canvas);
            var first = new ControlBase(parent); var second = new ControlBase(parent); var third = new ControlBase(parent);
            first.Dock = second.Dock = third.Dock = dock;
            Recurse.Invoke(parent, [skin]); // Warm up the unchanged-layout branch from the crash log.
            if (dirty) parent.InvalidateDock();
            var calls = 0; var thirdCalls = 0; var mutated = false; ControlBase? added = null;
            second.AfterLayout += (_, _) => calls++;
            third.AfterLayout += (_, _) => thirdCalls++;
            void Mutate(ControlBase _, EventArgs args)
            {
                if (mutated) return; mutated = true;
                switch (operation)
                {
                    case "reorder": first.BringToFront(); break;
                    case "add": added = new ControlBase(parent); break;
                    case "remove": parent.RemoveChild(second, false); break;
                    case "reparent": second.Parent = elsewhere; break;
                    case "dispose": second.Dispose(); break;
                }
            }
            if (before) first.BeforeLayout += Mutate; else first.AfterLayout += Mutate;
            Recurse.Invoke(parent, [skin]);
            Check(mutated && thirdCalls == 1, "Every remaining sibling is laid out exactly once after " + operation);
            Check(calls == (operation is "remove" or "reparent" or "dispose" ? 0 : 1),  $"Removed/reparented/disposed siblings must not be laid out from a stale snapshot: {operation}, before={before}, dirty={dirty}, dock={dock}, calls={calls}");
            if (added != null)
            {
                var addedCalls = 0; added.AfterLayout += (_, _) => addedCalls++;
                Recurse.Invoke(parent, [skin]);
                Check(addedCalls == 1, "A new sibling participates in the following layout pass");
            }
            if (operation == "remove") second.Dispose();
            if (operation == "dispose") parent.RemoveChild(second, false); // Already disposed; avoid disposing it again with the canvas.
        }
        System.Console.WriteLine("PASS real Gwen layout callbacks can reorder/add/remove/reparent/dispose siblings without crashing or duplicate traversal");
    }
}
