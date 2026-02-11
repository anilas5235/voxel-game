using UnityEditor;
using UnityEngine;
using static Runtime.Engine.Behaviour.ChunkPartition;

namespace Runtime.Engine.World.Editor
{
    [CustomEditor(typeof(VoxelWorld))]
    public class VoxelWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(10);
            
            ShowPartitionGizmos =
                GUILayout.Toggle(ShowPartitionGizmos, "Show Partition Gizmos");

            ShowOcclusionGizmos =
                GUILayout.Toggle(ShowOcclusionGizmos, "Show Occlusion Gizmos");
        }
    }
}