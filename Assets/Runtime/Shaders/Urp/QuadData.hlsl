#ifndef QUAD_DATA_INCLUDED
#define QUAD_DATA_INCLUDED

// ─────────────────────────────────────────────────────────────────────────────
// QuadData – one entry per unique face shape in the voxel atlas.
// Used by VoxelExpand.compute to expand a point into 4 vertices.
// ─────────────────────────────────────────────────────────────────────────────

struct QuadData
{
    float3 position00; // corner offsets from the voxel-point origin
    float3 position01;
    float3 position02;
    float3 position03;
    float3 normal;
    float2 uv00;
    float2 uv01;
    float2 uv02;
    float2 uv03;
};

#endif // QUAD_DATA_INCLUDED
