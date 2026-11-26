using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mori.Core.Art;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>사슬 하나의 설정(파일에 저장). 길이 · 굵기는 그림 픽셀 단위.</summary>
    [Serializable]
    public sealed class ChainCfg
    {
        public string name = "";              // "hero:머리1" (프리셋이면 프리셋 이름)
        public string preset = "머리 가닥";
        public float lenPx = 48, widthPx = 5, tipPx = 1.5f;
        public int segs = 6;
        public float gravity = 0.8f, drag = 0.06f, stiff = 0.12f, follow = 0.25f;
        public float angle = 70;              // 쉬는 방향(도): 0 = 바로 아래 · 90 = 등 뒤 수평 · 180 = 위
        public string root = "#E6E6EE", tip = "#9C9CB4", outline = "#1A1420";
        public bool outlineOn = true, off;

        public ChainCfg Clone() => (ChainCfg)MemberwiseClone();
        public void CopyLook(ChainCfg o)
        {
            preset = o.name; lenPx = o.lenPx; widthPx = o.widthPx; tipPx = o.tipPx; segs = o.segs;
            gravity = o.gravity; drag = o.drag; stiff = o.stiff; follow = o.follow; angle = o.angle;
            root = o.root; tip = o.tip; outline = o.outline; outlineOn = o.outlineOn;
        }
    }

    [Serializable]
    public sealed class ChainFile
    {
        public List<ChainCfg> chains = new List<ChainCfg>();
        public List<ChainCfg> presets = new List<ChainCfg>();
    }

    /// <summary>
    /// 사슬 설정 창고: StreamingAssets/Fight/chains.json. 프리셋(머리 가닥 · 천 자락 · 리본 · 작은 장신구 + 저장한 것)에서 꺼내 쓴다.
    /// </summary>
    public static class ChainStore
    {
        static ChainFile file;
        public static int Version { get; private set; }
        public static string FilePath => Path.Combine(ArtLib.Dir, "chains.json");

        public static readonly ChainCfg[] BuiltIn =
        {
            new ChainCfg { name = "머리 가닥", gravity = 0.35f, drag = 0.06f, stiff = 0.35f, follow = 0.3f },
            new ChainCfg { name = "천 자락", gravity = 0.6f, drag = 0.1f, stiff = 0.22f, follow = 0.2f },
            new ChainCfg { name = "리본", gravity = 0.5f, drag = 0.14f, stiff = 0.28f, follow = 0.3f },
            new ChainCfg { name = "작은 장신구", gravity = 0.8f, drag = 0.1f, stiff = 0.55f, follow = 0.5f },
            new ChainCfg { name = "뻣뻣하게", gravity = 0.2f, drag = 0.12f, stiff = 0.8f, follow = 0.6f },
        };

        static ChainFile F
        {
            get
            {
                if (file != null) return file;
                file = new ChainFile();
                try { if (File.Exists(FilePath)) file = JsonUtility.FromJson<ChainFile>(File.ReadAllText(FilePath)) ?? new ChainFile(); }
                catch (Exception e) { Debug.LogWarning("[Mori] chains.json 읽기 실패: " + e.Message); }
                return file;
            }
        }

        public static IEnumerable<ChainCfg> Presets => BuiltIn.Concat(F.presets);
        public static IEnumerable<ChainCfg> All => F.chains;

        /// <summary>owner("hero" · "foe")의 사슬 설정 — 없으면 이름으로 짐작한 프리셋으로 만든다(저장은 바꿀 때).</summary>
        public static ChainCfg Get(string owner, string name)
        {
            string id = owner + ":" + name;
            var c = F.chains.FirstOrDefault(x => x.name == id);
            if (c != null) return c;
            var guess = name.Contains("천") || name.Contains("망토") || name.Contains("옷") ? BuiltIn[1]
                      : name.Contains("리본") || name.Contains("끈") ? BuiltIn[2]
                      : name.Contains("귀") || name.Contains("장신") || name.Contains("방울") ? BuiltIn[3] : BuiltIn[0];
            c = guess.Clone(); c.name = id; c.preset = guess.name;
            F.chains.Add(c);
            return c;
        }

        public static void Save()
        {
            Version++;
            try { Directory.CreateDirectory(ArtLib.Dir); KitBackup.Before(FilePath); File.WriteAllText(FilePath, JsonUtility.ToJson(F, true)); }
            catch (Exception e) { Debug.LogWarning("[Mori] chains.json 저장 실패: " + e.Message); }
        }

        /// <summary>지금 설정을 프리셋으로(같은 이름이면 덮어씀).</summary>
        public static void SavePreset(string presetName, ChainCfg from)
        {
            var p = from.Clone(); p.name = presetName; p.preset = ""; p.off = false;
            F.presets.RemoveAll(x => x.name == presetName);
            F.presets.Add(p);
            Save();
        }

        public static void Reload() { file = null; Version++; }
    }

    /// <summary>스프라이트가 화면에 놓인 자리(사슬을 그림 픽셀 격자에 맞추려고).</summary>
    public struct SpriteGeom
    {
        public bool Valid, Flip;
        public float Left, Top, K;      // K = 그림 1px 의 화면 크기
        public int TexW, TexH;
    }

    /// <summary>
    /// 캐릭터 한 명의 사슬들: 애니메이션의 사슬(@이름 뼈 + ~이름 그림)마다 — 뼈대를 세계 좌표에서 흔들고,
    /// 직접 그린 그림을 그 뼈대에 맞춰 휘어 캐릭터와 같은 픽셀 격자에 그린다(쉬는 자세면 원래 그림 그대로).
    /// 몸 뒤 사슬 = back 층, 앞 사슬 = front 층(캐릭터 그림 바로 뒤 · 앞).
    /// </summary>
    public sealed class ChainRig
    {
        sealed class Piece
        {
            public ChainSim Sim = new ChainSim();
            public VisualElement El;
            public Texture2D Tex;
            public byte[] Buf, Flip;
            public int S;
            public bool Used;
        }

        readonly VisualElement back, front;
        readonly Dictionary<string, Piece> pieces = new Dictionary<string, Piece>();
        readonly ChainSim.Params prm = new ChainSim.Params();
        readonly Dictionary<ArtLib.ArtPx, byte[]> mirrored = new Dictionary<ArtLib.ArtPx, byte[]>();
        readonly Dictionary<(ArtLib.ArtPx, List<(double, double)>), double> halfW = new Dictionary<(ArtLib.ArtPx, List<(double, double)>), double>();
        public const float BaseGravity = 25f * Mori.Core.Fight.FightUnit.Sf;      // U/s² — 길이 단위 바꾸기(FightUnit.S)와 같이 줄임

        public ChainRig(VisualElement back, VisualElement front) { this.back = back; this.front = front; }

        public void Hide() { foreach (var p in pieces.Values) { p.El.style.display = DisplayStyle.None; p.Sim.Ready = false; } }

        public void Update(StageWorld w, float W, float H, float u, Vector2 shake, ArtLib.Anim anim, int frame, SpriteGeom g, int facing, string owner, float dt)
        {
            foreach (var p in pieces.Values) p.Used = false;
            if (mirrored.Count > 64) mirrored.Clear();
            if (halfW.Count > 256) halfW.Clear();
            if (anim != null && g.Valid && anim.Tracks != null && u > 0)
                foreach (var t in anim.Tracks)
                {
                    if (t.Joints == null || t.Joints.Length == 0) continue;
                    int fi = Mathf.Clamp(frame, 0, t.Joints.Length - 1);
                    var joints = t.Joints[fi];
                    var art = t.Art != null && fi < t.Art.Length ? t.Art[fi] : null;
                    if (joints == null || joints.Count == 0 || art == null) continue;
                    var cfg = ChainStore.Get(owner, t.Name);
                    if (cfg.off) continue;
                    if (!pieces.TryGetValue(t.Name, out var pc)) pieces[t.Name] = pc = NewPiece();
                    pc.Used = true;
                    var host = t.Back ? back : front;
                    if (pc.El.parent != host) host.Add(pc.El);
                    Run(pc, cfg, w, W, H, u, shake, g, joints, art, dt);
                }
            foreach (var p in pieces.Values) if (!p.Used) { p.El.style.display = DisplayStyle.None; p.Sim.Ready = false; }
        }

        Piece NewPiece()
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return new Piece { El = el };
        }

        void Run(Piece pc, ChainCfg cfg, StageWorld w, float W, float H, float u, Vector2 shake, SpriteGeom g, List<(double x, double y)> joints, ArtLib.ArtPx art, float dt)
        {
            int m = joints.Count;
            // 화면에 보이는 방향의 그림 좌표(좌우 뒤집힘 반영)
            var rest = new (double x, double y)[m];
            for (int i = 0; i < m; i++) rest[i] = (g.Flip ? g.TexW - joints[i].x : joints[i].x, joints[i].y);
            byte[] apx = art.Rgba; int aL = art.L;
            if (g.Flip)
            {
                if (!mirrored.TryGetValue(art, out apx)) mirrored[art] = apx = Mirror(art.Rgba, art.W, art.H);
                aL = g.TexW - (art.L + art.W);
            }
            if (!halfW.TryGetValue((art, joints), out double hw))
            {
                var canvasJ = new List<(double, double)>(joints);
                halfW[(art, joints)] = hw = BoneWarp.HalfWidth(art.Rgba, art.W, art.H, art.L, art.T, canvasJ);
            }
            // 뿌리: 그림 좌표 → 화면 → 세계
            float sx = g.Left + (float)rest[0].x * g.K, sy = g.Top + (float)rest[0].y * g.K;
            double wx = w.CamX + (sx - shake.x - W * 0.5f) / u, wy = w.CamY + (w.Ground(H) - (sy - shake.y)) / u;
            double pxU = g.K / u;
            var cur = new (double x, double y)[m];
            if (m >= 2)
            {
                if (prm.Lens == null || prm.Lens.Length != m - 1) { prm.Lens = new double[m - 1]; prm.RestX = new double[m - 1]; prm.RestY = new double[m - 1]; }
                for (int i = 0; i + 1 < m; i++)
                {
                    double dx = rest[i + 1].x - rest[i].x, dy = rest[i + 1].y - rest[i].y, d = Math.Sqrt(dx * dx + dy * dy);
                    prm.Lens[i] = Math.Max(0.5, d) * pxU;
                    prm.RestX[i] = d > 1e-6 ? dx / d : 0; prm.RestY[i] = d > 1e-6 ? -dy / d : -1;      // 세계는 위가 +
                }
                prm.Gravity = BaseGravity * cfg.gravity;
                prm.Drag = cfg.drag; prm.Stiff = cfg.stiff; prm.Follow = cfg.follow;
                pc.Sim.Step(dt, wx, wy, prm);
                for (int i = 0; i < m && i < pc.Sim.Count; i++)
                {
                    var sp = w.ToScreen(pc.Sim.X[i], pc.Sim.Y[i], W, H) + shake;
                    cur[i] = ((sp.x - g.Left) / g.K, (sp.y - g.Top) / g.K);
                }
            }
            else cur[0] = rest[0];
            // 그릴 판: 뿌리 둘레 정사각형(사슬이 아무리 휘어도 뿌리에서 길이 + 반폭 안)
            double total = 0;
            for (int i = 0; i + 1 < m; i++) total += Math.Sqrt((rest[i + 1].x - rest[i].x) * (rest[i + 1].x - rest[i].x) + (rest[i + 1].y - rest[i].y) * (rest[i + 1].y - rest[i].y));
            double reach = total + hw * 2 + Math.Max(art.W, art.H) * (m < 2 ? 1 : 0) + 4;
            int S = Mathf.CeilToInt((float)reach) * 2 + 2;
            S = Mathf.Clamp(S, 8, 2048);
            if (pc.Tex == null || pc.S != S)
            {
                if (pc.Tex != null) TexGrave.Kill(pc.Tex);
                pc.S = S;
                pc.Tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Mori chain" };
                pc.Buf = new byte[S * S * 4]; pc.Flip = new byte[S * S * 4];
            }
            Array.Clear(pc.Buf, 0, pc.Buf.Length);
            int ox = Mathf.FloorToInt((float)cur[0].x) - S / 2, oy = Mathf.FloorToInt((float)cur[0].y) - S / 2;
            if (m >= 2) BoneWarp.Warp(apx, art.W, art.H, aL, art.T, rest, cur, hw, pc.Buf, ox, oy, S, S);
            else
            {
                // 뼈가 한 마디뿐: 그림을 그대로(뿌리를 따라 옮기기만)
                int dx = Mathf.RoundToInt((float)(cur[0].x - rest[0].x)), dy = Mathf.RoundToInt((float)(cur[0].y - rest[0].y));
                for (int y = 0; y < art.H; y++)
                    for (int x = 0; x < art.W; x++)
                    {
                        int si = (y * art.W + x) * 4; if (apx[si + 3] == 0) continue;
                        int tx = aL + x + dx - ox, ty = art.T + y + dy - oy;
                        if (tx < 0 || ty < 0 || tx >= S || ty >= S) continue;
                        Buffer.BlockCopy(apx, si, pc.Buf, (ty * S + tx) * 4, 4);
                    }
            }
            // 텍스처는 아래 → 위 줄
            for (int y = 0; y < S; y++) Buffer.BlockCopy(pc.Buf, y * S * 4, pc.Flip, (S - 1 - y) * S * 4, S * 4);
            pc.Tex.SetPixelData(pc.Flip, 0);
            pc.Tex.Apply(false);
            var el = pc.El;
            el.style.display = DisplayStyle.Flex;
            el.style.backgroundImage = new StyleBackground(pc.Tex);
            el.style.left = g.Left + ox * g.K; el.style.top = g.Top + oy * g.K;
            el.style.width = S * g.K; el.style.height = S * g.K;
        }

        static byte[] Mirror(byte[] src, int w, int h)
        {
            var o = new byte[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Buffer.BlockCopy(src, (y * w + x) * 4, o, (y * w + (w - 1 - x)) * 4, 4);
            return o;
        }

        public static Color32 Hex(string h)
        {
            if (!string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out var c)) return c;
            return new Color32(230, 230, 238, 255);
        }
    }
}
