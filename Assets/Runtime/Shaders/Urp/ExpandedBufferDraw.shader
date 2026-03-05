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
        _AOColor ("AO Color", Color) = (0, 0, 0, 1)
        _AOCurve ("AO Curve", Vector) = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AO Intensity",Range(0, 1)) = 1
        _AOPower ("AO Power", Range(1, 10)) = 1
        [NoScaleOffset] _Textures("Textures", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ─────────────────────────────────────────────────────────────
        // Shared data types used by every pass
        // ─────────────────────────────────────────────────────────────

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "VoxelCommon.hlsl"

        // ──────────────────────────────────────────────────────────────
        // Vertex stage
        // ──────────────────────────────────────────────────────────────
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            uint4 packed : TEXCOORD1;
        };

        StructuredBuffer<ExpandedVertex> _ExpandedBuffer;

        Varyings vert(uint vertex_id : SV_VertexID)
        {
            ExpandedVertex ev = _ExpandedBuffer[get_vertex_buffer_index(vertex_id)];

            Varyings o;
            o.positionCS = TransformObjectToHClip(ev.positionOS);
            o.uv = ev.uv;
            o.packed = ev.packed;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

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

            // ── Material properties ───────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            struct UnpackedFragData
            {
                uint texture_index;
                uint4 sun_light;
                uint4 artificial_light;
                uint ao;
            };

            UnpackedFragData unpack_frag_data(uint4 packed)
            {
                UnpackedFragData data;
                data.texture_index = get_tex_index(packed);
                data.sun_light = get_sun_light(packed);
                data.artificial_light = get_artificial_light(packed);
                data.ao = get_ao(packed);
                return data;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- Texture array sample ---
                UnpackedFragData extra = unpack_frag_data(IN.packed);
                float2 uv = IN.uv;
                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, extra.texture_index);

                // --- Ambient occlusion ---
                float4 ao_color = calc_ao_color(_AOColor, albedo, _AOCurve, extra.ao, _AOIntensity, _AOPower, uv);

                // --- Sun light level ---
                float sun_light = calc_sun_light(extra.sun_light, uv);

                // --- Final colour ---
                return half4(ao_color.rgb * sun_light, 1);
            }
            ENDHLSL
        }
        // ═════════════════════════════════════════════════════════════
        // Pass – Depth Only
        // ═════════════════════════════════════════════════════════════
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            Cull Back
            ZTest LEqual
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag_depth

            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float4 _AOCurve;
                float _AOIntensity;
                float _AOPower;
            CBUFFER_END

            half frag_depth(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }        
    }
}