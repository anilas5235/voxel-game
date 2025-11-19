using Runtime.Engine.VoxelConfig.Data;
using Runtime.Engine.World;
using UnityEngine;

namespace Runtime.Shaders
{
    public class VoxelPostProcessHandler : MonoBehaviour
    {
        public Camera target;
        public ushort currentVoxelId;
        private VoxelRegistry _voxelRegistry;
        private PostProcessManager _postProcessManager;

        private void OnEnable()
        {
            _voxelRegistry = VoxelDataImporter.Instance.VoxelRegistry;
            _postProcessManager = PostProcessManager.Instance;
        }

        private void FixedUpdate()
        {
            if (!target)
            {
                Debug.LogWarning("VoxelPostProcessHandler: Target camera is not assigned.");
                return;
            }

            Vector3 camPos = target.transform.position;

            ushort voxelId = VoxelWorld.Instance.GetVoxel(Vector3Int.FloorToInt(camPos));
            if (voxelId == currentVoxelId) return;

            currentVoxelId = voxelId;
            UpdatePostProcessing(currentVoxelId);
        }

        private void UpdatePostProcessing(ushort voxelId)
        {
            _postProcessManager.ResetLiftGammaGain();
            _postProcessManager.ResetColorAdjustments();
            RenderSettings.fog = false;

            if (!_voxelRegistry.GetVoxelDefinition(voxelId, out VoxelDefinition info)) return;

            if (info.meshLayer != MeshLayer.Transparent) return;
            if (info.voxelType == VoxelType.Liquid)
            {
                _postProcessManager.SetLiftGammaGain(info.postProcess.postProcessColor);
                _postProcessManager.SetColorAdjustments(info.postProcess.contrast, info.postProcess.saturation);
                RenderSettings.fog = info.postProcess.enableFog;
                RenderSettings.fogColor = info.postProcess.postProcessColor;
                RenderSettings.fogDensity = info.postProcess.fogDensity;
            }
        }
    }
}