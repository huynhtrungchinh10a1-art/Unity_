Shader "Custom/SandstormScreenHaze"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _SandColor ("Sand Tint & Base Alpha", Color) = (0.76, 0.60, 0.38, 0.45)
        _WindSpeed ("Wind Direction & Speed (XY)", Vector) = (0.35, 0.08, 0, 0)
        _Density ("Storm Density", Range(0, 2)) = 0.8
        _NoiseScale ("Dust Scale", Float) = 3.5
        _CenterClarity ("Center Clarity (0=Full, 1=Clear)", Range(0, 1)) = 0.45
        _GustSpeed ("Wind Gust Frequency", Float) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _SandColor;
            float4 _WindSpeed;
            float _Density;
            float _NoiseScale;
            float _CenterClarity;
            float _GustSpeed;

            // Fast procedural hash
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth 2D value noise
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _Time.y;

                // 1. Từng đợt gió giật (Wind Gust pulsing)
                float gust = sin(t * _GustSpeed) * 0.18 + 0.82;

                // 2. Lớp 1: Đám mây bụi cát lớn trôi chậm theo chiều gió
                float2 uv1 = uv * _NoiseScale - _WindSpeed.xy * (t * 0.7);
                float dustLayer1 = valueNoise(uv1);

                // 3. Lớp 2: Vệt gió cát kéo sợi dài tạt ngang với tốc độ nhanh hơn
                float2 uv2 = float2(uv.x * 3.0, uv.y * 0.7) * _NoiseScale - _WindSpeed.xy * (t * 1.5);
                float dustLayer2 = valueNoise(uv2 + float2(4.3, 1.7));

                // 4. Lớp 3: Hạt cát mịn li ti bay rít trong không khí
                float2 uv3 = uv * (_NoiseScale * 2.8) - _WindSpeed.xy * (t * 2.3);
                float dustLayer3 = valueNoise(uv3 + float2(8.1, 9.2));

                // Hòa trộn 3 lớp cát
                float sandStorm = dustLayer1 * 0.45 + dustLayer2 * 0.40 + dustLayer3 * 0.15;

                // 5. Độ rõ vùng tâm (Center Clarity) để người chơi không bị mù mắt trong combat
                float2 centered = uv - float2(0.5, 0.5);
                float dist = length(centered) * 1.414; // 0 ở tâm, ~1 ở góc mép
                float clarity = lerp(1.0 - _CenterClarity, 1.0, saturate(dist * 1.2));

                // 6. Tính Alpha cuối cùng
                float finalAlpha = saturate(sandStorm * gust * clarity * _Density * _SandColor.a * IN.color.a);

                return fixed4(_SandColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
