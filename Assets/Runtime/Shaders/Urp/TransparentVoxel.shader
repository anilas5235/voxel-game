Shader "Custom/TransparentVoxel"
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
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Transparent"
        }

        // ─────────────────────────────────────────────────────────────
        // Shared data types used by every pass
        // ─────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "VoxelCommon.hlsl"

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texCoord0 : TEXCOORD0; // xy = tile UV
            uint4 packed : TEXCOORD1; // x: (texArrayIndex u16, sunLightLevel u4, 4 bit unused, ao u8)
            // y:half16 , 16 bit unused
            // z and w unused
            float4 positionSS : TEXCOORD3;
        };

        // ── Geometry stage ────────────────────────────────────────────
        // Expands a single point (one per quad) into a triangle strip (4 verts).
        [maxvertexcount(4)]
        void geom(point GeomInput IN[1], inout TriangleStream<Varyings> stream)
        {
            uint quad_index = get_quad_index(IN[0].packedUV0);
            QuadData q = quad_buffer[quad_index];

            float3 origin = IN[0].positionWS;

            Varyings o;
            o.packed = IN[0].packedUV0;

            o.positionCS = TransformWorldToHClip(origin + q.position00);
            o.texCoord0 = q.uv00;
            o.positionSS = ComputeScreenPos(o.positionCS);
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position01);
            o.texCoord0 = q.uv01;
            o.positionSS = ComputeScreenPos(o.positionCS);
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position02);
            o.texCoord0 = q.uv02;
            o.positionSS = ComputeScreenPos(o.positionCS);
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position03);
            o.texCoord0 = q.uv03;
            o.positionSS = ComputeScreenPos(o.positionCS);
            stream.Append(o);

            stream.RestartStrip();
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
            #pragma geometry geom
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
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            struct FragExtraData
            {
                uint texture_index;
                uint light;
                uint ao;
                float depth_fade_dist;
                float glow;
            };

            FragExtraData unpack_frag_extra_data(uint4 packed)
            {
                FragExtraData data;
                data.texture_index = packed.y & 0xFFFF;
                data.light = packed.y >> 16 & 0xF;
                data.ao = packed.y >> 24 & 0xFF;
                data.depth_fade_dist = f16tof32(packed.z & 0xFFFF);
                data.glow = (float)(packed.z >> 16 & 0xFF);
                return data;
            }            

            float depth_fade(const float4 positionSS, const float dist, const float texAlpha)
            {
                if (dist <= 0.1f) return texAlpha;

                float2 ndc = positionSS.xy / positionSS.w;

                float raw_depth = SampleSceneDepth(ndc);
                float scene_eye_depth = LinearEyeDepth(raw_depth, _ZBufferParams);

                float inter = saturate((scene_eye_depth - positionSS.w) / dist);
                return lerp(texAlpha, 1, inter);
            }

            // ── Fragment shader ───────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // --- Texture array sample ---
                // texCoord1.x = texture array slice index
                // texCoord0.xy = UV within the tile
                FragExtraData extra = unpack_frag_extra_data(IN.packed);
                float2 uv = IN.texCoord0;
                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, extra.texture_index);

                // --- Ambient occlusion ---

                float4 ao_color = lerp(_AOColor, albedo,
               ao_interpolate(_AOCurve, extra.ao, _AOIntensity, _AOPower, uv));

                // --- Sun light level ---
                float sun_light = lerp(0.05, 1.0, extra.light / 15.0f);

                float alpha = depth_fade(IN.positionSS, extra.depth_fade_dist, albedo.w);

                // --- Final colour ---
                return half4(ao_color.rgb * sun_light * (1 + extra.glow / 8.0f), alpha);
            }
            ENDHLSL
        }

        // ═════════════════════════════════════════════════════════════
        // Pass – Scene Selection (editor highlight / outline)
        // ═════════════════════════════════════════════════════════════
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }

            Cull Off
            ZTest LEqual
            ZWrite On
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma geometry geom          
            #pragma fragment frag_sel

            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float4 _AOCurve;
                float _AOIntensity;
                float _AOPower;
            CBUFFER_END

            int _ObjectId;
            int _PassValue;            

            half4 frag_sel(Varyings IN) : SV_Target
            {
                return half4(_ObjectId, _PassValue, 1, 1);
            }
            ENDHLSL
        }

        // ═════════════════════════════════════════════════════════════
        // Pass – Scene Picking (editor GPU picking)
        // ═════════════════════════════════════════════════════════════
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }

            Cull Off
            ZTest LEqual
            ZWrite On
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma geometry geom          
            #pragma fragment frag_pick

            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float4 _AOCurve;
                float _AOIntensity;
                float _AOPower;
            CBUFFER_END

            float4 _SelectionID;           

            half4 frag_pick(Varyings IN) : SV_Target
            {
                return _SelectionID;
            }
            ENDHLSL
        }
    }
}