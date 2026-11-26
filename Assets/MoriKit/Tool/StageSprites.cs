using System;
using System.Collections.Generic;
using Mori.Core.Fight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 결투 캐릭터 · 배경 · 지형 그림.
    ///  1) PSD(가장 먼저): Assets/StreamingAssets/Fight/*.psd — ArtLib 이 직접 읽고, 저장하면 실행 중에도 바로 바뀐다(빌드에서도).
    ///  2) png(예전 방식): Assets/Mori/Client/Resources/Fight/*.png — 한 장짜리.
    ///  3) 둘 다 없으면 코드 그림.
    /// 상태 이름(없으면 -idle, 그것도 없으면 코드 그림):
    ///  · 플레이어: hero-idle · hero-run · hero-jump · hero-fall · hero-dash · hero-attack1~3(또는 hero-attack) · hero-counter(누른 순간) · hero-counter-hit · hero-counter-crash · hero-hurt · hero-down
    ///  · 결투자: foe-idle · foe-walk · foe-windup · foe-attack · foe-charge · foe-air · foe-stance · foe-stun · foe-ko
    ///  · 배경: bg · 지형: pillar(기둥) · platform(발판)
    /// 방패 · 예고 표식 · 잔상 같은 표시는 그림 위에 그대로 얹힌다. 크기 · 발 위치 · 방향은 튜닝 패널 ‘화면 › 캐릭터 그림’.
    /// </summary>
    public sealed partial class StageField
    {
        public const string DIR = "Fight/";
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        Texture2D HeroTex, FoeTex, BgTex, AllyTex;
        /// <summary>편집기 · 작업실: 캐릭터가 이 상태를 계속 재생(null = 평소대로).</summary>
        public static string ForceHero, ForceFoe;
        /// <summary>작업실 연출 카드: 플레이어 그림 시계를 이 시간(ms)에 고정(음수 = 평소대로 흐름).</summary>
        public static float ForceHeroMs = -1;

        static Texture2D Png(string name)
        {
            if (!cache.TryGetValue(name, out var t)) cache[name] = t = Resources.Load<Texture2D>(DIR + name);
            return t;
        }

        static bool Has(string name) => ArtLib.Get(name) != null || Png(name) != null;

        /// <summary>앞 이름부터 차례로 찾아 처음 있는 상태 이름(없으면 null).</summary>
        static string First(params string[] names)
        {
            foreach (var n in names) if (Has(n)) return n;
            return null;
        }

        /// <summary>캐릭터 하나의 애니메이션 시계: 상태가 바뀌면 처음부터.</summary>
        sealed class Clock { public string Key; public float Ms; public int Ver = -1; }
        readonly Clock heroClock = new Clock(), foeClock = new Clock(), allyClock = new Clock(), cutClock = new Clock();
        double allyPrevShot = 9;
        bool allyRunAnim, allyWalkAnim;
        /// <summary>동료 머리 위 화면 위치(드론 매달기용, 이번 프레임).</summary>
        Vector2 allyTopPx; bool allyTopOk;
        float cutInT = -1, cutInDur = 0.8f;

        /// <summary>동료 컷인(여주인공 얼굴 띠) 시작 — 그림 ally-cutin, 없으면 글자 띠.</summary>
        public void AllyCutIn(float dur) { cutInDur = Mathf.Max(0.2f, dur); cutInT = 0; cutClock.Key = null; }
        /// <summary>연결 · 준비 · 대기 · 긴 대기(그림만 — 판정 상태가 바뀌면 즉시 끊김).</summary>
        readonly AnimFlow heroFlow = new AnimFlow("hero"), foeFlow = new AnimFlow("foe");
        public string HeroFlowPhase => heroFlow.PhaseName;

        // ───────── 카운터 성공 · 크래시 모션(1회 · 끝까지) ─────────
        string react;
        float reactMs;
        int reactPrio, reactFace;

        /// <summary>
        /// 카운터 성공(hero-counter-hit) · 크래시(hero-counter-crash) 모션 시작. 그림이 없으면 아무것도 안 함(지금 그림 그대로).
        /// 재생 중엔 다른 상태 그림보다 앞서 끝까지(맞음 · 다운만 끊음). 크래시가 성공보다 우선.
        /// </summary>
        public void HeroReact(string key, int prio)
        {
            if (!Has(key)) return;
            if (react != null && prio < reactPrio) return;
            react = key; reactMs = 0; reactPrio = prio;
            var wd = World?.D;
            reactFace = wd != null && System.Math.Abs(wd.E.X - wd.P.X) > 1e-6 ? (wd.E.X > wd.P.X ? 1 : -1) : 0;   // 시작 순간 결투자 쪽을 봄 — 모션 동안 고정
            heroClock.Key = null;            // 같은 모션이 연달아 와도 첫 프레임부터
        }

        // ───────── 공중: 도약(1회) + 공중 몸통(세로 속도로 프레임 고름) ─────────
        const string AIR = "hero-air";
        string takeoff;                 // 지금 재생 중인 도약 그림(null = 없음)
        float takeoffMs, fallHoldMs;
        bool wasGrounded = true, jumpShown, djShown;

        /// <summary>
        /// 공중 상태 이름. hero-air 를 그렸으면 공중 전체가 hero-air 하나(도약은 Takeoff 가 그 위에 얹음 — 상태가 쪼개지지 않아 끊김이 없음).
        /// 안 그렸으면 예전처럼 hero-doublejump(없으면 hero-jump) · hero-jump · hero-fall.
        /// </summary>
        string AirKey(PlayerFsm h, PState st)
        {
            if (Has(AIR)) return AIR;
            if (st == PState.DoubleJump && Has("hero-doublejump")) return "hero-doublejump";
            return h.Vy > 0 ? "hero-jump" : "hero-fall";
        }

        /// <summary>
        /// 도약 그림: 땅에서 뛰어오르는 순간 hero-jump, 더블 점프 순간 hero-doublejump(없으면 hero-jump). 한 번 끝까지 재생 —
        /// 그 사이 정점을 넘겨 떨어지기 시작해도 끊지 않음(착지 · 공격 · 대쉬 같은 상태 변화만 끊음). 공중에서 한 번씩만.
        /// </summary>
        string Takeoff(PlayerFsm h, PState st, bool leftGround, float dtMs)
        {
            if (leftGround && h.Vy > 0 && !jumpShown) { jumpShown = true; Start(First("hero-jump")); }      // 발판에서 걸어 떨어질 때는 도약 없음
            if (st == PState.DoubleJump && !djShown) { djShown = true; Start(First("hero-doublejump", "hero-jump")); }
            if (takeoff == null) return null;
            takeoffMs += dtMs;
            var a = ArtLib.Get(takeoff);
            float len = a != null && a.TotalMs > 0 ? a.TotalMs : 0;
            if (takeoffMs >= len) { takeoff = null; return null; }
            return takeoff;

            void Start(string k) { takeoff = k; takeoffMs = 0; }
        }

        /// <summary>
        /// 공중 몸통 프레임: hero-air 를 ‘오름 → 정점 → 낙하’ 순서로 그리면 세로 속도로 골라 줌.
        ///  · |세로 속도| ≤ 정점 판정 세로 속도(플레이어 › 점프 › 체공 느낌) = 가운데 프레임(정점) — 정점에서 깜빡이지 않게 묶음
        ///  · 오름 = 점프 초속 → 정점 사이를 앞쪽 프레임에 · 낙하 = 정점 → 최대 낙하 속도 사이를 뒤쪽 프레임에
        /// </summary>
        static int AirIndex(ArtLib.Anim a, double vy, FightTune T)
        {
            if (a == null || a.Frames == null || a.Frames.Length <= 1) return 0;
            int n = a.Frames.Length, mid = (n - 1) / 2;
            double band = Math.Max(0.0001, T.ApexBand);
            if (Math.Abs(vy) <= band) return mid;
            if (vy > 0)
            {
                if (mid <= 0) return 0;
                double q = (T.JumpV - vy) / Math.Max(0.0001, T.JumpV - band);
                return Mathf.Clamp((int)Math.Floor(Math.Max(0, Math.Min(0.9999, q)) * mid), 0, mid - 1);
            }
            int lo = mid + 1, cnt = n - lo;
            if (cnt <= 0) return n - 1;
            double f = (-band - vy) / Math.Max(0.0001, T.MaxFall - band);
            return Mathf.Clamp(lo + (int)Math.Floor(Math.Max(0, Math.Min(0.9999, f)) * cnt), lo, n - 1);
        }
        public string FoeFlowPhase => foeFlow.PhaseName;

        /// <summary>흐름이 준비 루프 중이고 지금 그림이 그 준비 그림이면 프레임을 되풀이(1회 그림도). 아니면 −1.</summary>
        static int LoopReady(AnimFlow fl, string key, string flowKey, Clock c, ref Texture2D tex)
        {
            if (!fl.ReadyLoop || key != flowKey) return -1;
            var a = ArtLib.Get(key);
            if (a == null || a.Frames.Length == 0 || a.TotalMs <= 0) return -1;
            int i = Mathf.Clamp(a.IndexAt(Mathf.Repeat(c.Ms, a.TotalMs)), 0, a.Frames.Length - 1);
            tex = a.Frames[i];
            return i;
        }

        static Texture2D Frame(string key, Clock c, float dtMs)
        {
            if (key == null) { c.Key = null; return null; }
            if (key != c.Key || c.Ver != ArtLib.Version) { if (key != c.Key) c.Ms = 0; c.Key = key; c.Ver = ArtLib.Version; }
            else c.Ms += dtMs;
            var a = ArtLib.Get(key);
            return a != null ? a.At(c.Ms) : Png(key);
        }

        void UpdateSprites()
        {
            var w = World;
            var r = contentRect;
            if (w == null || !(r.width >= 10) || !(r.height >= 10))
            {
                heroImg.style.display = foeImg.style.display = bgImg.style.display = allyImg.style.display = cutIn.style.display = DisplayStyle.None;
                rigHero.Hide(); rigFoe.Hide();
                Fx.Update(null, 0, 0, 1, 1, Vector2.zero, 0);
                HideFrom(bgBack, 1); HideFrom(bgFront, 0); HideFrom(terrain, 0);
                return;
            }
            float W = r.width, H = r.height, u = w.PxU(W, H);
            var T = w.D.T;
            float fxDtMs = Time.unscaledDeltaTime * 1000f;
            float dtMs = fxDtMs * Mathf.Clamp01(w.FxSlow);           // 히트스톱(연출 카드) 동안 캐릭터 그림도 느리게
            var fsc = Fx.Screen;
            float heroDtMs = fxDtMs * Mathf.Clamp01(fsc.ScaleOf(1)), foeDtMs = fxDtMs * Mathf.Clamp01(fsc.ScaleOf(2));   // ‘한쪽만’ 히트스톱
            // 보이는 크기: 넓이 배율을 얼마나 따라갈지(1 = 판정과 같은 비율) × 전체 배율 — 그림에만
            float zf = Mathf.Pow((float)(FightTune.Pos(T.CamZoom, 0.05 * FightUnit.S) / FightUnit.S), 1f - Mathf.Clamp01((float)T.ImgFollowZoom));      // 넓이 배율은 예전 단위 기준으로
            float ui = u * zf * (float)FightTune.Pos(T.ImgScaleAll, 0.05);

            // 배경: PSD 층 → 없으면 png 한 장
            BgTex = BgPreset == 0 && ArtLib.Bg == null ? Png("bg") : null;
            bgImg.style.display = BgTex != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (BgTex != null) bgImg.style.backgroundImage = new StyleBackground(BgTex);
            BgLayers(w, W, H, u, dtMs, ui / u * (float)FightTune.Pos(T.ImgScaleBg, 0.05));
            Terrain(w, W, H, u);

            // 플레이어
            var h = w.Hero;
            var st = h.State;
            bool hurt = w.HeroHurtT < 0.3f || h.HurtT > 0;
            string hs = w.D.UltOn && h.UltLockT > 0 ? "hero-ult"                           // 궁극기: 폭격 요청 자세(위치 고정 동안)
                : st == PState.Stun || st == PState.Knockback || st == PState.WallBounce ? "hero-down"
                : hurt ? "hero-hurt"
                : st == PState.Dash ? "hero-dash"
                : st == PState.Attack ? "hero-attack" + Mathf.Clamp(h.ComboIndex, 1, 3)
                : st == PState.Counter ? "hero-counter"                                    // 카운터 누른 순간의 한 번 휘두름 — 그 뒤 배리어 동안은 실제 행동 그림(배리어는 둘레 효과로)
                : !h.Grounded ? AirKey(h, st)
                : st == PState.Move ? "hero-run" : "hero-idle";
            string hk = First(hs, hs == "hero-down" ? "hero-hurt" : hs.StartsWith("hero-attack") ? "hero-attack" : hs == "hero-fall" || hs == "hero-doublejump" ? "hero-jump" : hs, "hero-idle");
            string hflow = heroFlow.Update(hs, heroDtMs, T, Has);         // 연결 · 준비 · 긴 대기(그림만)
            if (hflow != hs && Has(hflow)) hk = hflow;
            // 카운터 성공 · 크래시 모션: 끝까지 재생(맞음 · 다운만 끊음)
            if (react != null)
            {
                // 실제로 맞았을 때만 끊음 — 크래시 · 카운터 성공 자체의 밀려남(넉백 · 경직)으로는 끊지 않음
                if (w.HeroHurtT < 0.3f || h.HurtT > 0) react = null;
                else
                {
                    reactMs += heroDtMs;
                    var ra = ArtLib.Get(react);
                    if (ra != null && reactMs < ra.TotalMs) hk = react;
                    else { heroFlow.AfterOverride(react); react = null; }
                }
            }
            int airIdx = -1;
            bool leftGround = wasGrounded && !h.Grounded;
            wasGrounded = h.Grounded;
            if (h.Grounded) { jumpShown = djShown = false; takeoff = null; fallHoldMs = 0; }
            if (hs == AIR && hk == AIR && react == null)
            {
                string tk = Takeoff(h, st, leftGround, heroDtMs);                      // 도약(점프 · 더블 점프) — 끝까지 재생한 뒤 공중 몸통으로
                if (tk != null) hk = tk;
                else
                {
                    var am = ArtLib.Get(AIR);
                    airIdx = AirIndex(am, h.Vy, T);
                    // 긴 낙하: 마지막 프레임(최대 낙하 속도 근처)에 머물면 hero-fall 루프로
                    if (am != null && airIdx >= am.Frames.Length - 1 && h.Vy < 0 && Has("hero-fall"))
                    {
                        fallHoldMs += heroDtMs;
                        if (fallHoldMs >= (float)T.FallLoopSec * 1000f) { hk = "hero-fall"; airIdx = -1; }
                    }
                    else fallHoldMs = 0;
                }
            }
            else takeoff = null;              // 공격 · 대쉬 · 맞음 등 다른 상태가 끼면 도약은 끊김(공중에서 한 번씩만이라 다시 나오지 않음)
            if (ForceHero != null && Has(ForceHero)) hk = ForceHero;
            HeroTex = Frame(hk, heroClock, heroDtMs);
            if (airIdx >= 0) { var aa = ArtLib.Get(AIR); if (aa != null && aa.Frames.Length > 0) HeroTex = aa.Frames[Mathf.Clamp(airIdx, 0, aa.Frames.Length - 1)]; }
            if (ForceHeroMs >= 0 && ForceHero != null && hk == ForceHero) { heroClock.Ms = ForceHeroMs; var fa0 = ArtLib.Get(hk); if (fa0 != null) HeroTex = fa0.At(ForceHeroMs); }
            int heroLoopIdx = LoopReady(heroFlow, hk, hflow, heroClock, ref HeroTex);       // 준비 루프(1회 그림이어도 준비 시간 동안 되풀이)
            Color htint = hurt && Mathf.Repeat(w.HeroHurtT * 20, 1) < 0.5f ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (fsc.BodyOf(1, out var hbc, out bool hbAdd) && !hbAdd) htint = Color.Lerp(htint, new Color(hbc.r, hbc.g, hbc.b, htint.a), hbc.a);      // 연출 카드 ‘몸’(색 입히기)
            double hh = HeroTex != null ? HeroTex.height / (double)PostSettings.Ppu : 0;      // 1U = ppu px(MFS 와 같음) — 캔버스가 커져도 픽셀 크기는 그대로
            int hFoot = HeroTex != null ? (ArtLib.Get(hk)?.Foot ?? 0) : 0;
            int hFace = react != null && reactFace != 0 ? reactFace : h.Facing;       // 크래시 · 성공 모션 동안은 결투자를 마주 봄
            var hg = Place(heroImg, HeroTex, w, W, H, ui * (float)FightTune.Pos(T.ImgScaleHero, 0.05), h.X, h.Y, hh, T.HeroImgDown, hFace, htint, hFoot);
            bool imgRight = T.ImgFaceRight >= 0.5;
            Fx.Hero = new PrefabFx.CharShot
            {
                ok = HeroTex != null, tex = HeroTex, feet = new Vector2((float)h.X, (float)(h.Y - T.HeroImgDown - (HeroTex != null ? hh * hFoot / Mathf.Max(1, HeroTex.height) : 0))),
                hU = (float)hh * ui * (float)FightTune.Pos(T.ImgScaleHero, 0.05) / Mathf.Max(0.0001f, u), flip = (hFace >= 0) != imgRight,
            };
            var ha = ArtLib.Get(hk);
            rigHero.Update(w, W, H, u, shakeNow, ha, ha != null ? (airIdx >= 0 ? Mathf.Clamp(airIdx, 0, ha.Frames.Length - 1) : heroLoopIdx >= 0 ? heroLoopIdx : ha.IndexAt(heroClock.Ms)) : 0, hg, hFace, "hero", heroDtMs / 1000f);

            // 결투자
            var f = w.Foe;
            var d = w.D;
            bool threat = d.NextThreat(out double left, out HitKind kind, out bool stance);
            float telB = (float)FightTune.Pos(d.TelegraphOf(f.Cur), 0.05);
            float wind = threat ? Mathf.Clamp01(1 - (float)left / telB) : 0;
            bool reveal = threat && stance && !f.InCounter && ((f.Cur != null && f.Cur.Counter) || d.CounterPrepping);
            bool dis = f.Disabled && !f.KO;
            // 걷기: 실제 이동 속도 · 방향(결투자는 늘 플레이어를 봄 → 멀어지는 쪽이면 뒷걸음)
            double fvx = System.Math.Abs(f.Vx) > 0.1 ? f.Vx : f.StepT > 0 ? f.StepVx : 0;
            bool walking = System.Math.Abs(fvx) > 0.1 || f.StepT > 0;
            bool away = walking && System.Math.Sign(fvx) == -f.Facing;
            string fs = f.KO ? "foe-ko"
                : dis ? "foe-stun"
                : f.InCounter || reveal ? "foe-stance"
                : f.ChargeLeft > 0 ? "foe-charge"
                : f.DashT > 0 ? "foe-dash"                                                  // 자연 이동: 대쉬 · 백대쉬
                : f.TurnT > 0 && f.Grounded && Has("foe-turn") ? "foe-turn"                  // 돌아서기(그림이 있을 때만)
                : !f.Grounded ? (f.Vy > 0 && Has("foe-jump") ? "foe-jump" : "foe-air")       // 오름(있으면) · 공중
                : threat && left < 0.2 ? "foe-attack"
                : wind > 0 ? "foe-windup"
                : walking ? (away ? "foe-walk-back" : "foe-walk") : "foe-idle";
            string fk = First(fs, fs == "foe-attack" || fs == "foe-charge" ? "foe-windup" : fs == "foe-walk-back" || fs == "foe-dash" ? "foe-walk" : fs, "foe-idle");
            string fflow = foeFlow.Update(fs, foeDtMs, T, Has);
            if (fflow != fs && Has(fflow)) fk = fflow;
            if (ForceFoe != null && Has(ForceFoe)) fk = ForceFoe;
            // 걷기 그림: 이동 속도에 맞춰 빠르게 · 느리게, 뒷걸음 그림이 없으면 걷기를 거꾸로
            bool walkAnim = fk == "foe-walk" || fk == "foe-walk-back";
            float fdt = foeDtMs;
            if (walkAnim && T.WalkSync >= 0.5)
            {
                double refV = T.WalkRefSpeed > 0 ? T.WalkRefSpeed : FightTune.Pos(T.EnemySpeed, 0.1 * FightUnit.S);
                fdt *= Mathf.Clamp((float)(System.Math.Abs(fvx) / System.Math.Max(0.01, refV)), 0.25f, 3f);
            }
            FoeTex = Frame(fk, foeClock, fdt);
            int foeIdx = LoopReady(foeFlow, fk, fflow, foeClock, ref FoeTex);
            if (fk == "foe-walk" && away && T.WalkBackReverse >= 0.5)
            {
                var wa = ArtLib.Get(fk);
                if (wa != null && wa.Frames.Length > 1 && wa.TotalMs > 0)
                {
                    foeIdx = wa.IndexAt(wa.TotalMs - Mathf.Repeat(foeClock.Ms, wa.TotalMs) - 0.001f);
                    FoeTex = wa.Frames[foeIdx];
                }
            }
            Color tc = f.InCounter || reveal ? StageLook.Stance : StageLook.KindColor(kind);
            Color ftint = Color.Lerp(Color.white, tc, Mathf.Clamp01((f.InCounter || reveal ? 1 : wind) * (float)T.ImgTint));
            if (w.FoeHitT < 0.1f) ftint = new Color(1.6f, 1.6f, 1.6f);
            if (dis) ftint = Color.Lerp(Color.white, new Color(0.6f, 0.6f, 0.7f), 0.5f + 0.5f * Mathf.Sin((float)w.SongT * 30));
            if (f.KO) ftint.a = 0.5f;
            if (fsc.BodyOf(2, out var fbc, out bool fbAdd) && !fbAdd) ftint = Color.Lerp(ftint, new Color(fbc.r, fbc.g, fbc.b, ftint.a), fbc.a);
            double fh = (FoeTex != null ? FoeTex.height / (double)PostSettings.Ppu : 0) * (d.Phase == 2 ? T.FoeImgRage : 1);
            int fFoot = FoeTex != null ? (ArtLib.Get(fk)?.Foot ?? 0) : 0;
            var fg = Place(foeImg, FoeTex, w, W, H, ui * (float)FightTune.Pos(T.ImgScaleFoe, 0.05), f.X, f.Y, fh, T.FoeImgDown, f.Facing, ftint, fFoot);
            Fx.Foe = new PrefabFx.CharShot
            {
                ok = FoeTex != null, tex = FoeTex, feet = new Vector2((float)f.X, (float)(f.Y - T.FoeImgDown - (FoeTex != null ? fh * fFoot / Mathf.Max(1, FoeTex.height) : 0))),
                hU = (float)fh * ui * (float)FightTune.Pos(T.ImgScaleFoe, 0.05) / Mathf.Max(0.0001f, u), flip = (f.Facing >= 0) != imgRight,
            };
            var fa = ArtLib.Get(fk);
            rigFoe.Update(w, W, H, u, shakeNow, fa, fa != null ? (foeIdx >= 0 ? Mathf.Clamp(foeIdx, 0, fa.Frames.Length - 1) : fa.IndexAt(foeClock.Ms)) : 0, fg, f.Facing, "foe", foeDtMs / 1000f);

            // 동료(여주인공) · 컷인
            AllySprite(w, W, H, u, ui, heroDtMs);
            CutInUpdate(W, H, fxDtMs);

            // 프리팹(3D · 파티클): 결투 카메라와 같은 자리 · 배율로
            Fx.Update(w, W, H, u, ui, shakeNow, fxDtMs / 1000f);
        }

        /// <summary>
        /// 동료 그림(땅에 선 인간형, 발 기준 · 발밑 선 적용):
        /// ally-ult(궁극기 위치 고정 동안) · ally-shoot-strong(강한 사격 · 버팀) · ally-shoot(쏜 직후) · ally-fly(드론에 매달려 비행 — 없으면 ally-jump + 코드 드론) · ally-jump(공중) · ally-turn(돌아서는 중) · ally-walk-back(적을 보며 뒷걸음, 없으면 걷기 거꾸로) · ally-run · ally-walk · ally-idle.
        /// 없으면 차례로 비슷한 그림 → ally-idle → 코드 그림.
        /// </summary>
        void AllySprite(StageWorld w, float W, float H, float u, float ui, float dtMs)
        {
            var d = w.D;
            var T = d.T;
            allyTopOk = false;
            if (!d.AllyOn) { AllyTex = null; allyImg.style.display = DisplayStyle.None; return; }
            var a = d.A;
            if (a.ShotAgo < allyPrevShot) allyClock.Key = null;            // 새로 쏨: 쏘는 그림 첫 프레임부터
            allyPrevShot = a.ShotAgo;
            var sa = ArtLib.Get("ally-shoot");
            double shootLen = System.Math.Max(T.AllyShootTime, sa != null ? sa.TotalMs / 1000.0 : 0);
            double walkV = System.Math.Max(0.05 * FightUnit.S, T.AllyWalkSpeed);
            // 걷기 · 뛰기 그림은 경계에서 왔다 갔다 하지 않게(들어갈 때와 나올 때 기준이 다름)
            double sp = a.Speed;
            allyRunAnim = allyRunAnim ? sp > walkV * 0.9 : sp > walkV * 1.2;
            allyWalkAnim = allyWalkAnim ? sp > 0.25 * FightUnit.S : sp > 0.6 * FightUnit.S;
            string s = a.Flying ? "ally-fly"                                       // 드론에 매달려 비행
                : w.Hero.UltLockT > 0 && d.UltOn ? "ally-ult"
                : a.State == AllyState.Hold ? "ally-shoot-strong"
                : a.ShotAgo < shootLen ? "ally-shoot"
                : !a.Grounded ? "ally-jump"
                : a.TurnT > 0 && Has("ally-turn") ? "ally-turn"
                : allyWalkAnim && a.Backpedal ? "ally-walk-back"                  // 적을 보며 뒷걸음
                : allyRunAnim ? "ally-run"
                : allyWalkAnim ? "ally-walk" : "ally-idle";
            string k = First(s, s == "ally-shoot-strong" ? "ally-shoot" : s == "ally-run" ? "ally-walk" : s == "ally-ult" ? "ally-shoot" : s == "ally-jump" ? (allyWalkAnim ? "ally-walk" : "ally-idle") : s == "ally-walk-back" ? "ally-walk" : s == "ally-fly" ? "ally-jump" : s, "ally-idle");
            // 걷기 · 뛰기 그림은 실제 속도에 맞춰 빠르게 · 느리게
            float adt = dtMs;
            if (k == "ally-walk" || k == "ally-run" || k == "ally-walk-back")
            {
                double refV = k == "ally-run" ? System.Math.Max(0.05 * FightUnit.S, T.AllyRunSpeed) : walkV;
                adt *= Mathf.Clamp((float)(a.Speed / refV), 0.4f, 2f);
            }
            AllyTex = Frame(k, allyClock, adt);
            if (k == "ally-walk" && s == "ally-walk-back")
            {
                // 뒷걸음 그림이 없으면 걷기를 거꾸로
                var wa = ArtLib.Get(k);
                if (wa != null && wa.Frames.Length > 1 && wa.TotalMs > 0) AllyTex = wa.Frames[wa.IndexAt(wa.TotalMs - Mathf.Repeat(allyClock.Ms, wa.TotalMs) - 0.001f)];
            }
            if (AllyTex == null) { allyImg.style.display = DisplayStyle.None; return; }
            float sc = ui * (float)FightTune.Pos(T.ImgScaleAlly, 0.05);
            double hh = AllyTex.height / (double)PostSettings.Ppu;
            int foot = ArtLib.Get(k)?.Foot ?? 0;
            var wob = AllyWobble(w, u * FightUnit.Sf);
            float iu = System.Math.Max(0.0001f, u);
            var ag = Place(allyImg, AllyTex, w, W, H, sc, a.X + wob.x / iu, a.Y - wob.y / iu, hh, 0, a.Facing, new Color(1, 1, 1, a.Alpha), foot);
            if (ag.Valid) { allyTopPx = new Vector2(ag.Left + ag.TexW * ag.K / 2, ag.Top + ag.TexH * ag.K * 0.06f); allyTopOk = true; }
        }

        void CutInUpdate(float W, float H, float dtMs)
        {
            if (cutInT < 0) { cutIn.style.display = DisplayStyle.None; return; }
            cutInT += dtMs / 1000f;
            if (cutInT >= cutInDur) { cutInT = -1; cutIn.style.display = DisplayStyle.None; return; }
            const float SLIDE = 0.14f;
            float kin = Mathf.Clamp01(cutInT / SLIDE), kout = Mathf.Clamp01((cutInDur - cutInT) / SLIDE);
            float ein = 1 - (1 - kin) * (1 - kin), eout = 1 - (1 - kout) * (1 - kout);
            float bandH = Mathf.Max(56f, H * 0.2f), top = H * 0.58f - bandH / 2;
            cutIn.style.display = DisplayStyle.Flex;
            cutIn.style.left = -W * (1 - ein) + W * (1 - eout);
            cutIn.style.width = W;
            cutIn.style.top = top; cutIn.style.height = bandH;
            var tex = Frame("ally-cutin", cutClock, dtMs);
            if (tex == null) { cutInT = -1; cutIn.style.display = DisplayStyle.None; return; }     // 그림이 없으면 아무것도 안 띄움(글자 알림 없음)
            float picH = bandH * 1.3f, picW = 0;
            if (tex != null)
            {
                picW = picH * tex.width / Mathf.Max(1f, tex.height);
                cutInPic.style.display = DisplayStyle.Flex;
                cutInPic.style.backgroundImage = new StyleBackground(tex);
                cutInPic.style.width = picW; cutInPic.style.height = picH;
                cutInPic.style.left = W * 0.08f; cutInPic.style.top = bandH - picH;      // 띠 위로 머리가 살짝 튀어나옴
            }
            else cutInPic.style.display = DisplayStyle.None;
            cutInTxt.style.fontSize = Mathf.Clamp(bandH * 0.36f, 18f, 56f);
            cutInTxt.style.left = W * 0.08f + picW + 24f;
            cutInTxt.style.right = 16f;
        }

        SpriteGeom Place(VisualElement v, Texture2D tex, StageWorld w, float W, float H, float u, double x, double y, double hU, double downU, int facing, Color tint, int footPx = 0)
        {
            if (tex == null || hU <= 0) { v.style.display = DisplayStyle.None; return default; }
            v.style.display = DisplayStyle.Flex;
            v.style.backgroundImage = new StyleBackground(tex);
            float hp = (float)hU * u, wp = hp * tex.width / Mathf.Max(1f, tex.height);
            // 도트 그림은 키울 때만 픽셀 크기를 고르게(정수 배). 줄일 때는 설정한 크기 그대로 —
            // 예전엔 줄일 때도 0.25 단위로 맞추고 0.25 아래는 막아서, 큰 그림(512 등)이 작은 화면에서 실제보다 크게 보이고 크기 설정이 먹지 않았다.
            if (tex.filterMode == FilterMode.Point && tex.height > 0)
            {
                float k = hp / tex.height;
                if (k >= 1) { k = Mathf.Round(k); hp = tex.height * k; wp = tex.width * k; }
            }
            var feet = w.ToScreen(x, y - downU, W, H) + shakeNow;
            feet.y += footPx * hp / Mathf.Max(1f, tex.height);            // 발밑 선: 그 아래(바닥을 뚫는 부분)만큼 그림을 내림
            v.style.width = wp; v.style.height = hp;
            v.style.left = Mathf.Round(feet.x - wp / 2); v.style.top = Mathf.Round(feet.y - hp);
            bool right = w.D.T.ImgFaceRight >= 0.5;
            float flip = (facing >= 0) == right ? 1 : -1;
            v.style.scale = new Scale(new Vector3(flip, 1, 1));
            v.style.unityBackgroundImageTintColor = tint;
            return new SpriteGeom { Valid = true, Flip = flip < 0, Left = Mathf.Round(feet.x - wp / 2), Top = Mathf.Round(feet.y - hp), K = hp / Mathf.Max(1, tex.height), TexW = tex.width, TexH = tex.height };
        }

        // ───────── 그림 조각 풀(배경 타일 · 지형) ─────────
        static VisualElement Piece(VisualElement parent, int i)
        {
            while (parent.childCount <= i)
            {
                var v = new VisualElement { pickingMode = PickingMode.Ignore };
                v.style.position = Position.Absolute;
                v.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                parent.Add(v);
            }
            var e = parent[i];
            e.style.display = DisplayStyle.Flex;
            return e;
        }

        static void HideFrom(VisualElement parent, int from)
        {
            for (int i = from; i < parent.childCount; i++) parent[i].style.display = DisplayStyle.None;
        }

        static void Put(VisualElement e, Texture2D tex, float x, float y, float w, float h)
        {
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
        }

        float bgClock;

        /// <summary>
        /// 배경 PSD 층: 시차(이름의 숫자)만큼 카메라를 따라 움직인다. ‘고정’ = 화면에 붙음(화면을 덮게), ‘앞’ = 캐릭터 앞.
        /// 1U = 파일 이름의 px(기본 64), 캔버스 가로 가운데 = 결투장 가운데, 바닥선(파일 이름 ‘바닥1500’, 기본 캔버스 맨 아래) = 바닥.
        /// </summary>
        /// <summary>bs = 보이는 크기 배율(따라 움직이는 층만, 그 층의 바닥선 · 원점 기준으로 키움).</summary>
        void BgLayers(StageWorld w, float W, float H, float u, float dtMs, float bs = 1)
        {
            var bg = BgPreset == 0 ? ArtLib.Bg : null;      // 단색 배경 프리셋이면 배경 그림은 숨김
            int nb = 1, nf = 0;                  // bgBack 의 0번은 png 배경
            if (bg != null)
            {
                bgClock += dtMs;
                float bpu = bg.PxPerU / FightUnit.Sf;              // 파일 이름 px 은 예전 U 기준 → 지금 U(FightUnit.S)로
                float s = u * bs / Mathf.Max(1f, bpu);       // 캔버스 1px 의 화면 크기
                foreach (var L in bg.Layers)
                {
                    var tiles = L.At(bgClock);
                    var host = L.Front ? bgFront : bgBack;
                    foreach (var t in tiles)
                    {
                        float x, y, tw, th;
                        if (L.Fixed)
                        {
                            float k = Mathf.Max(W / bg.W, H / bg.H);
                            float ox = (W - bg.W * k) / 2, oy = (H - bg.H * k) / 2;
                            x = ox + t.X * k; y = oy + t.Y * k; tw = t.W * k; th = t.H * k;
                        }
                        else
                        {
                            double wx = (t.X - bg.W / 2.0) / bpu, wy = (bg.GroundY - t.Y) / bpu;
                            double par = L.Par;
                            var a = w.ToScreen(wx + w.CamX * (1 - par), wy + w.CamY * (1 - par), W, H) + shakeNow * (float)par;
                            if (bs != 1)
                            {
                                var o = w.ToScreen(w.CamX * (1 - par), w.CamY * (1 - par), W, H) + shakeNow * (float)par;    // 이 층의 원점(바닥선 가운데)
                                a = o + (a - o) * bs;
                            }
                            x = a.x; y = a.y; tw = t.W * s; th = t.H * s;
                            if (x > W || y > H || x + tw < 0 || y + th < 0) continue;     // 화면 밖
                        }
                        // 타일 사이 틈이 보이지 않게 반 픽셀 겹침
                        Put(Piece(host, L.Front ? nf++ : nb++), t.Tex, Mathf.Floor(x), Mathf.Floor(y), Mathf.Ceil(tw) + 1, Mathf.Ceil(th) + 1);
                    }
                }
            }
            HideFrom(bgBack, nb);
            HideFrom(bgFront, nf);
        }

        /// <summary>지형 그림: 기둥 = 윗면 + 몸통(아래로 반복), 발판 = 왼끝 + 가운데(반복) + 오른끝. 크기는 1U = 파일 이름 px.</summary>
        void Terrain(StageWorld w, float W, float H, float u)
        {
            int n = 0;
            var m = w.D.Map;
            var pl = ArtLib.Pillar;
            if (pl != null && pl.Body != null)
            {
                float s = u / Mathf.Max(1f, pl.PxPerU / FightUnit.Sf);
                foreach (var b in m.Solids)
                {
                    var top = w.ToScreen((b.X0 + b.X1) / 2, b.Y1, W, H) + shakeNow;
                    var bot = w.ToScreen(b.X0, b.Y0, W, H) + shakeNow;
                    float bw = pl.Body.width * s, cx = top.x - bw / 2;
                    if (cx > W || cx + bw < 0) continue;
                    float y = top.y;
                    if (pl.Cap != null) { float ch = pl.Cap.height * s; Put(Piece(terrain, n++), pl.Cap, cx, y, pl.Cap.width * s, ch); y += ch; }
                    float bh = pl.Body.height * s;
                    for (int guard = 0; y < bot.y - 0.5f && bh > 0.5f && guard < 64; guard++, y += bh)
                    {
                        float hh = Mathf.Min(bh, bot.y - y);
                        var e = Piece(terrain, n++);
                        Put(e, pl.Body, cx, y, bw, hh);
                        if (hh < bh - 0.5f) e.style.backgroundSize = new BackgroundSize(bw, bh);   // 바닥에서 잘림(늘리지 않음)
                    }
                }
            }
            var pf = ArtLib.Platform;
            if (pf != null && pf.Mid != null)
            {
                float s = u / Mathf.Max(1f, pf.PxPerU / FightUnit.Sf);
                foreach (var b in m.Platforms)
                {
                    var a = w.ToScreen(b.X0, b.Y1, W, H) + shakeNow;
                    var z = w.ToScreen(b.X1, b.Y1, W, H) + shakeNow;
                    if (a.x > W || z.x < 0) continue;
                    float ph = pf.Mid.height * s;
                    float lw = pf.Left != null ? pf.Left.width * s : 0, rw = pf.Right != null ? pf.Right.width * s : 0;
                    if (pf.Left != null) Put(Piece(terrain, n++), pf.Left, a.x, a.y, lw, ph);
                    if (pf.Right != null) Put(Piece(terrain, n++), pf.Right, z.x - rw, a.y, rw, ph);
                    float mw = pf.Mid.width * s;
                    for (float x = a.x + lw; x < z.x - rw - 0.5f && mw > 0.5f; x += mw)
                    {
                        float ww = Mathf.Min(mw, z.x - rw - x);
                        var e = Piece(terrain, n++);
                        Put(e, pf.Mid, x, a.y, ww, ph);
                        if (ww < mw - 0.5f) e.style.backgroundSize = new BackgroundSize(mw, ph);   // 마지막 조각은 늘리지 않고 잘라 보이게
                    }
                }
            }
            HideFrom(terrain, n);
        }
    }
}
