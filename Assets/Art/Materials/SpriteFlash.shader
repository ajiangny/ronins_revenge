// Draws a sprite as a flat silhouette (alpha from the sprite), for white hit flashes and Utsusemi's gold armour
// outline. URP doesn't hand a SpriteRenderer's colour to this shader, so the colour comes from _FlashColor, set per
// renderer through a MaterialPropertyBlock; it defaults to white.
Shader "Ninja/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _FlashColor ("Flash colour", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _FlashColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = i.color * _FlashColor;
                return fixed4(c.rgb, tex2D(_MainTex, i.uv).a * c.a);
            }
            ENDCG
        }
    }
}
