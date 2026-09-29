using System;

namespace Syms.Simulation.World
{
    [Serializable]
    public struct ChunkId : IEquatable<ChunkId>
    {
        public string Value;
        public ChunkId(string value) { Value = value ?? string.Empty; }
        public bool Equals(ChunkId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is ChunkId && Equals((ChunkId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(ChunkId left, ChunkId right) { return left.Equals(right); }
        public static bool operator !=(ChunkId left, ChunkId right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct BlockId : IEquatable<BlockId>
    {
        public string Value;
        public BlockId(string value) { Value = value ?? string.Empty; }
        public bool Equals(BlockId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is BlockId && Equals((BlockId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(BlockId left, BlockId right) { return left.Equals(right); }
        public static bool operator !=(BlockId left, BlockId right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct PlaceId : IEquatable<PlaceId>
    {
        public string Value;
        public PlaceId(string value) { Value = value ?? string.Empty; }
        public bool Equals(PlaceId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is PlaceId && Equals((PlaceId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(PlaceId left, PlaceId right) { return left.Equals(right); }
        public static bool operator !=(PlaceId left, PlaceId right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct CellId : IEquatable<CellId>
    {
        public string Value;
        public CellId(string value) { Value = value ?? string.Empty; }
        public bool Equals(CellId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is CellId && Equals((CellId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(CellId left, CellId right) { return left.Equals(right); }
        public static bool operator !=(CellId left, CellId right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct AnchorId : IEquatable<AnchorId>
    {
        public string Value;
        public AnchorId(string value) { Value = value ?? string.Empty; }
        public bool Equals(AnchorId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is AnchorId && Equals((AnchorId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(AnchorId left, AnchorId right) { return left.Equals(right); }
        public static bool operator !=(AnchorId left, AnchorId right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct RouteId : IEquatable<RouteId>
    {
        public string Value;
        public RouteId(string value) { Value = value ?? string.Empty; }
        public bool Equals(RouteId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is RouteId && Equals((RouteId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(RouteId left, RouteId right) { return left.Equals(right); }
        public static bool operator !=(RouteId left, RouteId right) { return !left.Equals(right); }
    }
}

