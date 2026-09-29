using System.Collections.Generic;
using NUnit.Framework;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class LooseOctreeTests
    {
        [Test]
        public void QueryReturnsIntersectingRuntimeEntries()
        {
            var tree = new LooseOctree<string>(
                new SpatialBounds(new Float3(0f, 0f, 0f), new Float3(100f, 100f, 100f)), 1, 4, 1.25f);
            tree.Insert("near", new SpatialBounds(new Float3(2f, 2f, 2f), new Float3(1f, 1f, 1f)));
            tree.Insert("far", new SpatialBounds(new Float3(40f, 40f, 40f), new Float3(1f, 1f, 1f)));
            var results = new List<string>();

            tree.Query(new SpatialBounds(new Float3(2f, 2f, 2f), new Float3(5f, 5f, 5f)), results);

            CollectionAssert.AreEquivalent(new[] { "near" }, results);
        }
    }
}

