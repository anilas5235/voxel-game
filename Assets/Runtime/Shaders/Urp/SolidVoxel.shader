Shader "Custom/VoxelShader"
{
    Properties
    {
        _AOColor ("AO Color", Color) = (0, 0, 0, 1)
        _AOCurve("AOCurve", Vector, 4) = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AOIntensity", Range(0, 1)) = 1
        _AOPower("AOPower", Range(1, 10)) = 1
        [NoScaleOffset] _Textures ("Textures", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Geometry"
        }
        // ─────────────────────────────────────────────────────────────
        // Shared data types used by every pass
        // ─────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "VoxelCommon.hlsl"

        StructuredBuffer<ExpandedVertex> _ExpandedBuffer;

        ExpandedVertex get_expanded_vertex(uint vertex_id)
        {
            return _ExpandedBuffer[get_vertex_buffer_index(vertex_id)];
        }
        ENDHLSL


        // ═════════════════════════════════════════════════════════════
        // Pass – Forward / Unlit
        // ═════════════════════════════════════════════════════════════
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

            // ── Material properties ───────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            // ──────────────────────────────────────────────────────────────
            // Vertex stage
            // ──────────────────────────────────────────────────────────────
            struct Varyings : DefaultVoxelVaryings
            {
            };

            Varyings vert(uint vertex_id : SV_VertexID)
            {
                ExpandedVertex ev = get_expanded_vertex(vertex_id);

                Varyings o;
                o.positionCS = TransformObjectToHClip(ev.positionOS);
                o.uv = ev.uv;
                o.packed = ev.packed;
                return o;
            }

            // ──────────────────────────────────────────────────────────────
            // Fragment stage
            // ──────────────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                // --- Unpack data ---
                float2 uv = IN.uv;
                uint tex_index = get_tex_index(IN.packed);
                uint4 sun_light_data = get_sun_light(IN.packed);
                uint4 artificial_light_data = get_artificial_light(IN.packed);
                uint ao_data = get_ao(IN.packed);
                
                // --- Texture array sample ---
                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, tex_index);

                // --- Ambient occlusion ---
                float4 ao_color = calc_ao_color(_AOColor, albedo, _AOCurve, ao_data, _AOIntensity, _AOPower, uv);

                // --- Sun light level ---
                float sun_light = calc_sun_light(sun_light_data, uv);

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
            #pragma vertex   vert_depth
            #pragma fragment frag_depth

            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ──────────────────────────────────────────────────────────────
            // Vertex stage
            // ──────────────────────────────────────────────────────────────
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings vert_depth(uint vertex_id : SV_VertexID)
            {
                ExpandedVertex ev = get_expanded_vertex(vertex_id);

                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(ev.positionOS);
                return o;
            }

            half frag_depth(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}