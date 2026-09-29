using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Objects;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    public enum GatewayKind
    {
        Door,
        Window,
        Archway,
        Gate,
        Hatch,
        Stairs,
        Ladder,
        Lift,
        ExteriorEntrance,
        OpenBoundary,
        Junction,
        Special
    }

    public enum GatewayPortalShape { Point, Segment, Area }

    /// <summary>
    /// Baked traversable extent of a gateway. Open outdoor boundaries use a
    /// segment or area instead of pretending that the crossing is a door-sized
    /// point. The localization handle resolves the exact crossing on load.
    /// </summary>
    [Serializable]
    public sealed class GatewayPortalGeometry
    {
        public GatewayPortalShape Shape;
        public Float3 Start;
        public Float3 End;
        public float HalfWidth;
        public string LocalizationHandle;
    }

    [Flags]
    public enum GatewayChannel
    {
        None = 0, Traversal = 1, Visibility = 2, Audibility = 4, All = Traversal | Visibility | Audibility
    }

    [Flags]
    public enum GatewayBlocker
    {
        None = 0,
        Closed = 1,
        Locked = 2,
        Jammed = 4,
        Barred = 8,
        Barricaded = 16,
        Sealed = 32,
        PermissionDenied = 64,
        Obstruction = 128,
        Debris = 256,
        DamagedImpassable = 512,
        Occupied = 1024,
        Congested = 2048,
        Fire = 4096,
        Flood = 8192,
        Collapse = 16384,
        EnvironmentalHazard = 32768,
        DestinationUnavailable = 65536
    }

    public enum GatewayTraversalOutcome { Unknown, Succeeded, Denied, Blocked, Hazardous }
    public enum LandingSurface { Unknown, Soft, Earth, Timber, Stone, Metal, Water }

    [Serializable]
    public sealed class GatewayFallProfile
    {
        public float DropMetres;
        public float InjuryPerMetre = 8f;
        public float FatalThresholdMetres = 8f;
        public float FatalChanceAtThreshold = 0.25f;
        public float FatalChancePerAdditionalMetre = 0.1f;
        public float LandingModifier = 1f;
        public LandingSurface Landing = LandingSurface.Unknown;
        public string RequiredClimbingCapability;

        public float FatalChance
        {
            get
            {
                if (DropMetres < FatalThresholdMetres) return 0f;
                float result = FatalChanceAtThreshold
                    + ((DropMetres - FatalThresholdMetres) * FatalChancePerAdditionalMetre);
                return result < 0f ? 0f : (result > 1f ? 1f : result);
            }
        }
    }

    [Serializable]
    public sealed class ExteriorPortalSocket
    {
        public CellId Cell;
        public NormalizedCellPosition Position;
        public string WorldConnectionId;
    }

    [Serializable]
    public sealed class GatewayRequirementSet
    {
        public TravelMode AllowedModes = TravelMode.Walk | TravelMode.Run;
        public List<string> RequiredCapabilities = new List<string>();
        public List<string> AnyAccessClaims = new List<string>();
        public string RequiredKeyId;
        public bool AllowBreak;
        public bool AllowMoveObstruction;
    }

    /// <summary>A direction through a physical gateway. Reverse travel is always authored explicitly.</summary>
    [Serializable]
    public sealed class GatewayArc
    {
        public string Id;
        public GatewayId Gateway;
        public RoomId SourceRoom;
        public BuildingId SourceBuilding;
        public ExteriorPortalSocket ExteriorSource;
        public RoomId DestinationRoom;
        public BuildingId DestinationBuilding;
        public ExteriorPortalSocket ExteriorDestination;
        public GatewayRequirementSet Requirements = new GatewayRequirementSet();
        public float CrossingDistance;
        public float CostMultiplier = 1f;
        public double FixedDelaySeconds;
        public GatewayFallProfile Fall;

        public bool LeadsOutside { get { return ExteriorDestination != null; } }
        public bool StartsOutside { get { return ExteriorSource != null; } }
    }

    [Serializable]
    public sealed class BuildingGateway
    {
        public GatewayId Id;
        public GatewayKind Kind;
        public GatewayChannel Channels = GatewayChannel.Traversal;
        public GatewayPortalGeometry Portal = new GatewayPortalGeometry();
        public long Revision;
        public List<GatewayArc> Arcs = new List<GatewayArc>();
        public List<string> Tags = new List<string>();
    }

    /// <summary>Baked navigable cost between two portals/anchors inside one room.</summary>
    [Serializable]
    public sealed class RoomTraversalPath
    {
        public RoomPathId Id;
        public RoomId Room;
        public GatewayId IncomingGateway;
        public GatewayId OutgoingGateway;
        public AnchorId IncomingAnchor;
        public AnchorId OutgoingAnchor;
        public float PhysicalDistance;
        public float TerrainMultiplier = 1f;
        public float SlopeMultiplier = 1f;
        public float CongestionMultiplier = 1f;
        public float HazardMultiplier = 1f;
        public double FixedDelaySeconds;
        public string LocalizationPathHandle;

        public float CombinedMultiplier
        {
            get
            {
                return Math.Max(0.01f, TerrainMultiplier) * Math.Max(0.01f, SlopeMultiplier)
                    * Math.Max(0.01f, CongestionMultiplier) * Math.Max(0.01f, HazardMultiplier);
            }
        }
    }

    [Serializable]
    public sealed class BuildingRoomGraph
    {
        public BuildingGraphId Id;
        public BuildingId Building;
        public List<BuildingId> MemberBuildings = new List<BuildingId>();
        public long Revision;
        public List<RoomId> Rooms = new List<RoomId>();
        public List<BuildingGateway> Gateways = new List<BuildingGateway>();
        public List<RoomTraversalPath> RoomPaths = new List<RoomTraversalPath>();
    }

    public interface IBuildingGraphTopology
    {
        bool TryGetGraph(BuildingId building, out BuildingRoomGraph graph);
        bool TryGetGateway(GatewayId gateway, out BuildingGateway value);
        IReadOnlyList<GatewayArc> GetOutgoing(BuildingId building, RoomId room);
        bool TryGetRoomPath(BuildingId building, RoomId room, GatewayId incoming, GatewayId outgoing,
            out RoomTraversalPath path);
    }

    [Serializable]
    public sealed class BuildingGraphTopology : IBuildingGraphTopology
    {
        public List<BuildingRoomGraph> Graphs = new List<BuildingRoomGraph>();
        [NonSerialized] private Dictionary<BuildingId, BuildingRoomGraph> _graphs;
        [NonSerialized] private Dictionary<GatewayId, BuildingGateway> _gateways;
        [NonSerialized] private Dictionary<string, List<GatewayArc>> _outgoing;
        [NonSerialized] private Dictionary<string, RoomTraversalPath> _paths;
        [NonSerialized] private int _indexedGraphCount = -1;

        public bool TryGetGraph(BuildingId building, out BuildingRoomGraph graph)
        { EnsureIndexes(); return _graphs.TryGetValue(building, out graph); }

        public bool TryGetGateway(GatewayId gateway, out BuildingGateway value)
        { EnsureIndexes(); return _gateways.TryGetValue(gateway, out value); }

        public IReadOnlyList<GatewayArc> GetOutgoing(BuildingId building, RoomId room)
        {
            EnsureIndexes();
            List<GatewayArc> result;
            return _outgoing.TryGetValue(Key(building, room), out result) ? result : Array.Empty<GatewayArc>();
        }

        public bool TryGetRoomPath(BuildingId building, RoomId room, GatewayId incoming,
            GatewayId outgoing, out RoomTraversalPath path)
        { EnsureIndexes(); return _paths.TryGetValue(PathKey(building, room, incoming, outgoing), out path); }

        public void RebuildIndexes() { _indexedGraphCount = -1; EnsureIndexes(); }

        public void AddOrReplaceGraph(BuildingRoomGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            EnsureIndexes();
            BuildingRoomGraph previous;
            if (_graphs.TryGetValue(graph.Building, out previous)) Graphs.Remove(previous);
            Graphs.Add(graph);
            _indexedGraphCount = -1;
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            if (_graphs != null && _indexedGraphCount == Graphs.Count) return;
            _graphs = new Dictionary<BuildingId, BuildingRoomGraph>();
            _gateways = new Dictionary<GatewayId, BuildingGateway>();
            _outgoing = new Dictionary<string, List<GatewayArc>>(StringComparer.Ordinal);
            _paths = new Dictionary<string, RoomTraversalPath>(StringComparer.Ordinal);
            for (int i = 0; i < Graphs.Count; i++)
            {
                BuildingRoomGraph graph = Graphs[i];
                if (graph == null) continue;
                _graphs[graph.Building] = graph;
                for (int member = 0; member < graph.MemberBuildings.Count; member++)
                    _graphs[graph.MemberBuildings[member]] = graph;
                for (int g = 0; g < graph.Gateways.Count; g++)
                {
                    BuildingGateway gateway = graph.Gateways[g];
                    if (gateway == null) continue;
                    _gateways[gateway.Id] = gateway;
                    for (int a = 0; a < gateway.Arcs.Count; a++)
                    {
                        GatewayArc arc = gateway.Arcs[a];
                        if (arc == null) continue;
                        List<GatewayArc> list;
                        AddOutgoing(graph.Building, arc.SourceRoom, arc);
                        for (int member = 0; member < graph.MemberBuildings.Count; member++)
                            AddOutgoing(graph.MemberBuildings[member], arc.SourceRoom, arc);
                    }
                }
                for (int p = 0; p < graph.RoomPaths.Count; p++)
                {
                    RoomTraversalPath path = graph.RoomPaths[p];
                    if (path == null) continue;
                    _paths[PathKey(graph.Building, path.Room,
                        path.IncomingGateway, path.OutgoingGateway)] = path;
                    for (int member = 0; member < graph.MemberBuildings.Count; member++)
                        _paths[PathKey(graph.MemberBuildings[member], path.Room,
                            path.IncomingGateway, path.OutgoingGateway)] = path;
                }
            }
            _indexedGraphCount = Graphs.Count;
        }

        private void AddOutgoing(BuildingId building, RoomId room, GatewayArc arc)
        {
            List<GatewayArc> list;
            string key = Key(building, room);
            if (!_outgoing.TryGetValue(key, out list))
            { list = new List<GatewayArc>(); _outgoing.Add(key, list); }
            list.Add(arc);
        }

        private static string Key(BuildingId building, RoomId room)
        { return building.Value + "\n" + room.Value; }

        private static string PathKey(BuildingId building, RoomId room, GatewayId incoming, GatewayId outgoing)
        { return Key(building, room) + "\n" + incoming.Value + "\n" + outgoing.Value; }
    }
}
