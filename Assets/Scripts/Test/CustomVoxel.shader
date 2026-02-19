Shader "Custom/CustomVoxel"
{
    Properties
    {
        _AOColor("AOColor", Color) = (0, 0, 0, 0)
        _AOCurve("AOCurve", Vector, 4) = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AOIntensity", Range(0, 1)) = 1
        _AOPower("AOPower", Range(1, 10)) = 1
        [NoScaleOffset]_Textures("Textures", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Geometry"
            "DisableBatching"="False"
        }
        Pass
        {
            Name "Universal Forward"

            Cull Back
            Blend One Zero
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };

            struct GeomData
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0 : INTERP0;
                float4 texCoord1 : INTERP1;
                float4 Ambient_Occlusion : INTERP2;
                float3 positionWS : INTERP3;
                float3 normalWS : INTERP4;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
            CBUFFER_END
            
            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            [maxvertexcount(4)]
            void geom(point GeomData IN[1], inout TriangleStream<Varyings> outStream)
            {
                float3 n = IN[0].normalWS;
                float3 worldUp = float3(0, 1, 0);

                if (abs(n.y) > 0.99) worldUp = float3(1, 0, 0);

                float3 left_ws = normalize(cross(worldUp, n));
                float3 up_ws = cross(n, left_ws);

                //Bottom right
                GeomData OUT = IN[0];
                OUT.texCoord0 = float4(0, 0, 0, 0);
                outStream.Append(OUT);

                //Bottom left
                OUT.positionWS = IN[0].positionWS + left_ws;
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.texCoord0 = float4(1, 0, 1, 0);
                outStream.Append(OUT);

                //Top right
                OUT.positionWS = IN[0].positionWS + up_ws;
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.texCoord0 = float4(0, 1, 0, 1);
                outStream.Append(OUT);

                //Top left
                OUT.positionWS = IN[0].positionWS + left_ws + up_ws;
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.texCoord0 = float4(1, 1, 1, 1);
                outStream.Append(OUT);
                outStream.RestartStrip();
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, IN.uv);
                return color;
            }
            ENDHLSL
        }
    }
}