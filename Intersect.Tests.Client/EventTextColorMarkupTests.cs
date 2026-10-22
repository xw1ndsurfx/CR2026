using Intersect;
using Intersect.Client.Utilities;
using Intersect.Framework.Core.GameObjects.Events;
using NUnit.Framework;
namespace Intersect.Tests.Client;
[TestFixture] public sealed class EventTextColorMarkupTests
{
    [Test] public void SelectedWordsKeepTheirColourAndFollowingWordsReturnToThePreviousColour()
    {
        var source="Before selected after";
        var result=EventTextColorMarkup.ApplySelection(source,7,8,new Color(255,255,0,0));
        var parts=TextColorParser.Parse(result,Color.White);
        Assert.That(string.Concat(parts.Select(p=>p.Text)),Is.EqualTo(source));
        Assert.That(parts.Single(p=>p.Text=="selected").Color,Is.EqualTo(Color.Red));
        Assert.That(parts.Last().Color,Is.EqualTo(Color.White));
        source=@"\c{blue}Blue selected still blue";
        result=EventTextColorMarkup.ApplySelection(source,source.IndexOf("selected"),8,Color.Red);
        parts=TextColorParser.Parse(result,Color.White);
        Assert.That(parts.Last().Color,Is.EqualTo(Color.Blue));
        Assert.That(parts.Single(p=>p.Text=="selected").Color,Is.EqualTo(Color.Red));
    }
    [Test] public void WhitespaceBetweenColouredSpansAndEventVariablesArePreserved()
    {
        var source=@"\c{red}Hello\c{} \c{blue}world\c{}  ";
        Assert.That(string.Concat(TextColorParser.Parse(source,Color.White).Select(p=>p.Text)),Is.EqualTo("Hello world  "));
        var variable=@"Hello \pn!";
        Assert.That(EventTextColorMarkup.ApplySelection(variable,0,0,Color.Red),Is.EqualTo(variable));
        var result=EventTextColorMarkup.ApplySelection(variable,0,variable.Length,Color.Red);
        Assert.That(string.Concat(TextColorParser.Parse(result,Color.White).Select(p=>p.Text)),Is.EqualTo(variable));
    }
}
