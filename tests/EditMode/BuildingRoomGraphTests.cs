using System;
using NUnit.Framework;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Travel;
using Syms.Simulation.World;
using Syms.World.Baking;

namespace Syms.Tests.EditMode
{
    public sealed class BuildingRoomGraphTests
    {
        [Test]
        public void DirectedPathsUsePortalToPortalDistance()
        {
            BuildingGraphTopology topology = ThreeRoomGraph();
            var sim = new Sim("visitor", "V", "One");
            CompositeRoute route;
            RouteFailureState failure;

            bool found = Planner(topology, new GatewayStateService()).TryPlanInterior(
                Request(sim, "a", "c"), out route, out failure);

            Assert.That(found, Is.True);
            Assert.That(route.TotalDurationSeconds, Is.EqualTo(42d).Within(0.001d));
            Assert.That(route.Segments.Count, Is.EqualTo(4));
            Assert.That(route.Segments[2].PhysicalDistance, Is.EqualTo(30f));

            CompositeRoute reverse;
            Assert.That(Planner(topology, new GatewayStateService()).TryPlanInterior(
                Request(sim, "c", "a"), out reverse, out failure), Is.False);
        }

        [Test]
        public void ResidentKnowsLockedExitButVisitorInitiallyAttemptsIt()
        {
            BuildingGraphTopology topology = ThreeRoomGraph();
            var states = new GatewayStateService();
            states.SetBlocker(new GatewayId("g.ab"), GatewayBlocker.Locked, true);
            var resident = new Sim("resident", "R", "One");
            resident.Residence.Active = true;
            resident.Residence.Building = new BuildingId("building");
            CompositeRoute route;
            RouteFailureState failure;

            Assert.That(Planner(topology, states).TryPlanInterior(
                Request(resident, "a", "c"), out route, out failure), Is.False);
            Assert.That(failure.Kind, Is.EqualTo(RouteFailureKind.Confined));
            Assert.That(failure.Dependencies, Contains.Item("gateway:g.ab"));

            var visitor = new Sim("visitor", "V", "One");
            Assert.That(Planner(topology, states).TryPlanInterior(
                Request(visitor, "a", "c"), out route, out failure), Is.True);
        }

        [Test]
        public void GatewayAccessDistinguishesObstructionCapabilityAndHardBars()
        {
            BuildingGraphTopology topology = ThreeRoomGraph();
            BuildingGateway gateway;
            topology.TryGetGateway(new GatewayId("g.ab"), out gateway);
            GatewayArc arc = gateway.Arcs[0];
            arc.Requirements.AllowMoveObstruction = true;
            var state = new GatewayRuntimeState
            { Gateway = gateway.Id, Blockers = GatewayBlocker.Obstruction | GatewayBlocker.Barred };
            var sim = new Sim("sim", "A", "B");
            var context = new GatewayAccessContext { Sim = sim };
            context.Capabilities.Add("move-obstruction");

            GatewayAccessEvaluation evaluation = new GatewayAccessEvaluator()
                .Evaluate(arc, gateway, state, context);

            Assert.That(evaluation.Allowed, Is.False);
            Assert.That(evaluation.BlockingReasons & GatewayBlocker.Obstruction, Is.EqualTo(GatewayBlocker.None));
            Assert.That(evaluation.BlockingReasons & GatewayBlocker.Barred, Is.Not.EqualTo(GatewayBlocker.None));
        }

        [Test]
        public void KnowledgeIsBoundedStaleAwareAndExpiresAfterDemotionGrace()
        {
            var knowledge = new GatewayKnowledgeState();
            for (int i = 0; i < 25; i++)
                knowledge.Observe(new ObservedGatewayFact
                {
                    Gateway = new GatewayId("g." + i), ArcId = "forward", GatewayRevision = 1
                }, i);
            Assert.That(knowledge.Facts.Count, Is.EqualTo(20));
            ObservedGatewayFact fact;
            Assert.That(knowledge.TryRecall(new GatewayId("g.24"), "forward", 2, 30d, out fact), Is.False);

            knowledge.BeginEphemeralExpiry(100d);
            Assert.That(knowledge.ExpireIfDue(159.9d), Is.False);
            Assert.That(knowledge.ExpireIfDue(160d), Is.True);
            Assert.That(knowledge.Facts, Is.Empty);
        }

        [Test]
        public void ConfinementIndexWakesOnlyMatchingDependencies()
        {
            var index = new RouteDependencyIndex();
            var failure = new RouteFailureState { Kind = RouteFailureKind.Confined };
            failure.Dependencies.Add("gateway:jail-door");
            failure.Dependencies.Add("building:jail");
            index.Register("prisoner", failure);

            Assert.That(index.GetWaitingSims("gateway:jail-door"), Is.EqualTo(new[] { "prisoner" }));
            Assert.That(index.GetWaitingSims("gateway:other"), Is.Empty);
            Assert.That(index.GetConfinedSims(new BuildingId("jail")), Is.EqualTo(new[] { "prisoner" }));
        }

        [TestCase(FatalFallPolicy.NeverFatal, false)]
        [TestCase(FatalFallPolicy.Orchestrated, false)]
        [TestCase(FatalFallPolicy.Simulated, true)]
        public void FatalFallPolicyControlsDeath(FatalFallPolicy policy, bool dies)
        {
            var sim = new Sim("faller", "F", "One");
            FallOutcome outcome = new GatewayFallResolver().Resolve(sim, new GatewayId("window"),
                new GatewayFallProfile
                {
                    DropMetres = 10f, InjuryPerMetre = 20f, FatalThresholdMetres = 0f,
                    FatalChanceAtThreshold = 1f, FatalChancePerAdditionalMetre = 0f
                }, 1, 1, new WorldRiskSettings { FatalFallPolicy = policy });

            Assert.That(outcome.PotentiallyFatal, Is.True);
            Assert.That(outcome.Died, Is.EqualTo(dies));
            Assert.That(sim.LifeStatus == LifeStatus.Dead, Is.EqualTo(dies));
        }

        [Test]
        public void CellFreeRoomsAndGraphPassAuthoredValidation()
        {
            var content = new AuthoredTownContent();
            var buildingDefinition = new BuildingDefinition
            { Id = new BuildingDefinitionId("definition"), DisplayName = "House" };
            var building = new BuildingInstance
            { Id = new BuildingId("building"), Definition = buildingDefinition.Id };
            var roomDefinition = new RoomDefinition
            { Id = new RoomDefinitionId("room.definition"), DisplayName = "Room" };
            var room = new RoomInstance
            {
                Id = new RoomId("a"), Definition = roomDefinition.Id,
                Building = building.Id, Place = new PlaceId("place.a"),
                Scene = new RoomSceneBinding("scene.house", "rooms.interior", "room-volume-a")
            };
            content.BuildingDefinitions.Add(buildingDefinition);
            content.BuildingInstances.Add(building);
            content.RoomDefinitions.Add(roomDefinition);
            content.RoomInstances.Add(room);
            content.BuildingGraphs.Add(new BuildingRoomGraph
            {
                Id = new BuildingGraphId("graph"), Building = building.Id,
                Rooms = { room.Id }
            });

            AuthoredTownRuntime runtime = null;
            Assert.DoesNotThrow(() => runtime = content.BuildRuntime(new CapacityService()));
            Assert.That(runtime.FindRoom("scene.house", "room-volume-a").Id, Is.EqualTo(room.Id));
            Assert.That(runtime.FindRoomsInScene("scene.house", "rooms.interior").Count, Is.EqualTo(1));
        }

        [Test]
        public void ComposerJoinsInteriorGatewayAndExteriorCellLegs()
        {
            BuildingGraphTopology topology = ThreeRoomGraph();
            CompositeRoutePlanner planner = Planner(topology, new GatewayStateService());
            var interior = new CompositeRoute
            {
                Id = new RouteId("inside"), OriginBuilding = new BuildingId("building"),
                DestinationBuilding = new BuildingId("building"),
                OriginRoom = new RoomId("a"), DestinationRoom = new RoomId("b"),
                TotalDurationSeconds = 5d
            };
            interior.Segments.Add(new CompositeRouteSegment
            { Domain = RouteDomain.InteriorRoom, ExpectedDurationSeconds = 5d });
            var exit = new GatewayArc
            {
                Id = "exit", Gateway = new GatewayId("front-door"), SourceRoom = new RoomId("b"),
                ExteriorDestination = new ExteriorPortalSocket { Cell = new CellId("street") },
                CrossingDistance = 1f
            };
            var exterior = new CellRoute(new RouteId("outside"), 4, new CellId("street"), new CellId("road"));
            exterior.Segments.Add(new RouteSegment
            { SourceCell = new CellId("street"), DestinationCell = new CellId("road"), ExpectedDurationSeconds = 10d });
            exterior.TotalDurationSeconds = 10d;

            CompositeRoute composed = planner.Compose(new RouteId("whole"), interior, exit,
                exterior, null, null, new TravelProfile { SpeedMetresPerSecond = 1f });

            Assert.That(composed.Segments.Count, Is.EqualTo(3));
            Assert.That(composed.Segments[1].Domain, Is.EqualTo(RouteDomain.GatewayCrossing));
            Assert.That(composed.Segments[2].Domain, Is.EqualTo(RouteDomain.ExteriorCell));
            Assert.That(composed.TotalDurationSeconds, Is.EqualTo(16d).Within(0.001d));
        }

        [Test]
        public void PanicAllowsRiskyWindowOnlyWhenNoSafeRouteExists()
        {
            var sim = new Sim("sim", "A", "B");
            sim.Activity = new ActivitySegment { Kind = ActivityKind.Flee };
            var arc = new GatewayArc
            {
                Gateway = new GatewayId("window"),
                Fall = new GatewayFallProfile
                { DropMetres = 4f, FatalThresholdMetres = 10f }
            };
            var policy = new EmergencyEscapePolicy();

            Assert.That(policy.MayAttemptDangerousGateway(sim, arc, null, false, false,
                new WorldRiskSettings()), Is.True);
            Assert.That(policy.MayAttemptDangerousGateway(sim, arc, null, true, false,
                new WorldRiskSettings()), Is.False);
        }

        [Test]
        public void SceneCompilerBakesNavMeshDistanceAndWindowFallSurvey()
        {
            var input = new SceneBuildingBakeInput
            { Graph = new BuildingGraphId("graph"), Building = new BuildingId("building") };
            input.Buildings.Add(new SceneManagedBuildingBakeRecord
            {
                Building = input.Building,
                Definition = new BuildingDefinitionId("building.definition")
            });
            input.Rooms.Add(new SceneRoomBakeRecord
            {
                Room = new RoomId("room"), Definition = new RoomDefinitionId("definition"),
                Building = input.Building, Place = new PlaceId("place"),
                Scene = new RoomSceneBinding("scene", "rooms.interior", "room-volume")
            });
            var arc = new GatewayArc
            { Id = "window.out", Gateway = new GatewayId("window"), SourceRoom = new RoomId("room") };
            input.Gateways.Add(new SceneGatewayBakeRecord
            {
                Gateway = arc.Gateway, StableSceneObjectId = "window-object",
                Kind = GatewayKind.Window, Channels = GatewayChannel.All,
                DirectedArcs = { arc }
            });
            input.WindowFalls.Add(new SceneWindowFallSurvey
            {
                Gateway = arc.Gateway, ArcId = arc.Id, VerticalDropMetres = 7f,
                Landing = LandingSurface.Stone, LandingModifier = 1.3f, LandingReachable = true
            });
            input.NavMeshPaths.Add(new SceneNavMeshPathBakeRecord
            {
                Path = new RoomPathId("path"), Room = new RoomId("room"),
                OutgoingGateway = arc.Gateway, NavMeshDistance = 23f,
                BakedNavMeshPathHandle = "navmesh/path/1"
            });

            SceneBuildingBakeOutput output = new SceneBuildingGraphCompiler().Compile(input);

            Assert.That(output.Graph.RoomPaths[0].PhysicalDistance, Is.EqualTo(23f));
            Assert.That(output.Graph.Gateways[0].Arcs[0].Fall.DropMetres, Is.EqualTo(7f));
            Assert.That(output.Rooms[0].Scene.SceneObjectId, Is.EqualTo("room-volume"));
        }

        private static CompositeRoutePlanner Planner(BuildingGraphTopology topology, GatewayStateService states)
        {
            return new CompositeRoutePlanner(topology, states, new GatewayAccessEvaluator(),
                new GatewayKnowledgeService(), new TravelTimeCalculator());
        }

        private static CompositeRouteRequest Request(Sim sim, string origin, string destination)
        {
            return new CompositeRouteRequest
            {
                RouteId = new RouteId("route"), Sim = sim, Building = new BuildingId("building"),
                OriginRoom = new RoomId(origin), DestinationRoom = new RoomId(destination),
                Profile = new TravelProfile { SpeedMetresPerSecond = 1f }
            };
        }

        private static BuildingGraphTopology ThreeRoomGraph()
        {
            var graph = new BuildingRoomGraph
            { Id = new BuildingGraphId("graph"), Building = new BuildingId("building") };
            graph.Rooms.Add(new RoomId("a"));
            graph.Rooms.Add(new RoomId("b"));
            graph.Rooms.Add(new RoomId("c"));
            graph.Gateways.Add(Gateway("g.ab", "a", "b"));
            graph.Gateways.Add(Gateway("g.bc", "b", "c"));
            graph.RoomPaths.Add(Path("path.a", "a", string.Empty, "g.ab", 10f));
            graph.RoomPaths.Add(Path("path.b", "b", "g.ab", "g.bc", 30f));
            var topology = new BuildingGraphTopology();
            topology.Graphs.Add(graph);
            return topology;
        }

        private static BuildingGateway Gateway(string id, string source, string destination)
        {
            var gateway = new BuildingGateway { Id = new GatewayId(id), Kind = GatewayKind.Door };
            gateway.Arcs.Add(new GatewayArc
            {
                Id = id + ".forward", Gateway = gateway.Id,
                SourceRoom = new RoomId(source), DestinationRoom = new RoomId(destination),
                CrossingDistance = 1f
            });
            return gateway;
        }

        private static RoomTraversalPath Path(string id, string room, string incoming,
            string outgoing, float distance)
        {
            return new RoomTraversalPath
            {
                Id = new RoomPathId(id), Room = new RoomId(room),
                IncomingGateway = new GatewayId(incoming), OutgoingGateway = new GatewayId(outgoing),
                PhysicalDistance = distance
            };
        }
    }
}
