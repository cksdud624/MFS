using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mori.Core.Fight;
using MoriFx;
using UnityEngine;
using UnityEngine.UIElements;
#if MORI_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Mori.Client
{
    /// <summary>순간(사건) 하나에 붙인 프리팹 설정. 위치 · 크기는 게임 단위(U), 회전은 도.</summary>
    [Serializable]
    public sealed class FxCfg
    {
        public string key = "";            // "hero.dash" …
        public string prefab = "";         // 등록부 이름(비면 꺼짐)
        public float dx, dy;               // 기준점에서 비켜 놓기(U) — dx 는 바라보는 쪽이 +
        public float scale = 1, rot;       // 크기(배) · 회전(도)
        public float life;                 // 0 = 자동(파티클이 다 끝나면 · 파티클이 없으면 2초)
        public bool flip = true;           // 바라보는 방향이 왼쪽이면 좌우 뒤집기
        public bool follow;                // 캐릭터를 따라 움직이기
        public bool off;
        public FxCfg Clone() => (FxCfg)MemberwiseClone();
    }

    [Serializable]
    public sealed class FxFile
    {
        public int unit = 2;               // 길이 단위 판(2 = 1U = 그림 100px)
        public List<FxCfg> list = new List<FxCfg>();
        public float res = 1;              // 프리팹 화면 해상도(화면 대비 배, 0.25 ~ 1)
        public bool glowFix = true;        // 더하기(빛) 파티클이 투명 바탕에서 사라지지 않게 보정
        public bool behind;                // 캐릭터 뒤에 그리기(기본 = 앞)
    }

    /// <summary>프리팹 연결 표: StreamingAssets/Fight/prefabs.json(빌드에 들어감).</summary>
    public static class FxStore
    {
        static FxFile file;
        public static int Version { get; private set; }
        public static string FilePath => Path.Combine(ArtLib.Dir, "prefabs.json");

        public static FxFile F
        {
            get
            {
                if (file != null) return file;
                file = new FxFile();
                try
                {
                    if (File.Exists(FilePath))
                    {
                        string json = File.ReadAllText(FilePath);
                        file = JsonUtility.FromJson<FxFile>(json) ?? new FxFile();
                        if (json.IndexOf("\"unit\"", StringComparison.Ordinal) < 0)      // 예전 단위(64px 캐릭터 = 3U) → 지금(1U = 100px)
                            foreach (var c in file.list) { c.dx *= FxCard.LEGACY_U; c.dy *= FxCard.LEGACY_U; c.scale *= FxCard.LEGACY_U; }
                        file.unit = 2;
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Mori] prefabs.json 읽기 실패: " + e.Message); }
                return file;
            }
        }

        /// <summary>이 순간의 설정(없으면 null — 만들지 않음).</summary>
        public static FxCfg Find(string key) => F.list.FirstOrDefault(x => x.key == key);

        /// <summary>이 순간의 설정(없으면 빈 설정을 만들어 둠 — 패널용).</summary>
        public static FxCfg Get(string key)
        {
            var c = Find(key);
            if (c != null) return c;
            c = new FxCfg { key = key };
            F.list.Add(c);
            return c;
        }

        public static void Save()
        {
            Version++;
            try
            {
                Directory.CreateDirectory(ArtLib.Dir);
                var keep = new FxFile { unit = 2, res = F.res, glowFix = F.glowFix, behind = F.behind, list = F.list.Where(x => !string.IsNullOrEmpty(x.prefab)).ToList() };
                KitBackup.Before(FilePath);
                File.WriteAllText(FilePath, JsonUtility.ToJson(keep, true));
            }
            catch (Exception e) { Debug.LogWarning("[Mori] prefabs.json 저장 실패: " + e.Message); }
        }

        public static void Reload() { file = null; Version++; }
    }

    /// <summary>프리팹을 붙일 수 있는 순간들(키 · 이름 · 누구 기준).</summary>
    public static class FxKeys
    {
        public static readonly (string key, string name)[] ALL =
        {
            ("hero.slash1", "플레이어 검기 1타"), ("hero.slash2", "플레이어 검기 2타"), ("hero.slash3", "플레이어 검기 3타"),
            ("hero.mech1", "플레이어 기계 1타"), ("hero.mech2", "플레이어 기계 2타"), ("hero.mech3", "플레이어 기계 3타"),
            ("hero.hit.slash1", "검기 1타 적중"), ("hero.hit.slash2", "검기 2타 적중"), ("hero.hit.slash3", "검기 3타 적중"),
            ("hero.hit.mech1", "기계 1타 적중"), ("hero.hit.mech2", "기계 2타 적중"), ("hero.hit.mech3", "기계 3타 적중"),
            ("hero.counter", "플레이어 카운터 자세"), ("hero.counterAtk", "플레이어 카운터 공격"), ("hero.hit.counter", "카운터 공격 적중"),
            ("hero.dash", "플레이어 대쉬"), ("hero.backdash", "플레이어 뒤 대쉬"),
            ("hero.jump", "플레이어 점프"), ("hero.djump", "플레이어 더블 점프"), ("hero.land", "플레이어 착지"),
            ("hero.hurt", "플레이어 피격"), ("hero.countered", "플레이어 반격당함"), ("hero.wallbounce", "플레이어 월 바운스"),
            ("counter.success", "카운터 성공"), ("counter.crash", "카운터 크래시(퍼펙트 · 굿 공통)"), ("counter.crash.perfect", "카운터 크래시 · 퍼펙트만"), ("counter.crash.good", "카운터 크래시 · 굿만(완벽하지 않은 크래시)"), ("counter.bounce", "카운터 튕김(엇박)"),
            ("ult.start", "궁극기 시작(폭격 요청 · 조준)"), ("ult.strike", "궁극기 레이저 한 번(박마다)"), ("ult.hit", "궁극기 레이저 적중"), ("ult.end", "궁극기 끝"),
            ("ally.shot", "동료 사격(쏜 곳)"), ("ally.strike", "동료 강한 사격"), ("ally.cutin", "동료 컷인(동료 자리)"), ("ally.fly", "동료 드론 비행 시작"),
            ("foe.hit.melee", "결투자 근접 타격"), ("foe.hit.low", "결투자 낮은 타격"), ("foe.counterAtk", "결투자 카운터 공격"),
            ("foe.counter", "결투자 카운터 자세"), ("foe.shot", "결투자 에너지탄 발사"), ("foe.land", "결투자 착지(내려찍기)"),
            ("foe.damage", "결투자 맞음"), ("foe.evade", "결투자 회피 · 넘어가기"), ("foe.wallbounce", "결투자 월 바운스"), ("foe.ko", "결투자 K.O."),
            ("phase.break", "페이즈 전환"), ("map.start", "맵 패턴 시작"), ("map.strike", "맵 위험물 타격"),
        };

        public static string NameOf(string key) { foreach (var (k, n) in ALL) if (k == key) return n; return key; }
    }

    /// <summary>
    /// 연출 카드 보관: StreamingAssets/Fight/fx-cards/&lt;key&gt;.json(빌드에 들어감). 읽은 카드는 MoriFx.FxCards 에 등록.
    /// </summary>
    public static class FxCardStore
    {
        public static string Dir => Path.Combine(ArtLib.Dir, "fx-cards");
        static bool loaded;
        public static int Version => FxCards.Version;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            FxCards.LoadDir(Dir);
        }

        public static void Reload() { FxCards.Clear(); loaded = false; EnsureLoaded(); }

        /// <summary>트랙이 하나라도 켜져 있는 카드(없으면 null).</summary>
        public static FxCard Find(string key)
        {
            EnsureLoaded();
            var c = FxCards.Get(key);
            return c != null && c.tracks.Any(t => t.on) ? c : null;
        }

        public static FxCard Get(string key) { EnsureLoaded(); return FxCards.Get(key); }

        static string FileOf(string key)
        {
            var bad = Path.GetInvalidFileNameChars();
            return Path.Combine(Dir, new string(key.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()) + ".json");
        }

        public static bool Save(FxCard c)
        {
            if (c == null || string.IsNullOrEmpty(c.key)) return false;
            try
            {
                Directory.CreateDirectory(Dir);
                KitBackup.Before(FileOf(c.key));
                File.WriteAllText(FileOf(c.key), c.ToJson());
                FxCards.Add(c);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 연출 카드 저장 실패: " + e.Message); return false; }
        }

        public static void Delete(string key)
        {
            try { var f = FileOf(key); KitBackup.Before(f); if (File.Exists(f)) File.Delete(f); } catch (Exception e) { Debug.LogWarning("[Mori] 연출 카드 지우기 실패: " + e.Message); }
            FxCards.Remove(key);
        }

        /// <summary>예전 연결(prefabs.json) 하나를 이펙트 트랙 하나짜리 카드로.</summary>
        public static FxCard FromCfg(FxCfg c) => new FxCard
        {
            key = c.key, name = FxKeys.NameOf(c.key), length = c.life > 0 ? Mathf.RoundToInt(c.life * 60) : 30,
            tracks = new List<FxTrack>
            {
                new FxTrack { type = FxTrack.EFFECT, prefab = c.prefab, frames = c.life > 0 ? Mathf.RoundToInt(c.life * 60) : 0, x = c.dx, y = c.dy, scale = c.scale, rot = c.rot, flip = c.flip, follow = c.follow, anchor = c.key.StartsWith("counter.") || c.key == "map.strike" || c.key.StartsWith("foe.hit") || c.key == "foe.shot" || c.key == "foe.land" ? "hit" : "self" },
            },
        };
    }

    /// <summary>
    /// 이펙트 화면(개발킷 쪽 재생기): 연출 카드의 이펙트(프리팹) · 그림(스프라이트) 트랙을 결투 화면(UI) 위에 겹쳐 보인다.
    ///  · 멀리 떨어진 곳(세계 좌표 X = 100000)에 전용 직교 카메라 · 전용 층(31)을 두고, 찍은 것을 렌더 텍스처로 화면 층에 얹는다.
    ///  · 좌표 = 게임 단위(U) 그대로: 카메라를 결투 카메라와 같은 자리 · 같은 배율로 맞춰 캐릭터와 정확히 겹침(흔들림 포함).
    ///  · 아무것도 재생 중이 아니면 카메라를 꺼서 비용 0. 끝난 것은 꺼 두었다가 다시 씀.
    ///  · 빛 보정: 더하기(빛) 파티클은 알파를 안 써서 투명 바탕에선 사라짐 → 색 밝기를 알파로 옮겨 UI 에 그대로 보이게(Hidden/Mori/FxAlpha).
    ///  · 시간표는 MoriFx.FxDirector 가 맡고(게임 쪽 재생기와 같은 코드), 여기는 IFxHost 로 띄우고 지우기만.
    /// </summary>
    public sealed class PrefabFx : IFxHost
    {
        public const int LAYER = 31;
        // 화면(StageField)마다 다른 자리: 작업실 · 결투 화면의 이펙트가 서로의 카메라에 찍히지 않게 20000U 씩 떨어뜨림
        static int made;
        readonly Vector3 ORIGIN;
        const int MAX = 64;

        readonly VisualElement imgFront, imgBack;
        VisualElement img => FxStore.F.behind ? imgBack : imgFront;
        GameObject root;
        Camera cam;
        RenderTexture rtCam, rtOut;
        Material fix, matAdd, matAlpha;
        bool fixTried, matTried, culledOthers;

        /// <summary>시간표(카드 재생). 작업실은 이걸로 멈춤 · 되감기 · 한 칸 이동.</summary>
        public readonly FxDirector Dir;
        public FxScreen Screen => Dir.Screen;

        sealed class Inst
        {
            public string name;            // 풀 이름(프리팹 이름 · 그림은 "#sprite")
            public GameObject go;
            public ParticleSystem[] ps;
            public SpriteRenderer sr;
            public bool sim;               // 지난번에 Simulate 로 맞춤(멈춘 상태) → 다시 움직일 때 Play
            public List<(SpriteRenderer sr, float born)> ghosts;      // 잔상(남긴 그림들)
            public float nextEmit, lastAge = -1;
            public Renderer[] rends;       // 셰이더 값 곡선을 걸 렌더러
            public Mesh mesh;              // 조각내기
            public MeshRenderer mr;
        }
        readonly List<Inst> live = new List<Inst>();
        readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();
        readonly Dictionary<(Texture2D, int), Sprite> sprites = new Dictionary<(Texture2D, int), Sprite>();
        // 빠른 조율: 인스턴스마다 파티클 경로 · 원래 값(배율은 언제나 원래 값에 곱함)
        readonly Dictionary<GameObject, List<(string path, ParticleSystem ps, FxTweaks.Orig orig)>> origs = new Dictionary<GameObject, List<(string, ParticleSystem, FxTweaks.Orig)>>();

        StageWorld lastW;
        float scaleMul = 1;

        /// <summary>front = 캐릭터 앞 층, back = 캐릭터 뒤 층(‘그리는 곳’ 으로 고름).</summary>
        public PrefabFx(VisualElement front, VisualElement back)
        {
            imgFront = front; imgBack = back;
            imgFront.style.display = imgBack.style.display = DisplayStyle.None;
            ORIGIN = new Vector3(100000 + 20000 * (made++ % 64), 0, 0);
            Dir = new FxDirector(this) { Max = MAX };
        }

        void HideAll() { imgFront.style.display = imgBack.style.display = DisplayStyle.None; }

        public int Live => live.Count;

        // ───────── 사건 → 카드 ─────────
        public void OnEvents(StageWorld w, IEnumerable<(FsmEv ev, int combo)> pev, IEnumerable<DuelEvent> dev)
        {
            if (w == null) return;
            lastW = w;
            FxCardStore.EnsureLoaded();
            if (FxStore.F.list.Count == 0 && FxCards.All.Count == 0) return;
            var p = w.Hero; var e = w.Foe;
            Vector2 V(double x, double y) => new Vector2((float)x, (float)y);
            Vector2 heroToFoe = V(e.X - p.X, 0.0001 + (e.Y - p.Y) * 0.5);
            if (pev != null)
                foreach (var (ev, combo) in pev)
                {
                    string k = ev switch
                    {
                        FsmEv.Slash => "hero.slash" + Mathf.Clamp(combo, 1, 3),
                        FsmEv.Mech => "hero.mech" + Mathf.Clamp(combo, 1, 3),
                        FsmEv.CounterStart => "hero.counter",
                        FsmEv.CounterAtk => "hero.counterAtk",
                        FsmEv.Dash => "hero.dash",
                        FsmEv.BackDash => "hero.backdash",
                        FsmEv.Jump => "hero.jump",
                        FsmEv.DoubleJump => "hero.djump",
                        FsmEv.Land => "hero.land",
                        FsmEv.WallBounce or FsmEv.NearWallBounce => "hero.wallbounce",
                        _ => null,
                    };
                    if (k == null) continue;
                    int face = ev == FsmEv.Slash || ev == FsmEv.Mech || ev == FsmEv.CounterAtk ? p.AtkDir : p.Facing;
                    // 방향: 대쉬 · 월 바운스 = 실제 속도(8방향), 점프 = 위, 착지 = 아래, 공격 = 치는 쪽
                    Vector2 dir = ev switch
                    {
                        FsmEv.Dash or FsmEv.BackDash or FsmEv.WallBounce or FsmEv.NearWallBounce => V(p.Vx, p.Vy),
                        FsmEv.Jump or FsmEv.DoubleJump => new Vector2(0, 1),
                        FsmEv.Land => new Vector2(0, -1),
                        _ => new Vector2(face, 0),
                    };
                    if (ev == FsmEv.Mech && w.D.AllyOn) Play(k, w, w.D.A.MuzzleX, w.D.A.MuzzleY, w.D.A.Facing, new Vector2(w.D.A.Facing, 0));    // 동료가 쏨 — 동료 총구에서
                    else if (ev == FsmEv.Slash || ev == FsmEv.Mech)
                    {
                        // 휘두를 때 ‘타격 지점’ = 공격 판정의 가운데(맞든 안 맞든 칼끝이 닿는 곳)
                        var ab = p.AttackBox(Mathf.Clamp(combo, 1, 3));
                        Play(k, w, ab.Cx, ab.Cy, face, dir);
                    }
                    else Play(k, w, p.X, p.Y, face, dir);
                }
            if (dev != null)
                foreach (var d in dev)
                {
                    switch (d.Kind)
                    {
                        case DuelEv.Hit:
                            if (d.Act == -1) Play("foe.counterAtk", w, d.X, d.Y, e.Facing, -heroToFoe);
                            else if (d.Act == (int)HitKind.Low) Play("foe.hit.low", w, d.X, d.Y, e.Facing, -heroToFoe);
                            else if (d.Act == (int)HitKind.Melee) Play("foe.hit.melee", w, d.X, d.Y, e.Facing, -heroToFoe);
                            break;
                        case DuelEv.Shot: Play("foe.shot", w, d.X, d.Y, e.Facing, -heroToFoe); break;
                        case DuelEv.Land: Play("foe.land", w, d.X, d.Y, e.Facing, new Vector2(0, -1)); break;
                        case DuelEv.EnemyCounterStart: Play("foe.counter", w, e.X, e.Y, e.Facing, -heroToFoe); break;
                        case DuelEv.CounterSuccess: Play("counter.success", w, d.X, d.Y, p.Facing, heroToFoe); break;
                        case DuelEv.CounterCrash:
                            Play("counter.crash", w, d.X, d.Y, p.Facing, heroToFoe);                                    // 공통(예전 그대로)
                            Play(d.Act == 1 ? "counter.crash.perfect" : "counter.crash.good", w, d.X, d.Y, p.Facing, heroToFoe);   // Act 1 = 퍼펙트
                            break;
                        case DuelEv.CounterBounce: Play("counter.bounce", w, d.X, d.Y, p.Facing, heroToFoe); break;
                        case DuelEv.UltStart: Play("ult.start", w, d.X, d.Y, p.Facing, heroToFoe); break;
                        case DuelEv.UltStrike: Play("ult.strike", w, d.X, d.Y, p.Facing, new Vector2(0, -1)); break;
                        case DuelEv.UltHit: Play("ult.hit", w, d.X, d.Y, p.Facing, new Vector2(0, -1)); break;
                        case DuelEv.UltEnd: Play("ult.end", w, d.X, d.Y, p.Facing, heroToFoe); break;
                        case DuelEv.AllyShot: Play("ally.shot", w, d.X, d.Y, w.D.A.Facing, new Vector2(w.D.A.Facing, 0)); break;
                        case DuelEv.AllyStrike: Play("ally.strike", w, d.X, d.Y, w.D.A.Facing, new Vector2(w.D.A.Facing, 0)); break;
                        case DuelEv.AllyCutIn: Play("ally.cutin", w, d.X, d.Y, w.D.A.Facing, heroToFoe); break;
                        case DuelEv.AllyFly: Play("ally.fly", w, d.X, d.Y, w.D.A.Facing, new Vector2(0, 1)); break;
                        case DuelEv.EnemyCounter:
                        case DuelEv.PlayerHurt:
                        {
                            // 피격 지점: 플레이어 몸판정에서 결투자 쪽 가장자리(가슴 높이)
                            var hb = p.Hurtbox;
                            double hx = Math.Max(hb.X0, Math.Min(hb.X1, e.X)), hy = hb.Cy;
                            Play(d.Kind == DuelEv.PlayerHurt ? "hero.hurt" : "hero.countered", w, hx, hy, p.Facing, -heroToFoe);
                            break;
                        }
                        case DuelEv.Damage:
                        {
                            // 피격 · 적중 지점 = 공격 판정과 결투자 몸판정이 겹친 곳의 가운데(없으면 몸 가운데)
                            int c = Mathf.Clamp(p.ComboIndex, 1, 3);
                            var hb = e.Hurtbox;
                            var ab = d.Act == 3 || d.Act == 1 ? p.CounterBox() : p.AttackBox(c);
                            double hx = ab.Overlaps(hb) ? (Math.Max(ab.X0, hb.X0) + Math.Min(ab.X1, hb.X1)) / 2 : hb.Cx;
                            double hy = ab.Overlaps(hb) ? (Math.Max(ab.Y0, hb.Y0) + Math.Min(ab.Y1, hb.Y1)) / 2 : hb.Cy;
                            Play("foe.damage", w, hx, hy, e.Facing, heroToFoe);
                            string hk = d.Act == 0 ? "hero.hit.slash" + c : d.Act == 2 ? "hero.hit.mech" + c : d.Act == 3 ? "hero.hit.counter" : null;
                            if (hk != null) Play(hk, w, hx, hy, p.AtkDir, heroToFoe);
                            break;
                        }
                        case DuelEv.Evade: Play("foe.evade", w, e.X, e.Y, e.Facing, V(e.Vx, e.Vy)); break;
                        case DuelEv.WallBounce: Play("foe.wallbounce", w, e.X, e.Y, e.Facing, V(e.Vx, e.Vy)); break;
                        case DuelEv.KO: Play("foe.ko", w, e.X, e.Y, e.Facing, heroToFoe); break;
                        case DuelEv.PhaseBreak: Play("phase.break", w, e.X, e.Y, e.Facing, Vector2.zero); break;
                        case DuelEv.MapStart: Play("map.start", w, e.X, e.Y, e.Facing, Vector2.zero); break;
                        case DuelEv.MapStrike: Play("map.strike", w, d.X, d.Y, 1, new Vector2(0, -1)); break;
                    }
                }
        }

        /// <summary>순간 이름으로 ‘쓰는 쪽’ 을 정함: hero.* · counter.* = 플레이어, 나머지(foe.* · phase · map) = 결투자.</summary>
        public static bool HeroSide(string key) => key.StartsWith("hero.") || key.StartsWith("counter.");

        /// <summary>
        /// 순간 하나의 기준: heroSide = 플레이어가 ‘쓰는 쪽’(아니면 결투자). (hx, hy) = 타격 지점(없으면 쓰는 쪽 자리).
        /// 플레이어 번호 1 · 결투자 번호 2(따라가기).
        /// </summary>
        public static FxCtx Ctx(StageWorld w, bool heroSide, double hx, double hy, int facing, Vector2 dir = default)
        {
            var hp = new Vector2((float)w.Hero.X, (float)w.Hero.Y);
            var fp = new Vector2((float)w.Foe.X, (float)w.Foe.Y);
            var hbH = w.Hero.Hurtbox; var hbF = w.Foe.Hurtbox;
            var hc = new Vector2((float)hbH.Cx, (float)hbH.Cy);                      // 히트박스(몸) 가운데 — ‘둘 사이’ 기준
            var fc = new Vector2((float)hbF.Cx, (float)hbF.Cy);
            return new FxCtx
            {
                self = heroSide ? hp : fp, target = heroSide ? fp : hp, hit = new Vector2((float)hx, (float)hy),
                selfBody = heroSide ? hc : fc, targetBody = heroSide ? fc : hc, hasBody = true,
                facing = facing >= 0 ? 1 : -1, owner = heroSide ? 1 : 2, targetOwner = heroSide ? 2 : 1, dir = dir,
            };
        }

        /// <summary>이 순간의 카드를 재생(카드가 없으면 예전 프리팹 연결). 아무것도 없으면 그냥 넘어감.</summary>
        public FxRun Play(string key, StageWorld w, double hx, double hy, int facing, Vector2 dir = default)
        {
            var ctx = Ctx(w, HeroSide(key), hx, hy, facing, dir);
            var card = FxCardStore.Find(key);
            if (card != null) return Dir.Play(card, ctx);
            var c = FxStore.Find(key);
            if (c == null || c.off || string.IsNullOrEmpty(c.prefab)) return null;
            return Dir.Play(FxCardStore.FromCfg(c), ctx);
        }

        /// <summary>예전 연결 하나를 바로 재생(작업실 ‘프리팹’ 칸 ‘시험 재생’).</summary>
        public bool PlayCfg(FxCfg c, FxCtx ctx)
        {
            if (PrefabLib.Get(c.prefab) == null) return false;
            return Dir.Play(FxCardStore.FromCfg(c), ctx) != null;
        }

        // ───────── IFxHost ─────────
        public object Spawn(FxCard card, FxTrack k, FxCtx ctx, float age)
        {
            try
            {
                Ensure();
                if (k.type == FxTrack.EFFECT)
                {
                    var src = PrefabLib.Get(k.prefab);
                    if (src == null) return null;
                    var go = Take(k.prefab, src);
                    ApplyTweak(k.prefab, go);
                    var it = new Inst { name = k.prefab, go = go, ps = go.GetComponentsInChildren<ParticleSystem>(true), rends = go.GetComponentsInChildren<Renderer>(true) };
                    Seed(it.ps, k.seed);
                    foreach (var ps in it.ps) ps.Play(false);
                    foreach (var an in go.GetComponentsInChildren<Animator>(true)) { an.Rebind(); an.Update(0); }
                    live.Add(it);
                    return it;
                }
                if (k.type == FxTrack.SPRITE)
                {
                    var a = ArtLib.Get(k.anim);
                    if (a == null || a.Frames == null || a.Frames.Length == 0) return null;
                    var it = new Inst { name = "#sprite", go = TakeSprite() };
                    it.sr = it.go.GetComponent<SpriteRenderer>();
                    it.rends = new Renderer[] { it.sr };
                    live.Add(it);
                    return it;
                }
                if (k.type == FxTrack.SHATTER)
                {
                    var a = ArtLib.Get(k.anim);
                    if (a == null || a.Frames == null || a.Frames.Length == 0) return null;
                    var go = new GameObject("fx shatter") { layer = LAYER };
                    go.transform.SetParent(root.transform, false);
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sortingLayerName = FxLayer;
                    var mesh = new Mesh { name = "fx shatter" };
                    mesh.MarkDynamic();
                    mf.sharedMesh = mesh;
                    var it = new Inst { name = "#shatter", go = go, mesh = mesh, mr = mr, rends = new Renderer[] { mr } };
                    live.Add(it);
                    return it;
                }
                if (k.type == FxTrack.GHOST)
                {
                    var it = new Inst { name = "#ghost", ghosts = new List<(SpriteRenderer sr, float born)>() };
                    live.Add(it);
                    return it;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Mori] ‘{(k.type == FxTrack.SPRITE || k.type == FxTrack.SHATTER ? k.anim : k.prefab)}’ 재생 실패: " + e.Message); }
            return null;
        }

        GameObject TakeSprite()
        {
            GameObject go = null;
            if (pool.TryGetValue("#sprite", out var st)) while (st.Count > 0 && go == null) go = st.Pop();
            if (go == null)
            {
                go = new GameObject("fx sprite") { layer = LAYER };
                go.transform.SetParent(root.transform, false);
                go.AddComponent<SpriteRenderer>();
            }
            go.SetActive(true);
            go.GetComponent<SpriteRenderer>().sortingLayerName = FxLayer;
            return go;
        }

        void GiveSprite(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (!pool.TryGetValue("#sprite", out var st)) pool["#sprite"] = st = new Stack<GameObject>();
            if (st.Count < 64) st.Push(go); else UnityEngine.Object.Destroy(go);
        }

        public void Sync(object handle, FxCard card, FxTrack k, FxCtx ctx, float age, bool exact)
        {
            var it = (Inst)handle;
            if (it.ghosts != null) { SyncGhost(it, card, k, ctx, age, exact); return; }
            if (it.go == null) return;
            float dur = FxMotion.Dur(card, k, k.type == FxTrack.EFFECT ? AutoLength(k) : 1);
            float prog = dur > 0 ? age / dur : 0;
            var pose = FxMotion.Of(k, ctx, prog);
            if (it.sr != null) { PoseSprite(it, card, k, ctx, pose, age, dur, prog); FxMats.Apply(it.rends, k, prog); return; }
            if (it.mesh != null) { PoseShatter(it, card, k, ctx, pose, age, dur, prog); return; }
            FxMats.Apply(it.rends, k, prog);

            var t = it.go.transform;
            t.position = ORIGIN + new Vector3(pose.pos.x, pose.pos.y, 0);
            float s = Mathf.Max(0.01f, k.scale) * scaleMul;
            float sx = pose.stretched ? Mathf.Max(0.01f, pose.len) : s;          // 늘이기: 프리팹 1U = 길이 1
            t.localScale = new Vector3(pose.flip ? -sx : sx, s, s);
            t.localRotation = Quaternion.Euler(0, 0, pose.rot);
            float sp = Mathf.Max(0, k.speed);
            foreach (var ps in it.ps)
            {
                if (ps == null) continue;
                var m = ps.main; m.simulationSpeed = sp;
                if (exact) ps.Simulate(age * sp, false, true);
                else if (it.sim) ps.Play(false);
            }
            it.sim = exact;
        }

        /// <summary>그림을 다시 읽어 텍스처가 지워진 스프라이트는 버림(쌓이지 않게 · 지워진 텍스처를 그리지 않게).</summary>
        void PruneSprites()
        {
            var dead = new List<(Texture2D, int)>();
            foreach (var kv in sprites) if (kv.Key.Item1 == null || kv.Value == null || kv.Value.texture == null) dead.Add(kv.Key);
            foreach (var k in dead) { if (sprites[k] != null) TexGrave.Kill(sprites[k]); sprites.Remove(k); }
        }

        Sprite SpriteOf(Texture2D tex, int pivot)
        {
            if (!sprites.TryGetValue((tex, pivot), out var sp) || sp == null || sp.texture == null)
            {
                if (sp != null) TexGrave.Kill(sp);
                if (sprites.Count > 512) PruneSprites();
                var pv = pivot == 0 ? new Vector2(0.5f, 0) : pivot == 2 ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f);
                sprites[(tex, pivot)] = sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pv, tex.height, 0, SpriteMeshType.FullRect);
            }
            return sp;
        }

        void PoseSprite(Inst it, FxCard card, FxTrack k, FxCtx ctx, FxMotion.Pose pose, float age, float dur, float prog)
        {
            var a = ArtLib.Get(k.anim);
            if (a == null || a.Frames.Length == 0) { it.sr.sprite = null; return; }
            float ms = age * 1000f * Mathf.Max(0, k.speed);
            int idx;
            if (a.Frames.Length == 1 || a.TotalMs <= 0) idx = 0;
            else
            {
                float tt = k.loop ? Mathf.Repeat(ms, a.TotalMs) : Mathf.Min(ms, a.TotalMs - 0.001f);
                idx = a.Frames.Length - 1;
                for (int i = 0; i < a.Frames.Length; i++) { if (tt < a.Ms[i]) { idx = i; break; } tt -= a.Ms[i]; }
            }
            var tex = a.Frames[idx];
            if (tex == null) { it.sr.sprite = null; return; }
            bool feet = k.pivot != "center" && !pose.stretched;
            it.sr.sprite = SpriteOf(tex, pose.stretched ? 2 : feet ? 0 : 1);
            it.sr.sortingOrder = 0;
            var col = FxMotion.Tint(k, prog); col.a = FxMotion.Alpha(k, age, dur, card.fps);
            it.sr.color = col;
            EnsureMats();
            var mat = k.blend == "alpha" ? matAlpha : matAdd;
            if (mat != null && it.sr.sharedMaterial != mat) it.sr.sharedMaterial = mat;

            // 크기: 캐릭터 그림과 같은 밀도(같은 캔버스 크기로 그리면 캐릭터와 정확히 겹침)
            var T = lastW?.D.T;
            bool foe = ctx.OwnerOf(k.anchor) == 2;
            double down = T == null ? 0 : foe ? T.FoeImgDown : T.HeroImgDown;
            double sc = T == null ? 1 : FightTune.Pos(foe ? T.ImgScaleFoe : T.ImgScaleHero, 0.05);
            float hU = (float)sc * scaleMul * (a.H > 0 ? a.H : tex.height) / PostSettings.Ppu * Mathf.Max(0.01f, k.scale);     // 1U = ppu px(캐릭터와 같은 밀도)
            var t = it.go.transform;
            if (pose.stretched)
            {
                // 늘이기: 그림 왼쪽 가운데가 시작점, 그림 폭이 길이만큼(높이는 크기 배율 그대로)
                float wU = hU * tex.width / Mathf.Max(1f, tex.height);
                t.position = ORIGIN + new Vector3(pose.pos.x, pose.pos.y, 0);
                t.localScale = new Vector3(hU * Mathf.Max(0.01f, pose.len) / Mathf.Max(0.001f, wU), hU, 1);
                t.localRotation = Quaternion.Euler(0, 0, pose.rot);
                return;
            }
            bool right = T == null || T.ImgFaceRight >= 0.5;
            bool flip = k.flip && ((ctx.facing >= 0) != right);
            float baseY = feet ? pose.pos.y - (float)down : pose.pos.y;
            t.position = ORIGIN + new Vector3(pose.pos.x, baseY, 0);
            t.localScale = new Vector3(flip ? -hU : hU, hU, 1);
            t.localRotation = Quaternion.Euler(0, 0, pose.rot);
        }

        /// <summary>조각내기: 그림의 지금 컷을 격자로 쪼개 날림(크기 = 그림 트랙과 같은 밀도).</summary>
        void PoseShatter(Inst it, FxCard card, FxTrack k, FxCtx ctx, FxMotion.Pose pose, float age, float dur, float prog)
        {
            var a = ArtLib.Get(k.anim);
            if (a == null || a.Frames.Length == 0) { it.mr.enabled = false; return; }
            it.mr.enabled = true;
            // 쪼갤 컷: 조각나기 시작한 순간(트랙 시작)의 컷 — speed 0 이면 첫 컷
            float ms = 0;
            int idx = a.IndexAt(ms);
            var tex = a.Frames[Mathf.Clamp(idx, 0, a.Frames.Length - 1)];
            if (tex == null) { it.mr.enabled = false; return; }
            EnsureMats();
            var mat = k.blend == "alpha" ? matAlpha : matAdd;
            if (mat != null && it.mr.sharedMaterial != mat) it.mr.sharedMaterial = mat;
            var T = lastW?.D.T;
            bool foe = ctx.OwnerOf(k.anchor) == 2;
            double down = T == null ? 0 : foe ? T.FoeImgDown : T.HeroImgDown;
            double sc = T == null ? 1 : FightTune.Pos(foe ? T.ImgScaleFoe : T.ImgScaleHero, 0.05);
            float hU = (float)sc * scaleMul * (a.H > 0 ? a.H : tex.height) / PostSettings.Ppu * Mathf.Max(0.01f, k.scale);     // 1U = ppu px(캐릭터와 같은 밀도)
            bool feet = k.pivot != "center";
            var c0 = k.Col; var c1 = string.IsNullOrEmpty(k.color2) ? c0 : k.Col2;
            FxShatter.Build(it.mesh, k, age, dur, tex.width / Mathf.Max(1f, tex.height), new Rect(0, 0, 1, 1), feet, hU, k.seed * 7919 + k.at * 31 + 17, c0, c1, Mathf.Clamp01(k.alpha));
            bool right = T == null || T.ImgFaceRight >= 0.5;
            bool flip = k.flip && ((ctx.facing >= 0) != right);
            var t = it.go.transform;
            t.position = ORIGIN + new Vector3(pose.pos.x, feet ? pose.pos.y - (float)down : pose.pos.y, 0);
            t.localScale = new Vector3(flip ? -hU : hU, hU, 1);
            t.localRotation = Quaternion.Euler(0, 0, pose.rot);
            FxMats.Apply(it.rends, k, prog);
            shatterMpb ??= new MaterialPropertyBlock();
            it.mr.GetPropertyBlock(shatterMpb);
            shatterMpb.SetTexture("_MainTex", tex);
            it.mr.SetPropertyBlock(shatterMpb);
        }
        MaterialPropertyBlock shatterMpb;

        // ───────── 잔상 · 몸 효과: 캐릭터의 지금 모습 ─────────
        /// <summary>캐릭터(번호 1 플레이어 · 2 결투자)의 지금 그림 · 발 위치(U) · 높이(fx 세계 U) · 좌우. StageField 가 매 프레임 채움.</summary>
        public struct CharShot { public Texture2D tex; public Vector2 feet; public float hU; public bool flip; public bool ok; }
        public CharShot Hero, Foe;
        CharShot ShotOf(int owner) => owner == 1 ? Hero : owner == 2 ? Foe : default;

        void SyncGhost(Inst it, FxCard card, FxTrack k, FxCtx ctx, float age, bool exact)
        {
            if (exact && age < it.lastAge - 1e-4f) { foreach (var g in it.ghosts) GiveSprite(g.sr != null ? g.sr.gameObject : null); it.ghosts.Clear(); it.nextEmit = 0; }
            it.lastAge = age;
            float emitEnd = card.Sec(Mathf.Max(1, k.frames)), step = card.Sec(Mathf.Max(1, k.gap)), life = card.Sec(Mathf.Max(1, k.life));
            int owner = ctx.OwnerOf(k.anchor == "target" ? "target" : "self");
            var shot = ShotOf(owner);
            EnsureMats();
            while (it.nextEmit <= age && it.nextEmit < emitEnd)
            {
                if (shot.ok && shot.tex != null)
                {
                    var go = TakeSprite();
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = SpriteOf(shot.tex, 0);
                    var mat = k.blend == "alpha" ? matAlpha : matAdd;
                    if (mat != null) sr.sharedMaterial = mat;
                    sr.sortingOrder = -1;
                    var t = go.transform;
                    t.position = ORIGIN + new Vector3(shot.feet.x + k.x * (ctx.facing >= 0 ? 1 : -1), shot.feet.y + k.y, 0.1f);
                    float h = shot.hU * Mathf.Max(0.01f, k.scale);
                    t.localScale = new Vector3(shot.flip ? -h : h, h, 1);
                    t.localRotation = Quaternion.identity;
                    it.ghosts.Add((sr, it.nextEmit));
                }
                it.nextEmit += step;
            }
            for (int i = it.ghosts.Count - 1; i >= 0; i--)
            {
                var (sr, born) = it.ghosts[i];
                float a = age - born;
                if (sr == null || a >= life || a < 0) { GiveSprite(sr != null ? sr.gameObject : null); it.ghosts.RemoveAt(i); continue; }
                float p = a / life;
                var c = FxMotion.Tint(k, p);
                c.a = Mathf.Clamp01(k.alpha) * (1 - p) * (k.fadeIn > 0 ? Mathf.Clamp01(a * card.fps / k.fadeIn) : 1);
                sr.color = c;
            }
        }

        // 몸 효과 ‘빛’: 캐릭터 그림을 빛(더하기)으로 한 겹 더 그려 번쩍이게
        readonly SpriteRenderer[] bodyFx = new SpriteRenderer[3];

        void UpdateBody()
        {
            for (int o = 1; o <= 2; o++)
            {
                var shot = ShotOf(o);
                bool on = Dir.Screen.BodyOf(o, out var c, out bool add) && add && shot.ok && shot.tex != null;
                if (!on) { if (bodyFx[o] != null) bodyFx[o].gameObject.SetActive(false); continue; }
                Ensure(); EnsureMats();
                if (bodyFx[o] == null)
                {
                    var go = new GameObject("fx body " + o) { layer = LAYER };
                    go.transform.SetParent(root.transform, false);
                    bodyFx[o] = go.AddComponent<SpriteRenderer>();
                }
                bodyFx[o].sortingLayerName = CharLayer;
                var sr = bodyFx[o];
                sr.gameObject.SetActive(true);
                sr.sprite = SpriteOf(shot.tex, 0);
                if (matAdd != null) sr.sharedMaterial = matAdd;
                sr.color = new Color(c.r, c.g, c.b, c.a);
                sr.sortingOrder = 50;
                sr.transform.position = ORIGIN + new Vector3(shot.feet.x, shot.feet.y, 0);
                sr.transform.localScale = new Vector3(shot.flip ? -shot.hU : shot.hU, shot.hU, 1);
            }
        }

        public bool BodyLive => bodyFx[1] != null && bodyFx[1].gameObject.activeSelf || bodyFx[2] != null && bodyFx[2].gameObject.activeSelf;

        void EnsureMats()
        {
            if (matTried) return;
            matTried = true;
            // 침식 셰이더(셰이더 값 곡선 · 발광 · 일렁임을 받는 것)가 있으면 그것, 없으면 단순 스프라이트 셰이더
            matAdd = FxMats.Erode(true); matAlpha = FxMats.Erode(false);
            if (matAdd != null && matAlpha != null) return;
            var sh = Resources.Load<Shader>("MoriFxSprite");
            if (sh == null || !sh.isSupported) { Debug.LogWarning("[Mori] MoriFxSprite 셰이더를 찾지 못해 기본 스프라이트 재질로 그립니다."); return; }
            matAdd = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            matAdd.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            matAdd.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            matAlpha = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            matAlpha.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            matAlpha.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        public void Despawn(object handle)
        {
            var it = (Inst)handle;
            live.Remove(it);
            if (it.ghosts != null) { foreach (var g in it.ghosts) GiveSprite(g.sr != null ? g.sr.gameObject : null); it.ghosts.Clear(); return; }
            FxMats.Clear(it.rends);
            if (it.name == "#sprite") { GiveSprite(it.go); return; }
            if (it.name == "#shatter") { if (it.mesh != null) UnityEngine.Object.Destroy(it.mesh); if (it.go != null) UnityEngine.Object.Destroy(it.go); return; }
            if (it.go == null) return;
            it.go.SetActive(false);
            if (!pool.TryGetValue(it.name, out var st)) pool[it.name] = st = new Stack<GameObject>();
            if (st.Count < 8) st.Push(it.go); else { origs.Remove(it.go); UnityEngine.Object.Destroy(it.go); }
        }

        readonly Dictionary<string, float> autoLen = new Dictionary<string, float>();
        int autoLenVer = -1;

        public float AutoLength(FxTrack k)
        {
            if (autoLenVer != PrefabLib.Version) { autoLen.Clear(); autoLenVer = PrefabLib.Version; }
            if (k.prefab != null && autoLen.TryGetValue(k.prefab, out var cached)) return cached / Mathf.Max(0.05f, k.speed);
            float raw = AutoLength1(k.prefab);
            if (k.prefab != null) autoLen[k.prefab] = raw;
            return raw / Mathf.Max(0.05f, k.speed);
        }

        float AutoLength1(string prefab)
        {
            var src = PrefabLib.Get(prefab);
            if (src == null) return 2;
            float m = 0;
            foreach (var ps in src.GetComponentsInChildren<ParticleSystem>(true))
            {
                var mm = ps.main;
                m = Mathf.Max(m, (mm.loop ? 2 : mm.duration) + mm.startLifetime.constantMax + mm.startDelay.constantMax);
            }
            return m > 0 ? m : 2;
        }

        public bool OwnerPos(int owner, out Vector2 pos)
        {
            pos = default;
            if (lastW == null) return false;
            if (owner == 1) { pos = new Vector2((float)lastW.Hero.X, (float)lastW.Hero.Y); return true; }
            if (owner == 2) { pos = new Vector2((float)lastW.Foe.X, (float)lastW.Foe.Y); return true; }
            return false;
        }

        /// <summary>무작위 씨앗: 같은 씨앗이면 언제나 같은 모양(되감기 · 비교가 정확), 0 = 매번 다르게.</summary>
        public static void Seed(ParticleSystem[] list, int seed)
        {
            for (int i = 0; i < list.Length; i++)
            {
                var ps = list[i];
                if (ps == null) continue;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (seed != 0) { ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(seed * 7919 + i * 104729); }
                else ps.useAutoRandomSeed = true;
            }
        }

        /// <summary>빠른 조율 값(fx-tweaks)을 이 인스턴스에 넣음(조율이 없으면 원래 값으로 되돌림).</summary>
        void ApplyTweak(string prefab, GameObject go)
        {
            if (!origs.TryGetValue(go, out var list)) return;
            var t = FxTweaks.Find(prefab);
            var all = t?.Get("", false);
            foreach (var (path, ps, orig) in list)
            {
                if (ps == null) continue;
                try { FxTweaks.Apply(ps, orig, all, t?.Get(path, false)); }
                catch (Exception e) { Debug.LogWarning($"[Mori] 조율 적용 실패({prefab}/{path}): " + e.Message); }
            }
        }

        /// <summary>이 프리팹으로 만들어 둔 것을 모두 버림(프리팹 파일이 바뀐 뒤 — 원래 값을 새로 읽게).</summary>
        public void Forget(string prefab)
        {
            Dir.StopAll();
            if (pool.TryGetValue(prefab, out var st))
            {
                foreach (var g in st) if (g != null) { origs.Remove(g); UnityEngine.Object.Destroy(g); }
                st.Clear();
            }
        }

        /// <summary>프리팹이 원래(게임에서) 있던 오브젝트 층 — 킷은 전용 층으로 바꿔 찍으므로, 게임에서 어느 카메라가 그리는지 진단할 때 씀.</summary>
        readonly Dictionary<string, HashSet<int>> srcLayers = new Dictionary<string, HashSet<int>>();

        GameObject Take(string name, GameObject src)
        {
            if (pool.TryGetValue(name, out var st))
                while (st.Count > 0)
                {
                    var g = st.Pop();
                    if (g == null) continue;
                    g.SetActive(true);
                    return g;
                }
            var go = UnityEngine.Object.Instantiate(src, root.transform);
            go.name = name;
            srcLayers[name] = new HashSet<int>(src.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer));   // 게임에서 쓰는 원래 층(색 진단)
            SetLayer(go.transform);
            // 파티클이 부모(우리가 주는 크기 · 좌우 뒤집기)를 따르게 — 기본 ‘Local’ 이면 부모 크기를 무시함
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; }
            origs[go] = FxTweaks.Systems(go).Select(x => (x.path, x.ps, FxTweaks.Capture(x.ps))).ToList();
            go.SetActive(true);
            return go;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = LAYER;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }

        // ───────── 매 프레임 ─────────
        /// <summary>시간표를 한 칸 진행하고 카메라 · 화면 층을 갱신. u = 1U 의 화면 px, ui = 그림 배율이 들어간 1U.</summary>
        public void Update(StageWorld w, float W, float H, float u, float ui, Vector2 shake, float dt)
        {
            // 배치가 아직 안 끝난 첫 프레임엔 크기가 NaN — NaN 은 ‘< 10’ 비교를 통과하므로 ‘>= 10 이 아님’ 으로 거름
            if (w == null || !(W >= 10) || !(H >= 10) || !(u > 0) || float.IsNaN(ui) || float.IsInfinity(u))
            {
                HideAll();
                if (cam != null) cam.enabled = false;
                if (w == null) Dir.StopAll();
                return;
            }
            lastW = w;
            scaleMul = u > 0 ? ui / u : 1;
            Dir.Tick(dt);
            UpdateBody();
            for (int i = live.Count - 1; i >= 0; i--) if (live[i].go == null && live[i].ghosts == null) live.RemoveAt(i);
            bool urp = UrpWanted;
            if (urp) Ensure();
            if ((!urp && live.Count == 0 && !BodyLive) || cam == null) { HideAll(); if (cam != null) cam.enabled = false; urpLive = false; return; }

            // 카메라: 결투 화면과 같은 자리 · 같은 배율(화면 좌표 = 가운데 + (x − CamX) × u, 바닥선 = Ground)
            float res = urp ? Mathf.Max(0.25f, PixelScale) : Mathf.Clamp(FxStore.F.res, 0.25f, 1f);
            int rw = Mathf.Clamp(Mathf.RoundToInt(W * res), 16, 8192), rh = Mathf.Clamp(Mathf.RoundToInt(H * res), 16, 8192);
            // MFS 화면(URP)일 땐 HDR 텍스처로: URP 는 카메라가 텍스처에 그릴 때 중간 그림도 그 텍스처 형식을 따라서,
            // 8비트(ARGB32)면 HDR 밝기(Intensity 4.5 같은 Glow Color)가 블룸 · 톤매핑 전에 1 로 잘려 게임처럼 빛나지 않았음
            bool wantHdr = urp && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            if (rtCam == null || rtCam.width != rw || rtCam.height != rh || rtHdr != wantHdr)
            {
                Release();
                rtHdr = wantHdr;
                rtCam = new RenderTexture(rw, rh, 24, wantHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32) { name = "Mori Fx cam" };
                rtOut = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32) { name = "Mori Fx out" };
                cam.targetTexture = rtCam;
            }
            if (float.IsNaN(shake.x) || float.IsNaN(shake.y)) shake = Vector2.zero;
            double cx = w.CamX - shake.x / u;
            double cy = w.CamY + (w.Ground(H) - H * 0.5f) / u + shake.y / u;
            if (double.IsNaN(cx) || double.IsNaN(cy)) return;
            cam.transform.position = ORIGIN + new Vector3((float)cx, (float)cy, -100);
            cam.orthographicSize = H * 0.5f / u;
            cam.aspect = W / H;
            cam.enabled = true;

            if (urp)
            {
                // MFS 화면: 결투 그림(무대 판) + 이펙트를 MFS 와 같은 URP 카메라 설정으로 → 볼륨 · 글로벌 라이트 2D 가 게임과 같은 코드로
                UrpCamera();
                PoseStage((float)cx, (float)cy, cam.orthographicSize, W / H);
                urpLive = true;
                HideAll();                        // 이펙트는 카메라 그림(Output)에 같이 들어감 — UI 층엔 안 씀
                return;
            }
            urpLive = false;
            if (stageSr != null) stageSr.gameObject.SetActive(false);

            // 지난 프레임에 찍힌 것을 화면 층으로(빛 보정이 켜져 있으면 셰이더로 알파 보정)
            if (!fixTried) { fixTried = true; var sh = Resources.Load<Shader>("MoriFxAlpha"); if (sh != null && sh.isSupported) fix = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; }
            Texture shown = rtCam;
            if (FxStore.F.glowFix && fix != null) { Graphics.Blit(rtCam, rtOut, fix); shown = rtOut; }
            var on = img; var offEl = on == imgFront ? imgBack : imgFront;
            offEl.style.display = DisplayStyle.None;
            on.style.backgroundImage = new StyleBackground(Background.FromRenderTexture((RenderTexture)shown));
            on.style.display = DisplayStyle.Flex;
        }

        // ───────── MFS 화면(URP): 무대 판 + 같은 카메라 설정 ─────────
        /// <summary>StageView 가 알려 주는 화면 px 배율(패널 단위 → 실제 픽셀).</summary>
        public float PixelScale = 1;
        bool urpLive, rtHdr;
        /// <summary>URP 로 그린 최종 화면(무대 + 이펙트 + 볼륨). URP 모드가 아니면 null.</summary>
        public RenderTexture Output => urpLive ? rtCam : null;

        public static bool UrpAvailable
        {
            get
            {
#if MORI_URP
                return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                return false;
#endif
            }
        }

        public static bool UrpWanted => PostSettings.F.mfs && UrpAvailable;

        SpriteRenderer stageSr;
        Texture2D stageTex;
        Sprite stageSprite;
        Material stageLit, stageUnlit;

        /// <summary>결투 그림(이펙트 층을 뺀 StageField 화면)을 무대 판에 옮김(매 프레임, GPU 복사).</summary>
        public void SetStageImage(RenderTexture src)
        {
            if (src == null || !urpLive) return;
            Ensure();
            if (stageTex == null || stageTex.width != src.width || stageTex.height != src.height)
            {
                if (stageSprite != null) UnityEngine.Object.Destroy(stageSprite);
                if (stageTex != null) UnityEngine.Object.Destroy(stageTex);
                stageTex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { name = "Mori Stage", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                stageTex.Apply(false, false);
                stageSprite = Sprite.Create(stageTex, new Rect(0, 0, src.width, src.height), new Vector2(0.5f, 0.5f), src.height, 0, SpriteMeshType.FullRect);
                if (stageSr != null) stageSr.sprite = stageSprite;
            }
            try { Graphics.CopyTexture(src, stageTex); }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 무대 그림 복사 실패: " + e.Message); }
        }

        void PoseStage(float cx, float cy, float ortho, float aspect)
        {
            if (stageSr == null)
            {
                var go = new GameObject("Mori Stage Plate") { layer = LAYER };
                go.transform.SetParent(root.transform, false);
                stageSr = go.AddComponent<SpriteRenderer>();
                stageSr.sprite = stageSprite;
            }
            stageSr.gameObject.SetActive(stageSprite != null);
            var P = PostSettings.F;
            stageSr.sortingLayerName = CharLayer;
            stageSr.sortingOrder = -30000;
            if (stageLit == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (lit != null) stageLit = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                if (unlit != null) stageUnlit = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
            }
            var m = P.light ? stageLit : stageUnlit;
            if (m != null && stageSr.sharedMaterial != m) stageSr.sharedMaterial = m;
            // 판 = 카메라 화면 꼭 맞게(스프라이트 1 = 높이 1)
            float h = ortho * 2;
            stageSr.transform.position = ORIGIN + new Vector3(cx, cy, 50);
            stageSr.transform.localScale = new Vector3(h, P.stageFlip ? -h : h, 1);
        }

        /// <summary>정렬 레이어: 비우면 이름으로 짐작(캐릭터 · 이펙트), 없으면 Default.</summary>
        public static string CharLayer
        {
            get
            {
                var set = PostSettings.F.charLayer;
                foreach (var l in SortingLayer.layers) if (!string.IsNullOrEmpty(set) && l.name == set) return set;
                // 자동: 글로벌 라이트 2D 가 비추는 레이어 중(캐릭터 · 가운데 이름 먼저) — 빛을 못 받는 레이어면 2D URP 에선 새까맣게 나옴
                string lit = null;
                foreach (var l in SortingLayer.layers)
                {
                    if (!LitByGlobal(l.name)) continue;
                    var lo = l.name.ToLowerInvariant();
                    if (lo.Contains("char") || lo.Contains("player") || lo.Contains("unit") || lo.Contains("actor")) return l.name;
                    if (lit == null || (lo.Contains("mid") && !lit.ToLowerInvariant().Contains("mid"))) lit = l.name;
                }
                return lit ?? PickLayer("", new[] { "char", "player", "unit", "actor" }, false);
            }
        }

        static float lightsAt = -10;
#if MORI_URP
        static Light2D[] globals = Array.Empty<Light2D>();
#endif

#if MORI_URP
        // Light2D 의 대상 레이어 목록은 공개되어 있지 않아(IsLitLayer 는 internal) 내부 칸을 읽음 — URP 버전에 따라 이름이 다를 수 있어 둘 다 시도
        static System.Reflection.FieldInfo applyField;
        static bool Targets(Light2D g, int layerId)
        {
            applyField ??= typeof(Light2D).GetField("m_ApplyToSortingLayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (applyField?.GetValue(g) is int[] ids) return Array.IndexOf(ids, layerId) >= 0;
            var prop = typeof(Light2D).GetProperty("targetSortingLayers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop?.GetValue(g) is int[] ids2) return Array.IndexOf(ids2, layerId) >= 0;
            return true;          // 못 읽으면 비춘다고 봄(자동 선택이 막히지 않게)
        }
#endif

        /// <summary>이 정렬 레이어를 켜져 있는 글로벌 라이트 2D 가 비추는지(장면의 라이트를 2초마다 다시 찾음).</summary>
        public static bool LitByGlobal(string layer)
        {
#if MORI_URP
            if (Time.realtimeSinceStartup - lightsAt > 2f || lightsAt < 0)
            {
                lightsAt = Time.realtimeSinceStartup;
                var all = UnityEngine.Object.FindObjectsByType<Light2D>();
                globals = Array.FindAll(all, x => x != null && x.lightType == Light2D.LightType.Global);
            }
            int id = SortingLayer.NameToID(layer);
            foreach (var g in globals) if (g != null && g.isActiveAndEnabled && Targets(g, id)) return true;
            return false;
#else
            return false;
#endif
        }
        /// <summary>색 진단: 게임 카메라 · 볼륨 · 지금 재생 중인 이펙트의 재질 · 정렬 레이어 · 비추는 글로벌 라이트를 글로.</summary>
        public string Diagnose()
        {
            var sb = new System.Text.StringBuilder();
#if MORI_URP
            var main = Camera.main;
            if (main == null || main == cam) sb.AppendLine("■ 게임 Main Camera: 없음 — 게임 장면 위에서 킷을 켜야(메뉴 Mori ▸ 킷 켜고 플레이) 게임의 볼륨 · 설정을 써요.");
            else
            {
                main.TryGetComponent(out UniversalAdditionalCameraData md);
                sb.AppendLine($"■ 게임 Main Camera: {main.name} · HDR {(main.allowHDR ? "켬" : "끔")} · 후처리 {(md != null && md.renderPostProcessing ? "켬" : "끔")} · 볼륨 층 {(md != null ? (int)md.volumeLayerMask : -1)}");
            }
            if (cam != null)
            {
                var d = cam.GetUniversalAdditionalCameraData();
                sb.AppendLine($"■ 킷 카메라: HDR {(cam.allowHDR ? "켬" : "끔")} · 후처리 {(d.renderPostProcessing ? "켬" : "끔")} · 볼륨 기준점 {(d.volumeTrigger != null ? d.volumeTrigger.name : "없음")} · 그림 형식 {(rtCam != null ? rtCam.format.ToString() : "없음")}");
            }
            var vols = UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>();
            sb.AppendLine($"■ 볼륨 {vols.Length}개");
            foreach (var v in vols)
            {
                if (v == null) continue;
                var prof = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
                string has = prof == null ? "프로필 없음" : string.Join(" · ", prof.components.Where(c => c != null && c.active).Select(c => c.GetType().Name));
                sb.AppendLine($"  - {v.name}: {(v.isGlobal ? "Global" : "Local(구역형)")} · 무게 {v.weight:0.##} · {(v.enabled && v.gameObject.activeInHierarchy ? "켜짐" : "꺼짐")} · {has}");
            }
            var lights = UnityEngine.Object.FindObjectsByType<Light2D>();
            int shown = 0;
            sb.AppendLine("■ 지금 이펙트(재생 중인 것)");
            foreach (var it in live)
            {
                if (it.go == null) continue;
                foreach (var r in it.go.GetComponentsInChildren<Renderer>(true))
                {
                    if (shown >= 12) break;
                    shown++;
                    var m = r.sharedMaterial;
                    string sh = m != null && m.shader != null ? m.shader.name : "재질 없음";
                    int lid = r.sortingLayerID;
                    var hit = lights.Where(l => l != null && l.isActiveAndEnabled && l.lightType == Light2D.LightType.Global && Targets(l, lid)).Select(l => $"{l.name}({l.intensity:0.##})");
                    string hitS = string.Join(", ", hit);
                    sb.AppendLine($"  - {r.name}: {sh} · 정렬 {SortingLayer.IDToName(lid)} · 글로벌 라이트 {(hitS.Length > 0 ? hitS : "없음")}");
                }
            }
            if (shown == 0) sb.AppendLine("  (재생 중인 이펙트가 없어요 — 연출을 재생하는 동안 다시 눌러 주세요)");
            // 게임에서 이펙트를 어느 카메라 · 렌더러가 그리는지(전역 조명 0 이어도 원색이면 보통 조명 없는 카메라가 따로 그림)
            var fxLayers = new HashSet<int>();
            foreach (var it in live) if (it.go != null && it.name != null && srcLayers.TryGetValue(it.name, out var ls)) fxLayers.UnionWith(ls);
            sb.AppendLine("■ 이펙트 프리팹의 원래 오브젝트 층: " + (fxLayers.Count > 0 ? string.Join(", ", fxLayers.Select(l => $"{LayerMask.LayerToName(l)}({l})")) : "(재생 중인 프리팹 없음)"));
            sb.AppendLine("■ 장면의 카메라");
            foreach (var c in Camera.allCameras.Concat(UnityEngine.Object.FindObjectsByType<Camera>()).Distinct())
            {
                if (c == null || c.name.StartsWith("Mori PrefabFx")) continue;
                c.TryGetComponent(out UniversalAdditionalCameraData cd);
                string rtype = cd != null ? cd.renderType.ToString() : "?";
                string rend = "?";
                try { rend = cd?.scriptableRenderer?.GetType().Name ?? "?"; } catch { }
                var see = fxLayers.Where(l => (c.cullingMask & (1 << l)) != 0).Select(l => LayerMask.LayerToName(l)).ToList();
                string stack = cd != null && cd.renderType == CameraRenderType.Base && cd.cameraStack != null && cd.cameraStack.Count > 0 ? " · 위에 겹침: " + string.Join(", ", cd.cameraStack.Where(x => x != null).Select(x => x.name)) : "";
                sb.AppendLine($"  - {c.name}: {rtype} · 렌더러 {rend} · {(c.isActiveAndEnabled ? "켜짐" : "꺼짐")} · 이펙트 층을 {(see.Count > 0 ? "그림(" + string.Join(", ", see) + ")" : "안 그림")}{stack}");
            }
#else
            sb.AppendLine("URP 가 아닌 프로젝트라 진단할 게 없어요.");
#endif
            return sb.ToString();
        }

        public static string FxLayer => PickLayer(PostSettings.F.fxLayer, new[] { "effect", "fx", "vfx" }, true);

        static string PickLayer(string set, string[] hints, bool lastIfNone)
        {
            var layers = SortingLayer.layers;
            if (!string.IsNullOrEmpty(set)) foreach (var l in layers) if (l.name == set) return set;
            foreach (var l in layers) foreach (var h in hints) if (l.name.ToLowerInvariant().Contains(h)) return l.name;
            return lastIfNone && layers.Length > 0 ? layers[layers.Length - 1].name : "Default";
        }

        /// <summary>카메라를 MFS(장면의 Main Camera)와 같은 URP 설정으로: 후처리 · HDR · 볼륨 마스크 · 렌더러 · 안티에일리어싱.</summary>
        void UrpCamera()
        {
#if MORI_URP
            var d = cam.GetUniversalAdditionalCameraData();
            var main = Camera.main;
            UniversalAdditionalCameraData md = null;
            if (main != null && main != cam) main.TryGetComponent(out md);
            d.renderPostProcessing = PostSettings.F.volume && (md == null || md.renderPostProcessing);
            d.volumeLayerMask = md != null ? md.volumeLayerMask : (LayerMask)~0;
            // 볼륨 기준점 = 게임의 Main Camera 자리 — 킷 카메라는 장면에서 아주 먼 곳(ORIGIN)에 있어서,
            // 구역형(Local) 볼륨(게임 화면 근처에 콜라이더로 범위를 둔 블룸 · 톤매핑)이 킷에는 안 먹던 문제
            d.volumeTrigger = main != null && main != cam ? main.transform : cam.transform;
            if (md != null)
            {
                d.antialiasing = md.antialiasing;
                d.antialiasingQuality = md.antialiasingQuality;
                d.stopNaN = md.stopNaN;
                d.dithering = md.dithering;
                if (!rendererCopied)
                {
                    rendererCopied = true;
                    var f = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && f.GetValue(md) is int idx) d.SetRenderer(idx);
                }
            }
            cam.allowHDR = main != null ? main.allowHDR : true;
            // 2D 라이트는 ‘카메라가 찍는 오브젝트 층(Culling Mask)’ 에 그 라이트 오브젝트의 층이 있어야 먹음.
            // 킷 카메라는 층 31(킷 전용)만 찍어서 장면의 Global Light 2D(Default 층)가 빠졌고 → 무대가 새까맣게 나왔다.
            // 라이트들의 층을 더함(킷 카메라는 장면에서 멀리 떨어진 곳을 보므로 장면의 다른 오브젝트는 안 찍힘).
            if (Time.realtimeSinceStartup - lightMaskAt > 2f || lightMaskAt < 0)
            {
                lightMaskAt = Time.realtimeSinceStartup;
                lightMask = 0;
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light2D>()) if (l != null) lightMask |= 1 << l.gameObject.layer;
            }
            cam.cullingMask = (1 << LAYER) | lightMask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
#endif
        }
        bool rendererCopied;
        int lightMask; float lightMaskAt = -10;

        void Ensure()
        {
            FxNoise.Ensure();
            if (root != null && cam != null) return;
            root = new GameObject("Mori PrefabFx") { hideFlags = HideFlags.DontSave };
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.transform.position = ORIGIN;
            var cg = new GameObject("Mori PrefabFx Camera") { hideFlags = HideFlags.DontSave };
            cg.transform.SetParent(root.transform, false);
            cam = cg.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << LAYER;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 400;
            cam.allowHDR = false; cam.allowMSAA = false;
            cam.depth = -100;
            cam.enabled = false;
            if (!culledOthers)
            {
                culledOthers = true;
                foreach (var c in Camera.allCameras) if (c != cam && !c.name.StartsWith("Mori PrefabFx")) c.cullingMask &= ~(1 << LAYER);      // 다른 카메라엔 안 찍히게
            }
        }

        void Release()
        {
            if (cam != null) cam.targetTexture = null;
            if (RenderTexture.active == rtCam || RenderTexture.active == rtOut) RenderTexture.active = null;
            if (rtCam != null) { rtCam.Release(); UnityEngine.Object.Destroy(rtCam); rtCam = null; }
            if (rtOut != null) { rtOut.Release(); UnityEngine.Object.Destroy(rtOut); rtOut = null; }
        }

        /// <summary>화면이 닫힐 때: 프리팹 · 카메라 · 텍스처 정리.</summary>
        public void Dispose()
        {
            Dir.StopAll();
            live.Clear();
            pool.Clear();
            origs.Clear();
            foreach (var s in sprites.Values) if (s != null) UnityEngine.Object.Destroy(s);
            sprites.Clear();
            HideAll();
            Release();
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; cam = null;
            for (int i = 0; i < bodyFx.Length; i++) bodyFx[i] = null;
            stageSr = null;
            if (stageSprite != null) UnityEngine.Object.Destroy(stageSprite);
            if (stageTex != null) UnityEngine.Object.Destroy(stageTex);
            stageSprite = null; stageTex = null; urpLive = false; rendererCopied = false;
            if (fix != null) UnityEngine.Object.Destroy(fix);
            if (matAdd != null) UnityEngine.Object.Destroy(matAdd);
            if (matAlpha != null) UnityEngine.Object.Destroy(matAlpha);
            fix = matAdd = matAlpha = null; fixTried = matTried = false;
        }
    }
}
