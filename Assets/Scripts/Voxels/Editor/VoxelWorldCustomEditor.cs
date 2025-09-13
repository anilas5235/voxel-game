using UnityEditor;
using UnityEngine;
using Voxels.Data;

namespace Voxels.Editor
{
    [CustomEditor(typeof(VoxelWorld))]
    public class VoxelWorldCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            VoxelWorld voxelWorld = (VoxelWorld)target;

            //if (Application.isPlaying && GUILayout.Button("Generate World")) voxelWorld.GenerateWorld();
        }
    }
}