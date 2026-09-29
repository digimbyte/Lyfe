using System;
using System.Collections.Generic;

namespace Syms.Simulation.World
{
    [Serializable]
    public struct SpatialBounds
    {
        public Float3 Center;
        public Float3 Size;

        public SpatialBounds(Float3 center, Float3 size) { Center = center; Size = size; }

        public bool Intersects(SpatialBounds other)
        {
            return Math.Abs(Center.X - other.Center.X) * 2f <= Size.X + other.Size.X
                && Math.Abs(Center.Y - other.Center.Y) * 2f <= Size.Y + other.Size.Y
                && Math.Abs(Center.Z - other.Center.Z) * 2f <= Size.Z + other.Size.Z;
        }

        public bool Contains(SpatialBounds other)
        {
            return Math.Abs(other.Center.X - Center.X) + (other.Size.X * 0.5f) <= Size.X * 0.5f
                && Math.Abs(other.Center.Y - Center.Y) + (other.Size.Y * 0.5f) <= Size.Y * 0.5f
                && Math.Abs(other.Center.Z - Center.Z) + (other.Size.Z * 0.5f) <= Size.Z * 0.5f;
        }
    }

    public interface ISpatialIndex<T>
    {
        void Insert(T item, SpatialBounds bounds);
        bool Remove(T item);
        void Query(SpatialBounds bounds, IList<T> results);
        void Clear();
    }

    /// <summary>
    /// Runtime-only broad-phase index. Its nodes are deliberately not stable IDs
    /// and must never be included in save data.
    /// </summary>
    public sealed class LooseOctree<T> : ISpatialIndex<T>
    {
        private readonly Node _root;
        private readonly int _capacity;
        private readonly int _maximumDepth;
        private readonly float _looseness;

        public LooseOctree(SpatialBounds worldBounds, int capacity, int maximumDepth, float looseness)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maximumDepth < 1) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            if (looseness < 1f) throw new ArgumentOutOfRangeException(nameof(looseness));
            _capacity = capacity;
            _maximumDepth = maximumDepth;
            _looseness = looseness;
            _root = new Node(worldBounds, 0);
        }

        public void Insert(T item, SpatialBounds bounds)
        {
            if (!_root.Bounds.Contains(bounds)) throw new ArgumentOutOfRangeException(nameof(bounds), "Bounds are outside the octree root.");
            Insert(_root, new Entry(item, bounds));
        }

        public bool Remove(T item) { return Remove(_root, item); }

        public void Query(SpatialBounds bounds, IList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            Query(_root, bounds, results);
        }

        public void Clear()
        {
            _root.Entries.Clear();
            _root.Children = null;
        }

        private void Insert(Node node, Entry entry)
        {
            if (node.Children != null)
            {
                int childIndex = FindContainingChild(node.Children, entry.Bounds);
                if (childIndex >= 0) { Insert(node.Children[childIndex], entry); return; }
            }

            node.Entries.Add(entry);
            if (node.Entries.Count <= _capacity || node.Depth >= _maximumDepth) return;
            if (node.Children == null) node.Children = CreateChildren(node);
            for (int i = node.Entries.Count - 1; i >= 0; i--)
            {
                int childIndex = FindContainingChild(node.Children, node.Entries[i].Bounds);
                if (childIndex < 0) continue;
                Entry moved = node.Entries[i];
                node.Entries.RemoveAt(i);
                Insert(node.Children[childIndex], moved);
            }
        }

        private bool Remove(Node node, T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < node.Entries.Count; i++)
            {
                if (!comparer.Equals(node.Entries[i].Item, item)) continue;
                node.Entries.RemoveAt(i);
                return true;
            }
            if (node.Children == null) return false;
            for (int i = 0; i < node.Children.Length; i++)
                if (Remove(node.Children[i], item)) return true;
            return false;
        }

        private static void Query(Node node, SpatialBounds query, IList<T> results)
        {
            if (!node.Bounds.Intersects(query)) return;
            for (int i = 0; i < node.Entries.Count; i++)
                if (node.Entries[i].Bounds.Intersects(query)) results.Add(node.Entries[i].Item);
            if (node.Children == null) return;
            for (int i = 0; i < node.Children.Length; i++) Query(node.Children[i], query, results);
        }

        private Node[] CreateChildren(Node parent)
        {
            var children = new Node[8];
            Float3 baseSize = new Float3(
                parent.Bounds.Size.X * 0.5f,
                parent.Bounds.Size.Y * 0.5f,
                parent.Bounds.Size.Z * 0.5f);
            Float3 looseSize = new Float3(baseSize.X * _looseness, baseSize.Y * _looseness, baseSize.Z * _looseness);
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -0.25f : 0.25f;
                float y = (i & 2) == 0 ? -0.25f : 0.25f;
                float z = (i & 4) == 0 ? -0.25f : 0.25f;
                var center = new Float3(
                    parent.Bounds.Center.X + (parent.Bounds.Size.X * x),
                    parent.Bounds.Center.Y + (parent.Bounds.Size.Y * y),
                    parent.Bounds.Center.Z + (parent.Bounds.Size.Z * z));
                children[i] = new Node(new SpatialBounds(center, looseSize), parent.Depth + 1);
            }
            return children;
        }

        private static int FindContainingChild(Node[] children, SpatialBounds bounds)
        {
            for (int i = 0; i < children.Length; i++)
                if (children[i].Bounds.Contains(bounds)) return i;
            return -1;
        }

        private sealed class Node
        {
            public readonly SpatialBounds Bounds;
            public readonly int Depth;
            public readonly List<Entry> Entries = new List<Entry>();
            public Node[] Children;
            public Node(SpatialBounds bounds, int depth) { Bounds = bounds; Depth = depth; }
        }

        private struct Entry
        {
            public T Item;
            public SpatialBounds Bounds;
            public Entry(T item, SpatialBounds bounds) { Item = item; Bounds = bounds; }
        }
    }
}
