using System;
using System.IO;
using MoriFx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>결투 화면 후처리 기본값(StreamingAssets/Fight/post.json). 연출 카드의 화면 효과 트랙이 여기에 더해짐.</summary>
    [Serializable]
    public sealed class PostSettings
    {
        public bool on = true;
        public float bloom = 0.6f;          // 빛 번짐 세기
        public float threshold = 0.72f;     // 이 밝기부터 번짐(0 ~ 1)
        public float knee = 0.25f;          // 문턱 부드럽게
        public int steps = 5;               // 번짐 넓이(단계)
        public float vignette = 0.15f;      // 가장자리 어둡게
        public float chroma;                // 기본 색수차(px@1080)
        public float sat = 1;               // 기본 채도
        public float res = 1;               // 후처리 해상도(화면 대비)
        public float ppu = 100;             // 그림 크기: 1U = 몇 px(MFS 스프라이트 Pixels Per Unit 과 같게). 캔버스 크기와 상관없이 픽셀 크기가 같음
        public static float Ppu => Mathf.Max(1f, F.ppu);
        public bool flipY;                  // 효과 중심이 위아래로 뒤집혀 보이면 켬(그래픽 장치에 따라)
        // MFS 화면(URP): 결투 그림 + 이펙트를 게임과 같은 URP 카메라 설정으로(볼륨 · 글로벌 라이트 2D 를 게임 코드 그대로)
        public bool mfs = true;             // 켬 = URP 카메라로 그림
        public bool volume = true;          // 볼륨(후처리: Bloom · 톤매핑 · 컬러 그레이딩)
        public bool light = true;           // 글로벌 라이트 2D(무대 판이 빛을 받음)
        public string charLayer = "";       // 무대 판(캐릭터 · 배경)의 정렬 레이어(비우면 짐작)
        public string fxLayer = "";         // 킷이 만드는 이펙트(그림 · 잔상 · 조각)의 정렬 레이어
        public bool stageFlip;              // 무대 판이 위아래 뒤집혀 보이면 켬(그래픽 장치에 따라)

        static PostSettings f;
        public static int Version { get; private set; }
        public static string FilePath => Path.Combine(ArtLib.Dir, "post.json");

        public static PostSettings F
        {
            get
            {
                if (f != null) return f;
                f = new PostSettings();
                try { if (File.Exists(FilePath)) f = JsonUtility.FromJson<PostSettings>(File.ReadAllText(FilePath)) ?? new PostSettings(); }
                catch (Exception e) { Debug.LogWarning("[Mori] post.json 읽기 실패: " + e.Message); }
                return f;
            }
        }

        public static void Save()
        {
            Version++;
            try { Directory.CreateDirectory(ArtLib.Dir); KitBackup.Before(FilePath); File.WriteAllText(FilePath, JsonUtility.ToJson(F, true)); }
            catch (Exception e) { Debug.LogWarning("[Mori] post.json 저장 실패: " + e.Message); }
        }
    }

    /// <summary>
    /// 결투 화면 + 후처리. 결투 화면(StageField)을 화면 밖 UI 패널(렌더 텍스처)에 그린 뒤,
    /// 빛 번짐 · 색수차 · 방사 블러 · 충격파 · 기울기 · 색 보정을 거쳐 이 자리에 보여 준다.
    /// 후처리를 끄면(또는 셰이더가 없으면) 결투 화면을 그대로 이 안에 둔다. 입력은 받지 않음(그림만).
    /// </summary>
    public sealed class StageView : VisualElement
    {
        public readonly StageField Field;
        readonly VisualElement img;
        GameObject go;
        PanelSettings ps;
        UIDocument doc;
        RenderTexture rt, outRt;
        Material mat;
        bool matTried;
        readonly Vector4[] lights = new Vector4[4], lightCols = new Vector4[4];

        public StageView(StageField field)
        {
            Field = field;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            pickingMode = PickingMode.Ignore;
            Add(field);
            img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.position = Position.Absolute;
            img.style.left = 0; img.style.top = 0; img.style.right = 0; img.style.bottom = 0;
            img.style.display = DisplayStyle.None;
            Add(img);
            field.AfterRepaint = Process;
            RegisterCallback<DetachFromPanelEvent>(_ => GoDirect());
        }

        /// <summary>결투 화면을 이 안으로 되돌리고 화면 밖 패널 · 텍스처를 정리.</summary>
        void GoDirect()
        {
            if (Field.parent != this) Insert(0, Field);
            img.style.display = DisplayStyle.None;
            if (go != null) UnityEngine.Object.Destroy(go);
            go = null; doc = null;
            if (ps != null) UnityEngine.Object.Destroy(ps);
            ps = null;
            Free(ref rt); Free(ref outRt);
            shownTex = null;
        }

        static void Free(ref RenderTexture t)
        {
            if (t == null) return;
            if (RenderTexture.active == t) RenderTexture.active = null;
            t.Release(); UnityEngine.Object.Destroy(t); t = null;
        }

        /// <summary>매 프레임(StageField.Repaint 끝에서): 크기 맞추고 후처리.</summary>
        void Process()
        {
            var P = PostSettings.F;
            float W = layout.width, H = layout.height;
            if (!matTried) { matTried = true; var sh = Resources.Load<Shader>("MoriPost"); if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; else Debug.LogWarning("[Mori] MoriPost 셰이더를 쓸 수 없어 후처리를 끕니다."); }
            bool urp = PrefabFx.UrpWanted;
            if ((!P.on && !urp) || (mat == null && !urp) || panel == null || float.IsNaN(W) || W < 16 || H < 16 || Field.World == null)
            {
                if (go != null || Field.parent != this) GoDirect();
                return;
            }
            // 화면 px 배율: 화면 폭 / 패널 폭
            float rootW = panel.visualTree.layout.width;
            float sppp = rootW > 1 ? Screen.width / rootW : 1;
            float k = sppp * Mathf.Clamp(P.res, 0.25f, 1f);
            int rw = Mathf.Clamp(Mathf.RoundToInt(W * k), 16, 8192), rh = Mathf.Clamp(Mathf.RoundToInt(H * k), 16, 8192);
            if (!Ensure(rw, rh, k)) { GoDirect(); return; }
            Field.Fx.PixelScale = k;

            // MFS 화면(URP): 이펙트 층을 뺀 결투 그림을 무대 판으로 → URP 카메라(볼륨 · 라이트)가 그린 것을 보여 줌.
            //  우리 후처리는 그 위에 화면 효과 트랙만(빛 번짐 · 비네트 · 채도 기본값은 MFS 볼륨이 맡으므로 끔)
            Texture src0 = rt;
            if (urp)
            {
                Field.Fx.SetStageImage(rt);
                var o = Field.Fx.Output;
                if (o == null) { Show(rt); return; }
                if (!P.on || mat == null) { Show(o); return; }
                src0 = o;
            }

            // 빛 번짐: 밝은 곳만 → 반씩 줄이며 흐림 → 다시 키우며 더함
            var s = Field.Fx.Screen;
            int steps = Mathf.Clamp(P.steps, 1, 8);
            var mips = new RenderTexture[steps];
            int mw = Mathf.Max(1, rw / 2), mh = Mathf.Max(1, rh / 2);
            mat.SetVector("_Threshold", new Vector4(Mathf.Clamp01(P.threshold), Mathf.Max(0.001f, P.knee), Mathf.Max(0, (urp ? 0 : P.bloom) + s.glow), 0));
            for (int i = 0; i < steps; i++)
            {
                mips[i] = RenderTexture.GetTemporary(mw, mh, 0, RenderTextureFormat.ARGB32);
                mips[i].filterMode = FilterMode.Bilinear;
                if (i == 0) Graphics.Blit(src0, mips[0], mat, 0);
                else Graphics.Blit(mips[i - 1], mips[i], mat, 1);
                mw = Mathf.Max(1, mw / 2); mh = Mathf.Max(1, mh / 2);
            }
            for (int i = steps - 1; i > 0; i--) Graphics.Blit(mips[i], mips[i - 1], mat, 2);

            // 화면 효과: 중심(게임 단위) → 화면 uv
            var w = Field.World;
            Vector2 Uv(Vector2 p)
            {
                var sp = w.ToScreen(p.x, p.y, W, H);
                float v = 1 - sp.y / H;
                return new Vector2(sp.x / W, P.flipY ? 1 - v : v);
            }
            float u = w.PxU(W, H);
            mat.SetTexture("_BloomTex", mips[0]);
            mat.SetFloat("_Aspect", W / H);
            mat.SetFloat("_Chroma", ((urp ? 0 : P.chroma) + s.chroma) / 1080f);
            var bc = s.blurAnchored ? Uv(s.blurAt) : new Vector2(0.5f, 0.5f);
            mat.SetVector("_Blur", new Vector4(bc.x, bc.y, Mathf.Clamp01(s.blur), 0));
            var wc = Uv(s.waveAt);
            mat.SetVector("_Wave", new Vector4(wc.x, wc.y, s.waveR * u / H, Mathf.Max(0.001f, s.waveW * u / H)));
            mat.SetFloat("_WaveStr", s.waveStr / 1080f);
            mat.SetFloat("_Tilt", s.tilt * Mathf.Deg2Rad);
            mat.SetFloat("_Sat", (urp ? 1 : P.sat) * s.sat);
            mat.SetFloat("_Invert", Mathf.Clamp01(s.invert));
            mat.SetFloat("_Vignette", urp ? 0 : Mathf.Clamp01(P.vignette));
            mat.SetVector("_Tint", new Vector4(s.tint.r, s.tint.g, s.tint.b, Mathf.Clamp01(s.tint.a)));
            // 빛(주변 밝히기) · 일렁임
            int ln = Mathf.Clamp(s.lightN, 0, 4);
            for (int i = 0; i < 4; i++)
            {
                if (i < ln)
                {
                    var L = s.LightAt(i); var lc = s.LightCol(i);
                    var lu = Uv(new Vector2(L.x, L.y));
                    lights[i] = new Vector4(lu.x, lu.y, L.z * u / H, L.w);
                    lightCols[i] = new Vector4(lc.r, lc.g, lc.b, 1);
                }
                else { lights[i] = Vector4.zero; lightCols[i] = Vector4.zero; }
            }
            mat.SetVectorArray("_Lights", lights);
            mat.SetVectorArray("_LightCols", lightCols);
            mat.SetFloat("_LightN", ln);
            var hc = Uv(s.hazeAt);
            mat.SetVector("_Haze", new Vector4(hc.x, hc.y, s.hazeR * u / H, s.hazeStr / 1080f));
            mat.SetFloat("_HazeT", Time.unscaledTime * Mathf.Max(0.1f, s.hazeFreq));
            FxNoise.Ensure();
            Graphics.Blit(src0, outRt, mat, 3);
            for (int i = 0; i < steps; i++) RenderTexture.ReleaseTemporary(mips[i]);

            Show(outRt);
        }

        void Show(Texture t)
        {
            if (shownTex != t)
            {
                shownTex = t;
                img.style.backgroundImage = t is RenderTexture r ? new StyleBackground(Background.FromRenderTexture(r)) : new StyleBackground((Texture2D)t);
            }
            img.style.display = DisplayStyle.Flex;
        }
        Texture shownTex;

        /// <summary>화면 밖 패널(결투 화면을 그리는 곳)과 텍스처를 크기에 맞게 준비.</summary>
        bool Ensure(int rw, int rh, float scale)
        {
            try
            {
                if (go == null)
                {
                    ps = ScriptableObject.CreateInstance<PanelSettings>();
                    ps.hideFlags = HideFlags.DontSave;
                    ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MoriTheme");
                    ps.scaleMode = PanelScaleMode.ConstantPixelSize;
                    ps.clearColor = true;
                    ps.colorClearValue = Color.black;
                    ps.sortingOrder = -100;
                    ps.SetScreenToPanelSpaceFunction(_ => new Vector2(float.NaN, float.NaN));     // 입력은 받지 않음
                    go = new GameObject("Mori StagePost") { hideFlags = HideFlags.DontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.SetActive(false);
                    doc = go.AddComponent<UIDocument>();
                    doc.panelSettings = ps;
                    go.SetActive(true);
                }
                if (rt == null || rt.width != rw || rt.height != rh)
                {
                    Free(ref rt); Free(ref outRt);
                    rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32) { name = "Mori Stage", filterMode = FilterMode.Bilinear };
                    rt.Create();
                    outRt = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32) { name = "Mori Stage Post", filterMode = FilterMode.Bilinear };
                    outRt.Create();
                    ps.targetTexture = rt;
                    shownTex = null;
                }
                if (!Mathf.Approximately(ps.scale, scale)) ps.scale = scale;
                var root = doc.rootVisualElement;
                if (root == null) return false;
                if (Field.parent != root)
                {
                    root.pickingMode = PickingMode.Ignore;
                    // 같은 스타일(글자 · 색)을 쓰게 본 화면의 스타일 시트를 옮겨 붙임
                    var src = panel?.visualTree?.styleSheets;
                    if (src != null) for (int i = 0; i < src.Value.count; i++) if (!root.styleSheets.Contains(src.Value[i])) root.styleSheets.Add(src.Value[i]);
                    root.Add(Field);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Mori] 후처리 준비 실패 — 끄고 그대로 그립니다: " + e.Message);
                PostSettings.F.on = false;
                return false;
            }
        }
    }
}
