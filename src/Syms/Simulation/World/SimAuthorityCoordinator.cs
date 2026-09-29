using System;
using Syms.Content;
using Syms.Simulation.People;
using Syms.Simulation.Travel;

namespace Syms.Simulation.World
{
    public interface IAnchorProvider
    {
        bool TryGetObjectAnchor(ObjectId objectId, out MaterializationTarget target);
        bool TryGetRoomTarget(RoomId room, out MaterializationTarget target);
        bool TryGetActivityAnchor(RoomId room, AnchorId preferred, out MaterializationTarget target);
        bool TryGetEntranceAnchor(PlaceId place, out MaterializationTarget target);
    }

    public sealed class SimAuthorityCoordinator
    {
        private readonly IWorldTopology _topology;
        private readonly ISimMaterializationBridge _bridge;
        private readonly IAnchorProvider _anchors;
        private readonly StreamingAuthorityPolicy _policy;

        public SimAuthorityCoordinator(
            IWorldTopology topology,
            ISimMaterializationBridge bridge,
            IAnchorProvider anchors,
            StreamingAuthorityPolicy policy)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public StreamingAction Update(Sim sim, int distanceInBlocks)
        {
            return Update(sim, distanceInBlocks, sim == null ? 0d : sim.LastSimulatedAt);
        }

        public StreamingAction Update(Sim sim, int distanceInBlocks, double cityTime)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            bool materialized = sim.Spatial.Kind == SpatialStateKind.Materialized;
            StreamingAction action = _policy.Evaluate(distanceInBlocks, materialized);
            if (action == StreamingAction.Materialize)
            {
                sim.GatewayKnowledge.ExpireIfDue(cityTime);
                sim.GatewayKnowledge.CancelEphemeralExpiry();
                Materialize(sim);
            }
            else if (action == StreamingAction.Demote)
            {
                Demote(sim);
                sim.GatewayKnowledge.BeginEphemeralExpiry(cityTime);
            }
            return action;
        }

        public bool CommitObservedPose(Sim sim, int distanceInBlocks, LastObservedPose pose)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            if (!_policy.CanCommitObservedPose(distanceInBlocks)) return false;
            if (sim.Spatial.Kind != SpatialStateKind.Materialized) return false;
            TraversalCell observedCell;
            if (!string.IsNullOrEmpty(pose.Cell.Value) && !_topology.TryGetCell(pose.Cell, out observedCell)) return false;
            sim.Spatial.LastObservedPose = pose;
            if (!string.IsNullOrEmpty(pose.Cell.Value))
            {
                sim.Spatial.Address.Chunk = observedCell.Chunk;
                sim.Spatial.Address.Block = observedCell.Block;
                sim.Spatial.Address.Place = observedCell.Place;
                sim.Spatial.Address.Cell = pose.Cell;
            }
            if (!string.IsNullOrEmpty(pose.Room.Value)) sim.Spatial.Room = pose.Room;
            if (!string.IsNullOrEmpty(pose.Anchor.Value)) sim.Spatial.Anchor = pose.Anchor;
            if (sim.Spatial.Transit != null)
            {
                if (sim.Spatial.Transit.Domain == TransitDomain.ExteriorCell)
                {
                    sim.Spatial.Transit.CurrentCell = pose.Cell;
                    sim.Spatial.Transit.Position = pose.CellPosition;
                }
                else if (!string.IsNullOrEmpty(pose.Room.Value))
                {
                    sim.Spatial.Transit.CurrentRoom = pose.Room;
                }
                sim.Spatial.Transit.JourneyProgress = pose.RouteProgress;
            }
            return true;
        }

        private void Materialize(Sim sim)
        {
            var target = new MaterializationTarget { Room = sim.Spatial.Room, Anchor = sim.Spatial.Anchor };
            float routeProgress = 0f;
            if (sim.Spatial.Kind == SpatialStateKind.InTransit && sim.Spatial.Transit != null)
            {
                if (sim.Spatial.Transit.Domain == TransitDomain.ExteriorCell)
                {
                    target.IsExterior = true;
                    target.ExteriorAddress = sim.Spatial.Address;
                    target.ExteriorAddress.Cell = sim.Spatial.Transit.CurrentCell;
                    TraversalCell transitCell;
                    if (_topology.TryGetCell(target.ExteriorAddress.Cell, out transitCell))
                    {
                        target.ExteriorAddress.Chunk = transitCell.Chunk;
                        target.ExteriorAddress.Block = transitCell.Block;
                        target.ExteriorAddress.Place = transitCell.Place;
                        target.WorldPosition = transitCell.Bounds.ToWorld(sim.Spatial.Transit.Position);
                    }
                    target.CellPosition = sim.Spatial.Transit.Position;
                }
                else
                {
                    MaterializationTarget roomTarget;
                    if (_anchors.TryGetRoomTarget(sim.Spatial.Transit.CurrentRoom, out roomTarget)) target = roomTarget;
                    else target.Room = sim.Spatial.Transit.CurrentRoom;
                    target.RoomPath = sim.Spatial.Transit.CurrentRoomPath;
                    target.Gateway = sim.Spatial.Transit.CurrentGateway;
                    target.PathProgress = sim.Spatial.Transit.SegmentProgress;
                }
                routeProgress = sim.Spatial.Transit.JourneyProgress;
            }
            else if (sim.Activity != null && !string.IsNullOrEmpty(sim.Activity.Object.Value)
                && _anchors.TryGetObjectAnchor(sim.Activity.Object, out target))
            {
            }
            else if (!_anchors.TryGetActivityAnchor(sim.Spatial.Room, sim.Spatial.Anchor, out target)
                && !_anchors.TryGetEntranceAnchor(sim.Spatial.Address.Place, out target))
            {
                target.Room = sim.Spatial.Room;
                target.Anchor = sim.Spatial.Anchor;
            }

            LastObservedPose pose = sim.Spatial.LastObservedPose;
            long revision = sim.Activity == null ? 0 : sim.Activity.Revision;
            if (pose != null && !pose.IsValid(revision, _topology.Version, routeProgress)) pose = null;
            if (pose != null && !string.IsNullOrEmpty(pose.Room.Value) && pose.Room != sim.Spatial.Room) pose = null;
            string agentId = _bridge.MaterializeAt(sim.Id, target, pose);
            if (target.IsExterior) sim.Spatial.Address = target.ExteriorAddress;
            sim.Spatial.Room = target.Room;
            sim.Spatial.Anchor = target.Anchor;
            sim.Spatial.MaterializedAgentId = agentId;
            sim.Spatial.Kind = SpatialStateKind.Materialized;
        }

        private void Demote(Sim sim)
        {
            if (!string.IsNullOrEmpty(sim.Spatial.MaterializedAgentId))
                _bridge.Demote(sim.Spatial.MaterializedAgentId);
            sim.Spatial.MaterializedAgentId = null;
            sim.Spatial.Anchor = new AnchorId();
            sim.Spatial.Kind = sim.Spatial.Transit == null ? SpatialStateKind.AtPlace : SpatialStateKind.InTransit;
        }
    }
}
