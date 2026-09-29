using System;
using Syms.Content;
using Syms.Simulation.Travel;
using Syms.Simulation.Buildings;

namespace Syms.Simulation.World
{
    public enum SpatialStateKind { AtPlace, InTransit, Materialized }
    public enum StreamingAction { None, Materialize, Demote }

    [Serializable]
    public sealed class LastObservedPose
    {
        public string SceneId;
        public RoomId Room;
        public RoomSceneBinding RoomScene;
        public AnchorId Anchor;
        public CellId Cell;
        public NormalizedCellPosition CellPosition;
        public Float3 WorldPosition;
        public Float3 EulerRotation;
        public long ActivityRevision;
        public long TopologyVersion;
        public float RouteProgress;

        public bool IsValid(long activityRevision, long topologyVersion, float routeProgress)
        {
            return ActivityRevision == activityRevision
                && TopologyVersion == topologyVersion
                && Math.Abs(RouteProgress - routeProgress) < 0.0001f;
        }
    }

    [Serializable]
    public sealed class SimSpatialState
    {
        public SpatialStateKind Kind = SpatialStateKind.AtPlace;
        public WorldAddress Address;
        public RoomId Room;
        public AnchorId Anchor;
        public TransitState Transit;
        public string MaterializedAgentId;
        public LastObservedPose LastObservedPose;

        public void InvalidateObservedPose() { LastObservedPose = null; }
    }

    public sealed class StreamingAuthorityPolicy
    {
        public int MaterializeDistanceBlocks = 1;
        public int DemoteDistanceBlocks = 2;

        public StreamingAction Evaluate(int distanceInBlocks, bool isMaterialized)
        {
            if (distanceInBlocks < 0) throw new ArgumentOutOfRangeException(nameof(distanceInBlocks));
            if (!isMaterialized && distanceInBlocks <= MaterializeDistanceBlocks) return StreamingAction.Materialize;
            if (isMaterialized && distanceInBlocks > DemoteDistanceBlocks) return StreamingAction.Demote;
            return StreamingAction.None;
        }

        public bool CanCommitObservedPose(int distanceInBlocks) { return distanceInBlocks <= MaterializeDistanceBlocks; }
    }

    public interface ISimMaterializationBridge
    {
        string MaterializeAt(string simId, MaterializationTarget target, LastObservedPose reusablePose);
        void Demote(string materializedAgentId);
    }

    [Serializable]
    public sealed class MaterializationTarget
    {
        public bool IsExterior;
        public WorldAddress ExteriorAddress;
        public NormalizedCellPosition CellPosition;
        public Float3 WorldPosition;
        public RoomId Room;
        public RoomSceneBinding RoomScene;
        public AnchorId Anchor;
        public Float3 RoomLocalPosition;
        public RoomPathId RoomPath;
        public GatewayId Gateway;
        public float PathProgress;
    }
}
