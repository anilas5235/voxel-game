using System.Collections.Generic;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Runtime.Engine.World;
using UnityEngine;

/// <summary>
/// Debug helper MonoBehaviour that places one instance of each registered voxel
/// in a 3D grid in the world for visual inspection.
/// </summary>
public class PlaceAllVoxelsTest : MonoBehaviour
{
    /// <summary>
    /// Size of the grid along each axis used when placing voxels.
    /// </summary>
    [SerializeField] private int gridSize = 16;

    // Disabled automatic placement to avoid test blocks at spawn. Use the public method below to run manually.
    // private void FixedUpdate() { }

    /// <summary>
    /// Manually triggers placement of all registered voxels into a grid once,
    /// logging success and failure for each placement.
    /// </summary>
    public void PlaceAllVoxelsManually()
    {
        if (VoxelWorld.Instance.SetVoxel(50, Vector3Int.one, false))
        {
            PlaceVoxelsInGrid();
            Debug.Log("All voxels placed successfully (manual run).");
        }
        else
        {
            Debug.LogWarning("VoxelWorld not ready - could not place test voxels.");
        }
    }

    /// <summary>
    /// Iterates through all registered voxel IDs and places them in a 3D grid above the world,
    /// triggering a remesh on the last voxel to update rendering.
    /// </summary>
    private void PlaceVoxelsInGrid()
    {
        List<KeyValuePair<ushort, string>> list = VoxelDataImporter.Instance.VoxelRegistry.GetAllEntries();
        int index = 0;
        for (int y = 0; y < gridSize; y++)
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            Vector3 position = new(x, 150 + y, z);
            bool remesh = index == list.Count - 1;
            bool status = VoxelWorld.Instance.SetVoxel(list[index].Key, position.V3Int(), remesh);
            Debug.Log(
                $"Placing voxel ID {list[index]} at {position.V3Int()}: {(status ? "Success" : "Failed")}, remesh: {remesh}");
            index++;
            if (index >= list.Count) return;
        }
    }
}