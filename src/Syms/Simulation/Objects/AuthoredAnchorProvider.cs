using System;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.World;

namespace Syms.Simulation.Objects
{
    /// <summary>Resolves logical room anchors only when a Unity agent materializes.</summary>
    public sealed class AuthoredAnchorProvider : IAnchorProvider
    {
        private readonly AuthoredTownRuntime _town;

        public AuthoredAnchorProvider(AuthoredTownRuntime town)
        { _town = town ?? throw new ArgumentNullException(nameof(town)); }

        public bool TryGetObjectAnchor(ObjectId objectId, out MaterializationTarget target)
        {
            InteractiveObjectState value = _town.Objects.Find(objectId);
            if (value == null || !value.Enabled)
            { target = null; return false; }
            target = new MaterializationTarget
            {
                Room = value.Room,
                Anchor = value.Anchor,
                RoomLocalPosition = value.LocalPosition
            };
            RoomRuntimeInstance room = _town.FindRoom(value.Room);
            if (room != null) target.RoomScene = room.Scene;
            return true;
        }

        public bool TryGetRoomTarget(RoomId roomId, out MaterializationTarget target)
        {
            RoomRuntimeInstance room = _town.FindRoom(roomId);
            if (room == null) { target = null; return false; }
            target = new MaterializationTarget { Room = room.Id, RoomScene = room.Scene };
            return true;
        }

        public bool TryGetActivityAnchor(RoomId roomId, AnchorId preferred,
            out MaterializationTarget target)
        {
            RoomRuntimeInstance room = _town.FindRoom(roomId);
            if (room == null) { target = null; return false; }
            RoomAnchorPlacement anchor = !string.IsNullOrEmpty(preferred.Value)
                ? room.FindAnchor(preferred) : null;
            if (anchor == null && !string.IsNullOrEmpty(room.DefaultAnchor.Value))
                anchor = room.FindAnchor(room.DefaultAnchor);
            if (anchor == null)
            {
                for (int i = 0; i < room.Anchors.Count; i++)
                    if (room.Anchors[i] != null && (room.Anchors[i].Kind == RoomAnchorKind.Activity
                        || room.Anchors[i].Kind == RoomAnchorKind.Materialization))
                    { anchor = room.Anchors[i]; break; }
            }
            if (anchor == null) { target = null; return false; }
            target = new MaterializationTarget
            { Room = room.Id, RoomScene = room.Scene, Anchor = anchor.Id, RoomLocalPosition = anchor.LocalPosition };
            return true;
        }

        public bool TryGetEntranceAnchor(PlaceId place, out MaterializationTarget target)
        {
            for (int r = 0; r < _town.Rooms.Count; r++)
            {
                RoomRuntimeInstance room = _town.Rooms[r];
                if (room == null || room.Place != place) continue;
                for (int a = 0; a < room.Anchors.Count; a++)
                {
                    RoomAnchorPlacement anchor = room.Anchors[a];
                    if (anchor == null || anchor.Kind != RoomAnchorKind.Entrance) continue;
                    target = new MaterializationTarget
                    { Room = room.Id, RoomScene = room.Scene, Anchor = anchor.Id, RoomLocalPosition = anchor.LocalPosition };
                    return true;
                }
            }
            target = null;
            return false;
        }
    }
}
