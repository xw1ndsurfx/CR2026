using System.Reflection;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CinematicInteractionTests
{
    private static void Jump(CommandInstance frame, Stack<CommandInstance> stack, string label)
    {
        var method = typeof(CommandProcessing).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "ProcessCommand" && m.GetParameters()[0].ParameterType == typeof(GoToLabelCommand));
        method.Invoke(null, [new GoToLabelCommand { Label = label }, null, null, frame, stack]);
    }

    [Test]
    public void RepeatedInteractionWaitLoopPreservesCinematicCallerAndDoesNotGrowStack()
    {
        var caller = new CommandInstance(new EventPage());
        var page = new EventPage();
        page.CommandLists.Values.First().Add(new LabelCommand { Label = "await-interaction" });
        var branch = new ConditionalBranchCommand(page.CommandLists);
        var stack = new Stack<CommandInstance>(); stack.Push(caller); stack.Push(new(page));
        for (var i = 0; i < 100; i++)
        {
            var waiting = new CommandInstance(page, branch.BranchIds[1]); stack.Push(waiting);
            Jump(waiting, stack, "await-interaction");
            Assert.That(stack.Count, Is.EqualTo(2), "A waiting branch must replace its common Event frame, not accumulate copies.");
            Assert.That(stack.Last(), Is.SameAs(caller));
            Assert.That(stack.Peek().Command, Is.TypeOf<LabelCommand>());
        }
    }

    [Test]
    public void JumpToNestedLabelRestoresRootThenBranchInExecutionOrder()
    {
        var page = new EventPage();
        var branch = new ConditionalBranchCommand(page.CommandLists);
        page.CommandLists.Values.First().Add(branch);
        page.CommandLists[branch.BranchIds[0]].Add(new LabelCommand { Label = "nested" });
        var root = new CommandInstance(page);
        var stack = new Stack<CommandInstance>(); stack.Push(root);
        Jump(root, stack, "nested");
        Assert.That(stack.Count, Is.EqualTo(2));
        Assert.That(stack.Peek().CommandList, Is.SameAs(page.CommandLists[branch.BranchIds[0]]));
        Assert.That(stack.Peek().Command, Is.TypeOf<LabelCommand>());
    }

    [Test]
    public void MissingLabelLeavesNativeEventAndCallerUntouched()
    {
        var root = new CommandInstance(new EventPage());
        var stack = new Stack<CommandInstance>(); stack.Push(root);
        Jump(root, stack, "missing");
        Assert.That(stack.Single(), Is.SameAs(root));
    }
}
