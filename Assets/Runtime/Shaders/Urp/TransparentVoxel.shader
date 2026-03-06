Shader "Custom/TransparentVoxel"
{
    Properties
    {
        _AOColor ("AO Color", Color) = (0, 0, 0, 1)
        _AOCurve("AOCurve", Vector, 4) = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AOIntensity", Range(0, 1)) = 1
        _AOPower("AOPower", Range(1, 10)) = 1
        _IndexOffset("Index Offset", Int) = 0
        [NoScaleOffset] _Textures ("Textures", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Transparent"
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

            Cull Off
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ── Material properties ───────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                int _IndexOffset;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            // ──────────────────────────────────────────────────────────────
            // Vertex stage
            // ──────────────────────────────────────────────────────────────
            struct Varyings : DefaultVoxelVaryings
            {
                float4 positionSS : TEXCOORD3;
            };


            Varyings vert(uint vertex_id : SV_VertexID)
            {
                ExpandedVertex ev = get_expanded_vertex(vertex_id+_IndexOffset);

                Varyings o;
                o.positionCS = TransformObjectToHClip(ev.positionOS);
                o.uv = ev.uv;
                o.packed = ev.packed;
                o.positionSS = ComputeScreenPos(o.positionCS);
                return o;
            }

            // ──────────────────────────────────────────────────────────────
            // Fragment stage
            // ──────────────────────────────────────────────────────────────
            float depth_fade(const float4 positionSS, const float dist, const float alpha)
            {
                if (dist <= 0.1f) return alpha;

                float2 ndc = positionSS.xy / positionSS.w;

                float raw_depth = SampleSceneDepth(ndc);
                float scene_eye_depth = LinearEyeDepth(raw_depth, _ZBufferParams);

                float t = saturate((scene_eye_depth - positionSS.w) / dist);
                return lerp(alpha, 1, t);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- Unpack data ---
                float2 uv = IN.uv;
                uint tex_index = get_tex_index(IN.packed);
                uint4 sun_light_data = get_sun_light(IN.packed);
                uint4 artificial_light_data = get_artificial_light(IN.packed);
                uint ao_data = get_ao(IN.packed);
                float depth_fade_dist = get_depth_fade_dist(IN.packed);
                float glow_data = get_glow(IN.packed);

                // --- Texture array sample ---
                const float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, tex_index);

                // --- Ambient occlusion ---
                const float4 ao_color = calc_ao_color(_AOColor, albedo, _AOCurve, ao_data, _AOIntensity, _AOPower, uv);

                // --- Sun light level ---
                const float sun_light = calc_sun_light(sun_light_data, uv);

                // --- Depth fade ---
                const float alpha = depth_fade(IN.positionSS, depth_fade_dist, albedo.w);

                // --- Glow ---
                const float glow = calc_glow(glow_data);

                // --- Final colour ---
                return half4(ao_color.rgb * sun_light * glow, alpha);
            }
            ENDHLSL
        }
    }
}