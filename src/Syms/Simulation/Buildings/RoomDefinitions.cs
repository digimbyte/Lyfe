using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    public enum RoomEnvironment
    {
        Indoor,
        Outdoor,
        CoveredOutdoor,
        Underground
    }

    public enum RoomAccess
    {
        Public,
        ResidentsAndGuests,
        StaffOnly,
        Private,
        Restricted,
        Secure
    }

    public enum RoomCategory
    {
        Residential,
        Lodging,
        FoodAndDrink,
        Education,
        Childcare,
        Civic,
        LawAndOrder,
        Medical,
        ElderCare,
        Funerary,
        Religious,
        Commerce,
        Trade,
        Agriculture,
        Industry,
        Entertainment,
        Transport,
        PublicSpace,
        Utility,
        Storage
    }

    public enum RoomAnchorKind
    {
        Entrance,
        Exit,
        Materialization,
        Activity,
        Work,
        Rest,
        Social,
        Queue,
        Service,
        Observation,
        Emergency,
        Quest
    }

    [Serializable]
    public sealed class RoomFacilityDefinition
    {
        public string LocalId;
        public FacilityKind Kind;
        public int SuggestedCapacity = 1;
        public bool Optional;
    }

    /// <summary>
    /// Reusable semantic archetype. It never creates geometry, capacity, or jobs.
    /// </summary>
    [Serializable]
    public sealed class RoomDefinition
    {
        public RoomDefinitionId Id;
        public string DisplayName;
        public RoomCategory Category;
        public RoomEnvironment Environment = RoomEnvironment.Indoor;
        public RoomAccess DefaultAccess = RoomAccess.Public;
        public List<ActivityKind> SupportedActivities = new List<ActivityKind>();
        public List<RoomFacilityDefinition> SuggestedFacilities = new List<RoomFacilityDefinition>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class RoomAnchorPlacement
    {
        public AnchorId Id;
        public RoomAnchorKind Kind;
        public Float3 LocalPosition;
        public ActivityKind Activity;
        public int Capacity = 1;
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class RoomFacilityPlacement
    {
        public string LocalId;
        public FacilityKind Kind;
        public int Capacity = 1;
        public AnchorId Anchor;
    }

    [Serializable]
    public struct RoomSceneBinding : IEquatable<RoomSceneBinding>
    {
        public string SceneId;
        public string CullingLayer;
        public string SceneObjectId;

        public RoomSceneBinding(string sceneId, string cullingLayer, string sceneObjectId)
        {
            SceneId = sceneId ?? string.Empty;
            CullingLayer = cullingLayer ?? string.Empty;
            SceneObjectId = sceneObjectId ?? string.Empty;
        }

        public bool IsDefined { get { return !string.IsNullOrEmpty(SceneId) && !string.IsNullOrEmpty(SceneObjectId); } }
        public bool Equals(RoomSceneBinding other)
        {
            return string.Equals(SceneId, other.SceneId, StringComparison.Ordinal)
                && string.Equals(CullingLayer, other.CullingLayer, StringComparison.Ordinal)
                && string.Equals(SceneObjectId, other.SceneObjectId, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return obj is RoomSceneBinding && Equals((RoomSceneBinding)obj); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SceneId == null ? 0 : SceneId.GetHashCode();
                hash = (hash * 397) ^ (CullingLayer == null ? 0 : CullingLayer.GetHashCode());
                return (hash * 397) ^ (SceneObjectId == null ? 0 : SceneObjectId.GetHashCode());
            }
        }
    }

    /// <summary>
    /// A designer-authored logical room or outdoor civic space. Building interiors
    /// route only by RoomId, anchors, paths, and gateways. No geometry is discovered
    /// or generated from this data at runtime.
    /// </summary>
    [Serializable]
    public sealed class RoomInstance
    {
        public RoomId Id;
        public RoomDefinitionId Definition;
        public BuildingId Building;
        public PlaceId Place;
        public RoomSceneBinding Scene;
        public AnchorId DefaultAnchor;
        public bool HasAccessOverride;
        public RoomAccess Access;
        public bool Enabled = true;
        public List<RoomAnchorPlacement> Anchors = new List<RoomAnchorPlacement>();
        public List<RoomFacilityPlacement> Facilities = new List<RoomFacilityPlacement>();
        public List<BuildingRoleTemplate> Positions = new List<BuildingRoleTemplate>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class RoomRuntimeInstance
    {
        public RoomId Id;
        public RoomDefinitionId Definition;
        public BuildingId Building;
        public PlaceId Place;
        public RoomSceneBinding Scene;
        public AnchorId DefaultAnchor;
        public RoomAccess Access;
        public List<RoomAnchorPlacement> Anchors = new List<RoomAnchorPlacement>();
        public List<string> Tags = new List<string>();

        public RoomAnchorPlacement FindAnchor(AnchorId id)
        {
            for (int i = 0; i < Anchors.Count; i++)
                if (Anchors[i] != null && Anchors[i].Id == id) return Anchors[i];
            return null;
        }
    }
}
