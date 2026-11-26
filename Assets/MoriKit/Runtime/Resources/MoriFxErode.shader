// Mori 이펙트 셰이더(스프라이트 · 파티클 · 메시 공용): 침식(디졸브) + 가장자리 발광 + 일렁임 + 흐름 + HDR 발광.
//  · _Erode 0 = 그대로 → 1 = 다 사라짐(노이즈 _MoriNoise 기준, 가는 곳부터). 가장자리는 _EdgeColor × _EdgeGlow 로 빛남.
//  · _Wobble 일렁임(노이즈로 UV 흔들기) · _Scroll 노이즈 흐름 속도 · _Glow 밝기(1 넘으면 HDR → 빛 번짐).
//  · 섞기: _SrcBlend / _DstBlend(빛 = SrcAlpha One · 보통 = SrcAlpha OneMinusSrcAlpha).
//  · 연출 카드의 ‘셰이더 값 곡선’ 으로 위 값들을 시간에 따라 바꾼다(MaterialPropertyBlock).
Shader "MoriFx/Erode"
{
    Properties
    {
        _MainTex ("그림", 2D) = "white" {}
        _Color ("색", Color) = (1,1,1,1)
        _Glow ("밝기(HDR)", Float) = 1
        _Erode ("침식 0~1", Range(0,1)) = 0
        _EdgeWidth ("가장자리 폭", Range(0,0.5)) = 0.08
        [HDR] _EdgeColor ("가장자리 색", Color) = (1,0.8,0.4,1)
        _EdgeGlow ("가장자리 밝기", Float) = 3
        _NoiseScale ("노이즈 크기", Float) = 1
        _Wobble ("일렁임", Range(0,0.2)) = 0
        _Scroll ("노이즈 흐름 속도", Float) = 0.3
        [HideInInspector] _SrcBlend ("Src", Float) = 5
        [HideInInspector] _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _MoriNoise;
            fixed4 _Color; float4 _EdgeColor;
            float _Glow, _Erode, _EdgeWidth, _EdgeGlow, _NoiseScale, _Wobble, _Scroll;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 nuv : TEXCOORD1; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.nuv = v.uv * _NoiseScale;
                o.color = v.color * _Color;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Scroll;
                float2 w = (tex2D(_MoriNoise, i.nuv * 0.7 + float2(t, t * 0.6)).gg - 0.5) * _Wobble * 2;
                float4 c = tex2D(_MainTex, i.uv + w) * i.color;
                float n = tex2D(_MoriNoise, i.nuv + float2(t * 0.3, -t * 0.2)).r * 0.8 + tex2D(_MoriNoise, i.nuv * 3).b * 0.2;
                // 침식: 노이즈가 문턱보다 낮은 곳부터 사라짐. 그림이 옅은(가는) 곳이 먼저 끊기게 알파도 섞음
                float e = _Erode * (1 + _EdgeWidth);
                float m = n * 0.75 + c.a * 0.25;
                float keep = smoothstep(e - 0.02, e, m);
                float edge = (1 - smoothstep(e, e + _EdgeWidth, m)) * step(0.0001, _Erode);
                float4 o;
                o.rgb = c.rgb * _Glow + _EdgeColor.rgb * _EdgeGlow * edge * c.a;
                o.a = c.a * keep;
                o.rgb *= keep;
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
