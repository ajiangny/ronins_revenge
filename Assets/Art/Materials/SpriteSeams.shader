// Draws only the seams of a sprite: a hard network of cracks on its pixel grid, clipped to its silhouette, for Cracked.
// The colour comes from _FlashColor through a MaterialPropertyBlock, as in SpriteFlash. The cracks are worked out per
// pixel of the sprite's own art, so they stay one pixel wide at any scale and never spill past the body.
Shader "Ninja/SpriteSeams"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _FlashColor ("Seam colour", Color) = (1, 0.88, 0.54, 1)
        _Cell ("Crack size in art pixels", Float) = 14
        _Width ("Seam width in art pixels", Float) = 0.9
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
            float4 _MainTex_TexelSize;
            fixed4 _FlashColor;
            float _Cell;
            float _Width;

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

            float2 Scatter(float2 cell)
            {
                return frac(sin(float2(dot(cell, float2(127.1, 311.7)), dot(cell, float2(269.5, 183.3)))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 art = tex2D(_MainTex, i.uv);
                if (art.a < 0.5) discard;
                // The whole art pixel this falls in, so the crack is drawn on the sprite's grid.
                float2 pixel = floor(i.uv * _MainTex_TexelSize.zw) + 0.5;
                float2 at = pixel / _Cell;
                float2 home = floor(at);
                float nearest = 8.0;
                float second = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = home + float2(x, y);
                        float2 to = cell + Scatter(cell) - at;
                        float d = dot(to, to);
                        if (d < nearest)
                        {
                            second = nearest;
                            nearest = d;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }
                // Where two cells nearly tie is a border between them: that border is the seam.
                float gap = (sqrt(second) - sqrt(nearest)) * _Cell;
                if (gap > _Width) discard;
                fixed4 c = i.color * _FlashColor;
                return fixed4(c.rgb, c.a);
            }
            ENDCG
        }
    }
}
