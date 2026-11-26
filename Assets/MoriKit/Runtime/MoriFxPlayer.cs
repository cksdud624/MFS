using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoriFx
{
    /// <summary>
    /// 카드 모음: 폴더(fx-cards/*.json) 또는 TextAsset 에서 읽어 이름으로 찾는다.
    /// </summary>
    public static class FxCards
    {
        static readonly Dictionary<string, FxCard> map = new Dictionary<string, FxCard>(StringComparer.OrdinalIgnoreCase);
        public static int Version { get; private set; }
        public static IReadOnlyDictionary<string, FxCard> All => map;

        public static void Clear() { map.Clear(); Version++; }

        /// <summary>폴더의 *.json 을 모두 읽음(같은 이름은 덮어씀).</summary>
        public static int LoadDir(string dir)
        {
            int n = 0;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var c = FxCard.FromJson(File.ReadAllText(f));
                    if (c == null) continue;
                    if (string.IsNullOrEmpty(c.key)) c.key = Path.GetFileNameWithoutExtension(f);
                    map[c.key] = c; n++;
                }
                catch (Exception e) { Debug.LogWarning("[MoriFx] 카드 읽기 실패 " + f + ": " + e.Message); }
            }
            Version++;
            return n;
        }

        public static void Add(FxCard c) { if (c != null && !string.IsNullOrEmpty(c.key)) { map[c.key] = c; Version++; } }
        public static void Remove(string key) { if (key != null && map.Remove(key)) Version++; }
        public static FxCard Get(string key) => key != null && map.TryGetValue(key, out var c) ? c : null;
    }

    /// <summary>
    /// 게임에 붙이는 기본 재생기(장면에 하나). 개발킷에서 만든 카드를 그대로 재생한다.
    ///  1) 이 컴포넌트를 아무 오브젝트에 붙이고 카메라 · 프리팹 · 그림 목록을 채운다.
    ///  2) 카드 json 들을 cards 에 끌어다 넣거나 StreamingAssets/fx-cards 에 둔다.
    ///  3) 코드에서 MoriFxPlayer.Main.Play("hero.slash1", 내위치, 상대위치, 바라보는쪽).
    /// 히트스톱은 TimeScale 값으로 알려 줄 뿐 Time.timeScale 을 바꾸지 않는다(useTimeScale 를 켜면 바꿈).
    /// 1U = 유니티 1 단위. 따라가기는 Follow 에 Transform 을 등록(번호 → Transform).
    /// </summary>
    public sealed class MoriFxPlayer : MonoBehaviour, IFxHost
    {
        [Serializable] public sealed class Prefab { public string name; public GameObject prefab; }
        [Serializable] public sealed class Clip { public string name; public Sprite[] frames; public float fps = 12; }

        public static MoriFxPlayer Main { get; private set; }

        public Camera cam;
        public List<Prefab> prefabs = new List<Prefab>();
        public List<Clip> clips = new List<Clip>();
        public List<TextAsset> cards = new List<TextAsset>();
        public string cardsFolder = "fx-cards";            // StreamingAssets 아래(비우면 안 읽음)
        public Material addMaterial;                        // 그림 ‘빛’ 섞기(비우면 Resources/MoriFxSprite 셰이더로 만듦)
        public int sortingOrder = 100;
        public bool useTimeScale;
        public float shakeScale = 1;

        public FxDirector Director { get; private set; }
        public FxScreen Screen => Director != null ? Director.Screen : FxScreen.None;
        public readonly Dictionary<int, Transform> Follow = new Dictionary<int, Transform>();

        Vector3 camBase; float camSize; bool camSaved;
        readonly Dictionary<SpriteRenderer, Color> bodyBase = new Dictionary<SpriteRenderer, Color>();
        Texture2D white;

        void Awake()
        {
            Main = this;
            FxNoise.Ensure();
            Director = new FxDirector(this);
            foreach (var t in cards) if (t != null) { var c = FxCard.FromJson(t.text); if (c != null) { if (string.IsNullOrEmpty(c.key)) c.key = t.name; FxCards.Add(c); } }
            if (!string.IsNullOrEmpty(cardsFolder)) FxCards.LoadDir(Path.Combine(Application.streamingAssetsPath, cardsFolder));
        }

        void OnDestroy() { if (Main == this) Main = null; Director?.StopAll(); }

        public FxRun Play(string key, Vector2 self, Vector2 target, int facing, int owner = 0, int targetOwner = 0) =>
            Play(key, new FxCtx { self = self, target = target, hit = (self + target) * 0.5f, facing = facing >= 0 ? 1 : -1, owner = owner, targetOwner = targetOwner });

        /// <summary>‘둘 사이’ 를 몸 가운데(히트박스 가운데)로 쓰려면 이걸로 — selfBody · targetBody = 두 캐릭터 히트박스 가운데.</summary>
        public FxRun Play(string key, Vector2 self, Vector2 target, Vector2 selfBody, Vector2 targetBody, int facing, int owner = 0, int targetOwner = 0) =>
            Play(key, new FxCtx { self = self, target = target, selfBody = selfBody, targetBody = targetBody, hasBody = true, hit = (selfBody + targetBody) * 0.5f, facing = facing >= 0 ? 1 : -1, owner = owner, targetOwner = targetOwner });

        public FxRun Play(string key, FxCtx ctx)
        {
            var c = FxCards.Get(key);
            return c == null ? null : Director.Play(c, ctx);
        }

        void LateUpdate()
        {
            Director.Tick(useTimeScale ? Time.unscaledDeltaTime : Time.deltaTime);
            var s = Director.Screen;
            if (useTimeScale) Time.timeScale = s.timeScale;
            // 몸 효과(번쩍 · 색): 따라가기에 등록한 캐릭터의 SpriteRenderer 색을 바꿨다가 끝나면 되돌림
            foreach (var kv in Follow)
            {
                if (kv.Value == null) continue;
                var sr = kv.Value.GetComponentInChildren<SpriteRenderer>();
                if (sr == null) continue;
                if (s.BodyOf(kv.Key, out var bc, out _))
                {
                    if (!bodyBase.ContainsKey(sr)) bodyBase[sr] = sr.color;
                    var b0 = bodyBase[sr];
                    sr.color = Color.Lerp(b0, new Color(bc.r, bc.g, bc.b, b0.a), bc.a);
                }
                else if (bodyBase.TryGetValue(sr, out var b1)) { sr.color = b1; bodyBase.Remove(sr); }
            }
            if (cam != null)
            {
                if (!camSaved) { camSaved = true; camBase = cam.transform.localPosition; camSize = cam.orthographicSize; }
                float upx = cam.orthographic ? cam.orthographicSize * 2 / 1080f : 0.01f;
                Vector3 zoff = Vector3.zero;
                if (s.zoomAnchored && cam.orthographic && Mathf.Abs(s.zoom - 1) > 1e-4f)
                {
                    // 줌 중심이 화면에서 제자리에 있게(카메라를 중심 쪽으로 1 − 1/z 만큼)
                    var wc = cam.transform.parent != null ? cam.transform.parent.TransformPoint(camBase) : camBase;
                    var d = (Vector3)s.zoomAt - wc; d.z = 0;
                    zoff = d * (1 - 1 / Mathf.Max(0.2f, s.zoom));
                    if (cam.transform.parent != null) zoff = cam.transform.parent.InverseTransformVector(zoff);
                }
                cam.transform.localPosition = camBase + zoff + (Vector3)(s.shake * upx * shakeScale);
                if (cam.orthographic) cam.orthographicSize = camSize / Mathf.Max(0.2f, s.zoom);
            }
        }

        /// <summary>게임 카메라가 스스로 움직일 때: 흔들림 · 줌을 더하기 전 기준을 다시 잡음.</summary>
        public void Rebase() { camSaved = false; }

        void OnGUI()
        {
            var f = Screen.flash;
            if (f.a <= 0.001f || Event.current.type != EventType.Repaint) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            var old = GUI.color; GUI.color = f;
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), white);
            GUI.color = old;
        }

        // ───────── IFxHost ─────────
        sealed class H { public GameObject go; public ParticleSystem[] ps; public SpriteRenderer sr; public Clip clip; public bool sim; public string name; public List<Ghost> ghost; public float nextEmit, lastAge = -1; public Renderer[] rends; public Mesh mesh; public MeshRenderer mr; }
        Material erodeAdd, erodeAlpha; bool erodeTried;
        Material ErodeMat(bool add)
        {
            if (!erodeTried) { erodeTried = true; erodeAdd = FxMats.Erode(true); erodeAlpha = FxMats.Erode(false); }
            return add ? erodeAdd : erodeAlpha;
        }
        MaterialPropertyBlock texMpb;
        readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();

        GameObject Find(string n) { foreach (var p in prefabs) if (p != null && p.prefab != null && string.Equals(p.name, n, StringComparison.OrdinalIgnoreCase)) return p.prefab; foreach (var p in prefabs) if (p != null && p.prefab != null && p.prefab.name == n) return p.prefab; return null; }
        Clip FindClip(string n) { foreach (var c in clips) if (c != null && string.Equals(c.name, n, StringComparison.OrdinalIgnoreCase)) return c; return null; }

        public object Spawn(FxCard card, FxTrack k, FxCtx ctx, float age)
        {
            if (k.type == FxTrack.EFFECT)
            {
                var src = Find(k.prefab);
                if (src == null) return null;
                GameObject go = null;
                if (pool.TryGetValue(k.prefab, out var st)) while (st.Count > 0 && go == null) go = st.Pop();
                if (go == null) go = Instantiate(src, transform);
                go.SetActive(true);
                var h = new H { go = go, name = k.prefab, ps = go.GetComponentsInChildren<ParticleSystem>(true), rends = go.GetComponentsInChildren<Renderer>(true) };
                for (int i = 0; i < h.ps.Length; i++)
                {
                    var p = h.ps[i];
                    var m = p.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (k.seed != 0) { p.useAutoRandomSeed = false; p.randomSeed = (uint)(k.seed * 7919 + i * 104729); }
                    else p.useAutoRandomSeed = true;
                }
                foreach (var p in h.ps) p.Play(false);
                return h;
            }
            if (k.type == FxTrack.SPRITE)
            {
                var clip = FindClip(k.anim);
                if (clip == null || clip.frames == null || clip.frames.Length == 0) return null;
                var go = new GameObject("fx " + k.anim);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                var em = ErodeMat(k.blend != "alpha");
                if (em != null) sr.sharedMaterial = em;
                else if (k.blend == "add")
                {
                    if (addMaterial == null) { var sh = Resources.Load<Shader>("MoriFxSprite"); if (sh != null) addMaterial = new Material(sh); }
                    if (addMaterial != null) sr.sharedMaterial = addMaterial;
                }
                return new H { go = go, sr = sr, clip = clip, rends = new Renderer[] { sr } };
            }
            if (k.type == FxTrack.GHOST) return new H { ghost = new List<Ghost>() };
            if (k.type == FxTrack.SHATTER)
            {
                var clip = FindClip(k.anim);
                if (clip == null || clip.frames == null || clip.frames.Length == 0 || clip.frames[0] == null) return null;
                var go = new GameObject("fx shatter " + k.anim);
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sortingOrder = sortingOrder;
                var mesh = new Mesh { name = "fx shatter" }; mesh.MarkDynamic();
                mf.sharedMesh = mesh;
                var em = ErodeMat(k.blend != "alpha");
                if (em != null) mr.sharedMaterial = em;
                return new H { go = go, mesh = mesh, mr = mr, clip = clip, rends = new Renderer[] { mr } };
            }
            return null;
        }

        public void Sync(object handle, FxCard card, FxTrack k, FxCtx ctx, float age, bool exact)
        {
            var h = (H)handle;
            if (h.ghost != null) { SyncGhost(h, card, k, ctx, age, exact); return; }
            if (h.go == null) return;
            float dur = FxMotion.Dur(card, k, AutoLength(k));
            float prog = dur > 0 ? age / dur : 0;
            var pose = FxMotion.Of(k, ctx, prog);
            var t = h.go.transform;
            t.position = new Vector3(pose.pos.x, pose.pos.y, 0);
            float s = Mathf.Max(0.01f, k.scale);
            float sx = s;
            if (pose.stretched)
            {
                // 늘이기: 프리팹은 1U 길이 · 스프라이트는 그림 폭을 기준으로 len 까지
                float baseLen = h.sr != null && h.sr.sprite != null ? Mathf.Max(0.01f, h.sr.sprite.bounds.size.x) : 1f;
                sx = pose.len / baseLen;
            }
            if (h.mesh != null)
            {
                // 조각내기: 그림 높이 1 기준 메시를 그림의 실제 높이(U) × 크기로
                var sp = h.clip.frames[0];
                var tr = sp.textureRect; var tx = sp.texture;
                float hw = tr.height / Mathf.Max(1f, sp.pixelsPerUnit) * s;
                FxShatter.Build(h.mesh, k, age, dur, tr.width / Mathf.Max(1f, tr.height), new Rect(tr.x / tx.width, tr.y / tx.height, tr.width / tx.width, tr.height / tx.height),
                    k.pivot != "center", hw, k.seed * 7919 + k.at * 31 + 17, k.Col, string.IsNullOrEmpty(k.color2) ? k.Col : k.Col2, Mathf.Clamp01(k.alpha));
                t.localScale = new Vector3(pose.flip ? -hw : hw, hw, 1);
                t.localRotation = Quaternion.Euler(0, 0, pose.rot);
                FxMats.Apply(h.rends, k, prog);
                texMpb ??= new MaterialPropertyBlock();
                h.mr.GetPropertyBlock(texMpb); texMpb.SetTexture("_MainTex", tx); h.mr.SetPropertyBlock(texMpb);
                return;
            }
            t.localScale = new Vector3(pose.flip ? -sx : sx, s, s);
            t.localRotation = Quaternion.Euler(0, 0, pose.rot);
            FxMats.Apply(h.rends, k, prog);
            if (h.ps != null)
                foreach (var ps in h.ps)
                {
                    var m = ps.main; m.simulationSpeed = Mathf.Max(0, k.speed);
                    if (exact) ps.Simulate(age * Mathf.Max(0, k.speed), false, true);
                    else if (h.sim) ps.Play(false);
                }
            if (h.ps != null) h.sim = exact;
            if (h.sr != null)
            {
                int n = h.clip.frames.Length;
                int i = Mathf.FloorToInt(age * k.speed * Mathf.Max(0.1f, h.clip.fps));
                i = k.loop ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1);
                h.sr.sprite = h.clip.frames[i];
                var c = FxMotion.Tint(k, prog); c.a = FxMotion.Alpha(k, age, dur, card.fps);
                h.sr.color = c;
            }
        }

        // ───────── 잔상: 따라가는 캐릭터(Follow[번호])의 SpriteRenderer 를 몇 프레임마다 복사해 남김 ─────────
        sealed class Ghost { public SpriteRenderer sr; public float born; }

        void SyncGhost(H h, FxCard card, FxTrack k, FxCtx ctx, float age, bool exact)
        {
            if (exact && age < h.lastAge) { foreach (var g in h.ghost) if (g.sr != null) Destroy(g.sr.gameObject); h.ghost.Clear(); h.nextEmit = 0; }
            h.lastAge = age;
            float emitEnd = card.Sec(Mathf.Max(1, k.frames)), step = card.Sec(Mathf.Max(1, k.gap)), life = card.Sec(Mathf.Max(1, k.life));
            int owner = ctx.OwnerOf(k.anchor == "target" ? "target" : "self");
            Follow.TryGetValue(owner, out var ft);
            var src = ft != null ? ft.GetComponentInChildren<SpriteRenderer>() : null;
            while (h.nextEmit <= age && h.nextEmit < emitEnd)
            {
                if (src != null && src.sprite != null)
                {
                    var go = new GameObject("fx ghost");
                    go.transform.SetParent(transform, false);
                    go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                    go.transform.localScale = src.transform.lossyScale;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = src.sprite; sr.flipX = src.flipX; sr.flipY = src.flipY;
                    sr.sortingLayerID = src.sortingLayerID; sr.sortingOrder = src.sortingOrder - 1;
                    if (k.blend != "alpha")
                    {
                        if (addMaterial == null) { var sh = Resources.Load<Shader>("MoriFxSprite"); if (sh != null) addMaterial = new Material(sh); }
                        if (addMaterial != null) sr.sharedMaterial = addMaterial;
                    }
                    h.ghost.Add(new Ghost { sr = sr, born = h.nextEmit });
                }
                h.nextEmit += step;
            }
            for (int i = h.ghost.Count - 1; i >= 0; i--)
            {
                var g = h.ghost[i];
                float a = age - g.born;
                if (g.sr == null || a >= life) { if (g.sr != null) Destroy(g.sr.gameObject); h.ghost.RemoveAt(i); continue; }
                float p = a / life;
                var c = FxMotion.Tint(k, p);
                c.a = Mathf.Clamp01(k.alpha) * (1 - p);
                g.sr.color = c;
            }
        }

        public void Despawn(object handle)
        {
            var h = (H)handle;
            if (h.ghost != null) { foreach (var g in h.ghost) if (g.sr != null) Destroy(g.sr.gameObject); h.ghost.Clear(); return; }
            if (h.go == null) return;
            FxMats.Clear(h.rends);
            if (h.sr != null || h.mesh != null) { if (h.mesh != null) Destroy(h.mesh); Destroy(h.go); return; }
            h.go.SetActive(false);
            if (!pool.TryGetValue(h.name, out var st)) pool[h.name] = st = new Stack<GameObject>();
            st.Push(h.go);
        }

        public float AutoLength(FxTrack k)
        {
            var src = Find(k.prefab);
            if (src == null) return 2;
            float m = 0;
            foreach (var ps in src.GetComponentsInChildren<ParticleSystem>(true)) { var mm = ps.main; m = Mathf.Max(m, mm.duration + mm.startLifetime.constantMax + mm.startDelay.constantMax); }
            return m > 0 ? m / Mathf.Max(0.05f, k.speed) : 2;
        }

        public bool OwnerPos(int owner, out Vector2 pos)
        {
            if (Follow.TryGetValue(owner, out var t) && t != null) { pos = t.position; return true; }
            pos = default; return false;
        }
    }
}
