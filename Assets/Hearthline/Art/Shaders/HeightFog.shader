// Height fog and light shafts (ADR 0028), a full-screen pass before post-processing.
//
// Fog: denser low down, thinning with height above a base level (the valley floor), integrated along each view
// ray from the depth buffer, so it pools in hollows and along the water and drapes the ridges. It glows toward
// the sun. Light shafts: from each pixel, step toward the sun on screen; where the depth buffer shows open sky
// (gaps in the canopy, the edge of a ridge), add sunlight. Parameters come from shader globals that Atmosphere
// sets each frame, so the pass does nothing until a scene drives it (density 0).
Shader "Hearthline/HeightFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "HeightFog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _HL_HeightFog;        // x: density per metre at the base, y: base height, z: falloff per metre, w: max opacity
            float4 _HL_HeightFogColor;   // rgb: fog colour; a: how much the sun's colour glows through it
            float4 _HL_Shafts;           // x: strength, y: reach (0..1 of the way to the sun), z: decay per step
            float4 _HL_SunDirection;     // toward the sun
            float4 _HL_SunColor;

            static const int ShaftSteps = 24;

            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 0.000001;
            #else
                return rawDepth >= 0.999999;
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float raw = SampleSceneDepth(uv);
                bool sky = IsSky(raw);
            #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
            #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 eye = _WorldSpaceCameraPos;
                float3 ray = world - eye;
                float dist = length(ray);
                float3 dir = ray / max(dist, 0.0001);
                if (sky) { dist = 700.0; world = eye + dir * dist; }

                // ---- Height fog: density * exp(-falloff * (h - base)), integrated along the ray.
                float density = _HL_HeightFog.x, k = max(_HL_HeightFog.z, 0.0001);
                if (density > 0.0)
                {
                    float h0 = eye.y - _HL_HeightFog.y, h1 = world.y - _HL_HeightFog.y;
                    float dh = h1 - h0;
                    float a0 = exp(-k * clamp(h0, -60.0, 400.0)), a1 = exp(-k * clamp(h1, -60.0, 400.0));
                    float mean = abs(dh) > 0.01 ? (a0 - a1) / (k * dh) : a0;
                    float depthOptical = density * dist * mean;
                    float fog = min(1.0 - exp(-depthOptical), _HL_HeightFog.w);
                    float toSun = pow(saturate(dot(dir, _HL_SunDirection.xyz)), 6.0);
                    float3 tint = _HL_HeightFogColor.rgb + _HL_SunColor.rgb * toSun * _HL_HeightFogColor.a;
                    colour.rgb = lerp(colour.rgb, tint, fog);
                }

                // ---- Light shafts: march toward the sun on screen, gathering open sky through the canopy.
                if (_HL_Shafts.x > 0.0)
                {
                    float4 sunClip = mul(UNITY_MATRIX_VP, float4(eye + _HL_SunDirection.xyz * 1000.0, 1.0));
                    if (sunClip.w > 0.0)
                    {
                        float2 sunUv = sunClip.xy / sunClip.w * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                        sunUv.y = 1.0 - sunUv.y;
                    #endif
                        float2 step = (sunUv - uv) * _HL_Shafts.y / ShaftSteps;
                        float2 p = uv;
                        float weight = 1.0, light = 0.0;
                        for (int i = 0; i < ShaftSteps; i++)
                        {
                            p += step;
                            if (any(p < 0.0) || any(p > 1.0)) break;
                            light += (IsSky(SampleSceneDepth(p)) ? 1.0 : 0.0) * weight;
                            weight *= _HL_Shafts.z;
                        }
                        light /= ShaftSteps;
                        // Strongest looking toward the sun; none when it's behind you.
                        float facing = pow(saturate(dot(dir, _HL_SunDirection.xyz)), 2.0);
                        float above = saturate(_HL_SunDirection.y * 4.0);
                        colour.rgb += _HL_SunColor.rgb * light * facing * above * _HL_Shafts.x * (sky ? 0.3 : 1.0);
                    }
                }
                return colour;
            }
            ENDHLSL
        }
    }
}
