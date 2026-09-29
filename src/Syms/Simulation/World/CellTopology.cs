using System;
using System.Collections.Generic;

namespace Syms.Simulation.World
{
    public enum BlockKind { Unknown, Road, Property, Building, Park, Waterway, Civic, Industrial }
    public enum CellExitDirection { None, NegativeX, PositiveX, NegativeY, PositiveY, NegativeZ, PositiveZ }
    public enum PortalKind { Open, Door, Stairs, Lift, Ladder, Bridge, Crossing }

    [Flags]
    public enum TravelMode
    {
        None = 0,
        Walk = 1,
        Run = 2,
        Wheelchair = 4,
        Bicycle = 8,
        Vehicle = 16,
        Swim = 32,
        All = Walk | Run | Wheelchair | Bicycle | Vehicle | Swim
    }

    [Flags]
    public enum ConnectionBlocker
    {
        None = 0,
        Wall = 1,
        River = 2,
        Locked = 4,
        Fence = 8,
        Hazard = 16,
        Closed = 32,
        DestinationUnavailable = 64
    }

    [Serializable]
    public struct GridCoordinate2 : IEquatable<GridCoordinate2>
    {
        public int X;
        public int Y;
        public GridCoordinate2(int x, int y) { X = x; Y = y; }
        public bool Equals(GridCoordinate2 other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is GridCoordinate2 && Equals((GridCoordinate2)obj); }
        public override int GetHashCode() { return X ^ (Y << 16); }
        public static int ManhattanDistance(GridCoordinate2 a, GridCoordinate2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }

    [Serializable]
    public sealed class CityChunk
    {
        public ChunkId Id;
        public GridCoordinate2 Coordinate;
        public string SceneId;
    }

    [Serializable]
    public sealed class CityBlock
    {
        public BlockId Id;
        public ChunkId Chunk;
        public GridCoordinate2 Coordinate;
        public BlockKind Kind;
    }

    [Serializable]
    public sealed class TraversalCell
    {
        public CellId Id;
        public ChunkId Chunk;
        public BlockId Block;
        public PlaceId Place;
        public CellBounds Bounds;

        public TraversalCell(CellId id, ChunkId chunk, BlockId block, PlaceId place, CellBounds bounds)
        {
            Id = id;
            Chunk = chunk;
            Block = block;
            Place = place;
            Bounds = bounds;
        }
    }

    [Serializable]
    public sealed class CellConnection
    {
        public string Id;
        public CellId Source;
        public CellId Destination;
        public CellExitDirection Direction;
        public PortalKind Portal;
        public NormalizedCellPosition SourceExit;
        public NormalizedCellPosition DestinationEntry;
        public TravelMode AllowedModes = TravelMode.Walk | TravelMode.Run;
        public string RequiredCapability;
        public ConnectionBlocker Blockers;
        public float EstimatedDistance;
        public float TerrainMultiplier = 1f;
        public float SlopeMultiplier = 1f;
        public float CongestionMultiplier = 1f;
        public float HazardMultiplier = 1f;
        public double FixedDelaySeconds;

        public CellConnection(
            string id,
            CellId source,
            CellId destination,
            CellExitDirection direction,
            NormalizedCellPosition sourceExit,
            NormalizedCellPosition destinationEntry)
        {
            if (direction == CellExitDirection.None)
                throw new ArgumentException("A connection must use an explicit cell face.", nameof(direction));
            Id = id ?? string.Empty;
            Source = source;
            Destination = destination;
            Direction = direction;
            SourceExit = sourceExit;
            DestinationEntry = destinationEntry;
            ValidateFace(direction, sourceExit, destinationEntry);
        }

        public bool CanTraverse(TravelMode mode)
        {
            return CanTraverse(mode, null);
        }

        public bool CanTraverse(TravelMode mode, IList<string> capabilities)
        {
            ConnectionBlocker effectiveBlockers = Portal == PortalKind.Door
                ? Blockers & ~ConnectionBlocker.Closed
                : Blockers;
            if (effectiveBlockers != ConnectionBlocker.None || mode == TravelMode.None || (AllowedModes & mode) == 0) return false;
            if (string.IsNullOrEmpty(RequiredCapability)) return true;
            if (capabilities == null) return false;
            for (int i = 0; i < capabilities.Count; i++)
                if (string.Equals(capabilities[i], RequiredCapability, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void ValidateFace(
            CellExitDirection direction,
            NormalizedCellPosition sourceExit,
            NormalizedCellPosition destinationEntry)
        {
            const float tolerance = 0.0001f;
            bool valid;
            switch (direction)
            {
                case CellExitDirection.NegativeX: valid = sourceExit.X <= tolerance && destinationEntry.X >= 1f - tolerance; break;
                case CellExitDirection.PositiveX: valid = sourceExit.X >= 1f - tolerance && destinationEntry.X <= tolerance; break;
                case CellExitDirection.NegativeY: valid = sourceExit.Y <= tolerance && destinationEntry.Y >= 1f - tolerance; break;
                case CellExitDirection.PositiveY: valid = sourceExit.Y >= 1f - tolerance && destinationEntry.Y <= tolerance; break;
                case CellExitDirection.NegativeZ: valid = sourceExit.Z <= tolerance && destinationEntry.Z >= 1f - tolerance; break;
                case CellExitDirection.PositiveZ: valid = sourceExit.Z >= 1f - tolerance && destinationEntry.Z <= tolerance; break;
                default: valid = false; break;
            }
            if (!valid) throw new ArgumentException("Entry and exit positions must lie on opposite faces for the selected direction.");
        }
    }

    public interface IWorldTopology
    {
        long Version { get; }
        bool TryGetBlock(BlockId id, out CityBlock block);
        bool TryGetCell(CellId id, out TraversalCell cell);
        IReadOnlyList<CellConnection> GetConnections(CellId source);
    }

    [Serializable]
    public sealed class WorldTopology : IWorldTopology
    {
        public List<TraversalCell> Cells = new List<TraversalCell>();
        public List<CityChunk> Chunks = new List<CityChunk>();
        public List<CityBlock> Blocks = new List<CityBlock>();
        public List<CellConnection> Connections = new List<CellConnection>();
        public long Revision;

        [NonSerialized] private Dictionary<CellId, TraversalCell> _cells;
        [NonSerialized] private Dictionary<ChunkId, CityChunk> _chunks;
        [NonSerialized] private Dictionary<BlockId, CityBlock> _blocks;
        [NonSerialized] private Dictionary<CellId, List<CellConnection>> _connections;

        public long Version { get { return Revision; } }

        public void AddOrReplaceChunk(CityChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            EnsureIndexes();
            CityChunk previous;
            if (_chunks.TryGetValue(chunk.Id, out previous)) Chunks.Remove(previous);
            Chunks.Add(chunk);
            _chunks[chunk.Id] = chunk;
            Revision++;
        }

        public void AddOrReplaceBlock(CityBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            EnsureIndexes();
            CityBlock previous;
            if (_blocks.TryGetValue(block.Id, out previous)) Blocks.Remove(previous);
            Blocks.Add(block);
            _blocks[block.Id] = block;
            Revision++;
        }

        public void AddOrReplaceCell(TraversalCell cell)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));
            EnsureIndexes();
            TraversalCell previous;
            if (_cells.TryGetValue(cell.Id, out previous)) Cells.Remove(previous);
            Cells.Add(cell);
            _cells[cell.Id] = cell;
            Revision++;
        }

        public void AddConnection(CellConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            EnsureIndexes();
            if (!_cells.ContainsKey(connection.Source) || !_cells.ContainsKey(connection.Destination))
                throw new InvalidOperationException("Both connection cells must exist in the topology.");

            List<CellConnection> list;
            if (!_connections.TryGetValue(connection.Source, out list))
            {
                list = new List<CellConnection>();
                _connections.Add(connection.Source, list);
            }

            list.Add(connection);
            Connections.Add(connection);
            Revision++;
        }

        public bool SetBlockers(string connectionId, ConnectionBlocker blockers)
        {
            EnsureIndexes();
            foreach (KeyValuePair<CellId, List<CellConnection>> pair in _connections)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    if (string.Equals(pair.Value[i].Id, connectionId, StringComparison.Ordinal))
                    {
                        if (pair.Value[i].Blockers == blockers) return true;
                        pair.Value[i].Blockers = blockers;
                        Revision++;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool SetBlocker(string connectionId, ConnectionBlocker blocker, bool enabled)
        {
            EnsureIndexes();
            for (int i = 0; i < Connections.Count; i++)
            {
                CellConnection connection = Connections[i];
                if (connection == null || !string.Equals(connection.Id, connectionId, StringComparison.Ordinal)) continue;
                ConnectionBlocker next = enabled
                    ? connection.Blockers | blocker
                    : connection.Blockers & ~blocker;
                if (next != connection.Blockers)
                {
                    connection.Blockers = next;
                    Revision++;
                }
                return true;
            }
            return false;
        }

        public bool RemoveConnection(string connectionId)
        {
            EnsureIndexes();
            foreach (KeyValuePair<CellId, List<CellConnection>> pair in _connections)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    if (!string.Equals(pair.Value[i].Id, connectionId, StringComparison.Ordinal)) continue;
                    CellConnection removed = pair.Value[i];
                    pair.Value.RemoveAt(i);
                    Connections.Remove(removed);
                    Revision++;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetConnection(string connectionId, out CellConnection connection)
        {
            EnsureIndexes();
            for (int i = 0; i < Connections.Count; i++)
            {
                CellConnection candidate = Connections[i];
                if (candidate != null && string.Equals(candidate.Id, connectionId, StringComparison.Ordinal))
                {
                    connection = candidate;
                    return true;
                }
            }
            connection = null;
            return false;
        }

        public bool TryGetBlock(BlockId id, out CityBlock block) { EnsureIndexes(); return _blocks.TryGetValue(id, out block); }

        public int GetBlockDistance(BlockId from, BlockId to)
        {
            EnsureIndexes();
            CityBlock first;
            CityBlock second;
            if (!_blocks.TryGetValue(from, out first) || !_blocks.TryGetValue(to, out second)) return int.MaxValue;
            return GridCoordinate2.ManhattanDistance(first.Coordinate, second.Coordinate);
        }

        public bool TryGetCell(CellId id, out TraversalCell cell) { EnsureIndexes(); return _cells.TryGetValue(id, out cell); }

        public IReadOnlyList<CellConnection> GetConnections(CellId source)
        {
            EnsureIndexes();
            List<CellConnection> result;
            return _connections.TryGetValue(source, out result) ? result : EmptyConnections.Value;
        }

        private void EnsureIndexes()
        {
            if (_cells != null && _cells.Count == Cells.Count
                && _chunks != null && _chunks.Count == Chunks.Count
                && _blocks != null && _blocks.Count == Blocks.Count
                && CountIndexedConnections() == Connections.Count) return;

            _cells = new Dictionary<CellId, TraversalCell>();
            _chunks = new Dictionary<ChunkId, CityChunk>();
            _blocks = new Dictionary<BlockId, CityBlock>();
            _connections = new Dictionary<CellId, List<CellConnection>>();
            for (int i = 0; i < Cells.Count; i++) if (Cells[i] != null) _cells[Cells[i].Id] = Cells[i];
            for (int i = 0; i < Chunks.Count; i++) if (Chunks[i] != null) _chunks[Chunks[i].Id] = Chunks[i];
            for (int i = 0; i < Blocks.Count; i++) if (Blocks[i] != null) _blocks[Blocks[i].Id] = Blocks[i];
            for (int i = 0; i < Connections.Count; i++)
            {
                CellConnection connection = Connections[i];
                if (connection == null) continue;
                List<CellConnection> list;
                if (!_connections.TryGetValue(connection.Source, out list))
                {
                    list = new List<CellConnection>();
                    _connections.Add(connection.Source, list);
                }
                list.Add(connection);
            }
        }

        private int CountIndexedConnections()
        {
            if (_connections == null) return -1;
            int count = 0;
            foreach (List<CellConnection> list in _connections.Values) count += list.Count;
            return count;
        }

        private static class EmptyConnections
        {
            public static readonly IReadOnlyList<CellConnection> Value = new CellConnection[0];
        }
    }
}
