using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Exportation;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class ExportationUiPresentationTests
{
    private static ExportationRoute Route()
    {
        return new ExportationRoute
        {
            Lines =
            [
                new CargoLine { ItemId = Guid.NewGuid(), Group = CargoGroup.Materials, Quantity = 5 },
                new CargoLine { ItemId = Guid.NewGuid(), Group = CargoGroup.Fragile, Quantity = 3 },
                new CargoLine { ItemId = Guid.NewGuid(), Group = CargoGroup.Provisions, Quantity = 2 },
            ],
        };
    }

    [Test]
    public void RealServerPackedValuesDriveAllProgressBars()
    {
        var route = Route();
        var run = new ExportationRun();
        run.Packed[route.Lines[0].ItemId] = 3;
        run.Packed[route.Lines[1].ItemId] = 3;

        Assert.Multiple(() =>
        {
            Assert.That(ExportationUiPresentation.Cargo(route, run).Packed, Is.EqualTo(6));
            Assert.That(ExportationUiPresentation.Cargo(route, run).Required, Is.EqualTo(10));
            Assert.That(ExportationUiPresentation.Cargo(route, run).Percent, Is.EqualTo(60));
            Assert.That(ExportationUiPresentation.Cargo(route, run, true).Percent, Is.EqualTo(60));
            Assert.That(ExportationUiPresentation.Cargo(route, run, false).Percent, Is.EqualTo(60));
            Assert.That(ExportationUiPresentation.Cargo(route, run).Complete, Is.False);
        });

        run.Packed[route.Lines[0].ItemId] = 5;
        run.Packed[route.Lines[2].ItemId] = 2;
        Assert.That(ExportationUiPresentation.Cargo(route, run).Complete, Is.True);
        Assert.That(ExportationUiPresentation.Cargo(route, run).Percent, Is.EqualTo(100));
    }

    [Test]
    public void EmptyCrateHasNoMisleadingCompletedStatus()
    {
        var route = Route();
        route.Lines = [route.Lines[0]];
        var empty = ExportationUiPresentation.Cargo(route, new ExportationRun(), false);
        Assert.That(empty.Required, Is.Zero);
        Assert.That(empty.Packed, Is.Zero);
        Assert.That(empty.Percent, Is.Zero);
        Assert.That(empty.Complete, Is.False);
    }

    [Test]
    public void StaleCountsCannotOverflowTheCrateOrProgressBar()
    {
        var route = Route();
        var run = new ExportationRun();
        run.Packed[route.Lines[0].ItemId] = int.MaxValue;
        run.Packed[route.Lines[1].ItemId] = -5;
        var progress = ExportationUiPresentation.Cargo(route, run);
        Assert.That(progress.Packed, Is.EqualTo(5));
        Assert.That(progress.Percent, Is.EqualTo(50));
        Assert.That(run.Packed[route.Lines[0].ItemId], Is.EqualTo(int.MaxValue), "Presentation must not change server state.");
    }

    [TestCase("lingots_fer", CargoGroup.Materials, "item_metal")]
    [TestCase("planches", CargoGroup.Materials, "item_wood")]
    [TestCase("potion_rouge", CargoGroup.Fragile, "item_glass")]
    [TestCase("pommes", CargoGroup.Provisions, "item_wood")]
    public void PackingAudioMatchesTheVisibleCargo(string sprite, CargoGroup group, string sound) =>
        Assert.That(ExportationUiPresentation.PackingSound(new CargoLine { Sprite = sprite, Group = group }), Is.EqualTo(sound));

    [Test]
    public void ShelfStopsAllowingClicksWhenCompleteOrSealed()
    {
        var route = Route();
        var run = new ExportationRun();
        Assert.That(ExportationUiPresentation.CanPack(route, run, 0), Is.True);
        Assert.That(ExportationUiPresentation.CanPack(route, run, -1), Is.False);
        Assert.That(ExportationUiPresentation.CanPack(route, run, 3), Is.False);

        run.Packed[route.Lines[0].ItemId] = 5;
        Assert.That(ExportationUiPresentation.CanPack(route, run, 0), Is.False);
        Assert.That(ExportationUiPresentation.CanPack(route, run, 1), Is.True);
        run.Stage = DeliveryStage.InTransit;
        Assert.That(ExportationUiPresentation.CanPack(route, run, 1), Is.False);
    }
}
