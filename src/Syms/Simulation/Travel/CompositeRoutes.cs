using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    public enum RouteDomain { ExteriorCell, InteriorRoom, GatewayCrossing }

    [Serializable]
    public sealed class CompositeRouteSegment
    {
        public RouteDomain Domain;
        public BuildingId Building;
        public RoomId SourceRoom;
        public RoomId DestinationRoom;
        public RoomPathId RoomPath;
        public GatewayId Gateway;
        public string GatewayArcId;
        public CellId SourceCell;
        public CellId DestinationCell;
        public float PhysicalDistance;
        public float CostMultiplier = 1f;
        public double FixedDelaySeconds;
        public double ExpectedDurationSeconds;
        public bool Dangerous;
    }

    [Serializable]
    public sealed class CompositeRoute
    {
        public RouteId Id;
        public BuildingId OriginBuilding;
        public RoomId OriginRoom;
        public BuildingId DestinationBuilding;
        public RoomId DestinationRoom;
        public List<CompositeRouteSegment> Segments = new List<CompositeRouteSegment>();
        public List<string> Dependencies = new List<string>();
        public double TotalDurationSeconds;
    }

    public interface ICompositeRouteStore
    {
        void Store(CompositeRoute route);
        bool TryGet(RouteId id, out CompositeRoute route);
        bool Remove(RouteId id);
    }

    [Serializable]
    public sealed class CompositeRouteStore : ICompositeRouteStore
    {
        public List<CompositeRoute> Items = new List<CompositeRoute>();
        [NonSerialized] private Dictionary<RouteId, CompositeRoute> _index;
        public void Store(CompositeRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            EnsureIndex();
            CompositeRoute old;
            if (_index.TryGetValue(route.Id, out old)) Items.Remove(old);
            Items.Add(route); _index[route.Id] = route;
        }
        public bool TryGet(RouteId id, out CompositeRoute route)
        { EnsureIndex(); return _index.TryGetValue(id, out route); }
        public bool Remove(RouteId id)
        {
            EnsureIndex(); CompositeRoute route;
            if (!_index.TryGetValue(id, out route)) return false;
            _index.Remove(id); Items.Remove(route); return true;
        }
        private void EnsureIndex()
        {
            if (_index != null && _index.Count == Items.Count) return;
            _index = new Dictionary<RouteId, CompositeRoute>();
            for (int i = 0; i < Items.Count; i++) if (Items[i] != null) _index[Items[i].Id] = Items[i];
        }
    }

    [Serializable]
    public sealed class CompositeRouteRequest
    {
        public RouteId RouteId;
        public Sim Sim;
        public BuildingId Building;
        public RoomId OriginRoom;
        public AnchorId OriginAnchor;
        public RoomId DestinationRoom;
        public AnchorId DestinationAnchor;
        public TravelProfile Profile = new TravelProfile();
        public bool Emergency;
        public bool AllowDangerousTraversal;
        public float UnknownGatewayCostSeconds = 12f;
        public double CityTime;
        public WorldRiskSettings RiskSettings = new WorldRiskSettings();
    }

    public interface ICompositeRoutePlanner
    {
        bool TryPlanInterior(CompositeRouteRequest request, out CompositeRoute route,
            out RouteFailureState failure);
        CompositeRoute Compose(RouteId routeId, CompositeRoute originInterior,
            GatewayArc originExit, CellRoute exterior, GatewayArc destinationEntrance,
            CompositeRoute destinationInterior, TravelProfile profile);
    }

    /// <summary>
    /// Plans over one authored building graph. Exterior cell legs can be joined
    /// to its exterior socket segments by the city-level route coordinator.
    /// </summary>
    public sealed class CompositeRoutePlanner : ICompositeRoutePlanner
    {
        private readonly IBuildingGraphTopology _topology;
        private readonly IGatewayStateService _states;
        private readonly IGatewayAccessEvaluator _access;
        private readonly IGatewayKnowledgeService _knowledge;
        private readonly ITravelTimeCalculator _travelTime;

        public CompositeRoutePlanner(IBuildingGraphTopology topology, IGatewayStateService states,
            IGatewayAccessEvaluator access, IGatewayKnowledgeService knowledge,
            ITravelTimeCalculator travelTime)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _access = access ?? throw new ArgumentNullException(nameof(access));
            _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            _travelTime = travelTime ?? throw new ArgumentNullException(nameof(travelTime));
        }

        public bool TryPlanInterior(CompositeRouteRequest request, out CompositeRoute route,
            out RouteFailureState failure)
        {
            if (request == null || request.Sim == null) throw new ArgumentNullException(nameof(request));
            BuildingRoomGraph graph;
            if (!_topology.TryGetGraph(request.Building, out graph)
                || !Contains(graph.Rooms, request.OriginRoom) || !Contains(graph.Rooms, request.DestinationRoom))
            {
                route = null;
                failure = RouteFailureState.Stranded(request.DestinationRoom, request.CityTime,
                    "building-graph:" + request.Building.Value);
                return false;
            }

            if (request.OriginRoom == request.DestinationRoom)
            {
                route = new CompositeRoute { Id = request.RouteId, OriginBuilding = graph.Building,
                    DestinationBuilding = graph.Building, OriginRoom = request.OriginRoom,
                    DestinationRoom = request.DestinationRoom };
                RoomTraversalPath local;
                if (_topology.TryGetRoomPath(graph.Building, request.OriginRoom,
                    new GatewayId(), new GatewayId(), out local)) AddRoomPath(route, request, local, graph.Building);
                failure = null;
                return true;
            }

            var open = new List<SearchNode>();
            var costs = new Dictionary<string, double>(StringComparer.Ordinal);
            var previous = new Dictionary<string, SearchStep>(StringComparer.Ordinal);
            var blockedDependencies = new HashSet<string>(StringComparer.Ordinal);
            var start = new SearchNode(request.OriginRoom, new GatewayId());
            open.Add(start);
            costs[start.Key] = 0d;
            SearchNode found = null;

            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (costs[open[i].Key] < costs[open[best].Key]) best = i;
                SearchNode current = open[best];
                open.RemoveAt(best);
                if (current.Room == request.DestinationRoom) { found = current; break; }

                IReadOnlyList<GatewayArc> arcs = _topology.GetOutgoing(graph.Building, current.Room);
                for (int i = 0; i < arcs.Count; i++)
                {
                    GatewayArc arc = arcs[i];
                    if (arc.LeadsOutside || string.IsNullOrEmpty(arc.DestinationRoom.Value)) continue;
                    BuildingGateway gateway;
                    if (!_topology.TryGetGateway(arc.Gateway, out gateway)) continue;
                    GatewayRuntimeState state;
                    _states.TryGet(arc.Gateway, out state);
                    GatewayAccessContext context = CreateAccessContext(request);
                    GatewayAccessEvaluation actual = _access.Evaluate(arc, gateway, state, context);
                    GatewayKnowledgeResult known = _knowledge.GetKnowledge(request.Sim, graph.Building,
                        gateway, arc, state, request.CityTime);
                    if (known.IsKnown && !actual.Allowed)
                    {
                        AddDependencies(blockedDependencies, graph.Building, arc, actual);
                        continue;
                    }
                    if (actual.Dangerous)
                    {
                        if (!request.AllowDangerousTraversal) continue;
                        if (arc.Fall != null && !new EmergencyEscapePolicy().MayAttemptDangerousGateway(
                            request.Sim, arc, state, false, false, request.RiskSettings)) continue;
                    }

                    RoomTraversalPath roomPath;
                    if (!_topology.TryGetRoomPath(graph.Building, current.Room,
                            current.Incoming, arc.Gateway, out roomPath)) continue;
                    double seconds = _travelTime.CalculateSeconds(roomPath.PhysicalDistance,
                        roomPath.CombinedMultiplier, roomPath.FixedDelaySeconds, request.Profile);
                    seconds += _travelTime.CalculateSeconds(Math.Max(0f, arc.CrossingDistance),
                        Math.Max(0.01f, arc.CostMultiplier), arc.FixedDelaySeconds, request.Profile);
                    if (!known.IsKnown) seconds += Math.Max(0f, request.UnknownGatewayCostSeconds);
                    SearchNode next = new SearchNode(arc.DestinationRoom, arc.Gateway);
                    double candidate = costs[current.Key] + seconds;
                    double old;
                    if (costs.TryGetValue(next.Key, out old) && candidate >= old) continue;
                    costs[next.Key] = candidate;
                    previous[next.Key] = new SearchStep(current, arc, roomPath, seconds, actual.Dangerous);
                    ReplaceOpen(open, next);
                }
            }

            if (found == null)
            {
                route = null;
                failure = new RouteFailureState
                {
                    Kind = blockedDependencies.Count == 0 ? RouteFailureKind.Stranded : RouteFailureKind.Confined,
                    DestinationRoom = request.DestinationRoom,
                    BlockedAt = request.CityTime
                };
                foreach (string dependency in blockedDependencies) failure.Dependencies.Add(dependency);
                return false;
            }

            var reversed = new List<SearchStep>();
            SearchNode cursor = found;
            while (cursor.Key != start.Key)
            {
                SearchStep step = previous[cursor.Key];
                reversed.Add(step);
                cursor = step.Previous;
            }
            reversed.Reverse();
            route = BuildRoute(request, graph, reversed);
            failure = null;
            return true;
        }

        public CompositeRoute Compose(RouteId routeId, CompositeRoute originInterior,
            GatewayArc originExit, CellRoute exterior, GatewayArc destinationEntrance,
            CompositeRoute destinationInterior, TravelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var result = new CompositeRoute { Id = routeId };
            if (originInterior != null)
            {
                result.OriginBuilding = originInterior.OriginBuilding;
                result.OriginRoom = originInterior.OriginRoom;
                Append(result, originInterior);
            }
            if (originExit != null) AppendGateway(result, originExit, profile);
            if (exterior != null)
            {
                for (int i = 0; i < exterior.Segments.Count; i++)
                {
                    RouteSegment segment = exterior.Segments[i];
                    result.Segments.Add(new CompositeRouteSegment
                    {
                        Domain = RouteDomain.ExteriorCell,
                        SourceCell = segment.SourceCell,
                        DestinationCell = segment.DestinationCell,
                        PhysicalDistance = segment.PhysicalLength,
                        CostMultiplier = segment.CostMultiplier,
                        FixedDelaySeconds = segment.FixedDelaySeconds,
                        ExpectedDurationSeconds = segment.ExpectedDurationSeconds
                    });
                    result.TotalDurationSeconds += segment.ExpectedDurationSeconds;
                }
                result.Dependencies.Add("world-topology:" + exterior.TopologyVersion);
            }
            if (destinationEntrance != null) AppendGateway(result, destinationEntrance, profile);
            if (destinationInterior != null)
            {
                Append(result, destinationInterior);
                result.DestinationBuilding = destinationInterior.DestinationBuilding;
                result.DestinationRoom = destinationInterior.DestinationRoom;
            }
            else if (originInterior != null)
            {
                result.DestinationBuilding = originInterior.DestinationBuilding;
                result.DestinationRoom = originInterior.DestinationRoom;
            }
            return result;
        }

        private static void Append(CompositeRoute target, CompositeRoute source)
        {
            target.Segments.AddRange(source.Segments);
            target.TotalDurationSeconds += source.TotalDurationSeconds;
            for (int i = 0; i < source.Dependencies.Count; i++)
                if (!target.Dependencies.Contains(source.Dependencies[i]))
                    target.Dependencies.Add(source.Dependencies[i]);
        }

        private void AppendGateway(CompositeRoute target, GatewayArc arc, TravelProfile profile)
        {
            double seconds = _travelTime.CalculateSeconds(Math.Max(0f, arc.CrossingDistance),
                Math.Max(0.01f, arc.CostMultiplier), arc.FixedDelaySeconds, profile);
            target.Segments.Add(new CompositeRouteSegment
            {
                Domain = RouteDomain.GatewayCrossing,
                SourceRoom = arc.SourceRoom,
                DestinationRoom = arc.DestinationRoom,
                SourceCell = arc.ExteriorSource == null ? new CellId() : arc.ExteriorSource.Cell,
                DestinationCell = arc.ExteriorDestination == null ? new CellId() : arc.ExteriorDestination.Cell,
                Gateway = arc.Gateway,
                GatewayArcId = arc.Id,
                PhysicalDistance = arc.CrossingDistance,
                CostMultiplier = arc.CostMultiplier,
                FixedDelaySeconds = arc.FixedDelaySeconds,
                ExpectedDurationSeconds = seconds,
                Dangerous = arc.Fall != null
            });
            target.TotalDurationSeconds += seconds;
            target.Dependencies.Add("gateway:" + arc.Gateway.Value);
        }

        private CompositeRoute BuildRoute(CompositeRouteRequest request, BuildingRoomGraph graph,
            List<SearchStep> steps)
        {
            var route = new CompositeRoute
            {
                Id = request.RouteId,
                OriginBuilding = graph.Building,
                DestinationBuilding = graph.Building,
                OriginRoom = request.OriginRoom,
                DestinationRoom = request.DestinationRoom
            };
            for (int i = 0; i < steps.Count; i++)
            {
                SearchStep step = steps[i];
                double roomSeconds = _travelTime.CalculateSeconds(step.Path.PhysicalDistance,
                    step.Path.CombinedMultiplier, step.Path.FixedDelaySeconds, request.Profile);
                route.Segments.Add(new CompositeRouteSegment
                {
                    Domain = RouteDomain.InteriorRoom, Building = graph.Building,
                    SourceRoom = step.Arc.SourceRoom, DestinationRoom = step.Arc.SourceRoom,
                    RoomPath = step.Path.Id, PhysicalDistance = step.Path.PhysicalDistance,
                    CostMultiplier = step.Path.CombinedMultiplier,
                    FixedDelaySeconds = step.Path.FixedDelaySeconds,
                    ExpectedDurationSeconds = roomSeconds, Dangerous = step.Dangerous
                });
                double crossingSeconds = _travelTime.CalculateSeconds(Math.Max(0f, step.Arc.CrossingDistance),
                    Math.Max(0.01f, step.Arc.CostMultiplier), step.Arc.FixedDelaySeconds, request.Profile);
                route.Segments.Add(new CompositeRouteSegment
                {
                    Domain = RouteDomain.GatewayCrossing, Building = graph.Building,
                    SourceRoom = step.Arc.SourceRoom, DestinationRoom = step.Arc.DestinationRoom,
                    Gateway = step.Arc.Gateway, GatewayArcId = step.Arc.Id,
                    PhysicalDistance = step.Arc.CrossingDistance,
                    CostMultiplier = step.Arc.CostMultiplier,
                    FixedDelaySeconds = step.Arc.FixedDelaySeconds,
                    ExpectedDurationSeconds = crossingSeconds, Dangerous = step.Dangerous
                });
                route.Dependencies.Add("gateway:" + step.Arc.Gateway.Value);
                route.TotalDurationSeconds += roomSeconds + crossingSeconds;
            }
            if (steps.Count > 0)
            {
                RoomTraversalPath finalPath;
                if (_topology.TryGetRoomPath(graph.Building, request.DestinationRoom,
                    steps[steps.Count - 1].Arc.Gateway, new GatewayId(), out finalPath))
                    AddRoomPath(route, request, finalPath, graph.Building);
            }
            route.Dependencies.Add("building-graph:" + graph.Building.Value + ":" + graph.Revision);
            return route;
        }

        private void AddRoomPath(CompositeRoute route, CompositeRouteRequest request,
            RoomTraversalPath path, BuildingId building)
        {
            double seconds = _travelTime.CalculateSeconds(path.PhysicalDistance,
                path.CombinedMultiplier, path.FixedDelaySeconds, request.Profile);
            route.Segments.Add(new CompositeRouteSegment
            {
                Domain = RouteDomain.InteriorRoom, Building = building,
                SourceRoom = path.Room, DestinationRoom = path.Room, RoomPath = path.Id,
                PhysicalDistance = path.PhysicalDistance, CostMultiplier = path.CombinedMultiplier,
                FixedDelaySeconds = path.FixedDelaySeconds, ExpectedDurationSeconds = seconds
            });
            route.TotalDurationSeconds += seconds;
        }

        private static GatewayAccessContext CreateAccessContext(CompositeRouteRequest request)
        {
            var result = new GatewayAccessContext { Sim = request.Sim, Mode = request.Profile.Mode,
                Emergency = request.Emergency, AllowDangerousTraversal = request.AllowDangerousTraversal,
                CityTime = request.CityTime };
            result.Capabilities.AddRange(request.Profile.Capabilities);
            result.Claims.AddRange(request.Sim.AccessClaims);
            return result;
        }

        private static void AddDependencies(HashSet<string> target, BuildingId building,
            GatewayArc arc, GatewayAccessEvaluation evaluation)
        {
            target.Add("gateway:" + arc.Gateway.Value);
            target.Add("building:" + building.Value);
            if ((evaluation.BlockingReasons & GatewayBlocker.PermissionDenied) != 0) target.Add("access");
            if ((evaluation.BlockingReasons & (GatewayBlocker.Fire | GatewayBlocker.Flood
                | GatewayBlocker.EnvironmentalHazard)) != 0) target.Add("hazard:" + arc.Gateway.Value);
        }

        private static bool Contains(List<RoomId> values, RoomId value)
        { for (int i = 0; i < values.Count; i++) if (values[i] == value) return true; return false; }

        private static void ReplaceOpen(List<SearchNode> open, SearchNode node)
        {
            for (int i = 0; i < open.Count; i++)
                if (open[i].Key == node.Key) { open[i] = node; return; }
            open.Add(node);
        }

        private sealed class SearchNode
        {
            public readonly RoomId Room;
            public readonly GatewayId Incoming;
            public readonly string Key;
            public SearchNode(RoomId room, GatewayId incoming)
            { Room = room; Incoming = incoming; Key = room.Value + "\n" + incoming.Value; }
        }

        private sealed class SearchStep
        {
            public readonly SearchNode Previous;
            public readonly GatewayArc Arc;
            public readonly RoomTraversalPath Path;
            public readonly double Seconds;
            public readonly bool Dangerous;
            public SearchStep(SearchNode previous, GatewayArc arc, RoomTraversalPath path,
                double seconds, bool dangerous)
            { Previous = previous; Arc = arc; Path = path; Seconds = seconds; Dangerous = dangerous; }
        }
    }
}
