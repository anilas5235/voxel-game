Shader "Custom/BufferTest"
{
    Properties
    {
        _AOColor("AOColor", Color) = (0, 0, 0, 0)
        _AOCurve("AOCurve", Vector, 4) = (0.75, 0.825, 0.9, 1)
        _AOIntensity("AOIntensity", Range(0, 1)) = 1
        _AOPower("AOPower", Range(1, 10)) = 1
        [NoScaleOffset]_Textures("Textures", 2DArray) = "" {}
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct GeomData
        {
            float4 positionCS : SV_POSITION;
            float4 texCoord0 : INTERP0;
            float4 texCoord1 : INTERP1;
            float4 Ambient_Occlusion : INTERP2;
            float3 positionWS : INTERP3;
            float3 normalWS : INTERP4;
        };

        [maxvertexcount(4)]
        void geom(point GeomData IN[1], inout TriangleStream<GeomData> outStream)
        {
            GeomData OUT;
            OUT.positionWS = IN[0].positionWS;
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.normalWS = IN[0].normalWS;
            OUT.texCoord0 = float4(0, 0, 0, 0);
            OUT.texCoord1 = IN[0].texCoord1;
            OUT.Ambient_Occlusion = IN[0].Ambient_Occlusion;
            outStream.Append(OUT);

            OUT.positionWS = IN[0].positionWS + float3(1, 0, 0);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.texCoord0 = float4(1, 0, 1, 0);
            outStream.Append(OUT);

            OUT.positionWS = IN[0].positionWS + float3(0, 1, 0);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.texCoord0 = float4(0, 1, 0, 1);
            outStream.Append(OUT);

            OUT.positionWS = IN[0].positionWS + float3(1, 1, 0);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.texCoord0 = float4(1, 1, 1, 1);
            outStream.Append(OUT);
            outStream.RestartStrip();
        }
        ENDHLSL


        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Geometry"
            "DisableBatching"="False"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalUnlitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                // LightMode: <None>
            }

            // Render State
            Cull Back
            Blend One Zero
            ZTest LEqual
            ZWrite On

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD1
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_UNLIT
            #define _FOG_FRAGMENT 1
            #define UNLIT_DEFAULT_DECAL_BLENDING 1
            #define UNLIT_DEFAULT_SSAO 1


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS;
                float3 normalWS;
                float4 texCoord0;
                float4 texCoord1;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
                float4 Ambient_Occlusion;
            };

            struct SurfaceDescriptionInputs
            {
                float4 uv0;
                float4 uv1;
                float4 Ambient_Occlusion;
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
                float4 uv2;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0 : INTERP0;
                float4 texCoord1 : INTERP1;
                float4 Ambient_Occlusion : INTERP2;
                float3 positionWS : INTERP3;
                float3 normalWS : INTERP4;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.texCoord0.xyzw = input.texCoord0;
                output.texCoord1.xyzw = input.texCoord1;
                output.Ambient_Occlusion.xyzw = input.Ambient_Occlusion;
                output.positionWS.xyz = input.positionWS;
                output.normalWS.xyz = input.normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                output.texCoord0 = input.texCoord0.xyzw;
                output.texCoord1 = input.texCoord1.xyzw;
                output.Ambient_Occlusion = input.Ambient_Occlusion.xyzw;
                output.positionWS = input.positionWS.xyz;
                output.normalWS = input.normalWS.xyz;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            #include_with_pragmas "Assets/Runtime/Shaders/Urp/Functions/compute_ao.hlsl"

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions

            struct Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
            {
            };

            void SG_ComputeAO_5dce8a19567304442be409564ab90429_float(float4 _Curve, float4 _Values, float _Intensity,
                                                                     float _Power,
                                                                     Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
                                                                     IN,
                                                                     out float4 AO_1)
            {
                float4 _Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4 = _Curve;
                float4 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4 = _Values;
                float _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float = _Intensity;
                float _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float = _Power;
                float4 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
                compute_ao_float(_Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4,
                                                 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4,
                                                 _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float,
                                                 _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float,
                                                 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4);
                AO_1 = _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
            }

            struct Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
            {
                half4 uv1;
                half4 uv0;
            };

            void SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                UnityTexture2DArray _Textures, UnitySamplerState Sampler,
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float IN, out float4 RGBA_1)
            {
                UnityTexture2DArray _Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray = _Textures;
                float4 _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4 = IN.uv1;
                float _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[0];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_G_2_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[1];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_B_3_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[2];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_A_4_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[3];
                float4 _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4 = IN.uv0;
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[0];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[1];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_B_3_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[2];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_A_4_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[3];
                float2 _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2 = float2(
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float,
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float);
                UnitySamplerState _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState = Sampler;
                float4 _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4 =
                    PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray.tex,
                        _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState.samplerstate,
                        _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2,
                        _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float);
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_R_4_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.r;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_G_5_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.g;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_B_6_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.b;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_A_7_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.a;
                RGBA_1 = _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4;
            }

            void Unity_Lerp_float(float A, float B, float T, out float Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Lerp_half(half A, half B, half T, out half Out)
            {
                Out = lerp(A, B, T);
            }

            struct Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
            {
                half4 uv0;
            };

            void SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                float4 _AOInterpolater, Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float IN,
                out float intensity_1)
            {
                float4 _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4 = _AOInterpolater;
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[0];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[1];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[2];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[3];
                float4 _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4 = IN.uv0;
                float _Split_cae434cd97c14f63a96614ca895d6518_R_1_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[0];
                float _Split_cae434cd97c14f63a96614ca895d6518_G_2_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[1];
                float _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[2];
                float _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[3];
                float _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float,
                    _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float,
                    _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                    _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float);
                float _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float,
                                                                               _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float,
                                                                               _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                                                                               _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float);
                float _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
                Unity_Lerp_float(_Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float,
                                                                 _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float,
                                                                 _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float,
                                                                 _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float);
                intensity_1 = _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
            }

            void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Divide_half(half A, half B, out half Out)
            {
                Out = A / B;
            }

            struct Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
            {
                half4 uv1;
            };

            void SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                half _MinLight, Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half IN, out half Out_2)
            {
                half _Property_2d22d37aee374e128936943480fe27a9_Out_0_Float = _MinLight;
                half4 _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4 = IN.uv1;
                half _Split_d5d0108048044af5bda86f38a68a3d02_R_1_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[0];
                half _Split_d5d0108048044af5bda86f38a68a3d02_G_2_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[1];
                half _Split_d5d0108048044af5bda86f38a68a3d02_B_3_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[2];
                half _Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[3];
                half _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float;
                Unity_Divide_half(_Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float, half(15),
                                              _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float);
                half _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
                Unity_Lerp_half(_Property_2d22d37aee374e128936943480fe27a9_Out_0_Float, half(1),
                                                                  _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float,
                                                                  _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float);
                Out_2 = _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
            }

            void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
            {
                Out = A * B;
            }

            struct Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
            {
                half4 uv0;
                half4 uv1;
            };

            void SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                float4 _AOInterpolater, UnityTexture2DArray _Textures, UnitySamplerState Sampler, float4 _AOColor,
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float IN, out float4 RGBA_1)
            {
                float4 _Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4 = _AOColor;
                UnityTexture2DArray _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray = _Textures;
                UnitySamplerState _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState = Sampler;
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv1 = IN.uv1;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv0 = IN.uv0;
                half4 _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4;
                SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                    _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray,
                    _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4);
                float4 _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4 = _AOInterpolater;
                Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181;
                _AOInterpolation_34061a478fc64a3e9f655b08c489a181.uv0 = IN.uv0;
                half _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float;
                SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                    _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float);
                float4 _Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4;
                Unity_Lerp_float4(_Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4,
                                 _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4,
                                 (_AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float.xxxx),
                                 _Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4);
                Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05;
                _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05.uv1 = IN.uv1;
                half _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float;
                SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                    half(0.05), _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float);
                float4 _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4 = float4(
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float, float(1));
                float4 _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
                Unity_Multiply_float4_float4(_Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4,
 _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4,
 _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4);
                RGBA_1 = _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
            }

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
                float4 Ambient_Occlusion;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                float _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float = _AOPower;
                float _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float = _AOIntensity;
                float4 _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4 = IN.uv2;
                float4 _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4 = _AOCurve;
                Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float _ComputeAO_1e9428f003fd40519877d28f86c1d0f5;
                float4 _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                SG_ComputeAO_5dce8a19567304442be409564ab90429_float(
                    _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4,
                    _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4,
                    _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float,
                    _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float, _ComputeAO_1e9428f003fd40519877d28f86c1d0f5,
                    _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4);
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                description.Ambient_Occlusion = _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                output.Ambient_Occlusion = input.Ambient_Occlusion;
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
                float3 BaseColor;
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                UnityTexture2DArray _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray =
                    UnityBuildTexture2DArrayStruct(_Textures);
                UnitySamplerState _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState =
                    UnityBuildSamplerStateStruct(SamplerState_Trilinear_Repeat);
                float4 _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4 = _AOColor;
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv0 = IN.uv0;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv1 = IN.uv1;
                float4 _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4;
                SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                    IN.Ambient_Occlusion, _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray,
                    _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState,
                    _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4);
                surface.BaseColor = (_VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4.xyz);
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                    ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                output.uv2 = input.uv2;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif

                output.Ambient_Occlusion = input.Ambient_Occlusion;


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                output.uv0 = input.texCoord0;
                output.uv1 = input.texCoord1;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // Render State
            Cull Back
            ZTest LEqual
            ZWrite On
            ColorMask R

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            // PassKeywords: <None>
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_DEPTHONLY


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            // GraphIncludes: <None>

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions
            // GraphFunctions: <None>

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags
            {
                "LightMode" = "MotionVectors"
            }

            // Render State
            Cull Back
            ZTest LEqual
            ZWrite On
            ColorMask RG

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            // PassKeywords: <None>
            // GraphKeywords: <None>

            // Defines

            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_MOTION_VECTORS


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpacePosition;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            // GraphIncludes: <None>

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions
            // GraphFunctions: <None>

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                description.Position = IN.ObjectSpacePosition;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpacePosition = input.positionOS;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // Render State
            Cull Back
            ZTest LEqual
            ZWrite On

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_NORMAL_WS
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_DEPTHNORMALSONLY


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : INTERP0;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.normalWS.xyz = input.normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                output.normalWS = input.normalWS.xyz;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            // GraphIncludes: <None>

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions
            // GraphFunctions: <None>

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // Render State
            Cull Back
            ZTest LEqual
            ZWrite On
            ColorMask 0

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_NORMAL_WS
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_SHADOWCASTER


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : INTERP0;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.normalWS.xyz = input.normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                output.normalWS = input.normalWS.xyz;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            // GraphIncludes: <None>

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions
            // GraphFunctions: <None>

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // Render State
            Cull Back
            Blend One Zero
            ZTest LEqual
            ZWrite On

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD1
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_GBUFFER


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS;
                float3 normalWS;
                float4 texCoord0;
                float4 texCoord1;
                #if !defined(LIGHTMAP_ON)
                float3 sh;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                float4 probeOcclusion;
                #endif
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
                float4 Ambient_Occlusion;
            };

            struct SurfaceDescriptionInputs
            {
                float4 uv0;
                float4 uv1;
                float4 Ambient_Occlusion;
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
                float4 uv2;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                #if !defined(LIGHTMAP_ON)
                float3 sh : INTERP0;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                float4 probeOcclusion : INTERP1;
                #endif
                float4 texCoord0 : INTERP2;
                float4 texCoord1 : INTERP3;
                float4 Ambient_Occlusion : INTERP4;
                float3 positionWS : INTERP5;
                float3 normalWS : INTERP6;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                #if !defined(LIGHTMAP_ON)
                output.sh = input.sh;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                output.probeOcclusion = input.probeOcclusion;
                #endif
                output.texCoord0.xyzw = input.texCoord0;
                output.texCoord1.xyzw = input.texCoord1;
                output.Ambient_Occlusion.xyzw = input.Ambient_Occlusion;
                output.positionWS.xyz = input.positionWS;
                output.normalWS.xyz = input.normalWS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                #if !defined(LIGHTMAP_ON)
                output.sh = input.sh;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                output.probeOcclusion = input.probeOcclusion;
                #endif
                output.texCoord0 = input.texCoord0.xyzw;
                output.texCoord1 = input.texCoord1.xyzw;
                output.Ambient_Occlusion = input.Ambient_Occlusion.xyzw;
                output.positionWS = input.positionWS.xyz;
                output.normalWS = input.normalWS.xyz;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            #include_with_pragmas "Assets/Runtime/Shaders/Urp/Functions/compute_ao.hlsl"

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions

            struct Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
            {
            };

            void SG_ComputeAO_5dce8a19567304442be409564ab90429_float(float4 _Curve, float4 _Values, float _Intensity,
                                                                     float _Power,
                                                                     Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
                                                                     IN, out float4 AO_1)
            {
                float4 _Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4 = _Curve;
                float4 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4 = _Values;
                float _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float = _Intensity;
                float _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float = _Power;
                float4 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
                compute_ao_float(_Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4,
                                 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4,
                                 _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float,
                                 _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float,
                                 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4);
                AO_1 = _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
            }

            struct Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
            {
                half4 uv1;
                half4 uv0;
            };

            void SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                UnityTexture2DArray _Textures, UnitySamplerState Sampler,
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float IN, out float4 RGBA_1)
            {
                UnityTexture2DArray _Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray = _Textures;
                float4 _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4 = IN.uv1;
                float _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[0];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_G_2_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[1];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_B_3_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[2];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_A_4_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[3];
                float4 _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4 = IN.uv0;
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[0];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[1];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_B_3_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[2];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_A_4_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[3];
                float2 _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2 = float2(
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float,
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float);
                UnitySamplerState _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState = Sampler;
                float4 _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4 =
                    PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray.tex,
                                                           _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState
                                                           .samplerstate,
                                                           _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2,
                                                           _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float);
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_R_4_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.r;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_G_5_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.g;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_B_6_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.b;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_A_7_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.a;
                RGBA_1 = _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4;
            }

            void Unity_Lerp_float(float A, float B, float T, out float Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Lerp_half(half A, half B, half T, out half Out)
            {
                Out = lerp(A, B, T);
            }

            struct Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
            {
                half4 uv0;
            };

            void SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                float4 _AOInterpolater, Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float IN,
                out float intensity_1)
            {
                float4 _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4 = _AOInterpolater;
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[0];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[1];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[2];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[3];
                float4 _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4 = IN.uv0;
                float _Split_cae434cd97c14f63a96614ca895d6518_R_1_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[0];
                float _Split_cae434cd97c14f63a96614ca895d6518_G_2_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[1];
                float _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[2];
                float _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[3];
                float _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float,
                                                      _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float,
                                                      _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                                                      _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float);
                float _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float,
                    _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float,
                    _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                    _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float);
                float _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
                Unity_Lerp_float(_Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float,
                                                                     _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float,
                                                                     _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float,
                                                                     _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float);
                intensity_1 = _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
            }

            void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Divide_half(half A, half B, out half Out)
            {
                Out = A / B;
            }

            struct Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
            {
                half4 uv1;
            };

            void SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                half _MinLight, Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half IN, out half Out_2)
            {
                half _Property_2d22d37aee374e128936943480fe27a9_Out_0_Float = _MinLight;
                half4 _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4 = IN.uv1;
                half _Split_d5d0108048044af5bda86f38a68a3d02_R_1_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[0];
                half _Split_d5d0108048044af5bda86f38a68a3d02_G_2_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[1];
                half _Split_d5d0108048044af5bda86f38a68a3d02_B_3_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[2];
                half _Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[3];
                half _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float;
                Unity_Divide_half(_Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float, half(15),
                       _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float);
                half _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
                Unity_Lerp_half(_Property_2d22d37aee374e128936943480fe27a9_Out_0_Float, half(1),
                             _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float,
                             _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float);
                Out_2 = _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
            }

            void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
            {
                Out = A * B;
            }

            struct Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
            {
                half4 uv0;
                half4 uv1;
            };

            void SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                float4 _AOInterpolater, UnityTexture2DArray _Textures, UnitySamplerState Sampler, float4 _AOColor,
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float IN, out float4 RGBA_1)
            {
                float4 _Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4 = _AOColor;
                UnityTexture2DArray _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray = _Textures;
                UnitySamplerState _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState = Sampler;
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv1 = IN.uv1;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv0 = IN.uv0;
                half4 _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4;
                SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                    _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray,
                    _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4);
                float4 _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4 = _AOInterpolater;
                Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181;
                _AOInterpolation_34061a478fc64a3e9f655b08c489a181.uv0 = IN.uv0;
                half _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float;
                SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                    _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float);
                float4 _Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4;
                Unity_Lerp_float4(_Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4,
                 _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4,
                 (_AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float.
                     xxxx),
                 _Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4);
                Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05;
                _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05.uv1 = IN.uv1;
                half _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float;
                SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                    half(0.05), _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float);
                float4 _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4 = float4(
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float, float(1));
                float4 _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
                Unity_Multiply_float4_float4(_Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4,
           _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4,
           _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4);
                RGBA_1 = _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
            }

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
                float4 Ambient_Occlusion;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                float _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float = _AOPower;
                float _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float = _AOIntensity;
                float4 _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4 = IN.uv2;
                float4 _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4 = _AOCurve;
                Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float _ComputeAO_1e9428f003fd40519877d28f86c1d0f5;
                float4 _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                SG_ComputeAO_5dce8a19567304442be409564ab90429_float(
                    _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4,
                    _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4,
                    _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float,
                    _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float, _ComputeAO_1e9428f003fd40519877d28f86c1d0f5,
                    _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4);
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                description.Ambient_Occlusion = _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                output.Ambient_Occlusion = input.Ambient_Occlusion;
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
                float3 BaseColor;
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                UnityTexture2DArray _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray =
                    UnityBuildTexture2DArrayStruct(_Textures);
                UnitySamplerState _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState =
                    UnityBuildSamplerStateStruct(SamplerState_Trilinear_Repeat);
                float4 _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4 = _AOColor;
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv0 = IN.uv0;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv1 = IN.uv1;
                float4 _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4;
                SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                    IN.Ambient_Occlusion, _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray,
                    _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState,
                    _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4);
                surface.BaseColor = (_VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4.xyz);
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                output.uv2 = input.uv2;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif

                output.Ambient_Occlusion = input.Ambient_Occlusion;


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                output.uv0 = input.texCoord0;
                output.uv1 = input.texCoord1;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitGBufferPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }

            // Render State
            Cull Off

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            // PassKeywords: <None>
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_DEPTHONLY
            #define SCENESELECTIONPASS 1
            #define ALPHA_CLIP_THRESHOLD 1


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            // GraphIncludes: <None>

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions
            // GraphFunctions: <None>

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }

            // Render State
            Cull Back

            // Debug
            // <None>

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            // Keywords
            // PassKeywords: <None>
            // GraphKeywords: <None>

            // Defines

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD1
            #define FEATURES_GRAPH_VERTEX
            /* WARNING: $splice Could not find named fragment 'PassInstancing' */
            #define SHADERPASS SHADERPASS_DEPTHONLY
            #define SCENEPICKINGPASS 1
            #define ALPHA_CLIP_THRESHOLD 1


            // custom interpolator pre-include
            /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            // --------------------------------------------------
            // Structs and Packing

            // custom interpolators pre packing
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0;
                float4 texCoord1;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
                float4 Ambient_Occlusion;
            };

            struct SurfaceDescriptionInputs
            {
                float4 uv0;
                float4 uv1;
                float4 Ambient_Occlusion;
            };

            struct VertexDescriptionInputs
            {
                float3 ObjectSpaceNormal;
                float3 ObjectSpaceTangent;
                float3 ObjectSpacePosition;
                float4 uv2;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0 : INTERP0;
                float4 texCoord1 : INTERP1;
                float4 Ambient_Occlusion : INTERP2;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.texCoord0.xyzw = input.texCoord0;
                output.texCoord1.xyzw = input.texCoord1;
                output.Ambient_Occlusion.xyzw = input.Ambient_Occlusion;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                output.texCoord0 = input.texCoord0.xyzw;
                output.texCoord1 = input.texCoord1.xyzw;
                output.Ambient_Occlusion = input.Ambient_Occlusion.xyzw;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }


            // --------------------------------------------------
            // Graph

            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _AOColor;
                float _AOIntensity;
                float _AOPower;
                float4 _AOCurve;
                UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END


            // Object and Global properties
            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);
            SAMPLER(SamplerState_Trilinear_Repeat);

            // Graph Includes
            #include_with_pragmas "Assets/Runtime/Shaders/Urp/Functions/compute_ao.hlsl"

            // -- Property used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif

            // -- Properties used by SceneSelectionPass
            #ifdef SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;
            #endif

            // Graph Functions

            struct Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
            {
            };

            void SG_ComputeAO_5dce8a19567304442be409564ab90429_float(float4 _Curve, float4 _Values, float _Intensity,
                                                                     float _Power,
                                                                     Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float
                                                                     IN,
                                                                     out float4 AO_1)
            {
                float4 _Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4 = _Curve;
                float4 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4 = _Values;
                float _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float = _Intensity;
                float _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float = _Power;
                float4 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
                compute_ao_float(_Property_0011818159bb4541bc77a26e8757db0a_Out_0_Vector4,
                                                 _Property_eb2bfb0e1c654ff3846298a857ddce73_Out_0_Vector4,
                                                 _Property_78a06c83876542839ce5c4fdd932e409_Out_0_Float,
                                                 _Property_012dacba6e2848738f683b8743b0a62a_Out_0_Float,
                                                 _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4);
                AO_1 = _computeaoCustomFunction_aa6decb566014f1393009ac0fef67cdf_ao_4_Vector4;
            }

            struct Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
            {
                half4 uv1;
                half4 uv0;
            };

            void SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                UnityTexture2DArray _Textures, UnitySamplerState Sampler,
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float IN, out float4 RGBA_1)
            {
                UnityTexture2DArray _Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray = _Textures;
                float4 _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4 = IN.uv1;
                float _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[0];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_G_2_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[1];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_B_3_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[2];
                float _Split_fce33dc1ce8f422aa31dfb7261973795_A_4_Float =
                    _UV_1f3f18aa318e47318698ef820d13521b_Out_0_Vector4[3];
                float4 _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4 = IN.uv0;
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[0];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[1];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_B_3_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[2];
                float _Split_4f233c3e1b334cc881ce7feabd4527cb_A_4_Float =
                    _UV_cd9af6de657d447e8199aa1f0bac578c_Out_0_Vector4[3];
                float2 _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2 = float2(
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_R_1_Float,
                    _Split_4f233c3e1b334cc881ce7feabd4527cb_G_2_Float);
                UnitySamplerState _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState = Sampler;
                float4 _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4 =
                    PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f2d83801dfca4cf1b97eb71d63898919_Out_0_Texture2DArray.tex,
                                                            _Property_c6e0a5dc0a834e12b2fc19a09fef8334_Out_0_SamplerState
                                                            .samplerstate,
                                                            _Vector2_b022ee6573ac4a4ea4c2603f2f8b746f_Out_0_Vector2,
                                                            _Split_fce33dc1ce8f422aa31dfb7261973795_R_1_Float);
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_R_4_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.r;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_G_5_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.g;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_B_6_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.b;
                float _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_A_7_Float =
                    _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4.a;
                RGBA_1 = _SampleTexture2DArray_da133cb911264c458326ac25ef813f13_RGBA_0_Vector4;
            }

            void Unity_Lerp_float(float A, float B, float T, out float Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Lerp_half(half A, half B, half T, out half Out)
            {
                Out = lerp(A, B, T);
            }

            struct Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
            {
                half4 uv0;
            };

            void SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                float4 _AOInterpolater, Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float IN,
                out float intensity_1)
            {
                float4 _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4 = _AOInterpolater;
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[0];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[1];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[2];
                float _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float =
                    _Property_783cb39bdbb24196af12a7ac5357a969_Out_0_Vector4[3];
                float4 _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4 = IN.uv0;
                float _Split_cae434cd97c14f63a96614ca895d6518_R_1_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[0];
                float _Split_cae434cd97c14f63a96614ca895d6518_G_2_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[1];
                float _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[2];
                float _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float =
                    _UV_2a98903522ee4156b8a5ff21d5900324_Out_0_Vector4[3];
                float _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_R_1_Float,
                                                                   _Split_7ff98decfaa24c06b24cbdf5055036b1_B_3_Float,
                                                                   _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                                                                   _Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float);
                float _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float;
                Unity_Lerp_float(_Split_7ff98decfaa24c06b24cbdf5055036b1_G_2_Float,
                 _Split_7ff98decfaa24c06b24cbdf5055036b1_A_4_Float,
                 _Split_cae434cd97c14f63a96614ca895d6518_B_3_Float,
                 _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float);
                float _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
                Unity_Lerp_float(_Lerp_ff2e48b4007f42cdb56e2dbf876c0dbb_Out_3_Float,
                                  _Lerp_85729c93c494472a9f2f87cf08d7117a_Out_3_Float,
                                  _Split_cae434cd97c14f63a96614ca895d6518_A_4_Float,
                                  _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float);
                intensity_1 = _Lerp_c29a435d0c8745fab32a7c4aa29ee5ec_Out_3_Float;
            }

            void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Divide_half(half A, half B, out half Out)
            {
                Out = A / B;
            }

            struct Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
            {
                half4 uv1;
            };

            void SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                half _MinLight, Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half IN, out half Out_2)
            {
                half _Property_2d22d37aee374e128936943480fe27a9_Out_0_Float = _MinLight;
                half4 _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4 = IN.uv1;
                half _Split_d5d0108048044af5bda86f38a68a3d02_R_1_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[0];
                half _Split_d5d0108048044af5bda86f38a68a3d02_G_2_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[1];
                half _Split_d5d0108048044af5bda86f38a68a3d02_B_3_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[2];
                half _Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float =
                    _UV_5d6b82f0ed244cb4aee70351666869f0_Out_0_Vector4[3];
                half _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float;
                Unity_Divide_half(_Split_d5d0108048044af5bda86f38a68a3d02_A_4_Float, half(15),
                                                            _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float);
                half _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
                Unity_Lerp_half(_Property_2d22d37aee374e128936943480fe27a9_Out_0_Float, half(1),
 _Divide_9db6ec721c934425b340fb5c765cd610_Out_2_Float,
 _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float);
                Out_2 = _Lerp_946ef89f993143b48fe5fe8f33f1dd03_Out_3_Float;
            }

            void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
            {
                Out = A * B;
            }

            struct Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
            {
                half4 uv0;
                half4 uv1;
            };

            void SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                float4 _AOInterpolater, UnityTexture2DArray _Textures, UnitySamplerState Sampler, float4 _AOColor,
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float IN, out float4 RGBA_1)
            {
                float4 _Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4 = _AOColor;
                UnityTexture2DArray _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray = _Textures;
                UnitySamplerState _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState = Sampler;
                Bindings_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv1 = IN.uv1;
                _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622.uv0 = IN.uv0;
                half4 _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4;
                SG_SampleFromTexArray_0b8ecfbfa8982cc4eaeb2eaf37e7e7e5_float(
                    _Property_4dbb3b33961e40408607c0fcc4598cea_Out_0_Texture2DArray,
                    _Property_c9136f363c124077939c29dfb084ed8e_Out_0_SamplerState,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622,
                    _SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4);
                float4 _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4 = _AOInterpolater;
                Bindings_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181;
                _AOInterpolation_34061a478fc64a3e9f655b08c489a181.uv0 = IN.uv0;
                half _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float;
                SG_AOInterpolation_7cae45583fc01d243b81f1bfa16a9d64_float(
                    _Property_b96917eb989e4e4aa443e0a53c1f91ad_Out_0_Vector4,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181,
                    _AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float);
                float4 _Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4;
                Unity_Lerp_float4(_Property_9a517af9bc0b4ed8b0a7fde17d5c66a5_Out_0_Vector4,
_SampleFromTexArray_c382d753cc904f0a9ecfb2b41627f622_RGBA_1_Vector4,
(_AOInterpolation_34061a478fc64a3e9f655b08c489a181_intensity_1_Float.
    xxxx),
_Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4);
                Bindings_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05;
                _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05.uv1 = IN.uv1;
                half _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float;
                SG_GetSunLightLevel_65b1e65376b59264c8832500ef266b2e_half(
                    half(0.05), _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float);
                float4 _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4 = float4(
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float,
                    _GetSunLightLevel_8853c6724f584280a8f42dc711ebda05_Out_2_Float, float(1));
                float4 _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
                Unity_Multiply_float4_float4(_Lerp_3f3d0883f9ed43a1baf88b46cb1f581c_Out_3_Vector4,
                                                                   _Vector4_d6e512ef784b42608ba0eedc573eb38d_Out_0_Vector4,
                                                                   _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4);
                RGBA_1 = _Multiply_a3b2207d295b4fbf8f3a01a60b382765_Out_2_Vector4;
            }

            // Custom interpolators pre vertex
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

            // Graph Vertex
            struct VertexDescription
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
                float4 Ambient_Occlusion;
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                VertexDescription description = (VertexDescription)0;
                float _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float = _AOPower;
                float _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float = _AOIntensity;
                float4 _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4 = IN.uv2;
                float4 _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4 = _AOCurve;
                Bindings_ComputeAO_5dce8a19567304442be409564ab90429_float _ComputeAO_1e9428f003fd40519877d28f86c1d0f5;
                float4 _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                SG_ComputeAO_5dce8a19567304442be409564ab90429_float(
                    _Property_3739124fe5a74f2882f857451ac56f1a_Out_0_Vector4,
                    _UV_b1955a1fcddf4f92a3e8d1b903fe31d5_Out_0_Vector4,
                    _Property_ff3abfa6287943e889febdfbc3d236b6_Out_0_Float,
                    _Property_004ebdf0004f4fd7a287934752f96566_Out_0_Float, _ComputeAO_1e9428f003fd40519877d28f86c1d0f5,
                    _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4);
                description.Position = IN.ObjectSpacePosition;
                description.Normal = IN.ObjectSpaceNormal;
                description.Tangent = IN.ObjectSpaceTangent;
                description.Ambient_Occlusion = _ComputeAO_1e9428f003fd40519877d28f86c1d0f5_AO_1_Vector4;
                return description;
            }

            // Custom interpolators, pre surface
            #ifdef FEATURES_GRAPH_VERTEX
            Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
            {
                output.Ambient_Occlusion = input.Ambient_Occlusion;
                return output;
            }

            #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
            #endif

            // Graph Pixel
            struct SurfaceDescription
            {
                float3 BaseColor;
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                UnityTexture2DArray _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray =
                    UnityBuildTexture2DArrayStruct(_Textures);
                UnitySamplerState _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState =
                    UnityBuildSamplerStateStruct(SamplerState_Trilinear_Repeat);
                float4 _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4 = _AOColor;
                Bindings_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv0 = IN.uv0;
                _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996.uv1 = IN.uv1;
                float4 _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4;
                SG_VoxelSubGraph_400a46270d8dede4791d3eed41979b8a_float(
                    IN.Ambient_Occlusion, _Property_d98662e00d4e4b17a6b0d462b0c7bdb2_Out_0_Texture2DArray,
                    _Property_ae043eac759248acbda1c0b8564f3f91_Out_0_SamplerState,
                    _Property_fd821fc372114cb4bef8551c3eb31789_Out_0_Vector4,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996,
                    _VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4);
                surface.BaseColor = (_VoxelSubGraph_fbffd65d7d4c4c01b98abde6ebfe8996_RGBA_1_Vector4.xyz);
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs
            #ifdef HAVE_VFX_MODIFICATION
            #define VFX_SRP_ATTRIBUTES Attributes
            #define VFX_SRP_VARYINGS Varyings
            #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
            #endif
            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                    ZERO_INITIALIZE(VertexDescriptionInputs, output);

                output.ObjectSpaceNormal = input.normalOS;
                output.ObjectSpaceTangent = input.tangentOS.xyz;
                output.ObjectSpacePosition = input.positionOS;
                output.uv2 = input.uv2;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif

                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #ifdef HAVE_VFX_MODIFICATION
                #if VFX_USE_GRAPH_VALUES
                uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
                /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
                #endif
                /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

                #endif

                output.Ambient_Occlusion = input.Ambient_Occlusion;


                #if UNITY_UV_STARTS_AT_TOP
                #else
                #endif


                output.uv0 = input.texCoord0;
                output.uv1 = input.texCoord1;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }

            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

            // --------------------------------------------------
            // Visual Effect Vertex Invocations
            #ifdef HAVE_VFX_MODIFICATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
            #endif
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    CustomEditorForRenderPipeline "UnityEditor.ShaderGraphUnlitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    FallBack "Hidden/Shader Graph/FallbackError"


    /*SubShader
    {

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings2Geom
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings2Frag
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings2Geom vert(Attributes IN)
            {
                Varyings2Geom OUT;
                OUT.positionOS = IN.positionOS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            [maxvertexcount(4)]
            void geom(point Varyings2Geom IN[1], inout TriangleStream<Varyings2Frag> outStream)
            {
                Varyings2Frag OUT;
                OUT.pos = TransformObjectToHClip(IN[0].positionOS);
                OUT.uv = float2(0, 0);
                outStream.Append(OUT);

                OUT.pos = TransformObjectToHClip(IN[0].positionOS + float4(1, 0, 0, 0));
                OUT.uv = float2(1, 0);
                outStream.Append(OUT);

                OUT.pos = TransformObjectToHClip(IN[0].positionOS + float4(0, 1, 0, 0));
                OUT.uv = float2(0, 1);
                outStream.Append(OUT);

                OUT.pos = TransformObjectToHClip(IN[0].positionOS + float4(1, 1, 0, 0));
                OUT.uv = float2(1, 1);
                outStream.Append(OUT);
                outStream.RestartStrip();
            }

            half4 frag(Varyings2Frag IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                return color;
            }
            ENDHLSL
        }
    }*/
}