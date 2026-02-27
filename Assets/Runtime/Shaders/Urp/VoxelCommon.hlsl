#ifndef VOXEL_COMMON_INCLUDED
#define VOXEL_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

// ─────────────────────────────────────────────────────────────────────────────
// Quad buffer
// ─────────────────────────────────────────────────────────────────────────────

struct QuadData
{
    float3 position00; // vertex offset from voxel origin
    float3 position01;
    float3 position02;
    float3 position03;
    float3 normal;
    float2 uv00;
    float2 uv01;
    float2 uv02;
    float2 uv03;
};

StructuredBuffer<QuadData> quad_buffer;

// ─────────────────────────────────────────────────────────────────────────────
// Geometry stage input/output structs
// ─────────────────────────────────────────────────────────────────────────────

struct GeomInput
{
    float3 positionWS : TEXCOORD0; // voxel world-space pos, base for quad corners
    uint4 packedUV0 : TEXCOORD1;
    /* X: quad index u16, 16 bit unused;
       Y: texArrayIndex u16, sunLightLevel u4, 4 bit unused, ao u8;
       Z: (transparent only) half16 depth_fade_dist in bits 0-15, glow u8 in bits 16-23;
       W: unused */
};

uint get_quad_index(uint4 packed)
{
    return packed.x & 0xFFFF; // lower 16 bits of uv0.x
}

// ─────────────────────────────────────────────────────────────────────────────
// Vertex input structs (shared by all passes)
// ─────────────────────────────────────────────────────────────────────────────

struct Attributes
{
    float3 positionOS : POSITION;
    uint4 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ─────────────────────────────────────────────────────────────────────────────
// Vertex shaders (shared by all passes)
// ─────────────────────────────────────────────────────────────────────────────

GeomInput vert(Attributes IN)
{
    UNITY_SETUP_INSTANCE_ID(IN);
    GeomInput o;
    o.positionWS = TransformObjectToWorld(IN.positionOS);
    o.packedUV0 = IN.uv0;
    return o;
}

// ─────────────────────────────────────────────────────────────────────────────
// AO helpers
// ─────────────────────────────────────────────────────────────────────────────

int compute_ao_corner(const int s1, const int s2, const int c)
{
    return s1 == 1 && s2 == 1 ? 0 : 3 - (s1 + s2 + c);
}

float scale_ao(const float4 curve, const int index, const float intensity, const float power)
{
    return pow(abs(curve[index] * intensity), power);
}

float ao_interpolate(const float4 curve, const int ao_data, const float intensity, const float power,
                     const float2 uv)
{
    // Bits: 0=up (UV.y=1), 1=up-right (UV=1,1), 2=right (UV.x=1), 3=down-right (UV=1,0),
    //       4=down (UV.y=0), 5=down-left (UV=0,0), 6=left (UV.x=0), 7=up-left (UV=0,1)
    int u = ao_data >> 0 & 1;
    int ur = ao_data >> 1 & 1;
    int r = ao_data >> 2 & 1;
    int dr = ao_data >> 3 & 1;
    int d = ao_data >> 4 & 1;
    int dl = ao_data >> 5 & 1;
    int l = ao_data >> 6 & 1;
    int ul = ao_data >> 7 & 1;

    float dlc = scale_ao(curve, compute_ao_corner(l, d, dl), intensity, power);
    float ulc = scale_ao(curve, compute_ao_corner(l, u, ul), intensity, power);
    float drc = scale_ao(curve, compute_ao_corner(r, d, dr), intensity, power);
    float urc = scale_ao(curve, compute_ao_corner(r, u, ur), intensity, power);

    return lerp(lerp(dlc, drc, uv.x), lerp(ulc, urc, uv.x), uv.y);
}
#endif // VOXEL_COMMON_INCLUDED
