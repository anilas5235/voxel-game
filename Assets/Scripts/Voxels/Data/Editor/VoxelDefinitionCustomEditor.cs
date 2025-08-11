using System;
using UnityEditor;
using UnityEngine;

namespace Voxels.Data.Editor
{
    [CustomEditor(typeof(VoxelDefinition))]
    public class VoxelDefinitionCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VoxelDefinition voxelDef = (VoxelDefinition)target;

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("textureMode"));

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

            EditorGUILayout.PropertyField(serializedObject.FindProperty("collision"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("transparent"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}