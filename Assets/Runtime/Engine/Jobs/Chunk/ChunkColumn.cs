namespace Runtime.Engine.Jobs.Chunk
{
    public struct ChunkColumn
    {
        public int Height;
        public Biome Biome;
        public ushort TopBlock;
        public ushort UnderBlock;
        public ushort StoneBlock;
        public float Temperature;
        public float Humidity;
    }
}