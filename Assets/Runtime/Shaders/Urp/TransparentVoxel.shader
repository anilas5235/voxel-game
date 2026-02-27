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
            Y: texArrayIndex u16, sunLightLevel u4, 4 bit unused, ao u8;
            Z: half16 in bits 0-15;
            W: unused */
        };

        uint get_quad_index(uint4 packed)
        {
            return packed.x & 0xFFFF; // lower 16 bits of uv0.x
        }

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texCoord0 : TEXCOORD0; // xy = tile UV
            uint packed : TEXCOORD1; // (texArrayIndex u16, sunLightLevel u4, 4 bit unused, ao u8)
            uint packedZ : TEXCOORD2; // uv0.z – half16 in bits 0-15
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
            o.packed = IN[0].packedUV0.y;
            o.packedZ = IN[0].packedUV0.z;

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
                uint texture_index;
                uint light;
                uint ao;
                half depth_fade_dist;
            };

            FragExtraData unpack_frag_extra_data(uint packed, uint packedZ)
            {
                FragExtraData data;
                data.texture_index = packed & 0xFFFF;
                data.light = packed >> 16 & 0xF;
                data.ao = packed >> 24 & 0xFF;
                data.depth_fade_dist = (half)(packedZ & 0xFFFF);
                return data;
            }

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

                // Interpolate the 4 corner AO values based on the pixel's UV within the quad.
                return lerp(lerp(dlc, drc, uv.x), lerp(ulc, urc, uv.x), uv.y);
            }

            half depth_fade(half depth_fade_dist)
            {
                return 1;
            }            


            // ── Fragment shader ───────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // --- Texture array sample ---
                // texCoord1.x = texture array slice index
                // texCoord0.xy = UV within the tile
                FragExtraData extra = unpack_frag_extra_data(IN.packed, IN.packedZ);
                float2 uv = IN.texCoord0;
                float4 albedo = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, extra.texture_index);

                // --- Ambient occlusion ---

                float4 ao_color = lerp(_AOColor, albedo,
                              ao_interpolate(_AOCurve, extra.ao, _AOIntensity, _AOPower, uv));

                // --- Sun light level ---
                float sun_light = lerp(0.05, 1.0, extra.light / 15.0f);

                // --- Final colour ---
                return half4(ao_color.rgb * sun_light,
                       albedo.w * depth_fade(extra.depth_fade_dist));
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

            Cull Off
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
                uint4 uv0 : TEXCOORD0;
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