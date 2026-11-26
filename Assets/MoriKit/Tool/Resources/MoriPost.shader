// 결투 화면 후처리(StagePost): 빛 번짐(Bloom) · 색수차 · 방사 블러 · 충격파 · 기울기 · 채도 · 반전 · 색 입히기 · 비네트.
// 0 = 밝은 곳 고르기 · 1 = 줄이기 · 2 = 키우며 더하기 · 3 = 합치기(모든 화면 효과)
Shader "Hidden/Mori/Post"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Threshold;      // x 문턱 · y 무릎 · z 세기
    float _Chroma;          // uv
    float4 _Blur;           // xy 중심(uv) · z 세기
    float4 _Wave;           // xy 중심(uv) · z 반지름(높이 비율) · w 두께
    float _WaveStr;         // uv
    float _Aspect;          // 가로 / 세로
    float _Tilt;            // 라디안
    float _Sat, _Invert, _Vignette;
    float4 _Tint;           // rgb · a 세기
    float4 _Lights[4];      // xy 중심(uv) · z 반지름(높이 비율) · w 세기
    float4 _LightCols[4];
    float _LightN;
    float4 _Haze;           // xy 중심(uv) · z 반지름(높이 비율) · w 세기(uv)
    float _HazeT;
    sampler2D _MoriNoise;

    half4 Prefilter(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
        soft = soft * soft / (4 * _Threshold.y + 1e-4);
        half k = max(soft, br - _Threshold.x) / max(br, 1e-4);
        return half4(c * k, 1);
    }

    half4 Down(v2f_img i) : SV_Target
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-1, -1, 1, 1);
        half3 c = tex2D(_MainTex, i.uv + o.xy).rgb + tex2D(_MainTex, i.uv + o.zy).rgb + tex2D(_MainTex, i.uv + o.xw).rgb + tex2D(_MainTex, i.uv + o.zw).rgb;
        return half4(c * 0.25, 1);
    }

    half4 Up(v2f_img i) : SV_Target
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
        half3 c = tex2D(_MainTex, i.uv + o.xy).rgb + tex2D(_MainTex, i.uv + o.zy).rgb + tex2D(_MainTex, i.uv + o.xw).rgb + tex2D(_MainTex, i.uv + o.zw).rgb;
        return half4(c * 0.25, 1);
    }

    half3 Sample(float2 uv)
    {
        // 색수차: 가운데에서 바깥쪽으로 빨강 · 파랑을 벌림
        float2 d = (uv - 0.5) * _Chroma * 2;
        half3 c;
        c.r = tex2D(_MainTex, uv + d).r;
        c.g = tex2D(_MainTex, uv).g;
        c.b = tex2D(_MainTex, uv - d).b;
        return c;
    }

    half4 Combine(v2f_img i) : SV_Target
    {
        float2 uv = i.uv;
        // 기울기: 가운데 기준으로 돌리고 가장자리가 비지 않게 조금 키움
        if (abs(_Tilt) > 1e-5)
        {
            float2 p = (uv - 0.5) * float2(_Aspect, 1);
            float s = sin(_Tilt), co = cos(_Tilt);
            p = float2(p.x * co - p.y * s, p.x * s + p.y * co);
            p /= 1 + abs(s) * (_Aspect + 1) * 0.5;
            uv = p / float2(_Aspect, 1) + 0.5;
        }
        // 충격파: 반지름 근처 띠에서 바깥으로 밀어냄
        if (_WaveStr > 1e-5)
        {
            float2 d = (uv - _Wave.xy) * float2(_Aspect, 1);
            float r = length(d);
            float x = (r - _Wave.z) / max(_Wave.w, 1e-4);
            float k = saturate(1 - x * x);
            uv -= normalize(d + 1e-6) / float2(_Aspect, 1) * _WaveStr * k;      // 가운데 쪽을 찍음 = 바깥으로 밀려 보임
        }
        // 일렁임(아지랑이): 원 안에서 노이즈로 화면을 흔듦(가장자리로 갈수록 약하게)
        if (_Haze.w > 1e-6)
        {
            float2 d = (uv - _Haze.xy) * float2(_Aspect, 1);
            float f = saturate(1 - length(d) / max(_Haze.z, 1e-4));
            f = f * f * (3 - 2 * f);
            float2 n = tex2D(_MoriNoise, uv * float2(_Aspect, 1) * 3 + float2(_HazeT * 0.13, _HazeT * 0.21)).rg - 0.5;
            uv += n * _Haze.w * f * 2;
        }
        half3 c;
        // 방사 블러: 중심 쪽으로 여러 번 겹쳐 찍기
        if (_Blur.z > 1e-4)
        {
            c = 0;
            float2 dir = (uv - _Blur.xy) * _Blur.z * 0.25;
            [unroll] for (int j = 0; j < 10; j++) c += Sample(uv - dir * (j / 9.0));
            c /= 10;
        }
        else c = Sample(uv);
        // 빛(주변 밝히기): 화면 색에 빛 색을 곱해 더함(물체가 그 색으로 밝아짐) + 약한 공기 빛
        [unroll] for (int li = 0; li < 4; li++)
        {
            float on = li < _LightN ? 1 : 0;
            float4 L = _Lights[li];
            float2 d = (uv - L.xy) * float2(_Aspect, 1);
            float f = saturate(1 - length(d) / max(L.z, 1e-4));
            f *= f;
            c += (c * 1.2 + 0.12) * _LightCols[li].rgb * L.w * f * on;
        }
        c += tex2D(_BloomTex, uv).rgb * _Threshold.z;
        half l = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(l.xxx, c, _Sat);
        c = lerp(c, 1 - saturate(c), _Invert);
        c = lerp(c, c * _Tint.rgb * 1.5 + _Tint.rgb * 0.15, _Tint.a);
        float2 v = (i.uv - 0.5) * float2(_Aspect, 1);
        c *= 1 - _Vignette * saturate(dot(v, v) * 1.6);
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Prefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Down
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Up
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Combine
            #pragma target 3.0
            ENDCG }
    }
    Fallback Off
}
