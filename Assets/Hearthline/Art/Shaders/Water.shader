// Creek water (ADR 0018). Transparent: colour by depth (clear in the shallows, darker in the middle),
// the creek bed seen through it with a little refraction, procedural ripples flowing downstream, sun glints,
// a sky reflection that grows at grazing angles (fresnel), and soft foam where it meets the banks.
// Needs the URP depth and opaque textures (both on in PC_RPAsset). All values are for tuning by eye.
Shader "Hearthline/Water"
{
    Properties
    {
        _ShallowColor ("Shallow colour", Color) = (0.32, 0.38, 0.28, 0.35)
        _DeepColor ("Deep colour", Color) = (0.06, 0.13, 0.14, 0.92)
        _DepthRange ("Depth (m) to reach the deep colour", Float) = 1.6
        _Refraction ("Refraction strength", Float) = 0.035
        _RippleScale ("Ripple scale", Float) = 0.45
        _RippleSpeed ("Ripple speed", Float) = 0.6
        _FlowDirection ("Flow direction (xz)", Vector) = (1, 0, 0.15, 0)
        _RippleStrength ("Ripple normal strength", Float) = 0.14
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _ReflectionStrength ("Sky reflection", Range(0, 1)) = 0.4
        _FoamColor ("Foam colour", Color) = (0.85, 0.85, 0.8, 1)
        _FoamDepth ("Foam depth (m)", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float4 _FlowDirection;
                float _DepthRange, _Refraction, _RippleScale, _RippleSpeed, _RippleStrength, _Smoothness, _ReflectionStrength, _FoamDepth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Cheap gradient of a sum of travelling waves: a ripple normal without textures.
            float2 RippleSlope(float2 p, float t)
            {
                float2 flow = normalize(_FlowDirection.xz + 1e-4);
                float2 s = 0;
                float2 dirs[4] = { flow, normalize(flow + float2(0.6, 0.8)), normalize(flow - float2(0.7, 0.5)), normalize(float2(-flow.y, flow.x) + flow * 0.3) };
                float freqs[4] = { 1.0, 1.9, 3.1, 5.3 };
                float amps[4] = { 0.5, 0.3, 0.18, 0.1 };
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float f = freqs[i] * _RippleScale;
                    float phase = dot(p, dirs[i]) * f - t * (1.0 + i * 0.35);
                    s += dirs[i] * cos(phase) * f * amps[i];
                }
                return s;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _RippleSpeed * 4.0;
                // Two layers at unrelated scales so the pattern does not read as a lattice.
                float2 slope = (RippleSlope(input.positionWS.xz, t) + 0.6 * RippleSlope(input.positionWS.xz * 2.37 + 17.0, t * 1.31)) * _RippleStrength;
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depth = max(0.0, sceneDepth - surfaceDepth);

                // Refract: offset the background lookup, but not with things in front of the water.
                float2 refractUV = screenUV + slope * _Refraction * saturate(depth);
                float refractedDepth = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                if (refractedDepth < surfaceDepth) refractUV = screenUV;
                float3 behind = SampleSceneColor(refractUV);

                float k = saturate(depth / max(0.01, _DepthRange));
                half4 water = lerp(_ShallowColor, _DeepColor, k);
                float3 color = lerp(behind, water.rgb, water.a);

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0);
                float3 reflected = GlossyEnvironmentReflection(reflect(-viewDir, normalWS), 1.0 - _Smoothness, 1.0);
                color = lerp(color, reflected, saturate(fresnel * 0.9 + 0.1) * _ReflectionStrength);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 h = normalize(light.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, h)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness;
                color += light.color * spec * light.shadowAttenuation * 1.5;

                float foam = saturate(1.0 - depth / max(0.01, _FoamDepth));
                foam *= 0.55 + 0.45 * sin(dot(input.positionWS.xz, float2(1.3, 0.9)) + t * 0.9 + slope.x * 6.0);
                color = lerp(color, _FoamColor.rgb * (0.6 + 0.4 * light.color), saturate(foam) * 0.7);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
