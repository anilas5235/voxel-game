using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Runtime.Engine.Jobs.Collider
{
    /// <summary>
    /// Parallel Job zum Backen der physikalischen Mesh-Collider für zuvor generierte Meshes.
    /// Nutzt Physics.BakeMesh mit Optimierungsoptionen.
    /// </summary>
    [BurstCompile]
    internal struct ColliderBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeList<int> MeshIDs; // Instanz IDs der Meshes

        /// <summary>
        /// Startet den Bake Vorgang für jeden Mesh.
        /// </summary>
        public void Execute(int index)
        {
            Physics.BakeMesh(MeshIDs[index], false,
                MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.CookForFasterSimulation);
        }
    }
}