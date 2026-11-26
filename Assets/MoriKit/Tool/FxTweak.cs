using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>파티클 시스템 하나(또는 전체)에 거는 배율. 1 = 그대로.</summary>
    [Serializable]
    public sealed class FxTweakSys
    {
        public string path = "";           // 프리팹 안 경로("" = 전체, "." = 맨 위, "Arc/Spark" …)
        public float size = 1, speed = 1, life = 1, count = 1, glow = 1, gravity = 1;
        public string color = "FFFFFF";    // 색 곱하기
        public bool IsDefault => size == 1 && speed == 1 && life == 1 && count == 1 && glow == 1 && gravity == 1 && string.Equals(color, "FFFFFF", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>프리팹 하나의 빠른 조율(StreamingAssets/Fight/fx-tweaks/&lt;프리팹&gt;.json). ‘프리팹에 적용’ 전까지는 이 파일로만 있음.</summary>
    [Serializable]
    public sealed class FxTweak
    {
        public string prefab = "";
        public List<FxTweakSys> systems = new List<FxTweakSys>();

        public FxTweakSys Get(string path, bool make)
        {
            var s = systems.FirstOrDefault(x => x.path == path);
            if (s == null && make) { s = new FxTweakSys { path = path }; systems.Add(s); }
            return s;
        }

        public bool IsDefault => systems.All(x => x.IsDefault);
    }

    /// <summary>
    /// 빠른 조율 보관 · 적용. 파티클의 원래 값을 기억해 두고 배율만 곱한다(몇 번을 바꿔도 원래 값에서 다시 계산).
    /// </summary>
    public static class FxTweaks
    {
        public static string Dir => Path.Combine(ArtLib.Dir, "fx-tweaks");
        static readonly Dictionary<string, FxTweak> map = new Dictionary<string, FxTweak>(StringComparer.OrdinalIgnoreCase);
        static bool loaded;
        public static int Version { get; private set; }

        static void Ensure()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!Directory.Exists(Dir)) return;
                foreach (var f in Directory.GetFiles(Dir, "*.json"))
                {
                    var t = JsonUtility.FromJson<FxTweak>(File.ReadAllText(f));
                    if (t == null) continue;
                    if (string.IsNullOrEmpty(t.prefab)) t.prefab = Path.GetFileNameWithoutExtension(f);
                    map[t.prefab] = t;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mori] fx-tweaks 읽기 실패: " + e.Message); }
        }

        public static FxTweak Find(string prefab) { Ensure(); return prefab != null && map.TryGetValue(prefab, out var t) ? t : null; }

        public static FxTweak Get(string prefab)
        {
            var t = Find(prefab);
            if (t == null) { t = new FxTweak { prefab = prefab }; map[prefab] = t; }
            return t;
        }

        static string FileOf(string prefab)
        {
            var bad = Path.GetInvalidFileNameChars();
            return Path.Combine(Dir, new string(prefab.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()) + ".json");
        }

        public static void Save(FxTweak t)
        {
            Version++;
            try
            {
                Directory.CreateDirectory(Dir);
                t.systems.RemoveAll(x => x.IsDefault);
                KitBackup.Before(FileOf(t.prefab));
                if (t.systems.Count == 0) { if (File.Exists(FileOf(t.prefab))) File.Delete(FileOf(t.prefab)); return; }
                File.WriteAllText(FileOf(t.prefab), JsonUtility.ToJson(t, true));
            }
            catch (Exception e) { Debug.LogWarning("[Mori] fx-tweaks 저장 실패: " + e.Message); }
        }

        public static void Set(FxTweak t) { map[t.prefab] = t; Save(t); }

        // ───────── 경로 ─────────
        /// <summary>프리팹(인스턴스) 맨 위에서 이 오브젝트까지의 경로(맨 위 = ".").</summary>
        public static string PathOf(Transform root, Transform t)
        {
            if (t == root) return ".";
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static List<(string path, ParticleSystem ps)> Systems(GameObject go)
        {
            var o = new List<(string, ParticleSystem)>();
            if (go == null) return o;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) o.Add((PathOf(go.transform, ps.transform), ps));
            return o;
        }

        // ───────── 원래 값 ─────────
        public sealed class Orig
        {
            public ParticleSystem.MinMaxCurve size, speed, life, rate, gravity;
            public ParticleSystem.MinMaxGradient color;
            public ParticleSystem.Burst[] bursts;
        }

        public static Orig Capture(ParticleSystem ps)
        {
            var m = ps.main; var e = ps.emission;
            var o = new Orig { size = m.startSize, speed = m.startSpeed, life = m.startLifetime, rate = e.rateOverTime, gravity = m.gravityModifier, color = m.startColor };
            o.bursts = new ParticleSystem.Burst[e.burstCount];
            e.GetBursts(o.bursts);
            return o;
        }

        /// <summary>원래 값 × (전체 배율 × 이 시스템 배율)을 파티클 시스템에 넣음.</summary>
        public static void Apply(ParticleSystem ps, Orig o, FxTweakSys all, FxTweakSys one)
        {
            float K(Func<FxTweakSys, float> f) => (all != null ? f(all) : 1) * (one != null ? f(one) : 1);
            var m = ps.main; var e = ps.emission;
            m.startSize = Mul(o.size, K(x => x.size));
            m.startSpeed = Mul(o.speed, K(x => x.speed));
            m.startLifetime = Mul(o.life, K(x => x.life));
            m.gravityModifier = Mul(o.gravity, K(x => x.gravity));
            float cnt = K(x => x.count);
            e.rateOverTime = Mul(o.rate, cnt);
            if (o.bursts.Length > 0)
            {
                var b = (ParticleSystem.Burst[])o.bursts.Clone();
                for (int i = 0; i < b.Length; i++) b[i].count = Mul(b[i].count, cnt);
                e.SetBursts(b);
            }
            var tint = Col(all) * Col(one);
            float glow = K(x => x.glow);
            tint = new Color(tint.r * glow, tint.g * glow, tint.b * glow, tint.a);
            m.startColor = Tint(o.color, tint);
        }

        static Color Col(FxTweakSys s)
        {
            if (s == null || string.IsNullOrEmpty(s.color)) return Color.white;
            return ColorUtility.TryParseHtmlString("#" + s.color.TrimStart('#'), out var c) ? c : Color.white;
        }

        public static ParticleSystem.MinMaxCurve Mul(ParticleSystem.MinMaxCurve c, float k)
        {
            if (Mathf.Approximately(k, 1)) return c;
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return new ParticleSystem.MinMaxCurve(c.constant * k);
                case ParticleSystemCurveMode.TwoConstants: return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
                default: c.curveMultiplier *= k; return c;
            }
        }

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color t)
        {
            if (t == Color.white) return g;
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(g.color * t);
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(g.colorMin * t, g.colorMax * t);
                case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(TintG(g.gradientMin, t), TintG(g.gradientMax, t));
                default:
                {
                    var r = new ParticleSystem.MinMaxGradient(TintG(g.gradient, t));
                    r.mode = g.mode;
                    return r;
                }
            }
        }

        static Gradient TintG(Gradient g, Color t)
        {
            if (g == null) return null;
            var n = new Gradient { mode = g.mode };
            n.SetKeys(g.colorKeys.Select(k => new GradientColorKey(k.color * new Color(t.r, t.g, t.b, 1), k.time)).ToArray(), g.alphaKeys);
            return n;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 조율 값을 프리팹 파일에 써 넣음(에디터 전용). 먼저 Assets/_backup 에 원본을 복사. 성공하면 조율 파일은 비움(이제 프리팹이 그 값).
        /// </summary>
        public static string BakeToPrefab(string prefab)
        {
            var t = Find(prefab);
            var src = PrefabLib.Get(prefab);
            if (t == null || t.IsDefault) return "바꾼 값이 없어요.";
            if (src == null) return "프리팹을 찾지 못했어요.";
            string path = UnityEditor.AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return "프리팹 파일이 아니에요(" + path + ").";
            try
            {
                const string BK = "Assets/_backup";
                if (!UnityEditor.AssetDatabase.IsValidFolder(BK)) UnityEditor.AssetDatabase.CreateFolder("Assets", "_backup");
                string bk = $"{BK}/{Path.GetFileNameWithoutExtension(path)}_{DateTime.Now:yyyyMMdd_HHmmss}.prefab";
                if (!UnityEditor.AssetDatabase.CopyAsset(path, bk)) return "백업에 실패해서 적용하지 않았어요.";
                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var all = t.Get("", false);
                    foreach (var (p, ps) in Systems(root)) Apply(ps, Capture(ps), all, t.Get(p, false));
                    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
                t.systems.Clear();
                Save(t);
                return "적용했어요 · 백업: " + bk;
            }
            catch (Exception e) { return "적용 실패: " + e.Message; }
        }
#endif
    }
}
