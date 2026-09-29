using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    public enum TransitStatus { Travelling, Arrived, Delayed, Stranded, RouteInvalid }
    public enum TransitDomain { ExteriorCell, InteriorRoom, Composite }

    [Serializable]
    public sealed class TransitState
    {
        public TransitDomain Domain = TransitDomain.ExteriorCell;
        public RouteId RouteId;
        public long TopologyVersion;
        public int SegmentIndex;
        public CellId CurrentCell;
        public BuildingId CurrentBuilding;
        public RoomId CurrentRoom;
        public RoomPathId CurrentRoomPath;
        public GatewayId CurrentGateway;
        public NormalizedCellPosition Position;
        public float SegmentProgress;
        public float JourneyProgress;
        public double DepartureTime;
        public double ExpectedArrivalTime;
        public TravelMode Mode;
        public float EffectiveSpeed;
        public List<string> Capabilities = new List<string>();
        public TransitStatus Status = TransitStatus.Travelling;

        public void Begin(CellRoute route, TravelProfile profile, double departureTime)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            RouteId = route.Id;
            TopologyVersion = route.TopologyVersion;
            SegmentIndex = 0;
            CurrentCell = route.Origin;
            Position = route.Segments.Count == 0 ? new NormalizedCellPosition() : route.Segments[0].Entry;
            SegmentProgress = 0f;
            JourneyProgress = 0f;
            DepartureTime = departureTime;
            ExpectedArrivalTime = departureTime + route.TotalDurationSeconds;
            Mode = profile.Mode;
            EffectiveSpeed = profile.SpeedMetresPerSecond;
            Capabilities.Clear();
            if (profile.Capabilities != null) Capabilities.AddRange(profile.Capabilities);
            Status = route.TotalDurationSeconds <= 0d ? TransitStatus.Arrived : TransitStatus.Travelling;
        }

        public void Begin(CompositeRoute route, TravelProfile profile, double departureTime)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Domain = TransitDomain.Composite;
            RouteId = route.Id;
            SegmentIndex = 0;
            CurrentBuilding = route.OriginBuilding;
            CurrentRoom = route.OriginRoom;
            SegmentProgress = 0f;
            JourneyProgress = 0f;
            DepartureTime = departureTime;
            ExpectedArrivalTime = departureTime + route.TotalDurationSeconds;
            Mode = profile.Mode;
            EffectiveSpeed = profile.SpeedMetresPerSecond;
            Capabilities.Clear();
            if (profile.Capabilities != null) Capabilities.AddRange(profile.Capabilities);
            Status = route.TotalDurationSeconds <= 0d ? TransitStatus.Arrived : TransitStatus.Travelling;
        }

        public void Advance(CompositeRoute route, double cityTime)
        {
            if (route == null || route.Id != RouteId)
                throw new ArgumentException("The supplied route does not match this transit state.", nameof(route));
            if (Status != TransitStatus.Travelling) return;
            double elapsed = Math.Max(0d, cityTime - DepartureTime);
            JourneyProgress = route.TotalDurationSeconds <= 0d ? 1f
                : Clamp01((float)(elapsed / route.TotalDurationSeconds));
            double cursor = 0d;
            for (int i = 0; i < route.Segments.Count; i++)
            {
                CompositeRouteSegment segment = route.Segments[i];
                double end = cursor + segment.ExpectedDurationSeconds;
                if (elapsed < end || i == route.Segments.Count - 1)
                {
                    SegmentIndex = i;
                    SegmentProgress = segment.ExpectedDurationSeconds <= 0d ? 1f
                        : Clamp01((float)((elapsed - cursor) / segment.ExpectedDurationSeconds));
                    CurrentBuilding = segment.Building;
                    CurrentRoom = segment.SourceRoom;
                    CurrentRoomPath = segment.RoomPath;
                    CurrentGateway = segment.Gateway;
                    if (elapsed < route.TotalDurationSeconds) return;
                    break;
                }
                cursor = end;
            }
            SegmentIndex = Math.Max(0, route.Segments.Count - 1);
            SegmentProgress = 1f;
            JourneyProgress = 1f;
            CurrentBuilding = route.DestinationBuilding;
            CurrentRoom = route.DestinationRoom;
            Status = TransitStatus.Arrived;
        }

        public void Advance(CellRoute route, double cityTime)
        {
            if (route == null || route.Id != RouteId)
                throw new ArgumentException("The supplied route does not match this transit state.", nameof(route));
            if (Status != TransitStatus.Travelling) return;

            double elapsed = Math.Max(0d, cityTime - DepartureTime);
            JourneyProgress = route.TotalDurationSeconds <= 0d ? 1f : Clamp01((float)(elapsed / route.TotalDurationSeconds));
            double cursor = 0d;
            for (int i = 0; i < route.Segments.Count; i++)
            {
                RouteSegment segment = route.Segments[i];
                double end = cursor + segment.ExpectedDurationSeconds;
                if (elapsed < end || i == route.Segments.Count - 1)
                {
                    SegmentIndex = i;
                    double movementElapsed = Math.Max(0d, elapsed - cursor - segment.FixedDelaySeconds);
                    SegmentProgress = segment.MovementDurationSeconds <= 0d
                        ? 1f
                        : Clamp01((float)(movementElapsed / segment.MovementDurationSeconds));
                    CurrentCell = segment.SourceCell;
                    Position = NormalizedCellPosition.Lerp(segment.Entry, segment.Exit, SegmentProgress);
                    if (elapsed < route.TotalDurationSeconds) return;
                    break;
                }
                cursor = end;
            }

            RouteSegment last = route.Segments[route.Segments.Count - 1];
            SegmentIndex = route.Segments.Count - 1;
            SegmentProgress = 1f;
            JourneyProgress = 1f;
            CurrentCell = last.DestinationCell;
            Position = last.DestinationEntry;
            Status = TransitStatus.Arrived;
        }

        public void InvalidateForTopology(long currentVersion)
        {
            if (TopologyVersion != currentVersion && Status == TransitStatus.Travelling)
                Status = TransitStatus.RouteInvalid;
        }

        private static float Clamp01(float value) { return value < 0f ? 0f : (value > 1f ? 1f : value); }
    }
}
