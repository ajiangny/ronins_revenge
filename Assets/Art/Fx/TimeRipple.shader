// A full-screen pass for Utsusemi's stopped time: thin rings that bend the picture a few pixels as they pass (spatial
// ripples), and _TimeStop, which drains and cools the colour while the world is held still. Driven entirely by
// globals from TimeRipple.cs, and only drawn while TimeRippleFeature sees something live.
Shader "Ninja/TimeRipple"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "TimeRipple"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Per ring: xy centre in viewport UV, z radius and w push, both in view heights.
            float4 _RippleRings[4];
            // Half the band's width, in view heights.
            float _RippleWidth;
            float _TimeStop;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // Measured in view heights so the rings stay round on any screen.
                float2 push = 0;
                for (int i = 0; i < 4; i++)
                {
                    float4 ring = _RippleRings[i];
                    if (ring.w <= 0) continue;
                    float2 d = (uv - ring.xy) * float2(aspect, 1);
                    float dist = length(d);
                    float x = (dist - ring.z) / _RippleWidth;
                    if (abs(x) >= 1) continue;
                    // One crest and one trough across the band, easing to nothing at its edges.
                    float wave = sin(x * PI) * (1 - x * x);
                    push += d / max(dist, 1e-4) * wave * ring.w;
                }
                uv += push / float2(aspect, 1);

                // Point sampling keeps the shifted pixels crisp, so the ripple reads as pixel art.
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                half grey = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 still = lerp(c.rgb, grey.xxx, 0.6) * half3(0.86, 0.93, 1.08);
                c.rgb = lerp(c.rgb, still, saturate(_TimeStop));
                return c;
            }
            ENDHLSL
        }
    }
}
