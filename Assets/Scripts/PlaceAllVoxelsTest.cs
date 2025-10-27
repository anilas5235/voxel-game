using System.Collections.Generic;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Voxels.Data;
using Runtime.Engine.World;
using UnityEngine;

public class PlaceAllVoxelsTest : MonoBehaviour
{
    [SerializeField] private int gridSize = 16;

    private void FixedUpdate()
    {
        if (VoxelWorld.Instance.SetVoxel(50, Vector3Int.one, false))
        {
            PlaceVoxelsInGrid();
            Debug.Log("All voxels placed successfully.");
            enabled = false;
        }
    }

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