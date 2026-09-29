using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.World;

namespace Syms.World.Baking
{
    /// <summary>Engine-neutral scene facts supplied by the consuming editor adapter.</summary>
    [Serializable]
    public sealed class SceneManagedBuildingBakeRecord
    {
        public BuildingId Building;
        public BuildingId ParentBuilding;
        public BuildingDefinitionId Definition;
        public WorldAddress Address;
        public BuildingOccupancyKind Occupancy;
        public float RatePerDay;
        public bool PopulationEligible = true;
        public DetentionMealSchedule DetentionMeals;
        public bool Enabled = true;
        public List<CapacityOverride> FacilityOverrides = new List<CapacityOverride>();
        public List<CapacityOverride> RoleOverrides = new List<CapacityOverride>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class SceneRoomBakeRecord
    {
        public RoomId Room;
        public RoomDefinitionId Definition;
        public BuildingId Building;
        public PlaceId Place;
        public RoomSceneBinding Scene;
        public AnchorId DefaultAnchor;
        public RoomAccess Access;
        public List<RoomAnchorPlacement> Anchors = new List<RoomAnchorPlacement>();
        public List<RoomFacilityPlacement> Facilities = new List<RoomFacilityPlacement>();
        public List<BuildingRoleTemplate> Positions = new List<BuildingRoleTemplate>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class SceneGatewayBakeRecord
    {
        public GatewayId Gateway;
        public string StableSceneObjectId;
        public GatewayKind Kind;
        public GatewayChannel Channels;
        public GatewayPortalGeometry Portal = new GatewayPortalGeometry();
        public long Revision;
        public List<GatewayArc> DirectedArcs = new List<GatewayArc>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class SceneNavMeshPathBakeRecord
    {
        public RoomPathId Path;
        public RoomId Room;
        public GatewayId IncomingGateway;
        public GatewayId OutgoingGateway;
        public AnchorId IncomingAnchor;
        public AnchorId OutgoingAnchor;
        public float NavMeshDistance;
        public float SlopeMultiplier = 1f;
        public float TerrainMultiplier = 1f;
        public float CongestionMultiplier = 1f;
        public float HazardMultiplier = 1f;
        public double FixedDelaySeconds;
        public string BakedNavMeshPathHandle;
    }

    [Serializable]
    public sealed class SceneWindowFallSurvey
    {
        public GatewayId Gateway;
        public string ArcId;
        public float VerticalDropMetres;
        public LandingSurface Landing;
        public float LandingModifier = 1f;
        public bool LandingReachable;
        public string RequiredClimbingCapability;
    }

    [Serializable]
    public sealed class SceneBuildingBakeInput
    {
        public BuildingGraphId Graph;
        /// <summary>Root manager/site. Every other building is its direct child.</summary>
        public BuildingId Building;
        public long Revision;
        public List<SceneManagedBuildingBakeRecord> Buildings = new List<SceneManagedBuildingBakeRecord>();
        public List<SceneRoomBakeRecord> Rooms = new List<SceneRoomBakeRecord>();
        public List<SceneGatewayBakeRecord> Gateways = new List<SceneGatewayBakeRecord>();
        public List<SceneNavMeshPathBakeRecord> NavMeshPaths = new List<SceneNavMeshPathBakeRecord>();
        public List<SceneWindowFallSurvey> WindowFalls = new List<SceneWindowFallSurvey>();
    }

    [Serializable]
    public sealed class SceneBuildingBakeOutput
    {
        public BuildingRoomGraph Graph;
        public List<BuildingInstance> Buildings = new List<BuildingInstance>();
        public List<RoomInstance> Rooms = new List<RoomInstance>();

        public void ApplyTo(AuthoredTownContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (Graph == null) throw new InvalidOperationException("Compiled building graph is missing.");
            var buildingIds = new HashSet<BuildingId>();
            for (int i = 0; i < Buildings.Count; i++) buildingIds.Add(Buildings[i].Id);
            for (int i = content.BuildingGraphs.Count - 1; i >= 0; i--)
                if (content.BuildingGraphs[i] != null && content.BuildingGraphs[i].Building == Graph.Building)
                {
                    buildingIds.Add(content.BuildingGraphs[i].Building);
                    for (int member = 0; member < content.BuildingGraphs[i].MemberBuildings.Count; member++)
                        buildingIds.Add(content.BuildingGraphs[i].MemberBuildings[member]);
                    content.BuildingGraphs.RemoveAt(i);
                }
            for (int i = content.RoomInstances.Count - 1; i >= 0; i--)
                if (content.RoomInstances[i] != null && buildingIds.Contains(content.RoomInstances[i].Building))
                    content.RoomInstances.RemoveAt(i);
            for (int i = content.BuildingInstances.Count - 1; i >= 0; i--)
                if (content.BuildingInstances[i] != null && buildingIds.Contains(content.BuildingInstances[i].Id))
                    content.BuildingInstances.RemoveAt(i);
            content.BuildingGraphs.Add(Graph);
            content.BuildingInstances.AddRange(Buildings);
            content.RoomInstances.AddRange(Rooms);
        }
    }

    public interface ISceneBuildingTopologySource
    {
        SceneBuildingBakeInput Extract(BuildingId building);
    }

    /// <summary>
    /// Compiles already-extracted scene hierarchy, room volumes, boundary spans,
    /// gateway components, NavMesh paths, and window-fall surveys. Runtime never
    /// performs this discovery.
    /// </summary>
    public sealed class SceneBuildingGraphCompiler
    {
        public SceneBuildingBakeOutput Compile(SceneBuildingBakeInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.Building.Value))
                throw new InvalidOperationException("A scene bake requires a stable root building/site ID.");
            var result = new SceneBuildingBakeOutput
            {
                Graph = new BuildingRoomGraph
                { Id = input.Graph, Building = input.Building, Revision = input.Revision }
            };
            HashSet<BuildingId> buildingIds = CompileBuildings(input, result);
            var roomIds = new HashSet<RoomId>();
            var roomOwners = new Dictionary<RoomId, BuildingId>();
            var sceneObjects = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Rooms.Count; i++)
            {
                SceneRoomBakeRecord source = input.Rooms[i];
                if (source == null || !buildingIds.Contains(source.Building) || !source.Scene.IsDefined
                    || string.IsNullOrEmpty(source.Scene.CullingLayer))
                    throw new InvalidOperationException("Every baked room must belong to the scene's root or direct child building and use a culling layer.");
                string sceneKey = source.Scene.SceneId + "\n" + source.Scene.SceneObjectId;
                if (!roomIds.Add(source.Room) || !sceneObjects.Add(sceneKey))
                    throw new InvalidOperationException("Scene room IDs and scene object IDs must be unique.");
                roomOwners.Add(source.Room, source.Building);
                result.Graph.Rooms.Add(source.Room);
                result.Rooms.Add(new RoomInstance
                {
                    Id = source.Room,
                    Definition = source.Definition,
                    Building = source.Building,
                    Place = source.Place,
                    Scene = source.Scene,
                    DefaultAnchor = source.DefaultAnchor,
                    HasAccessOverride = true,
                    Access = source.Access,
                    Anchors = new List<RoomAnchorPlacement>(source.Anchors),
                    Facilities = new List<RoomFacilityPlacement>(source.Facilities),
                    Positions = new List<BuildingRoleTemplate>(source.Positions),
                    Tags = new List<string>(source.Tags)
                });
            }
            ValidateOccupiedBuildingsHaveRooms(result);

            var gateways = new Dictionary<GatewayId, BuildingGateway>();
            for (int i = 0; i < input.Gateways.Count; i++)
            {
                SceneGatewayBakeRecord source = input.Gateways[i];
                if (source == null || string.IsNullOrEmpty(source.Gateway.Value)
                    || string.IsNullOrEmpty(source.StableSceneObjectId) || gateways.ContainsKey(source.Gateway))
                    throw new InvalidOperationException("Scene gateways require unique stable IDs and scene objects.");
                if (source.Kind == GatewayKind.OpenBoundary
                    && source.Portal.Shape == GatewayPortalShape.Point)
                    throw new InvalidOperationException("Open outdoor boundaries must bake a traversable segment or area, not a point gate.");
                for (int arcIndex = 0; arcIndex < source.DirectedArcs.Count; arcIndex++)
                {
                    GatewayArc arc = source.DirectedArcs[arcIndex];
                    if (arc == null) continue;
                    BuildingId owner;
                    if (!string.IsNullOrEmpty(arc.SourceRoom.Value)
                        && roomOwners.TryGetValue(arc.SourceRoom, out owner)) arc.SourceBuilding = owner;
                    if (!string.IsNullOrEmpty(arc.DestinationRoom.Value)
                        && roomOwners.TryGetValue(arc.DestinationRoom, out owner)) arc.DestinationBuilding = owner;
                }
                var gateway = new BuildingGateway
                {
                    Id = source.Gateway,
                    Kind = source.Kind,
                    Channels = source.Channels,
                    Portal = source.Portal,
                    Revision = source.Revision,
                    Arcs = new List<GatewayArc>(source.DirectedArcs),
                    Tags = new List<string>(source.Tags)
                };
                gateways.Add(gateway.Id, gateway);
                result.Graph.Gateways.Add(gateway);
            }

            ApplyFallSurveys(input.WindowFalls, gateways);
            for (int i = 0; i < input.NavMeshPaths.Count; i++)
            {
                SceneNavMeshPathBakeRecord path = input.NavMeshPaths[i];
                if (path == null || !roomIds.Contains(path.Room) || path.NavMeshDistance < 0f
                    || float.IsNaN(path.NavMeshDistance) || float.IsInfinity(path.NavMeshDistance))
                    throw new InvalidOperationException("Baked NavMesh paths require a valid room and finite distance.");
                result.Graph.RoomPaths.Add(new RoomTraversalPath
                {
                    Id = path.Path,
                    Room = path.Room,
                    IncomingGateway = path.IncomingGateway,
                    OutgoingGateway = path.OutgoingGateway,
                    IncomingAnchor = path.IncomingAnchor,
                    OutgoingAnchor = path.OutgoingAnchor,
                    PhysicalDistance = path.NavMeshDistance,
                    SlopeMultiplier = path.SlopeMultiplier,
                    TerrainMultiplier = path.TerrainMultiplier,
                    CongestionMultiplier = path.CongestionMultiplier,
                    HazardMultiplier = path.HazardMultiplier,
                    FixedDelaySeconds = path.FixedDelaySeconds,
                    LocalizationPathHandle = path.BakedNavMeshPathHandle
                });
            }
            return result;
        }

        private static HashSet<BuildingId> CompileBuildings(SceneBuildingBakeInput input,
            SceneBuildingBakeOutput output)
        {
            var ids = new HashSet<BuildingId>();
            bool foundRoot = false;
            for (int i = 0; i < input.Buildings.Count; i++)
            {
                SceneManagedBuildingBakeRecord source = input.Buildings[i];
                if (source == null || string.IsNullOrEmpty(source.Building.Value) || !ids.Add(source.Building))
                    throw new InvalidOperationException("Scene building IDs must be non-empty and unique.");
                bool root = source.Building == input.Building;
                if (root)
                {
                    if (!string.IsNullOrEmpty(source.ParentBuilding.Value))
                        throw new InvalidOperationException("The root manager/site cannot have a parent.");
                    foundRoot = true;
                }
                else if (source.ParentBuilding != input.Building)
                    throw new InvalidOperationException("Managed building hierarchy permits only direct children of the root.");
                if ((source.Occupancy == BuildingOccupancyKind.LodgingResidence
                    || source.Occupancy == BuildingOccupancyKind.DetentionCell) && root)
                    throw new InvalidOperationException("Lodging residences and detention cells must be managed child buildings.");
                if (source.Occupancy == BuildingOccupancyKind.LodgingResidence && source.PopulationEligible)
                    throw new InvalidOperationException("Lodging child residences must be leased rather than population-filled.");
                if (source.RatePerDay < 0f
                    || float.IsNaN(source.RatePerDay) || float.IsInfinity(source.RatePerDay))
                    throw new InvalidOperationException("Scene building occupancy values are invalid.");
                output.Buildings.Add(new BuildingInstance
                {
                    Id = source.Building,
                    ParentBuilding = source.ParentBuilding,
                    Definition = source.Definition,
                    Address = source.Address,
                    Occupancy = source.Occupancy,
                    RatePerDay = source.RatePerDay,
                    PopulationEligible = source.PopulationEligible,
                    DetentionMeals = source.DetentionMeals,
                    Enabled = source.Enabled,
                    FacilityOverrides = new List<CapacityOverride>(source.FacilityOverrides),
                    RoleOverrides = new List<CapacityOverride>(source.RoleOverrides),
                    Tags = new List<string>(source.Tags)
                });
                if (!root) output.Graph.MemberBuildings.Add(source.Building);
            }
            if (!foundRoot) throw new InvalidOperationException("Scene bake must contain its root manager/site building.");
            return ids;
        }

        private static void ValidateOccupiedBuildingsHaveRooms(SceneBuildingBakeOutput output)
        {
            for (int i = 0; i < output.Buildings.Count; i++)
            {
                BuildingInstance building = output.Buildings[i];
                if (building.Occupancy == BuildingOccupancyKind.None) continue;
                bool found = false;
                for (int room = 0; room < output.Rooms.Count && !found; room++)
                    found = output.Rooms[room] != null && output.Rooms[room].Building == building.Id;
                if (!found)
                    throw new InvalidOperationException("Every occupied building requires at least one authored room.");
            }
        }

        private static void ApplyFallSurveys(List<SceneWindowFallSurvey> surveys,
            Dictionary<GatewayId, BuildingGateway> gateways)
        {
            for (int i = 0; i < surveys.Count; i++)
            {
                SceneWindowFallSurvey survey = surveys[i];
                BuildingGateway gateway;
                if (survey == null || !gateways.TryGetValue(survey.Gateway, out gateway)
                    || gateway.Kind != GatewayKind.Window)
                    throw new InvalidOperationException("Fall surveys must reference a baked window gateway.");
                bool matched = false;
                for (int a = 0; a < gateway.Arcs.Count; a++)
                {
                    GatewayArc arc = gateway.Arcs[a];
                    if (arc == null || !string.Equals(arc.Id, survey.ArcId, StringComparison.Ordinal)) continue;
                    if (!survey.LandingReachable) arc.Requirements.AllowedModes = TravelMode.None;
                    arc.Fall = new GatewayFallProfile
                    {
                        DropMetres = Math.Max(0f, survey.VerticalDropMetres),
                        Landing = survey.Landing,
                        LandingModifier = Math.Max(0f, survey.LandingModifier),
                        RequiredClimbingCapability = survey.RequiredClimbingCapability
                    };
                    matched = true;
                    break;
                }
                if (!matched) throw new InvalidOperationException("Window fall survey references a missing directed arc.");
            }
        }
    }
}
