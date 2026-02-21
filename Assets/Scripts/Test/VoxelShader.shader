Shader "Custom/VoxelShader"
{
    Properties
    {
        _AOColor ("AO Color", Color) = (0, 0, 0, 0)
        _AOCurve ("AO Curve", Vector) = (0.75, 0.825, 0.9, 1)
        _AOIntensity ("AO Intensity", Range(0, 1)) = 1
        _AOPower ("AO Power", Range(1, 10)) = 1
        [NoScaleOffset]
        _Textures ("Textures", 2DArray) = "" {}
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

        struct GeomInput
        {
            float3 positionWS : TEXCOORD0; // voxel world-space pos, base for quad corners
            uint4 packedUV0 : TEXCOORD1; 
            /* X: quad index u16, 16 bit unused; 
            Y: texArrayIndex u16, sunLightLevel u4, ao u4, 8 bit unused; 
            Z and W unused */            
        };

        uint get_quad_index(uint4 packed)
        {
            return packed.x & 0xFFFF; // lower 16 bits of uv0.x
        }

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texCoord0 : TEXCOORD0; // xy = tile UV
            uint packed : TEXCOORD1; // (texArrayIndex u16, sunLightLevel u4, ao u4, 8 bit unused)
        };

        // ── Geometry stage ────────────────────────────────────────────
        // Expands a single point (one per quad) into a triangle strip (4 verts).
        [maxvertexcount(4)]
        void geom(point GeomInput IN[1], inout TriangleStream<Varyings> stream)
        {
            uint quadIndex = get_quad_index(IN[0].packedUV0);
            QuadData q = quad_buffer[quadIndex];

            float3 origin = IN[0].positionWS;

            Varyings o;
            o.packed = IN[0].packedUV0.y;

            o.positionCS = TransformWorldToHClip(origin + q.position00);
            o.texCoord0 = q.uv00;
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position01);
            o.texCoord0 = q.uv01;
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position02);
            o.texCoord0 = q.uv02;
            stream.Append(o);

            o.positionCS = TransformWorldToHClip(origin + q.position03);
            o.texCoord0 = q.uv03;
            stream.Append(o);

            stream.RestartStrip();
        }
        ENDHLSL

        // ═════════════════════════════════════════════════════════════
        // Pass 1 – Forward / Unlit
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
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma geometry geom
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Material properties ───────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float4 _AOCurve;
                float _AOIntensity;
                float _AOPower;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            // ── Vertex input ──────────────────────────────────────────
            // Each "vertex" is a point representing one quad face.
            // uv0.x  = quad index into quad_buffer
            // uv1.xyzw = (texArrayIndex, 0, 0, sunLightLevel)
            // uv2.xyzw = raw AO corner values (used to compute per-face AO)
            struct Attributes
            {
                float3 positionOS : POSITION;
                uint4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Vertex shader ─────────────────────────────────────────
            // Passes the voxel world-space origin and index data to geom.
            GeomInput vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                GeomInput o;
                o.positionWS = TransformObjectToWorld(IN.positionOS); // voxel world pos
                o.packedUV0 = IN.uv0;
                return o;
            }

            struct FragExtraData
            {
                int textureIndex;
                int light;
                int ao;
            };

            FragExtraData unpack_frag_extra_data(uint packed)
            {
                FragExtraData data;
                data.textureIndex = packed & 0xFFFF;
                data.light = (packed >> 16) & 0xF;
                data.ao = (packed >> 20) & 0xF;
                return data;
            }

            // ── Fragment shader ───────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // --- Texture array sample ---
                // texCoord1.x = texture array slice index
                // texCoord0.xy = UV within the tile
                FragExtraData extra = unpack_frag_extra_data(IN.packed);
                int texIndex = extra.textureIndex;
                float2 tileUV = IN.texCoord0;
                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, tileUV, texIndex);

                // --- Ambient occlusion ---
                //float aoIntensity = 0;
                //float4 aoColor = lerp(_AOColor, albedo, aoIntensity);

                // --- Sun light level ---
                float sunLight = lerp(0.05, 1.0, extra.light / 15.0f);

                // --- Final colour ---
                return half4(albedo.rgb * sunLight, 1);
            }
            ENDHLSL
        }

        // ═════════════════════════════════════════════════════════════
        // Pass 2 – Depth Only
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
            #pragma geometry geom          // shared from HLSLINCLUDE
            #pragma fragment frag_depth

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float4 _AOCurve;
                float _AOIntensity;
                float _AOPower;
            CBUFFER_END

            struct DepthAttributes
            {
                float3 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            GeomInput vert_depth(DepthAttributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                GeomInput o;
                o.positionWS = TransformObjectToWorld(IN.positionOS); // voxel world pos
                o.packedUV0 = IN.uv0;
                return o;
            }

            half frag_depth(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}