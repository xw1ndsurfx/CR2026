using Intersect.Framework.Core.Exportation;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class ExportationRulesTests
{
    private static ExportationRoute Route() => new()
    {
        Name = "Island shipment",
        SourceNpcId = Guid.NewGuid(), RecipientNpcId = Guid.NewGuid(),
        SourceMapId = Guid.NewGuid(), RecipientMapId = Guid.NewGuid(),
        QuestId = Guid.NewGuid(), MinimumProfessionLevel = 2,
        RewardItemId = Guid.NewGuid(), RewardQuantity = 240, ExperienceReward = 120,
        Lines =
        [
            new() { ItemId = Guid.NewGuid(), Group = CargoGroup.Materials, Quantity = 8 },
            new() { ItemId = Guid.NewGuid(), Group = CargoGroup.Fragile, Quantity = 4 },
        ],
    };

    private static ExportationActor Source(ExportationRoute r, Guid ship, bool hasBoat = true) =>
        new(r.SourceMapId, r.SourceNpcId, ship, true, hasBoat, false);
    private static ExportationActor Recipient(ExportationRoute r, Guid ship) =>
        new(r.RecipientMapId, r.RecipientNpcId, ship, true, true, false);

    private static ExportationRun Packed(ExportationRoute r, Guid ship)
    {
        Assert.That(ExportationRules.TryStart(r, Source(r, ship), 2, out var run), Is.True);
        foreach (var line in r.Lines)
            Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), line.ItemId, line.Group, line.Quantity), Is.True);
        return run!;
    }

    [Test]
    public void RouteRejectsDuplicateItemOrSameIsland()
    {
        var route = Route();
        Assert.That(route.IsValid, Is.True);
        route.Lines[1].ItemId = route.Lines[0].ItemId;
        Assert.That(route.IsValid, Is.False);
        route.Lines[1].ItemId = Guid.NewGuid();
        route.RecipientMapId = route.SourceMapId;
        Assert.That(route.IsValid, Is.False);
    }

    [Test]
    public void StartNeedsSourceMerchantLevelAndBoat()
    {
        var r = Route(); var boat = Guid.NewGuid();
        Assert.That(ExportationRules.TryStart(r, Source(r, boat), 1, out _), Is.False);
        Assert.That(ExportationRules.TryStart(r, Source(r, boat, false), 2, out _), Is.False);
        Assert.That(ExportationRules.TryStart(r, Source(r, Guid.Empty), 2, out _), Is.False);
        Assert.That(ExportationRules.TryStart(r, Source(r, boat), 2, out _), Is.True);
    }

    [Test]
    public void PackValidatesItemTypeQuantityAndCanRemoveGoods()
    {
        var r = Route(); var ship = Guid.NewGuid();
        Assert.That(ExportationRules.TryStart(r, Source(r, ship), 2, out var run), Is.True);
        var item = r.Lines[0];
        Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), item.ItemId, CargoGroup.Fragile, 1), Is.False);
        Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), item.ItemId, item.Group, 9), Is.False);
        Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), item.ItemId, item.Group, -1), Is.False);
        Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), item.ItemId, item.Group, 8), Is.True);
        Assert.That(ExportationRules.TryPack(r, run!, Source(r, ship), item.ItemId, item.Group, -2), Is.True);
        Assert.That(ExportationRules.Packed(run!, item.ItemId), Is.EqualTo(6));
        Assert.That(ExportationRules.ReadyToSeal(r, run!), Is.False);
    }

    [Test]
    public void SealingMustBeCompleteAndIsNotRepeatable()
    {
        var r = Route(); var ship = Guid.NewGuid(); var run = Packed(r, ship);
        Assert.That(ExportationRules.TrySeal(r, run, Source(r, ship, false)), Is.False);
        Assert.That(ExportationRules.TrySeal(r, run, Source(r, ship)), Is.True);
        var seal = run.CargoSealId;
        Assert.That(seal, Is.Not.EqualTo(Guid.Empty));
        Assert.That(ExportationRules.TrySeal(r, run, Source(r, ship)), Is.False);
        Assert.That(run.CargoSealId, Is.EqualTo(seal));
    }

    [Test]
    public void DeliveryRequiresActualBoatJourneyAndOnlyPaysOnce()
    {
        var r = Route(); var ship = Guid.NewGuid(); var run = Packed(r, ship);
        ExportationRules.TrySeal(r, run, Source(r, ship));
        Assert.That(ExportationRules.TryDeliver(r, run, Recipient(r, ship), out _), Is.False);
        Assert.That(ExportationRules.TryEmbark(r, run, Source(r, Guid.NewGuid()) with { IsAboard = true }), Is.False);
        Assert.That(ExportationRules.TryEmbark(r, run, Source(r, ship) with { IsAboard = true }), Is.True);
        Assert.That(ExportationRules.TryDock(r, run, Recipient(r, Guid.NewGuid())), Is.False);
        Assert.That(ExportationRules.TryDock(r, run, Recipient(r, ship)), Is.True);
        Assert.That(ExportationRules.TryDeliver(r, run, Recipient(r, ship) with { NpcId = Guid.NewGuid() }, out _), Is.False);
        Assert.That(ExportationRules.TryDeliver(r, run, Recipient(r, ship), out var reward), Is.True);
        Assert.That(reward.Quantity, Is.EqualTo(240));
        Assert.That(reward.ProfessionExperience, Is.EqualTo(120));
        Assert.That(ExportationRules.TryDeliver(r, run, Recipient(r, ship), out _), Is.False);
    }

    [TestCase(CargoFailure.Death)]
    [TestCase(CargoFailure.ShipSunk)]
    public void LostCargoMustBeRepackedAtTheOriginalMerchant(CargoFailure cause)
    {
        var r = Route(); var ship = Guid.NewGuid(); var run = Packed(r, ship);
        ExportationRules.TrySeal(r, run, Source(r, ship));
        if (cause == CargoFailure.ShipSunk)
            Assert.That(ExportationRules.TryLose(run, cause, Guid.NewGuid()), Is.False);
        Assert.That(ExportationRules.TryLose(run, cause, ship), Is.True);
        Assert.That(run.Stage, Is.EqualTo(DeliveryStage.Lost));
        Assert.That(run.CargoSealId, Is.EqualTo(Guid.Empty));
        Assert.That(run.Packed, Is.Empty);
        Assert.That(ExportationRules.TryRestart(r, run, Recipient(r, ship)), Is.False);
        Assert.That(ExportationRules.TryRestart(r, run, Source(r, ship)), Is.True);
        Assert.That(run.Attempt, Is.EqualTo(2));
        Assert.That(ExportationRules.ReadyToSeal(r, run), Is.False);
    }

    [Test]
    public void ReplacementCargoCanBeSealedAndDeliveredAfterADeath()
    {
        var r = Route();
        var firstShip = Guid.NewGuid();
        var replacementShip = Guid.NewGuid();
        var run = Packed(r, firstShip);

        Assert.That(ExportationRules.TrySeal(r, run, Source(r, firstShip)), Is.True);
        var firstSeal = run.CargoSealId;
        Assert.That(ExportationRules.TryLose(run, CargoFailure.Death), Is.True);
        Assert.That(ExportationRules.TryRestart(r, run, Source(r, replacementShip)), Is.True);

        // The same order/run is re-prepared at the original merchant.
        foreach (var line in r.Lines)
            Assert.That(ExportationRules.TryPack(r, run, Source(r, replacementShip),
                line.ItemId, line.Group, line.Quantity), Is.True);
        Assert.That(ExportationRules.TrySeal(r, run, Source(r, replacementShip)), Is.True);
        Assert.That(run.CargoSealId, Is.Not.EqualTo(firstSeal));
        Assert.That(run.Attempt, Is.EqualTo(2));

        Assert.That(ExportationRules.TryEmbark(r, run,
            Source(r, replacementShip) with { IsAboard = true }), Is.True);
        Assert.That(ExportationRules.TryDock(r, run, Recipient(r, replacementShip)), Is.True);
        Assert.That(ExportationRules.TryDeliver(r, run,
            Recipient(r, replacementShip), out var payout), Is.True);
        Assert.That(payout.Quantity, Is.EqualTo(r.RewardQuantity));
        Assert.That(ExportationRules.TryDeliver(r, run,
            Recipient(r, replacementShip), out _), Is.False);
    }

    [Test]
    public void DeathAfterDockingStillLosesCargoButLaterShipSinkDoesNot()
    {
        var r = Route(); var ship = Guid.NewGuid(); var run = Packed(r, ship);
        ExportationRules.TrySeal(r, run, Source(r, ship));
        ExportationRules.TryEmbark(r, run, Source(r, ship) with { IsAboard = true });
        ExportationRules.TryDock(r, run, Recipient(r, ship));
        Assert.That(ExportationRules.TryLose(run, CargoFailure.ShipSunk, ship), Is.False);
        Assert.That(ExportationRules.TryLose(run, CargoFailure.Death), Is.True);
    }

    [Test]
    public void ConfigurationAndPlayerRunAreSerializable()
    {
        var r = Route();
        var configuration = ExportationConfiguration.FromJson(
            new ExportationConfiguration { Routes = [r] }.ToJson());
        Assert.That(configuration.IsValid, Is.True);
        var run = Packed(r, Guid.NewGuid());
        var copied = System.Text.Json.JsonSerializer.Deserialize<ExportationRun>(
            System.Text.Json.JsonSerializer.Serialize(run))!;
        Assert.That(ExportationRules.ReadyToSeal(r, copied), Is.True);
    }

    [Test]
    public void InterfaceTextIsLocalized()
    {
        Assert.That(ExportationText.Get("SEAL THE CARGO", "fr"), Is.EqualTo("SCELLER LA CARGAISON"));
        Assert.That(ExportationText.Get("SEAL THE CARGO", "es"), Is.EqualTo("SELLAR LA CARGA"));
    }
}
