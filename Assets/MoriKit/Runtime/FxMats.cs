using UnityEngine;

namespace MoriFx
{
    /// <summary>‘셰이더 값 곡선’ 적용: 트랙의 mats 를 렌더러들에 MaterialPropertyBlock 으로(머티리얼 파일은 안 바뀜).</summary>
    public static class FxMats
    {
        static MaterialPropertyBlock mpb;

        public static void Apply(Renderer[] rs, FxTrack k, float prog)
        {
            if (rs == null || k.mats == null || k.mats.Count == 0) return;
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                foreach (var m in k.mats) if (m != null && !string.IsNullOrEmpty(m.name)) mpb.SetFloat(m.name, m.At(prog));
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>값을 걸었던 렌더러를 원래대로(풀에 돌려줄 때).</summary>
        public static void Clear(Renderer[] rs)
        {
            if (rs == null) return;
            foreach (var r in rs) if (r != null) r.SetPropertyBlock(null);
        }

        /// <summary>침식 셰이더 머티리얼(빛 · 보통). 없으면 null.</summary>
        public static Material Erode(bool add)
        {
            var sh = Resources.Load<Shader>("MoriFxErode");
            if (sh == null || !sh.isSupported) return null;
            var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            return m;
        }
    }
}
