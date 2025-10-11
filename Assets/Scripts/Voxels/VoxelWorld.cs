using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Utils;
using Voxels.Chunk;
using Voxels.Generation;
using Voxels.Data;

namespace Voxels
{
    public class VoxelWorld : Singleton<VoxelWorld>
    {
        public const int ChunkSize = 16;
        public const int HalfChunkSize = ChunkSize / 2;
        public const int ChunkHeight = 256;
        public const int HalfChunkHeight = ChunkHeight / 2;
        public const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;

        [Header("World")]
        [Tooltip("If empty a random seed will be generated on start. Can be any string; it will be hashed into a numeric seed.")]
        public string worldSeed;

        // Numeric seed used internally for deterministic RNGs. Not exposed in the inspector anymore.
        private long worldSeedNumeric;

        public int waterThreshold = 50;
        public float noiseScale = 0.03f;
        public GameObject chunkPrefab;

        private readonly List<Vector2Int> _chunksToLoad = new();
        private readonly List<Vector2Int> _chunksToUnload = new();
        private readonly HashSet<Vector2Int> _chunksToUpdate = new();

        // Queue that background generator threads will push completed chunk positions into.
        private readonly ConcurrentQueue<Vector2Int> _completedChunks = new();

        public WorldData WorldData { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (string.IsNullOrEmpty(worldSeed))
            {
                worldSeed = System.Guid.NewGuid().ToString();
            }

            // Derive a stable numeric seed from the string using a simple hash.
            unchecked
            {
                long hash = 23;
                foreach (char c in worldSeed) hash = hash * 31 + c;
                worldSeedNumeric = hash;
            }

            WorldData = new WorldData
            {
                ChunkData = new Dictionary<Vector2Int, ChunkData>(),
                Chunks = new Dictionary<Vector2Int, ChunkRenderer>(),
                ChunkSize = ChunkSize,
                ChunkHeight = ChunkHeight
            };
        }

        private void Start()
        {
            StartCoroutine(UpdateChunkRoutine());
        }

        public void LoadChunk(Vector2Int chunkPosition)
        {
            if (WorldData.Chunks.ContainsKey(chunkPosition) || _chunksToLoad.Contains(chunkPosition)) return;
            _chunksToUnload.Remove(chunkPosition);
            _chunksToLoad.Add(chunkPosition);
        }

        public void LoadChunk(List<Vector2Int> chunkPositions)
        {
            foreach (Vector2Int pos in chunkPositions) LoadChunk(pos);
        }

        public void UnloadChunk(Vector2Int chunkPosition)
        {
            if (!WorldData.Chunks.ContainsKey(chunkPosition) || _chunksToUnload.Contains(chunkPosition)) return;
            _chunksToLoad.Remove(chunkPosition);
            _chunksToUnload.Add(chunkPosition);
        }

        public void UnloadChunk(List<Vector2Int> chunkPositions)
        {
            foreach (Vector2Int pos in chunkPositions) UnloadChunk(pos);
        }

        private void FixedUpdate()
        {
            LoadChunkStep();
            UnloadChunkStep();
        }

        private IEnumerator UpdateChunkRoutine()
        {
            while (true)
            {
                // Pull completed background-generated chunks and schedule them for mesh update on main thread.
                while (_completedChunks.TryDequeue(out Vector2Int completed))
                {
                    if (!_chunksToUpdate.Contains(completed)) _chunksToUpdate.Add(completed);
                }

                UpdateChunkStep();
                yield return new WaitForSecondsRealtime(.05f);
            }
        }

        private void UpdateChunkStep()
        {
            if (_chunksToUpdate.Count == 0) return;
            Vector2Int pos = _chunksToUpdate.First();
            _chunksToUpdate.Remove(pos);

            if (!WorldData.ChunkData.TryGetValue(pos, out ChunkData data)) return;
            if (data == null)
            {
                _chunksToUpdate.Add(pos);
                return;
            }

            ChunkRenderer chunkRenderer = GetOrAddChunkRenderer(data, pos);
            chunkRenderer.UpdateChunk();
        }

        private void UnloadChunkStep()
        {
            if (_chunksToUnload.Count == 0) return;
            Vector2Int chunkPos = _chunksToUnload.First();
            _chunksToUnload.Remove(chunkPos);
            if (!WorldData.Chunks.TryGetValue(chunkPos, out ChunkRenderer chunkRenderer)) return;

            Destroy(chunkRenderer.gameObject);
            WorldData.Chunks.Remove(chunkPos);
        }

        private void LoadChunkStep()
        {
            if (_chunksToLoad.Count == 0) return;
            foreach (Vector2Int chunkPos in _chunksToLoad)
            {
                if (WorldData.Chunks.ContainsKey(chunkPos)) return;

                WorldData.ChunkData.TryGetValue(chunkPos, out ChunkData data);
                if (data == null)
                {
                    data = new ChunkData(this, chunkPos);
                    // Add the chunk data to the dictionary before starting background generation so other systems can query existence.
                    WorldData.ChunkData.Add(chunkPos, data);

                    // Gather voxel ids on main thread to pass to background generator
                    var ids = new WorldGeneration.GeneratorConfig
                    {
                        Stone = SafeGetId("std:Stone"),
                        Dirt = SafeGetId("std:Dirt"),
                        Grass = SafeGetId("std:Grass"),
                        Bedrock = SafeGetId("std:Bedrock"),
                        Water = SafeGetId("std:Water"),
                        Sand = SafeGetId("std:Sand"),
                        Sandstone = SafeGetId("std:Sandstone"),
                        Log = SafeGetId("std:Log"),
                        Leaves = SafeGetId("std:Leaves"),
                        OakLog = SafeGetId("std:OakLog"),
                        OakLeaves = SafeGetId("std:OakLeaves"),
                        BirchLog = SafeGetId("std:BirchLog"),
                        BirchLeaves = SafeGetId("std:BirchLeaves"),
                        Cactus = SafeGetId("std:Cactus"),
                        Coal = SafeGetId("std:CoalOre"),
                        Iron = SafeGetId("std:IronOre"),
                        Gold = SafeGetId("std:GoldOre"),
                        Diamond = SafeGetId("std:DiamondOre"),
                        Boat = SafeGetId("std:Boat"),
                        TallGrass = SafeGetId("std:TallGrass"),
                        Mushroom = SafeGetId("std:Mushroom"),
                        StoneBrick = SafeGetId("std:StoneBrick"),
                        Snow = SafeGetId("std:Snow")
                    };

                    // Start background generation. When done, the background task will enqueue the chunk position into _completedChunks.
                    Vector2Int pos = chunkPos;
                    ChunkData localData = data;
                    long seed = worldSeedNumeric;
                    float ns = noiseScale;
                    int water = waterThreshold;

                    Task.Run(() =>
                    {
                        Voxels.Generation.WorldGeneration.GenerateVoxels(localData, ns, water, seed, ids);
                        _completedChunks.Enqueue(pos);
                    });
                }

                // Do not add to _chunksToUpdate here; the background generator will enqueue when finished.
            }

            _chunksToLoad.Clear();
        }

        private ChunkRenderer GetOrAddChunkRenderer(ChunkData data, Vector2Int chunkPos)
        {
            if (WorldData.Chunks.TryGetValue(chunkPos, out ChunkRenderer chunkRenderer)) return chunkRenderer;
            GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition, Quaternion.identity);
            chunkObject.name = chunkPos.ToString();
            chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
            WorldData.Chunks.Add(chunkPos, chunkRenderer);
            chunkRenderer.Initialize(data);
            for (int x = -1; x < 2; x++)
            for (int z = -1; z < 2; z++)
            {
                Vector2Int neighborPos = chunkPos + new Vector2Int(x, z);
                if (!WorldData.ChunkData.TryGetValue(neighborPos, out ChunkData neighborChunk)) continue;
                if (neighborChunk != null) _chunksToUpdate.Add(neighborPos);
            }

            return chunkRenderer;
        }

        private void ClearWorld()
        {
            WorldData.ChunkData.Clear();
            foreach (ChunkRenderer chunk in WorldData.Chunks.Values) Destroy(chunk.gameObject);
            WorldData.Chunks.Clear();
        }

        public int GetVoxelFromWoldVoxPos(Vector3Int voxelWorldPos)
        {
            if (IsNotInYRange(voxelWorldPos.y)) return -1;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            if (chunk == null) return -1;
            return ChunkUtils.GetVoxel(chunk, GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos));
        }

        private static bool IsNotInYRange(int y)
        {
            return y is < 0 or >= ChunkHeight;
        }

        public void SetVoxelFromWorldVoxPos(Vector3Int voxelWorldPos, int voxelId)
        {
            if (IsNotInYRange(voxelWorldPos.y)) return;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            ChunkUtils.SetVoxel(chunk, GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos), voxelId);
        }

        public static Vector3Int GetVoxPosFromWorldVoxPos(ChunkData chunkData, Vector3Int voxelWorldPos)
        {
            return voxelWorldPos - chunkData.WorldPosition;
        }

        public static Vector3Int GetVoxPosFromWorldVoxPos(Vector2Int chunkPos, Vector3Int voxelWorldPos)
        {
            return voxelWorldPos - new Vector3Int(chunkPos.x * ChunkSize, 0, chunkPos.y * ChunkSize);
        }

        internal ChunkData GetChunkFrom(Vector3Int voxelWorldPos)
        {
            Vector2Int pos = GetChunkPosition(voxelWorldPos);
            WorldData.ChunkData.TryGetValue(pos, out ChunkData data);
            return data;
        }

        public static Vector2Int GetChunkPosition(Vector3Int voxelWorldPos)
        {
            return Vector2Int.FloorToInt(
                new Vector2(
                    voxelWorldPos.x / (float)ChunkSize,
                    voxelWorldPos.z / (float)ChunkSize
                )
            );
        }

        public static Vector2Int GetChunkPosition(Vector3 worldPos)
        {
            return GetChunkPosition(Vector3Int.FloorToInt(worldPos));
        }

        public void UpdateChunkMesh(Vector2Int chunkPosition)
        {
            _chunksToUpdate.Add(chunkPosition);
        }

        // Exposed safely so generation code in other classes can query voxel ids with a safe fallback.
        public static int SafeGetId(string name)
        {
            try
            {
                return Voxels.Data.VoxelRegistry.GetId(name);
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        // When the inspector string changes, update the internal numeric seed.
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(worldSeed)) return;

            unchecked
            {
                long hash = 23;
                foreach (char c in worldSeed) hash = hash * 31 + c;
                worldSeedNumeric = hash;
            }
        }
    }

    public struct WorldData
    {
        public Dictionary<Vector2Int, ChunkData> ChunkData;
        public Dictionary<Vector2Int, ChunkRenderer> Chunks;
        public int ChunkSize;
        public int ChunkHeight;
    }
}