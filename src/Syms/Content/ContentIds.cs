using System;

namespace Syms.Content
{
    [Serializable]
    public struct DetentionId : IEquatable<DetentionId>
    {
        public string Value;
        public DetentionId(string value) { Value = value ?? string.Empty; }
        public bool Equals(DetentionId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is DetentionId && Equals((DetentionId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(DetentionId a, DetentionId b) { return a.Equals(b); }
        public static bool operator !=(DetentionId a, DetentionId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct GatewayId : IEquatable<GatewayId>
    {
        public string Value;
        public GatewayId(string value) { Value = value ?? string.Empty; }
        public bool Equals(GatewayId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is GatewayId && Equals((GatewayId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(GatewayId a, GatewayId b) { return a.Equals(b); }
        public static bool operator !=(GatewayId a, GatewayId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct BuildingGraphId : IEquatable<BuildingGraphId>
    {
        public string Value;
        public BuildingGraphId(string value) { Value = value ?? string.Empty; }
        public bool Equals(BuildingGraphId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is BuildingGraphId && Equals((BuildingGraphId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(BuildingGraphId a, BuildingGraphId b) { return a.Equals(b); }
        public static bool operator !=(BuildingGraphId a, BuildingGraphId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct RoomPathId : IEquatable<RoomPathId>
    {
        public string Value;
        public RoomPathId(string value) { Value = value ?? string.Empty; }
        public bool Equals(RoomPathId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is RoomPathId && Equals((RoomPathId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(RoomPathId a, RoomPathId b) { return a.Equals(b); }
        public static bool operator !=(RoomPathId a, RoomPathId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct ObjectDefinitionId : IEquatable<ObjectDefinitionId>
    {
        public string Value;
        public ObjectDefinitionId(string value) { Value = value ?? string.Empty; }
        public bool Equals(ObjectDefinitionId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is ObjectDefinitionId && Equals((ObjectDefinitionId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(ObjectDefinitionId a, ObjectDefinitionId b) { return a.Equals(b); }
        public static bool operator !=(ObjectDefinitionId a, ObjectDefinitionId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct ObjectId : IEquatable<ObjectId>
    {
        public string Value;
        public ObjectId(string value) { Value = value ?? string.Empty; }
        public bool Equals(ObjectId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is ObjectId && Equals((ObjectId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(ObjectId a, ObjectId b) { return a.Equals(b); }
        public static bool operator !=(ObjectId a, ObjectId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct AffordanceSlotId : IEquatable<AffordanceSlotId>
    {
        public string Value;
        public AffordanceSlotId(string value) { Value = value ?? string.Empty; }
        public bool Equals(AffordanceSlotId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is AffordanceSlotId && Equals((AffordanceSlotId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(AffordanceSlotId a, AffordanceSlotId b) { return a.Equals(b); }
        public static bool operator !=(AffordanceSlotId a, AffordanceSlotId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct RoomDefinitionId : IEquatable<RoomDefinitionId>
    {
        public string Value;
        public RoomDefinitionId(string value) { Value = value ?? string.Empty; }
        public bool Equals(RoomDefinitionId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is RoomDefinitionId && Equals((RoomDefinitionId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(RoomDefinitionId a, RoomDefinitionId b) { return a.Equals(b); }
        public static bool operator !=(RoomDefinitionId a, RoomDefinitionId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct RoomId : IEquatable<RoomId>
    {
        public string Value;
        public RoomId(string value) { Value = value ?? string.Empty; }
        public bool Equals(RoomId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is RoomId && Equals((RoomId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(RoomId a, RoomId b) { return a.Equals(b); }
        public static bool operator !=(RoomId a, RoomId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct BuildingDefinitionId : IEquatable<BuildingDefinitionId>
    {
        public string Value;
        public BuildingDefinitionId(string value) { Value = value ?? string.Empty; }
        public bool Equals(BuildingDefinitionId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is BuildingDefinitionId && Equals((BuildingDefinitionId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(BuildingDefinitionId a, BuildingDefinitionId b) { return a.Equals(b); }
        public static bool operator !=(BuildingDefinitionId a, BuildingDefinitionId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct BuildingId : IEquatable<BuildingId>
    {
        public string Value;
        public BuildingId(string value) { Value = value ?? string.Empty; }
        public bool Equals(BuildingId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is BuildingId && Equals((BuildingId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(BuildingId a, BuildingId b) { return a.Equals(b); }
        public static bool operator !=(BuildingId a, BuildingId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct JobDefinitionId : IEquatable<JobDefinitionId>
    {
        public string Value;
        public JobDefinitionId(string value) { Value = value ?? string.Empty; }
        public bool Equals(JobDefinitionId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is JobDefinitionId && Equals((JobDefinitionId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(JobDefinitionId a, JobDefinitionId b) { return a.Equals(b); }
        public static bool operator !=(JobDefinitionId a, JobDefinitionId b) { return !a.Equals(b); }
    }

    [Serializable]
    public struct RoleSlotId : IEquatable<RoleSlotId>
    {
        public string Value;
        public RoleSlotId(string value) { Value = value ?? string.Empty; }
        public bool Equals(RoleSlotId other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is RoleSlotId && Equals((RoleSlotId)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(RoleSlotId a, RoleSlotId b) { return a.Equals(b); }
        public static bool operator !=(RoleSlotId a, RoleSlotId b) { return !a.Equals(b); }
    }
}
