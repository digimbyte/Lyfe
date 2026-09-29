using System;

namespace Syms.Simulation.World
{
    [Serializable]
    public struct Float3 : IEquatable<Float3>
    {
        public float X;
        public float Y;
        public float Z;

        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static float Distance(Float3 a, Float3 b)
        {
            float x = a.X - b.X;
            float y = a.Y - b.Y;
            float z = a.Z - b.Z;
            return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        public static Float3 Lerp(Float3 from, Float3 to, float progress)
        {
            float t = Clamp01(progress);
            return new Float3(
                from.X + ((to.X - from.X) * t),
                from.Y + ((to.Y - from.Y) * t),
                from.Z + ((to.Z - from.Z) * t));
        }

        public bool Equals(Float3 other) { return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z); }
        public override bool Equals(object obj) { return obj is Float3 && Equals((Float3)obj); }
        public override int GetHashCode() { return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2); }
        private static float Clamp01(float value) { return value < 0f ? 0f : (value > 1f ? 1f : value); }
    }

    [Serializable]
    public struct NormalizedCellPosition : IEquatable<NormalizedCellPosition>
    {
        public float X;
        public float Y;
        public float Z;

        public NormalizedCellPosition(float x, float y, float z)
        {
            X = Clamp(x, nameof(x));
            Y = Clamp(y, nameof(y));
            Z = Clamp(z, nameof(z));
        }

        public static NormalizedCellPosition Lerp(NormalizedCellPosition from, NormalizedCellPosition to, float progress)
        {
            float t = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            return new NormalizedCellPosition(
                from.X + ((to.X - from.X) * t),
                from.Y + ((to.Y - from.Y) * t),
                from.Z + ((to.Z - from.Z) * t));
        }

        public bool Equals(NormalizedCellPosition other) { return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z); }
        public override bool Equals(object obj) { return obj is NormalizedCellPosition && Equals((NormalizedCellPosition)obj); }
        public override int GetHashCode() { return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2); }

        private static float Clamp(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }
    }

    [Serializable]
    public struct CellBounds
    {
        public Float3 Minimum;
        public Float3 Size;

        public CellBounds(Float3 minimum, Float3 size)
        {
            if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
                throw new ArgumentOutOfRangeException(nameof(size), "All cell dimensions must be positive.");
            Minimum = minimum;
            Size = size;
        }

        public Float3 ToWorld(NormalizedCellPosition position)
        {
            return new Float3(
                Minimum.X + (Size.X * position.X),
                Minimum.Y + (Size.Y * position.Y),
                Minimum.Z + (Size.Z * position.Z));
        }

        public NormalizedCellPosition ToNormalized(Float3 worldPosition)
        {
            return new NormalizedCellPosition(
                (worldPosition.X - Minimum.X) / Size.X,
                (worldPosition.Y - Minimum.Y) / Size.Y,
                (worldPosition.Z - Minimum.Z) / Size.Z);
        }
    }
}

