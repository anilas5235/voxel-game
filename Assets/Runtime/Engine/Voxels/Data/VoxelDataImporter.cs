using UnityEngine;
using Utils;

namespace Runtime.Engine.Voxels.Data
{
    [DefaultExecutionOrder(-1000)]
    public class VoxelDataImporter : Singleton<VoxelDataImporter>
    {
        public Material voxelMaterial;

        [SerializeField] private VoxelRegistry voxelRegistry = new();
        public VoxelRegistry VoxelRegistry => voxelRegistry;

        protected override void Awake()
        {
            base.Awake();
            VoxelDataPackage[] voxelDataPackages = Resources.LoadAll<VoxelDataPackage>("VoxelDataPackages");
            if (voxelDataPackages == null || voxelDataPackages.Length == 0)
            {
                Debug.LogError("No VoxelDataPackage found in Resources/VoxelDataPackages. Please create a package.");
                return;
            }

            foreach (VoxelDataPackage package in voxelDataPackages) RegisterPackage(package);

            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            if (voxelMaterial)
            {
                Texture2DArray texArray = VoxelRegistry.GetTextureArray();
                if (texArray)
                    voxelMaterial.SetTexture("_Textures", texArray);
                else
                    Debug.LogWarning("Texture array is null, cannot assign to material.");
            }
            else
            {
                Debug.LogWarning("Voxel material is null, cannot assign texture array.");
            }
        }

        private void RegisterPackage(VoxelDataPackage package)
        {
            string prefix = package.packagePrefix;
            if (string.IsNullOrEmpty(prefix))
            {
                Debug.LogWarning("VoxelDataPackage prefix is empty. Using default 'UserPackage'.");
                prefix = "UserPackage";
            }

            foreach (VoxelDefinition definition in package.voxelTextures)
            {
                if (!definition)
                {
                    Debug.LogWarning("Found null VoxelDefinition in package: " + package.name);
                    continue;
                }

                VoxelRegistry.Register(prefix, definition);
            }

            VoxelRegistry.FinalizeRegistry();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            VoxelRegistry.Dispose();
        }
    }
}