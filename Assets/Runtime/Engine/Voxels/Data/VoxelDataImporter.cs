using Runtime.Engine.Jobs.Chunk;
using UnityEngine;
using Utils;
using System.Reflection;
using System.Text;

namespace Runtime.Engine.Voxels.Data
{
    [DefaultExecutionOrder(-1000)]
    public class VoxelDataImporter : Singleton<VoxelDataImporter>
    {
        public Material voxelSolidMaterial;
        public Material voxelTransparentMaterial;
        public VoxelRegistry VoxelRegistry { get; } = new();

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

            UpdateMaterials();
        }

        private void UpdateMaterials()
        {
            VoxelRegistry.ApplyToMaterial(voxelSolidMaterial, MeshLayer.Solid);
            VoxelRegistry.ApplyToMaterial(voxelTransparentMaterial, MeshLayer.Transparent);
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

        public GeneratorConfig CreateConfig()
        {
            GeneratorConfig config = new()
            {
                BrickGrey = VoxelRegistry.GetId("std:BrickGrey"),
                BrickRed = VoxelRegistry.GetId("std:BrickRed"),
                Cactus = VoxelRegistry.GetId("std:Cactus"),
                CottonBlue = VoxelRegistry.GetId("std:CottonBlue"),
                CottonGreen = VoxelRegistry.GetId("std:CottonGreen"),
                CottonRed = VoxelRegistry.GetId("std:CottonRed"),
                CottonTan = VoxelRegistry.GetId("std:CottonTan"),
                Dirt = VoxelRegistry.GetId("std:Dirt"),
                DirtGravel = VoxelRegistry.GetId("std:DirtGravel"),
                DirtSandy = VoxelRegistry.GetId("std:DirtSandy"),
                DirtSnowy = VoxelRegistry.GetId("std:DirtSnowy"),
                Flowers = VoxelRegistry.GetId("std:Flowers"),
                Glass = VoxelRegistry.GetId("std:Glass"),
                Grass = VoxelRegistry.GetId("std:Grass"),
                GrassF = VoxelRegistry.GetId("std:GrassF"),
                GrassFDead = VoxelRegistry.GetId("std:GrassFDead"),
                GrassFDry = VoxelRegistry.GetId("std:GrassFDry"),
                GreystoneRubyOre = VoxelRegistry.GetId("std:GreystoneRubyOre"),
                Ice = VoxelRegistry.GetId("std:Ice"),
                Lava = VoxelRegistry.GetId("std:Lava"),
                Leaves = VoxelRegistry.GetId("std:Leaves"),
                LeavesOrange = VoxelRegistry.GetId("std:LeavesOrange"),
                LogBirch = VoxelRegistry.GetId("std:LogBirch"),
                LogOak = VoxelRegistry.GetId("std:LogOak"),
                MushroomBrown = VoxelRegistry.GetId("std:MushroomBrown"),
                MushroomRed = VoxelRegistry.GetId("std:MushroomRed"),
                MushroomTan = VoxelRegistry.GetId("std:MushroomTan"),
                Oven = VoxelRegistry.GetId("std:Oven"),
                Planks = VoxelRegistry.GetId("std:Planks"),
                PlanksRed = VoxelRegistry.GetId("std:PlanksRed"),
                Rock = VoxelRegistry.GetId("std:Rock"),
                RockMossy = VoxelRegistry.GetId("std:RockMossy"),
                Sand = VoxelRegistry.GetId("std:Sand"),
                SandGrey = VoxelRegistry.GetId("std:SandGrey"),
                SandRed = VoxelRegistry.GetId("std:SandRed"),
                SandStoneRed = VoxelRegistry.GetId("std:SandStoneRed"),
                SandStoneRedElm = VoxelRegistry.GetId("std:SandStoneRedElm"),
                SandStoneRedSandy = VoxelRegistry.GetId("std:SandStoneRedSandy"),
                Stone = VoxelRegistry.GetId("std:Stone"),
                StoneCoalOre = VoxelRegistry.GetId("std:StoneCoalOre"),
                StoneDiamondOre = VoxelRegistry.GetId("std:StoneDiamondOre"),
                StoneGoldOre = VoxelRegistry.GetId("std:StoneGoldOre"),
                StoneGrassy = VoxelRegistry.GetId("std:StoneGrassy"),
                StoneGravel = VoxelRegistry.GetId("std:StoneGravel"),
                StoneGrey = VoxelRegistry.GetId("std:StoneGrey"),
                StoneGreySandy = VoxelRegistry.GetId("std:StoneGreySandy"),
                StoneIronBrownOre = VoxelRegistry.GetId("std:StoneIronBrownOre"),
                StoneIronGreenOre = VoxelRegistry.GetId("std:StoneIronGreenOre"),
                StoneSandy = VoxelRegistry.GetId("std:StoneSandy"),
                StoneSilverOre = VoxelRegistry.GetId("std:StoneSilverOre"),
                StoneSnowy = VoxelRegistry.GetId("std:StoneSnowy"),
                Water = VoxelRegistry.GetId("std:Water"),
                WheatStage1 = VoxelRegistry.GetId("std:WheatStage1"),
                WheatStage2 = VoxelRegistry.GetId("std:WheatStage2"),
                WheatStage3 = VoxelRegistry.GetId("std:WheatStage3"),
                WheatStage4 = VoxelRegistry.GetId("std:WheatStage4"),
                Workbench = VoxelRegistry.GetId("std:Workbench"),
            };

            // Debug logging: dump the important mappings to help locate misplaced blocks
            void LogId(string name, ushort id)
            {
                if (id == 0) Debug.LogWarning($"VoxelRegistry: '{name}' not found (id=0)");
                else if (VoxelRegistry.GetName(id, out string resolved)) Debug.Log($"VoxelRegistry: '{name}' => id={id}, resolvedName='{resolved}'");
                else Debug.Log($"VoxelRegistry: '{name}' => id={id} (no resolved name)");
            }

            LogId("std:Flowers", config.Flowers);
            LogId("std:Grass", config.Grass);
            LogId("std:GrassF", config.GrassF);
            LogId("std:GrassFDry", config.GrassFDry);
            LogId("std:WheatStage1", config.WheatStage1);
            LogId("std:WheatStage2", config.WheatStage2);
            LogId("std:WheatStage3", config.WheatStage3);
            LogId("std:WheatStage4", config.WheatStage4);

            // Validate mappings: ensure critical roles point to expected voxel names
            bool EnsureEndsWith(ref ushort id, string expectedSuffix, string logicalName)
            {
                if (id == 0) return false;
                if (VoxelRegistry.GetName(id, out string resolved))
                {
                    if (!resolved.EndsWith(":" + expectedSuffix))
                    {
                        Debug.LogError($"VoxelDataImporter: expected {logicalName} to map to *:{expectedSuffix} but got '{resolved}' (id={id}). Clearing mapping to avoid accidental placement.");
                        id = 0;
                        return false;
                    }
                    return true;
                }
                else
                {
                    Debug.LogWarning($"VoxelDataImporter: could not resolve name for id={id} when validating {logicalName}. Clearing mapping.");
                    id = 0;
                    return false;
                }
            }

            // Flowers and grass must map correctly; otherwise clear them so generation falls back to safe defaults.
            EnsureEndsWith(ref config.Flowers, "Flowers", "Flowers");
            EnsureEndsWith(ref config.Grass, "Grass", "Grass");
            EnsureEndsWith(ref config.GrassF, "GrassF", "GrassF");
            EnsureEndsWith(ref config.GrassFDry, "GrassFDry", "GrassFDry");
            EnsureEndsWith(ref config.WheatStage1, "WheatStage1", "WheatStage1");
            EnsureEndsWith(ref config.WheatStage2, "WheatStage2", "WheatStage2");
            EnsureEndsWith(ref config.WheatStage3, "WheatStage3", "WheatStage3");
            EnsureEndsWith(ref config.WheatStage4, "WheatStage4", "WheatStage4");

             return config;
         }
     }
 }
