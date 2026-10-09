using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

[TestFixture]
public class PetMapTraversalTests
{
    [TestCase(1, 5, 5, 1, 5, 5, true)]
    [TestCase(1, 5, 5, 1, 6, 5, true)]
    [TestCase(1, 5, 5, 1, 5, 6, true)]
    [TestCase(1, 5, 5, 1, 6, 6, true)]
    [TestCase(1, 5, 5, 1, 7, 5, false)]
    [TestCase(1, 5, 5, 1, 5, 7, false)]
    [TestCase(1, 5, 5, 2, 5, 5, false)]
    public void AdjacentMapTilesPermitSeamlessFollowing(
        int petGrid, int petX, int petY,
        int ownerGrid, int ownerX, int ownerY, bool expected)
    {
        Assert.That(PetMapTraversal.AreAdjacentGridCells(
            petGrid, petX, petY, ownerGrid, ownerX, ownerY), Is.EqualTo(expected));
    }
}
