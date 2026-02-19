using Runtime.Engine.Data;
using Runtime.Engine.Utils;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Light
{
    /// <summary>
    /// Burst-compiled parallel job that generates render and collider mesh data for a list of chunk positions
    /// using the greedy mesher and writes the results into provided <see cref="UnityEngine.Mesh.MeshDataArray"/>
    /// instances while recording the position-to-index mapping.
    /// </summary>
    [BurstCompile]
    internal partial struct LightBuildJob : IJobParallelFor
    {
        [ReadOnly] public ChunkAccessor Accessor;
        [ReadOnly] public NativeList<int3> Jobs;
        [ReadOnly] public VoxelEngineRenderGenData RenderGenData;
        [ReadOnly] public NativeArray<int3> NeighborOffsets;


        [WriteOnly] public NativeParallelHashMap<int3, PartitionLightData>.ParallelWriter Results;

        /// <summary>
        /// Executes mesh generation for the given job index by processing the corresponding chunk position,
        /// generating mesh data using the greedy meshing algorithm, and writing the results to the output arrays
        /// and mapping. This method is called in parallel for each index in the <see cref="Jobs"/> list, allowing
        /// for efficient processing of multiple chunks simultaneously.
        /// </summary>
        /// <param name="index">Index of the chunk position to process within the <see cref="Jobs"/> list.</param>
        public void Execute(int index)
        {
            LightJobData jobData = new(Jobs[index]);

            SortVoxels(ref jobData);

            if (!jobData.HasNoSolid)
            {
                AddSunlightSeeds(ref jobData);

                SunLightPropagation(ref jobData);
            }

            WriteResults(index, ref jobData);

            jobData.Dispose();
        }
    }
}