using System;

namespace Syms.Simulation.World
{
    [Serializable]
    public struct WorldAddress
    {
        public ChunkId Chunk;
        public BlockId Block;
        public PlaceId Place;
        public CellId Cell;
        public AnchorId Anchor;

        public WorldAddress(ChunkId chunk, BlockId block, PlaceId place, CellId cell, AnchorId anchor)
        {
            Chunk = chunk;
            Block = block;
            Place = place;
            Cell = cell;
            Anchor = anchor;
        }
    }
}
