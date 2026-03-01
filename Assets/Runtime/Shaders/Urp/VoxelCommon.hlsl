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
// Geometry stage input struct
// ─────────────────────────────────────────────────────────────────────────────

struct GeomInput
{
    float3 positionOS : TEXCOORD0;
    uint4 packedUV0 : TEXCOORD1;
    /* X: quad index u16, 16 bit unused;
       Y: texArrayIndex u16, sunLightLevel u4, 4 bit unused, ao u8;
       Z: (transparent only) half16 depth_fade_dist in bits 0-15, glow u8 in bits 16-23;
       W: unused */
};

// ─────────────────────────────────────────────────────────────────────────────
// Packed data accessors
// ─────────────────────────────────────────────────────────────────────────────

uint get_quad_index(uint4 packed)
{
    return packed.x & 0xFFFF; // lower 16 bits of uv0.x
}

uint get_tex_index(uint4 packed)
{
    return packed.y & 0xFFFF;
}

uint get_sun_light(uint4 packed)
{
    return packed.y >> 16 & 0xF;
}

uint get_ao(uint4 packed)
{
    return packed.y >> 24 & 0xFF;
}

float get_depth_fade_dist(uint4 packed)
{
    return f16tof32(packed.z & 0xFFFF);
}

float get_glow(uint4 packed)
{
    return (float)(packed.z >> 16 & 0xFF);
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
    o.positionOS = IN.positionOS;
    o.packedUV0 = IN.uv0;
    return o;
}

// ─────────────────────────────────────────────────────────────────────────────
// Frag helpers
// ─────────────────────────────────────────────────────────────────────────────

int compute_ao_corner(const int s1, const int s2, const int c)
{
    return s1 == 1 && s2 == 1 ? 0 : 3 - (s1 + s2 + c);
}

float scale_ao(const float4 curve, const int index, const float intensity, const float power)
{
    return pow(abs(curve[index] * intensity), power);
}

float4 calc_ao_color(const float4 ao_color, const float4 albedo, const float4 ao_curve, const int ao_data,
                     const float ao_intensity, const float ao_power, const float2 uv)
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

    float dlc = scale_ao(ao_color, compute_ao_corner(l, d, dl), ao_intensity, ao_power);
    float ulc = scale_ao(ao_color, compute_ao_corner(l, u, ul), ao_intensity, ao_power);
    float drc = scale_ao(ao_color, compute_ao_corner(r, d, dr), ao_intensity, ao_power);
    float urc = scale_ao(ao_color, compute_ao_corner(r, u, ur), ao_intensity, ao_power);

    float t = lerp(lerp(dlc, drc, uv.x), lerp(ulc, urc, uv.x), uv.y);
    return lerp(ao_color, albedo, t);
}

float calc_sun_light(const uint light_data)
{
    return lerp(0.05f, 1.0f, light_data / 15.0f);
}

float calc_glow(const float glow_data)
{
    return 1.0f + glow_data / 8.0f;
}

#endif // VOXEL_COMMON_INCLUDED
