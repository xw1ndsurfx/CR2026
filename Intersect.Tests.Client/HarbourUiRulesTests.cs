using Intersect.Client.Interface.Game;
using Intersect.Network.Packets.Server;
using NUnit.Framework;
namespace Intersect.Tests.Client;
public sealed class HarbourUiRulesTests
{
    [Test]
    public void MissingCurrencyInsufficientBalanceAndPremiumLockPurchases()
    {
        var entry=new HarbourEntry { CurrencyAvailable=true,Balance=99,Price=100 };
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.False);
        entry.Balance=100; entry.PremiumRequired=true;
        Assert.That(HarbourUiRules.CanBuy(entry,false),Is.False);
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.True);
        entry.CurrencyAvailable=false;
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.False);
    }
    [Test]
    public void SunkenShipCanBeFullyRepairedWithoutPremiumButCannotLaunch()
    {
        var entry=new HarbourEntry { Owned=true,Health=0,PremiumRequired=true,CurrencyAvailable=true,Balance=100,RepairPrice=50 };
        Assert.That(HarbourUiRules.CanLaunch(entry,true),Is.False);
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.False);
        Assert.That(HarbourUiRules.CanRepair(entry),Is.True);
        entry.Health=100;
        Assert.That(HarbourUiRules.CanRepair(entry),Is.False);
        Assert.That(HarbourUiRules.CanLaunch(entry,false),Is.False);
        Assert.That(HarbourUiRules.CanLaunch(entry,true),Is.True);
    }
    [Test]
    public void RetiredModelCannotBeBoughtButOwnedShipsRemainUsableAndRepairable()
    {
        var entry = new HarbourEntry { ForSale=false, CurrencyAvailable=true, Balance=100, Price=10, RepairPrice=5, Health=50 };
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.False);
        entry.Owned=true;
        Assert.That(HarbourUiRules.CanLaunch(entry,true),Is.True);
        Assert.That(HarbourUiRules.CanRepair(entry),Is.True);
    }
}
