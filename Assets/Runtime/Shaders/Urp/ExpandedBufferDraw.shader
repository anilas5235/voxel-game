// Draws the pre-expanded StructuredBuffer produced by CSExpand.
// One call: (PointCount * 6) indices, no geometry shader needed.
//
// ExpandedVertex layout (stride = 36 bytes):
//   float3 positionOS   (12 bytes)
//   float2 uv            (8 bytes)
//   uint4  packed       (16 bytes)
//     .x  low16=quadIndex  high16=texIndex
//     .y  light nibbles
//     .z  ao | depthFade | glow
//     .w  unused
//
// Index pattern per quad (4 consecutive verts v0..v3):
//   triangle 0: v0, v1, v2
//   triangle 1: v2, v1, v3

Shader "Custom/ExpandedBufferDraw"
{
    Properties
    {
        _AOColor    ("AO Color",    Color)             = (0, 0, 0, 1)
        _AOCurve    ("AO Curve",    Vector)            = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AO Intensity",Range(0, 1))       = 1
        _AOPower    ("AO Power",    Range(1, 10))      = 1
        [NoScaleOffset] _Textures("Textures", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VoxelCommon.hlsl"

            // ──────────────────────────────────────────────────────────────
            // Expanded vertex buffer (written by CSExpand)
            // ──────────────────────────────────────────────────────────────
            struct ExpandedVertex
            {
                float3 positionOS;
                float2 uv;
                uint4  packed;
            };

            StructuredBuffer<ExpandedVertex> _ExpandedBuffer;

            // ──────────────────────────────────────────────────────────────
            // Vert/Frag structs
            // ──────────────────────────────────────────────────────────────
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                uint4  packed     : TEXCOORD1;
            };

            // Index pattern for two triangles from 4 quad verts:
            //   quad v0..v3 → indices 0,1,2  2,1,3
            static const int QuadIndices[6] = { 0, 1, 2, 2, 1, 3 };

            Varyings vert(uint vertexID : SV_VertexID)
            {
                // Which quad and which corner within it?
                uint quadIdx    = vertexID / 6;          // one quad = 6 index entries
                uint localIdx   = vertexID % 6;
                uint cornerIdx  = QuadIndices[localIdx]; // 0..3
                uint bufIdx     = quadIdx * 4 + cornerIdx;

                ExpandedVertex ev = _ExpandedBuffer[bufIdx];

                Varyings o;
                o.positionCS = TransformObjectToHClip(ev.positionOS);
                o.uv         = ev.uv;
                o.packed     = ev.packed;
                return o;
            }

            // ── Material properties ───────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float  _AOIntensity;
                float  _AOPower;
                float4 _AOCurve;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            half4 frag(Varyings IN) : SV_Target
            {
                uint texIdx         = get_tex_index(IN.packed);
                uint4 sun_light     = get_sun_light(IN.packed);
                uint ao             = get_ao(IN.packed);

                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, IN.uv, texIdx);
                float4 ao_color = calc_ao_color(_AOColor, albedo, _AOCurve, ao, _AOIntensity, _AOPower, IN.uv);
                float  sun      = calc_sun_light(sun_light, IN.uv);

                return half4(ao_color.rgb * sun, 1);
            }
            ENDHLSL
        }
    }
}
