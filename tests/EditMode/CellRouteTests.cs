using NUnit.Framework;
using Syms.Simulation.Travel;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class CellRouteTests
    {
        [Test]
        public void RouteUsesConnectedFacesAndVariableCellSize()
        {
            WorldTopology topology = CreateTwoCellTopology();
            var planner = new CellRoutePlanner(topology, new TravelTimeCalculator());
            CellRoute route;

            bool found = planner.TryPlan(
                new RouteId("route"), new CellId("a"), Centre(), new CellId("b"), Centre(),
                new TravelProfile { SpeedMetresPerSecond = 1f }, out route);

            Assert.That(found, Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(2));
            Assert.That(route.TotalDurationSeconds, Is.EqualTo(10d).Within(0.001d));
        }

        [Test]
        public void BlockerPreventsRoute()
        {
            WorldTopology topology = CreateTwoCellTopology();
            topology.SetBlockers("a-b", ConnectionBlocker.Wall);
            var planner = new CellRoutePlanner(topology, new TravelTimeCalculator());
            CellRoute route;

            bool found = planner.TryPlan(
                new RouteId("route"), new CellId("a"), Centre(), new CellId("b"), Centre(),
                new TravelProfile(), out route);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TransitMovesIntoNextCellAnalytically()
        {
            WorldTopology topology = CreateTwoCellTopology();
            var planner = new CellRoutePlanner(topology, new TravelTimeCalculator());
            CellRoute route;
            planner.TryPlan(new RouteId("route"), new CellId("a"), Centre(), new CellId("b"), Centre(),
                new TravelProfile { SpeedMetresPerSecond = 1f }, out route);
            var state = new TransitState();
            state.Begin(route, new TravelProfile { SpeedMetresPerSecond = 1f }, 0d);

            state.Advance(route, 7d);

            Assert.That(state.CurrentCell, Is.EqualTo(new CellId("b")));
            Assert.That(state.Position.X, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(state.JourneyProgress, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void TravelTimeIncludesCostMultiplierAndFixedDelay()
        {
            var calculator = new TravelTimeCalculator();

            double seconds = calculator.CalculateSeconds(
                10f, 2f, 5d, new TravelProfile { SpeedMetresPerSecond = 2f });

            Assert.That(seconds, Is.EqualTo(15d).Within(0.001d));
        }

        [Test]
        public void TopologyChangeInvalidatesActiveTransitRoute()
        {
            WorldTopology topology = CreateTwoCellTopology();
            var planner = new CellRoutePlanner(topology, new TravelTimeCalculator());
            CellRoute route;
            planner.TryPlan(new RouteId("route"), new CellId("a"), Centre(), new CellId("b"), Centre(),
                new TravelProfile(), out route);
            var state = new TransitState();
            state.Begin(route, new TravelProfile(), 0d);

            topology.SetBlockers("a-b", ConnectionBlocker.Closed);
            state.InvalidateForTopology(topology.Version);

            Assert.That(state.Status, Is.EqualTo(TransitStatus.RouteInvalid));
            Assert.That(state.CurrentCell, Is.EqualTo(new CellId("a")));
        }

        [Test]
        public void SharedRouteStoreRebuildsIndexFromSavedList()
        {
            var savedRoute = new CellRoute(new RouteId("saved"), 1, new CellId("a"), new CellId("b"));
            var restored = new RouteStore();
            restored.Items.Add(savedRoute);
            CellRoute result;

            Assert.That(restored.TryGet(new RouteId("saved"), out result), Is.True);
            Assert.That(result, Is.SameAs(savedRoute));
        }

        private static WorldTopology CreateTwoCellTopology()
        {
            var topology = new WorldTopology();
            topology.AddOrReplaceCell(new TraversalCell(
                new CellId("a"), new ChunkId("c"), new BlockId("1"), new PlaceId("street"),
                new CellBounds(new Float3(0f, 0f, 0f), new Float3(10f, 10f, 10f))));
            topology.AddOrReplaceCell(new TraversalCell(
                new CellId("b"), new ChunkId("c"), new BlockId("2"), new PlaceId("shop"),
                new CellBounds(new Float3(10f, 0f, 0f), new Float3(10f, 10f, 10f))));
            topology.AddConnection(new CellConnection(
                "a-b", new CellId("a"), new CellId("b"), CellExitDirection.PositiveX,
                new NormalizedCellPosition(1f, 0.5f, 0.5f),
                new NormalizedCellPosition(0f, 0.5f, 0.5f)));
            return topology;
        }

        private static NormalizedCellPosition Centre() { return new NormalizedCellPosition(0.5f, 0.5f, 0.5f); }
    }
}
