using Player;
using Runtime.Engine.VoxelConfig.Data;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class VoxelSelect : MonoBehaviour
    {
        public RawImage voxelImage;
        public ushort voxelId;

        public VoxelEditor voxelEditor;

        private void OnEnable()
        {
            voxelEditor.OnVoxelIdChanged += OnVoxelIdChanged;
            OnVoxelIdChanged(voxelEditor.voxelId);
        }

        private void OnDisable()
        {
            voxelEditor.OnVoxelIdChanged -= OnVoxelIdChanged;
        }

        private void OnVoxelIdChanged(ushort newVoxelId)
        {
            voxelId = newVoxelId;
            if (VoxelDataImporter.Instance.VoxelRegistry.GetVoxelDefinition(voxelId, out VoxelDefinition definition))
            {
                Texture2D tex = definition.GetTexture(Direction.Forward);
                voxelImage.texture = tex;
            }
            else
            {
                voxelImage.texture = null;
            }
        }
    }
}