using Intersect.Client.Naval;
using Intersect.Client.Interface.Game.Hotbar;
using Intersect.Framework.Core.Naval;
using NUnit.Framework;
namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalRewardFeedTests
{
    [Test]
    public void ReceiptsDisplayOnceAndLoginHistoryNeverReplays()
    {
        var feed=new NavalRewardFeed();var old=new NavalRewardReceipt {SailingExperience=50};
        feed.Observe([old],true,100);Assert.That(feed.Current(100),Is.Null);
        var reward=new NavalRewardReceipt {SailingExperience=275};
        feed.Observe([old,reward],false,200);Assert.That(feed.Current(200),Is.SameAs(reward));
        feed.Observe([reward],false,500);Assert.That(feed.Current(6201),Is.Null);
        feed.Observe([reward],false,6400);Assert.That(feed.Current(6400),Is.Null);
        feed.Observe([reward],true,6500);Assert.That(feed.Current(6500),Is.Null);
        feed.Clear();Assert.That(feed.Current(6500),Is.Null);
    }
    [Test]
    public void ExpiredOrMalformedReceiptsAndStaleQueuedRewardsAreIgnored()
    {
        var feed=new NavalRewardFeed();feed.Observe([],true,0);
        feed.Observe([new(){AgeMilliseconds=9000},new(){Id=Guid.Empty},new(){Cargo=null!}],false,1);
        Assert.That(feed.Current(1),Is.Null);
        var first=new NavalRewardReceipt();var second=new NavalRewardReceipt();
        feed.Observe([first,second],false,10);Assert.That(feed.Current(10),Is.SameAs(first));
        Assert.That(feed.Current(11000),Is.Null,"Old queued messages must not appear long after combat.");
    }
    [Test]
    public void RewardTextGroupsItemsAndExplainsFullHoldsAndQualityReplacementInThreeLanguages()
    {
        var id=Guid.NewGuid();var reward=new NavalRewardReceipt {SailingExperience=275,CargoFull=true,ReplacedLots=1,DiscardedLots=2,
            Cargo=[new(){ItemId=id,Quantity=2},new(){ItemId=id,Quantity=3}]};
        foreach(var language in new[]{"en","fr","es"})
        {
            var lines=NavalRewardHud.Format(reward,language,_=>"Aureons");
            Assert.That(lines.Length,Is.EqualTo(4));Assert.That(lines[0],Does.Contain("275"));
            Assert.That(lines[1],Does.Contain("Aureons").And.Contain("5"));
            Assert.That(lines[2],Does.Contain("1"));Assert.That(lines[3],Does.Contain("2"));
            if(language!="en") Assert.That(lines[3],Is.Not.EqualTo(NavalRewardHud.Format(reward,"en",_=>"Aureons")[3]));
        }
        reward.DiscardedLots=0;Assert.That(NavalRewardHud.Format(reward,"fr",_=>"Aureons")[^1],Does.Contain("Cale pleine"));
    }
}
