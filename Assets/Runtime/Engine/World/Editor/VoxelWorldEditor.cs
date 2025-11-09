using System.IO;
using Runtime.Engine.Settings;
using UnityEditor;
using UnityEngine;
using Runtime.Engine.World;

namespace Runtime.Engine.World.Editor
{
    [CustomEditor(typeof(VoxelWorld))]
    public class VoxelWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw existing fields (focus, settings)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("focus"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));

            serializedObject.ApplyModifiedProperties();

            // Show seed controls if settings assigned
            var settingsProp = serializedObject.FindProperty("settings");
            VoxelEngineSettings settings = settingsProp.objectReferenceValue as VoxelEngineSettings;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Assign a VoxelEngineSettings asset to expose world seed controls.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World Seed (quick access)", EditorStyles.boldLabel);

            if (settings.Noise == null)
            {
                EditorGUILayout.HelpBox("This VoxelEngineSettings has no NoiseSettings assigned.", MessageType.Warning);
                if (GUILayout.Button("Create NoiseSettings next to settings"))
                {
                    string settingsPath = AssetDatabase.GetAssetPath(settings);
                    string folder = Path.GetDirectoryName(settingsPath);
                    if (string.IsNullOrEmpty(folder)) folder = "Assets";
                    NoiseSettings ns = ScriptableObject.CreateInstance<NoiseSettings>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "NoiseSettings.asset"));
                    AssetDatabase.CreateAsset(ns, assetPath);
                    AssetDatabase.SaveAssets();
                    settings.Noise = ns;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            string seedStr = settings.Noise.SeedString;

            seedStr = EditorGUILayout.TextField("Seed", seedStr);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings.Noise, "Change Noise Seed");
                settings.Noise.SeedString = seedStr;
                EditorUtility.SetDirty(settings.Noise);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
