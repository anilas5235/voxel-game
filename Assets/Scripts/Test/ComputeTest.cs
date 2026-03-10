using System;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;
using static Runtime.Engine.Utils.VoxelRenderConstants;

public class ComputeTest : MonoBehaviour
{
    struct PartitionMetadata
    {
        int3 partitionPos; // World partition coordinates
        uint pointCount; // Actual points generated
        float3 boundsMin; // AABB min for frustum culling
        float3 boundsMax; // AABB max
    };

    struct ChunkMetadata
    {
        int2 chunkPos; // World chunk XZ coordinates
        float3 boundsMin; // AABB min for coarse culling
        float3 boundsMax; // AABB max
        uint partitionMask; // Bitmask: which of 8 partitions are active (bit Y)
    };

    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Material material;


    private ComputeBuffer _VoxelData;
    private ComputeBuffer _Metadata;
    private ComputeBuffer _PointsOut;

    private ComputeBuffer _ArgBuffer;
    private MaterialPropertyBlock _MaterialPropertyBlock;

    private void Awake()
    {
        _VoxelData = new ComputeBuffer(VoxelsPerPartition, sizeof(uint));
        _Metadata = new ComputeBuffer(1, Marshal.SizeOf<PartitionMetadata>());
        _PointsOut = new ComputeBuffer(MaxPointsPerPartition, Marshal.SizeOf<Vertex>(), ComputeBufferType.Append);
        _ArgBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        _ArgBuffer.SetData(new uint[]
            { 36u, 1u, 0u, 0u, 0u }); // vertex count, instance count, start vertex, start instance
        _MaterialPropertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Draw;
    }

    private void Start()
    {
        uint[] dummyData = new uint[VoxelsPerPartition];

        for (int i = 0; i < VoxelsPerPartition; i++)
        {
            dummyData[i] = 1;
        }

        _VoxelData.SetData(dummyData);

        int kernel = computeShader.FindKernel("RebuildSolidPoints");
        computeShader.SetBuffer(kernel, "_VoxelData", _VoxelData);
        computeShader.SetBuffer(kernel, "_Metadata", _Metadata);
        computeShader.SetBuffer(kernel, "_PointsOut", _PointsOut);
        computeShader.SetInt("_PartitionIndex", 0);
        computeShader.Dispatch(kernel, 4, 4, 4);

        Vertex[] points = new Vertex[_PointsOut.count];
        _PointsOut.GetData(points);
        for (int i = 0; i < 100; i++)
        {
            Vertex vertex = points[i];
            Debug.Log(
                $"Vertex: {vertex.Position}, TextureIndex: {vertex.GetTextureIndex()}, QuadIndex: {vertex.GetQuadIndex()}");
        }

        //copy counter value to arg buffer for indirect draw
        ComputeBuffer.CopyCount(_PointsOut, _ArgBuffer, 0);
        uint[] argData = new uint[5];
        _ArgBuffer.GetData(argData);
        argData[0] *= 6u;
        _ArgBuffer.SetData(argData);
         Debug.Log($"Indirect args: {string.Join(", ", argData)}");
    }

    private void Draw(ScriptableRenderContext ctx, Camera cam)
    {
        /*CommandBuffer cmd = CommandBufferPool.Get("VoxelSolidDraw");
        cmd.SetGlobalBuffer("_PointData", _PointsOut);
        cmd.DrawProceduralIndirect(
            Matrix4x4.identity,
            material,
            0,
            MeshTopology.Points,
            _ArgBuffer,
            0);
        ctx.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);*/
        _MaterialPropertyBlock.SetBuffer("_PointData", _PointsOut);
        Graphics.DrawProceduralIndirect(
            material,
            new Bounds(Vector3.zero, Vector3.one * 100),
            MeshTopology.Triangles,
            _ArgBuffer,
            0,
            cam,
            _MaterialPropertyBlock,
            ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }
}