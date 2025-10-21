using System;
using Runtime.Engine.Voxels.Data;
using UnityEditor;

namespace Runtime.Engine.Voxels.Editor
{
    [CustomEditor(typeof(VoxelDefinition))]
    public class VoxelDefinitionCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VoxelDefinition voxelDef = (VoxelDefinition)target;

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshLayer"));
            if (voxelDef.voxelType != VoxelType.Flora)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textureMode"));
            }
            else
            {
                voxelDef.TextureMode = VoxelDefinition.VoxelTexMode.AllSame;
            }

            if (voxelDef.voxelType != VoxelType.Flora)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alwaysRenderAllFaces"));
            }
            else
            {
                voxelDef.alwaysRenderAllFaces = true;
            }

            if (voxelDef.meshLayer == MeshLayer.Transparent)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("voxelType"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("depthFadeDistance"));
                if (voxelDef.voxelType == VoxelType.Liquid)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("postProcess"));
                }
            }

            switch (voxelDef.TextureMode)
            {
                case VoxelDefinition.VoxelTexMode.AllSame:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("all"));
                    break;

                case VoxelDefinition.VoxelTexMode.TopBottomSides:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("top"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("bottom"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("side"));
                    break;

                case VoxelDefinition.VoxelTexMode.AllUnique:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("top"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("bottom"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("front"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("back"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("left"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("right"));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (voxelDef.voxelType != VoxelType.Flora)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("collision"));
            }
            else
            {
                voxelDef.collision = false;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}