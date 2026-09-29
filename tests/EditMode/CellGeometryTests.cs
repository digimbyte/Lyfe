using NUnit.Framework;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class CellGeometryTests
    {
        [Test]
        public void NormalizedPositionRoundTripsInVariableSizedCell()
        {
            var bounds = new CellBounds(new Float3(10f, -5f, 20f), new Float3(40f, 10f, 80f));
            var local = new NormalizedCellPosition(0.25f, 0.5f, 0.75f);

            NormalizedCellPosition result = bounds.ToNormalized(bounds.ToWorld(local));

            Assert.That(result.X, Is.EqualTo(local.X).Within(0.0001f));
            Assert.That(result.Y, Is.EqualTo(local.Y).Within(0.0001f));
            Assert.That(result.Z, Is.EqualTo(local.Z).Within(0.0001f));
        }

        [Test]
        public void BlockDistanceUsesStableTwoDimensionalGridCoordinates()
        {
            var topology = new WorldTopology();
            topology.AddOrReplaceBlock(new CityBlock { Id = new BlockId("a"), Coordinate = new GridCoordinate2(2, 3) });
            topology.AddOrReplaceBlock(new CityBlock { Id = new BlockId("b"), Coordinate = new GridCoordinate2(5, 7) });

            Assert.That(topology.GetBlockDistance(new BlockId("a"), new BlockId("b")), Is.EqualTo(7));
        }

        [Test]
        public void ConnectionMustUseOppositeCellFaces()
        {
            Assert.Throws<System.ArgumentException>(() => new CellConnection(
                "bad",
                new CellId("a"),
                new CellId("b"),
                CellExitDirection.PositiveX,
                new NormalizedCellPosition(0.5f, 0.5f, 0.5f),
                new NormalizedCellPosition(0f, 0.5f, 0.5f)));
        }
    }
}
