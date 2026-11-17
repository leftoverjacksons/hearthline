// The sky (ADR 0018): a gradient from horizon to zenith that warms at sunrise and sunset and darkens at
// night, a sun disc, drifting cumulus lit by the sun (bright tops, grey undersides), and faint stars at
// night. The sun's direction comes from URP's main light. Atmosphere drives _Exposure through the day.
// Values are for tuning by eye.
Shader "Hearthline/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith (day)", Color) = (0.27, 0.45, 0.78, 1)
        _HorizonColor ("Horizon (day)", Color) = (0.7, 0.8, 0.88, 1)
        _SunsetColor ("Sunset glow", Color) = (1.0, 0.52, 0.25, 1)
        _NightZenith ("Zenith (night)", Color) = (0.01, 0.015, 0.035, 1)
        _NightHorizon ("Horizon (night)", Color) = (0.04, 0.05, 0.08, 1)
        _SunSize ("Sun size", Range(0.001, 0.1)) = 0.025
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.48
        _CloudSharpness ("Cloud edge sharpness", Range(0.01, 1)) = 0.18
        _CloudScale ("Cloud scale", Float) = 1.6
        _CloudSpeed ("Cloud drift speed", Float) = 0.012
        _CloudShadow ("Cloud underside darkness", Range(0, 1)) = 0.45
        _Exposure ("Exposure", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _SunsetColor, _NightZenith, _NightHorizon;
                float _SunSize, _CloudCoverage, _CloudSharpness, _CloudScale, _CloudSpeed, _CloudShadow, _Exposure;
            CBUFFER_END
            // Set every frame by Atmosphere: the real sun, even when it is down and the moon is URP's main light.
            float4 _HL_SunDirection;
            half4 _HL_SunColor;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int i = 0; i < 5; i++) { v += a * Noise(p); p = p * 2.03 + 3.1; a *= 0.5; }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float3 sunDir = dot(_HL_SunDirection.xyz, _HL_SunDirection.xyz) > 0.01 ? normalize(_HL_SunDirection.xyz) : normalize(_MainLightPosition.xyz);
                float sunHeight = sunDir.y;                       // -1 night .. 1 noon
                float day = smoothstep(-0.35, 0.12, sunHeight);   // a long twilight
                float horizon = pow(1.0 - saturate(dir.y), 3.0);

                float3 dayColor = lerp(_ZenithColor.rgb, _HorizonColor.rgb, horizon);
                float3 nightColor = lerp(_NightZenith.rgb, _NightHorizon.rgb, horizon);
                float3 sky = lerp(nightColor, dayColor, day);

                // Sunrise and sunset: a warm band near the horizon, strongest towards the sun.
                float towardSun = saturate(dot(normalize(float3(dir.x, 0, dir.z) + 1e-4), normalize(float3(sunDir.x, 0, sunDir.z) + 1e-4)) * 0.5 + 0.5);
                float lowSun = 1.0 - smoothstep(0.0, 0.4, abs(sunHeight + 0.05));
                sky = lerp(sky, _SunsetColor.rgb, lowSun * horizon * (0.35 + 0.65 * towardSun) * 0.85);

                // Sun disc and halo.
                float cosSun = dot(dir, sunDir);
                float disc = smoothstep(1.0 - _SunSize * 0.02, 1.0 - _SunSize * 0.015, cosSun);
                float halo = pow(saturate(cosSun), 400.0) * 0.6 + pow(saturate(cosSun), 12.0) * 0.12;
                float3 sunSource = _HL_SunColor.a > 0.0 ? _HL_SunColor.rgb : _MainLightColor.rgb;
                float3 sunColor = sunSource / max(0.001, max(sunSource.r, max(sunSource.g, sunSource.b)));
                sky += sunColor * (disc * 6.0 + halo) * saturate(sunHeight + 0.1);

                // Stars, at night only.
                float3 starCell = floor(dir * 300.0);
                float night = 1.0 - day;
                float star = step(0.9975, Hash(starCell)) * night * night * night * saturate(dir.y * 3.0);
                sky += star * 0.8;

                // Clouds on a flattened dome, drifting.
                if (dir.y > 0.0)
                {
                    float2 uv = dir.xz / (dir.y + 0.18) * _CloudScale;
                    float t = _Time.y * _CloudSpeed;
                    float3 p = float3(uv + float2(t, t * 0.4), t * 0.3);
                    float density = Fbm(p);
                    float cover = smoothstep(1.0 - _CloudCoverage, 1.0 - _CloudCoverage + _CloudSharpness, density);
                    // Light the cloud: sample the density a little towards the sun for self-shadowing.
                    float toward = Fbm(p + float3(sunDir.x, 0, sunDir.z) * 0.25);
                    float lit = saturate(1.0 - (toward - density) * 3.0 - _CloudShadow * cover);
                    float3 cloudDay = lerp(_HorizonColor.rgb * 0.75, float3(1, 1, 1), lit) * lerp(1.0, sunColor, lowSun * 0.6);
                    float3 cloudNight = nightColor * 1.6;
                    float3 cloud = lerp(cloudNight, cloudDay, day);
                    cover *= smoothstep(0.0, 0.12, dir.y);          // thin out towards the horizon
                    sky = lerp(sky, cloud, cover * 0.95);
                }
                return half4(sky * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
