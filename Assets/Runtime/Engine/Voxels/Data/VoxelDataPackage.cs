using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    [CreateAssetMenu(fileName = "VoxelDataPackage", menuName = "Data/Voxel Data Package")]
    public class VoxelDataPackage : ScriptableObject
    {
        public string packagePrefix = "UserPackage";
        public List<VoxelDefinition> voxelTextures;
    }
}