using System;
using System.Collections.Generic;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    [Serializable]
    public sealed class TravelProfile
    {
        public TravelMode Mode = TravelMode.Walk;
        public float SpeedMetresPerSecond = 1.4f;
        public List<string> Capabilities = new List<string>();
    }

    [Serializable]
    public sealed class RouteSegment
    {
        public CellId SourceCell;
        public CellId DestinationCell;
        public NormalizedCellPosition Entry;
        public NormalizedCellPosition Exit;
        public NormalizedCellPosition DestinationEntry;
        public CellExitDirection ExitDirection;
        public PortalKind Portal;
        public float PhysicalLength;
        public float CostMultiplier = 1f;
        public double FixedDelaySeconds;
        public double MovementDurationSeconds;
        public double ExpectedDurationSeconds;
    }

    [Serializable]
    public sealed class CellRoute
    {
        public RouteId Id;
        public long TopologyVersion;
        public CellId Origin;
        public CellId Destination;
        public List<RouteSegment> Segments = new List<RouteSegment>();
        public double TotalDurationSeconds;

        public CellRoute(RouteId id, long topologyVersion, CellId origin, CellId destination)
        {
            Id = id;
            TopologyVersion = topologyVersion;
            Origin = origin;
            Destination = destination;
        }
    }

    public interface ITravelTimeCalculator
    {
        double CalculateSeconds(float distance, float costMultiplier, double fixedDelaySeconds, TravelProfile profile);
    }

    public sealed class TravelTimeCalculator : ITravelTimeCalculator
    {
        public double CalculateSeconds(float distance, float costMultiplier, double fixedDelaySeconds, TravelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.SpeedMetresPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(profile.SpeedMetresPerSecond));
            float multiplier = costMultiplier < 0.01f ? 0.01f : costMultiplier;
            return ((distance * multiplier) / profile.SpeedMetresPerSecond) + Math.Max(0d, fixedDelaySeconds);
        }
    }

    public interface IRoutePlanner
    {
        bool TryPlan(
            RouteId routeId,
            CellId origin,
            NormalizedCellPosition originPosition,
            CellId destination,
            NormalizedCellPosition destinationPosition,
            TravelProfile profile,
            out CellRoute route);
    }

    public sealed class CellRoutePlanner : IRoutePlanner
    {
        private readonly IWorldTopology _topology;
        private readonly ITravelTimeCalculator _travelTime;

        public CellRoutePlanner(IWorldTopology topology, ITravelTimeCalculator travelTime)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _travelTime = travelTime ?? throw new ArgumentNullException(nameof(travelTime));
        }

        public bool TryPlan(
            RouteId routeId,
            CellId origin,
            NormalizedCellPosition originPosition,
            CellId destination,
            NormalizedCellPosition destinationPosition,
            TravelProfile profile,
            out CellRoute route)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            TraversalCell ignored;
            if (!_topology.TryGetCell(origin, out ignored) || !_topology.TryGetCell(destination, out ignored))
            {
                route = null;
                return false;
            }

            List<CellConnection> path;
            if (!FindConnectionPath(origin, destination, profile, out path))
            {
                route = null;
                return false;
            }

            route = BuildRoute(routeId, origin, originPosition, destination, destinationPosition, path, profile);
            return true;
        }

        private bool FindConnectionPath(CellId origin, CellId destination, TravelProfile profile, out List<CellConnection> path)
        {
            var open = new List<CellId>();
            var cost = new Dictionary<CellId, double>();
            var previous = new Dictionary<CellId, CellConnection>();
            open.Add(origin);
            cost[origin] = 0d;

            while (open.Count > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < open.Count; i++)
                    if (cost[open[i]] < cost[open[bestIndex]]) bestIndex = i;

                CellId current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (current == destination) break;

                IReadOnlyList<CellConnection> connections = _topology.GetConnections(current);
                for (int i = 0; i < connections.Count; i++)
                {
                    CellConnection connection = connections[i];
                    if (!connection.CanTraverse(profile.Mode, profile.Capabilities)) continue;

                    float multiplier = CombinedMultiplier(connection);
                    double candidate = cost[current] + _travelTime.CalculateSeconds(
                        Math.Max(0.01f, connection.EstimatedDistance), multiplier, connection.FixedDelaySeconds, profile);
                    double known;
                    if (cost.TryGetValue(connection.Destination, out known) && candidate >= known) continue;

                    cost[connection.Destination] = candidate;
                    previous[connection.Destination] = connection;
                    if (!open.Contains(connection.Destination)) open.Add(connection.Destination);
                }
            }

            if (origin != destination && !previous.ContainsKey(destination))
            {
                path = null;
                return false;
            }

            path = new List<CellConnection>();
            CellId step = destination;
            while (step != origin)
            {
                CellConnection connection = previous[step];
                path.Add(connection);
                step = connection.Source;
            }
            path.Reverse();
            return true;
        }

        private CellRoute BuildRoute(
            RouteId id,
            CellId origin,
            NormalizedCellPosition originPosition,
            CellId destination,
            NormalizedCellPosition destinationPosition,
            List<CellConnection> connections,
            TravelProfile profile)
        {
            var route = new CellRoute(id, _topology.Version, origin, destination);
            CellId currentCellId = origin;
            NormalizedCellPosition currentPosition = originPosition;

            for (int i = 0; i < connections.Count; i++)
            {
                CellConnection connection = connections[i];
                TraversalCell cell;
                _topology.TryGetCell(currentCellId, out cell);
                float insideDistance = Float3.Distance(cell.Bounds.ToWorld(currentPosition), cell.Bounds.ToWorld(connection.SourceExit));
                float distance = insideDistance + Math.Max(0f, connection.EstimatedDistance);
                float multiplier = CombinedMultiplier(connection);
                double movementDuration = _travelTime.CalculateSeconds(distance, multiplier, 0d, profile);
                var segment = new RouteSegment
                {
                    SourceCell = currentCellId,
                    DestinationCell = connection.Destination,
                    Entry = currentPosition,
                    Exit = connection.SourceExit,
                    DestinationEntry = connection.DestinationEntry,
                    ExitDirection = connection.Direction,
                    Portal = connection.Portal,
                    PhysicalLength = distance,
                    CostMultiplier = multiplier,
                    FixedDelaySeconds = connection.FixedDelaySeconds,
                    MovementDurationSeconds = movementDuration,
                    ExpectedDurationSeconds = movementDuration + Math.Max(0d, connection.FixedDelaySeconds)
                };
                route.Segments.Add(segment);
                route.TotalDurationSeconds += segment.ExpectedDurationSeconds;
                currentCellId = connection.Destination;
                currentPosition = connection.DestinationEntry;
            }

            TraversalCell destinationCell;
            _topology.TryGetCell(destination, out destinationCell);
            float finalDistance = Float3.Distance(destinationCell.Bounds.ToWorld(currentPosition), destinationCell.Bounds.ToWorld(destinationPosition));
            if (finalDistance > 0.001f || route.Segments.Count == 0)
            {
                var finalSegment = new RouteSegment
                {
                    SourceCell = destination,
                    DestinationCell = destination,
                    Entry = currentPosition,
                    Exit = destinationPosition,
                    DestinationEntry = destinationPosition,
                    ExitDirection = CellExitDirection.None,
                    Portal = PortalKind.Open,
                    PhysicalLength = finalDistance,
                    CostMultiplier = 1f,
                    MovementDurationSeconds = _travelTime.CalculateSeconds(finalDistance, 1f, 0d, profile),
                    ExpectedDurationSeconds = _travelTime.CalculateSeconds(finalDistance, 1f, 0d, profile)
                };
                route.Segments.Add(finalSegment);
                route.TotalDurationSeconds += finalSegment.ExpectedDurationSeconds;
            }

            return route;
        }

        private static float CombinedMultiplier(CellConnection connection)
        {
            return Math.Max(0.01f, connection.TerrainMultiplier)
                * Math.Max(0.01f, connection.SlopeMultiplier)
                * Math.Max(0.01f, connection.CongestionMultiplier)
                * Math.Max(0.01f, connection.HazardMultiplier);
        }
    }
}
