// 프리팹 화면 빛 보정: 더하기(빛) 파티클은 알파를 쓰지 않아 투명 바탕의 렌더 텍스처에선 UI 에 안 보인다.
// 색 밝기를 알파로 옮기고(미리 곱한 색으로 보고) 다시 나눠서, UI 가 일반 알파로 겹쳐도 빛이 그대로 보이게 한다.
Shader "Hidden/Mori/FxAlpha"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float a = saturate(max(c.a, max(c.r, max(c.g, c.b))));
                float3 rgb = a > 0.0001 ? saturate(c.rgb / a) : float3(0, 0, 0);
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
