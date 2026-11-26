using System;
using Mori.Core.Fight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 결투장 그림(코드로 그린 네온 실루엣): 원경(시차) · 바닥(마디마다 빛남) · 끝 벽 · 기둥 · 발판 · 탄 · 결투자 · 플레이어 · 박자 표시.
    /// 타이밍을 읽기 쉽게:
    ///  · 결투자는 패턴을 시작하면(마디 첫 박) 공격 종류 색으로 물들고, 머리 위 점 두 개가 박마다 하나씩 켜진다(●○ → ●● → 타격).
    ///    머리 위 원이 줄어 안쪽 원에 닿는 순간이 타격.
    ///  · 위험 구역(설정 ‘위험 구역 표시’): 타격이 닿을 자리를 바닥 · 공중에 미리 칠한다. 마지막 반 박에 진해진다.
    ///  · 색 = 대응: 자홍 근접(카운터) · 연두 낮은 공격(점프) · 청록 탄(무빙) · 금빛 돌진(대쉬) · 보라 반격 자세(때리지 말기).
    ///  · 화면 아래 가운데: 마디의 박(지금 박이 켜짐) — 박에 맞춰 치면 추가 데미지.
    /// </summary>
    public sealed partial class StageField : VisualElement
    {
        public StageWorld World;
        /// <summary>그린 뒤 부름(후처리 StageView 가 씀).</summary>
        public Action AfterRepaint;
        public bool Guide = true;

        // 층(아래 → 위):
        //  배경 그림(bgBack: PSD 배경 층 · png bg) → 결투장 바닥 · 벽 · 코드 지형(back0) → 지형 그림(terrain)
        //  → 위험 구역 · 맵 위험물 · 탄 · 코드 캐릭터(back1) → 결투자 그림 → 플레이어 그림 → 앞 배경 층(bgFront) → 방패 · 표식 · 잔상 · 박자 · 판정 보기(front)
        readonly VisualElement bgBack, back0, terrain, back, bgFront, front, bgImg, foeImg, heroImg, allyImg, flash, cutIn, cutInPic;
        readonly Label cutInTxt;
        readonly ChainRig rigHero, rigFoe;
        /// <summary>등록한 3D · 파티클 프리팹(작업실 ‘프리팹’ 칸에서 순간마다 연결).</summary>
        public readonly PrefabFx Fx;

        /// <summary>판정 보기(F4): 몸판정 · 공격 판정 · 카운터 판정을 그림 위에 겹쳐 보인다.</summary>
        public static bool ShowBoxes;
        /// <summary>플레이어 공격 · 카운터 판정 범위(공격할 때 잠깐 번쩍임) 보이기.</summary>
        public static bool ShowAtkRange = true;
        /// <summary>코드로 그린 동작 잔상(검기 호 · 기계 흰 줄 · 카운터 고리) 보이기.</summary>
        public static bool ShowSwing = true;
        /// <summary>배경 프리셋(작업실 미리보기): 0 = 게임 배경 · 1 = 밝은 배경 · 2 = 흰색.</summary>
        public int BgPreset;

        public StageField()
        {
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            pickingMode = PickingMode.Ignore;
            bgBack = Layer0(); Add(bgBack);
            bgImg = Layer0(); bgBack.Add(bgImg);
            back0 = Layer0(); back0.generateVisualContent += Draw0; Add(back0);
            terrain = Layer0(); Add(terrain);
            back = Layer0(); back.generateVisualContent += Draw; Add(back);
            // 사슬(머리카락 · 옷자락): 캐릭터 그림 바로 뒤 · 앞
            var fxBack = Layer0(); Add(fxBack);          // 프리팹(캐릭터 뒤에)
            var fcb = Layer0(); Add(fcb);
            foeImg = Sprite0(); Add(foeImg);
            var fcf = Layer0(); Add(fcf);
            allyImg = Sprite0(); Add(allyImg);           // 동료(여주인공): 플레이어 그림 뒤
            var hcb = Layer0(); Add(hcb);
            heroImg = Sprite0(); Add(heroImg);
            var hcf = Layer0(); Add(hcf);
            rigFoe = new ChainRig(fcb, fcf); rigHero = new ChainRig(hcb, hcf);
            var fxFront = Layer0(); Add(fxFront);        // 프리팹(캐릭터 앞에 — 기본)
            foreach (var v in new[] { fxBack, fxFront }) v.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Fx = new PrefabFx(fxFront, fxBack);
            RegisterCallback<DetachFromPanelEvent>(_ => Fx.Dispose());
            bgFront = Layer0(); Add(bgFront);
            front = Layer0(); front.generateVisualContent += DrawFront; Add(front);
            // 동료 컷인(여주인공 얼굴 띠): 맨 위
            cutIn = new VisualElement { pickingMode = PickingMode.Ignore };
            cutIn.style.position = Position.Absolute;
            cutIn.style.display = DisplayStyle.None;
            cutIn.style.backgroundColor = new Color(0.06f, 0.07f, 0.1f, 0.88f);
            cutIn.style.borderTopWidth = cutIn.style.borderBottomWidth = 3;
            cutIn.style.borderTopColor = cutIn.style.borderBottomColor = new Color(0.55f, 0.85f, 1f, 0.95f);
            cutIn.style.overflow = Overflow.Visible;
            cutInPic = new VisualElement { pickingMode = PickingMode.Ignore };
            cutInPic.style.position = Position.Absolute;
            cutInPic.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            cutIn.Add(cutInPic);
            cutInTxt = new Label("") { pickingMode = PickingMode.Ignore };     // 글자 알림은 안 씀(그림만)
            cutInTxt.style.position = Position.Absolute;
            cutInTxt.style.color = Color.white;
            cutInTxt.style.unityFontStyleAndWeight = FontStyle.Bold;
            cutInTxt.style.unityTextAlign = TextAnchor.MiddleLeft;
            cutInTxt.style.whiteSpace = WhiteSpace.NoWrap;
            cutInTxt.style.top = 0; cutInTxt.style.bottom = 0;
            cutIn.Add(cutInTxt);
            Add(cutIn);
            flash = Layer0(); flash.style.display = DisplayStyle.None; Add(flash);     // 연출 카드 ‘섬광’
            bgImg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            foreach (var v in new[] { bgBack, terrain, bgFront }) v.style.overflow = Overflow.Hidden;
        }

        static VisualElement Layer0()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
            return v;
        }

        static VisualElement Sprite0()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.display = DisplayStyle.None;
            return v;
        }

        /// <summary>그림 위치 · 상태를 맞추고 다시 그린다(매 프레임 — 화면이 부른다).</summary>
        public void Repaint()
        {
            ArtLib.Tick(Time.unscaledDeltaTime);
            var w = World;
            if (w != null)
            {
                float shk = w.Shake * (float)w.D.T.ShakeMul;
                shakeNow = shk > 0.3f ? new Vector2(UnityEngine.Random.Range(-shk, shk), UnityEngine.Random.Range(-shk, shk)) : Vector2.zero;
                // 연출 카드: 흔들림(1080p 기준 px → 지금 화면) · 줌 · 히트스톱 · 섬광
                var fs = Fx.Screen;
                float hk = contentRect.height > 10 ? contentRect.height / 1080f : 1;
                shakeNow += new Vector2(fs.shake.x, -fs.shake.y) * hk * (float)w.D.T.ShakeMul;
                w.SetFxZoom(fs.zoom, fs.zoomAnchored, fs.zoomAt);
                w.FxSlow = fs.timeScale;
                if (fs.flash.a > 0.003f)
                {
                    flash.style.display = DisplayStyle.Flex;
                    flash.style.backgroundColor = fs.flash;
                }
                else flash.style.display = DisplayStyle.None;
            }
            else flash.style.display = DisplayStyle.None;
            UpdateSprites();
            back0.MarkDirtyRepaint();
            back.MarkDirtyRepaint();
            front.MarkDirtyRepaint();
            AfterRepaint?.Invoke();         // 후처리(StageView)
        }

        void DrawFront(MeshGenerationContext ctx)
        {
            var w = World;
            var r = contentRect;
            if (w == null || !(r.width >= 10) || !(r.height >= 10)) return;
            float W = r.width, H = r.height;
            float u = w.PxU(W, H);
            var p = ctx.painter2D;
            var sh = shakeNow;
            if (FoeTex != null) DrawFoe(p, w, W, H, u, sh, 1);
            if (HeroTex != null) DrawHero(p, w, W, H, u, sh, 1);
            if (ShowBoxes) Boxes(p, w, W, H, sh);
            Orbital(p, w, W, H, u, sh);
            EdgeMark(p, w, W, H);
            Metronome(p, w, W, H);
        }

        /// <summary>판정 보기: 몸판정(흰 · 빨강), 결투자 근접 판정(자홍), 카운터 판정(보라 · 자홍), 탄 판정(청록).</summary>
        void Boxes(Painter2D p, StageWorld w, float W, float H, Vector2 sh)
        {
            void Bx(Mori.Core.Fight.Box b, Color c, float fill)
            {
                var a = S(w, b.X0, b.Y1, W, H, sh);
                var z = S(w, b.X1, b.Y0, W, H, sh);
                p.fillColor = StageLook.A(c, fill);
                p.BeginPath(); Rect(p, a.x, a.y, z.x - a.x, z.y - a.y); p.Fill();
                p.strokeColor = c; p.lineWidth = 2;
                p.BeginPath(); Rect(p, a.x, a.y, z.x - a.x, z.y - a.y); p.Stroke();
            }
            var d = w.D;
            Bx(d.P.Hurtbox, Color.white, 0.08f);
            Bx(d.E.Hurtbox, StageLook.Foe, 0.08f);
            if (d.P.InCounter) Bx(d.P.CounterBox(), StageLook.ACT[Act.CTR], 0.12f);
            if (d.E.InCounter) Bx(d.E.CounterBox(), StageLook.Stance, 0.12f);
            foreach (var b in d.ActiveBoxes) Bx(b, StageLook.ACT[Act.CTR], 0.3f);
            foreach (var s in d.Shots) Bx(new Mori.Core.Fight.Box(s.X - s.Radius, s.X + s.Radius, s.Y - s.Radius, s.Y + s.Radius), StageLook.ACT[Act.ATK], 0.3f);
        }

        Vector2 shakeNow;

        static readonly Angle A0 = new Angle(0, AngleUnit.Degree), A180 = new Angle(180, AngleUnit.Degree), A360 = new Angle(360, AngleUnit.Degree);

        /// <summary>뒤 층: 하늘 · 원경(배경 그림이 없을 때) · 바닥 · 벽 · 코드 지형(지형 그림이 없을 때).</summary>
        void Draw0(MeshGenerationContext ctx)
        {
            var w = World;
            var r = contentRect;
            if (w == null || !(r.width >= 10) || !(r.height >= 10)) return;
            float W = r.width, H = r.height;
            w.ScreenW = W; w.ScreenH = H;
            float u = w.PxU(W, H);
            var p = ctx.painter2D;
            var sh = shakeNow;
            Background(p, w, W, H, u, sh);
            DrawTerrain(p, w, W, H, u, sh);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var w = World;
            var r = contentRect;
            if (w == null || !(r.width >= 10) || !(r.height >= 10)) return;
            float W = r.width, H = r.height;
            float u = w.PxU(W, H);
            var p = ctx.painter2D;
            var sh = shakeNow;
            if (Guide) Zones(p, w, W, H, u, sh);
            if (Guide) CrashRing(p, w, W, H, u, sh);
            Hazards(p, w, W, H, u, sh);
            DrawShots(p, w, W, H, u, sh);
            DrawFoe(p, w, W, H, u, sh, 0);
            if (AllyTex == null) DrawAlly(p, w, W, H, u, sh);
            DrawAllyDrone(p, w, W, H, u, sh);
            DrawHero(p, w, W, H, u, sh, 0);
        }

        /// <summary>동료 코드 그림(ally-idle 그림이 없을 때): 하늘색 선의 사람 모양 + 그림자 · 총구 섬광.</summary>
        void DrawAlly(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            var d = w.D;
            if (!d.AllyOn) return;
            var a = d.A;
            float us = u * FightUnit.Sf * (float)FightTune.Pos(d.T.ImgScaleAlly, 0.05);
            float sc = us / 100f * 1.45f;
            var wob = AllyWobble(w, us);
            var at = S(w, a.X, a.Y, W, H, sh) + wob;
            // 그림자
            double sup = d.Map.SupportBelow(a.X, a.Y + 0.01, Ally.HALF);
            var shp = S(w, a.X, sup, W, H, sh);
            float air = Mathf.Min(0.6f, (float)((a.Y - sup) / (4 * FightUnit.S)));
            p.fillColor = new Color(0, 0, 0, 0.4f);
            p.BeginPath(); Ellipse(p, shp + new Vector2(0, 2), 0.36f * us * (1 - air), 0.08f * us); p.Fill();
            bool hold = a.State == AllyState.Hold;
            bool shoot = a.ShotAgo < Mathf.Max(0.05f, (float)d.T.AllyShootTime);
            float sp = (float)(a.Speed / System.Math.Max(0.01, d.T.AllyRunSpeed));
            float run = sp > 0.05f ? Mathf.Sin((float)w.SongT * (8 + 10 * sp)) * Mathf.Clamp01(sp * 1.5f) : Mathf.Sin((float)w.SongT * 2.2f) * 0.08f;
            float lean = hold ? -0.25f : sp * 0.35f;                          // 강한 사격: 뒤로 버팀
            float arm = a.Flying ? -1.45f : shoot || hold ? -1.5f : 0.2f;       // 비행: 팔을 위로(드론 손잡이) · 쏠 땐 앞으로
            if (a.Flying) { run = Mathf.Sin((float)w.SongT * 3f) * 0.25f; lean = (float)(a.Vx / System.Math.Max(0.01, d.T.AllyFlySpeed)) * 0.4f * a.Facing; }
            float al = a.Alpha;
            var line = new Color(0.62f, 0.88f, 1f, al);
            Humanoid(p, at, sc, a.Facing, run, lean, a.Flying ? 0.25f : a.Grounded ? 0 : 0.6f, arm, line, new Color(0.14f, 0.18f, 0.26f, al), 2);
            allyTopPx = at + new Vector2(0, -1.5f * us); allyTopOk = true;
            if (a.ShotAgo < 0.08)
            {
                var mz = S(w, a.MuzzleX, a.MuzzleY, W, H, sh) + wob;
                p.fillColor = Color.white;
                p.BeginPath(); Ellipse(p, mz, 0.12f * us, 0.12f * us); p.Fill();
            }
        }

        /// <summary>드론(ally-fly 그림이 없을 때 코드로): 동료 머리 위 작은 쿼드콥터 + 매달린 줄.</summary>
        void DrawAllyDrone(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            var d = w.D;
            if (!d.AllyOn || !d.A.Flying || !allyTopOk || Has("ally-fly")) return;
            float us = u * FightUnit.Sf * (float)FightTune.Pos(d.T.ImgScaleAlly, 0.05);
            var hand = allyTopPx;
            var c = hand + new Vector2(0, -0.45f * us);
            float r = 0.2f * us;
            var line = new Color(0.62f, 0.88f, 1f);
            // 줄
            p.strokeColor = StageLook.A(line, 0.8f); p.lineWidth = 1.5f;
            p.BeginPath(); p.MoveTo(hand); p.LineTo(c); p.Stroke();
            // 몸통
            p.fillColor = new Color(0.16f, 0.2f, 0.28f, 1f);
            p.BeginPath(); Ellipse(p, c, r * 1.1f, r * 0.45f); p.Fill();
            p.strokeColor = line; p.lineWidth = 2;
            p.BeginPath(); Ellipse(p, c, r * 1.1f, r * 0.45f); p.Stroke();
            // 양쪽 날개(돌아가는 느낌)
            float spin = Mathf.Abs(Mathf.Sin((float)w.SongT * 45f));
            for (int s = -1; s <= 1; s += 2)
            {
                var hub = c + new Vector2(s * r * 1.5f, -r * 0.35f);
                p.strokeColor = line; p.lineWidth = 2;
                p.BeginPath(); p.MoveTo(c + new Vector2(s * r, 0)); p.LineTo(hub); p.Stroke();
                p.strokeColor = StageLook.A(line, 0.7f);
                p.BeginPath(); p.MoveTo(hub - new Vector2(r * 0.8f * spin, 0)); p.LineTo(hub + new Vector2(r * 0.8f * spin, 0)); p.Stroke();
            }
            // 아래 불빛
            p.fillColor = new Color(0.4f, 0.95f, 1f, 0.9f);
            p.BeginPath(); Ellipse(p, c + new Vector2(0, r * 0.3f), r * 0.2f, r * 0.2f); p.Fill();
        }

        /// <summary>플레이어가 맞으면 동료도 같이 휘청(그림 · 코드 그림 공통).</summary>
        static Vector2 AllyWobble(StageWorld w, float us)
        {
            bool hurt = w.HeroHurtT < 0.3f || w.Hero.HurtT > 0;
            return hurt ? new Vector2(Mathf.Sin((float)w.SongT * 55f) * 0.06f * us, Mathf.Cos((float)w.SongT * 41f) * 0.04f * us) : Vector2.zero;
        }

        Vector2 S(StageWorld w, double x, double y, float W, float H, Vector2 sh) => w.ToScreen(x, y, W, H) + sh;

        // ───────── 크래시 타이밍 링: 결투자 카운터 공격 순간에 딱 닿게 좁혀 옴 ─────────
        void CrashRing(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            var d = w.D;
            if (d == null || d.T.CrashRingOn < 0.5) return;
            double at = d.CrashAt;
            if (at < 0) return;
            float left = (float)(at - d.SongT), lead = (float)Math.Max(0.05, d.T.CrashRingLead * d.Beat), win = (float)d.CrashWindow;
            if (left > lead || left < -win) return;
            var e = d.E;
            var c = S(w, e.X, e.Cy, W, H, sh);
            float r0 = Mathf.Max(8, (float)(e.Height * 0.65) * u);                 // 목표: 결투자 몸 둘레
            float k = Mathf.Clamp01(1 - left / lead);
            float r = Mathf.Lerp(r0 * Mathf.Max(1.2f, (float)d.T.CrashRingSize), r0, k);
            bool inWin = Mathf.Abs(left) <= win;
            var gold = new Color(1f, 0.82f, 0.3f);
            // 목표 원(흐리게) — 링이 여기에 닿는 순간 = 카운터 공격 순간
            p.strokeColor = StageLook.A(StageLook.Stance, 0.35f); p.lineWidth = 2;
            p.BeginPath(); p.Arc(c, r0, A0, A360); p.Stroke();
            // 굿 판정 창 동안: 금빛 띠
            if (inWin)
            {
                p.strokeColor = StageLook.A(gold, 0.35f); p.lineWidth = 10;
                p.BeginPath(); p.Arc(c, r0, A0, A360); p.Stroke();
            }
            // 좁혀 오는 링
            p.strokeColor = StageLook.A(inWin ? gold : StageLook.Stance, 0.95f); p.lineWidth = inWin ? 4 : 3;
            p.BeginPath(); p.Arc(c, Mathf.Max(r0, r), A0, A360); p.Stroke();
        }

        // ───────── 궁극기: 궤도 폭격(임시 그림 — 연출 카드로 바꿔 끼울 수 있음) ─────────
        void Orbital(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            var d = w.D;
            if (d == null || !d.UltOn) return;
            var T = d.T;
            float el = (float)d.UltElapsed, cut = Mathf.Max(0.01f, (float)T.UltCut);
            float gy = S(w, 0, 0, W, H, sh).y;                                             // 바닥 화면 높이
            var lx = S(w, d.UltLaserX, 0, W, H, sh).x;
            float hw = Mathf.Max(2, (float)T.UltWidth * u);
            var hot = new Color(1f, 0.55f, 0.2f);
            if (el < cut)
            {
                // 조준: 화면이 어두워지고, 결투자 위치에 조준선이 좁혀 들어감
                float k = Mathf.Clamp01(el / cut);
                p.fillColor = new Color(0, 0, 0, Mathf.Clamp01((float)T.UltDim) * Mathf.Min(1, k * 3));
                p.BeginPath(); Rect(p, 0, 0, W, H); p.Fill();
                var c = new Vector2(lx, gy - 1.2f * u * FightUnit.Sf * 2f);
                float rr = Mathf.Lerp(hw * 4f, hw * 1.2f, k);
                p.strokeColor = StageLook.A(hot, 0.9f); p.lineWidth = 2.5f;
                p.BeginPath(); p.Arc(c, rr, A0, A360); p.Stroke();
                p.BeginPath(); p.MoveTo(c + new Vector2(-rr * 1.5f, 0)); p.LineTo(c + new Vector2(rr * 1.5f, 0));
                p.MoveTo(c + new Vector2(0, -rr * 1.5f)); p.LineTo(c + new Vector2(0, rr * 1.5f)); p.Stroke();
                // 우주에서 내려오는 가는 유도선
                p.strokeColor = StageLook.A(hot, 0.35f * k); p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(new Vector2(lx, 0)); p.LineTo(new Vector2(lx, gy)); p.Stroke();
                return;
            }
            // 폭격: 하늘 끝 → 바닥까지 굵은 레이저(박마다 맥동) + 바닥 폭발 고리
            float beat = (float)Math.Max(0.05, T.UltTickBeats * d.Beat);
            float ph = ((el - cut) % beat) / beat, pulse = 1f + 0.35f * (1 - ph) * (1 - ph);
            float ww = hw * pulse;
            p.fillColor = StageLook.A(hot, 0.35f);
            p.BeginPath(); Rect(p, lx - ww * 1.8f, 0, ww * 3.6f, gy); p.Fill();
            p.fillColor = StageLook.A(new Color(1f, 0.85f, 0.5f), 0.85f);
            p.BeginPath(); Rect(p, lx - ww, 0, ww * 2, gy); p.Fill();
            p.fillColor = new Color(1, 1, 1, 0.95f);
            p.BeginPath(); Rect(p, lx - ww * 0.35f, 0, ww * 0.7f, gy); p.Fill();
            p.strokeColor = StageLook.A(hot, 0.8f * (1 - ph)); p.lineWidth = 4;
            p.BeginPath(); p.Arc(new Vector2(lx, gy), ww * (1.5f + 3f * ph), A180, A360); p.Stroke();
            // 맵 곳곳 작은 폭격(연출만 — 데미지 없음): 박마다 자리가 바뀜
            int seed = (int)((el - cut) / beat);
            var rnd = new System.Random(seed * 7919 + 13);
            int nScatter = Mathf.Clamp((int)Math.Round(T.UltScatter), 0, 20);
            for (int i = 0; i < nScatter; i++)
            {
                double x = (rnd.NextDouble() * 2 - 1) * w.D.Map.HalfW;
                var bx = S(w, x, 0, W, H, sh).x;
                if (bx < -40 || bx > W + 40) continue;
                float bw = Mathf.Max(1.5f, hw * 0.25f);
                p.fillColor = StageLook.A(hot, 0.5f * (1 - ph));
                p.BeginPath(); Rect(p, bx - bw, 0, bw * 2, gy); p.Fill();
                p.strokeColor = StageLook.A(new Color(1f, 0.8f, 0.4f), 0.7f * (1 - ph)); p.lineWidth = 3;
                p.BeginPath(); p.Arc(new Vector2(bx, gy), bw * (2 + 6 * ph), A180, A360); p.Stroke();
            }
        }

        // ───────── 원경 · 바닥 ─────────
        static readonly Color Sky0 = new Color(0.05f, 0.06f, 0.10f), Sky1 = new Color(0.12f, 0.08f, 0.18f);

        static float Hash(int i) { unchecked { uint x = (uint)(i * 374761393 + 668265263); x = (x ^ (x >> 13)) * 1274126177u; return (x ^ (x >> 16)) / (float)uint.MaxValue; } }

        void Background(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            float gy = w.ToScreen(0, 0, W, H).y + sh.y;
            float pulse = w.BeatPulse * w.BeatPulse;
            if (BgPreset != 0)
            {
                // 그림 확인용 단색 배경: 밝은(옅은 회청) · 흰색 — 바닥선 · 눈금만 남김
                var c0 = BgPreset == 2 ? Color.white : new Color(0.86f, 0.89f, 0.93f);
                var c1 = BgPreset == 2 ? new Color(0.96f, 0.96f, 0.97f) : new Color(0.76f, 0.80f, 0.86f);
                p.fillColor = c0; p.BeginPath(); Rect(p, 0, 0, W, gy); p.Fill();
                p.fillColor = c1; p.BeginPath(); Rect(p, 0, gy, W, Mathf.Max(0, H - gy)); p.Fill();
                p.strokeColor = new Color(0.25f, 0.3f, 0.4f, 0.6f); p.lineWidth = 2;
                p.BeginPath(); p.MoveTo(new Vector2(0, gy)); p.LineTo(new Vector2(W, gy)); p.Stroke();
                return;
            }
            bool art = BgTex != null || ArtLib.Bg != null;
            if (!art)      // 배경 그림(PSD bg 또는 Resources/Fight/bg)이 있으면 하늘 · 원경은 그 그림이 대신
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f, b = (i + 1) / 6f;
                    p.fillColor = Color.Lerp(Sky0, Sky1, a);
                    p.BeginPath(); Rect(p, 0, H * a, W, H * (b - a) + 1); p.Fill();
                }
                // 먼 도시(시차 0.25) — 박마다 창문이 맥동
                Layer(p, w, W, H, u, gy, 0.25f, 0.5f, 2.6f, new Color(0.075f, 0.075f, 0.12f), pulse, true);
                // 가까운 탑(시차 0.55)
                Layer(p, w, W, H, u, gy, 0.55f, 0.8f, 4.2f, new Color(0.055f, 0.055f, 0.09f), pulse, false);
            }
            // 바닥(배경 PSD 가 있으면 그 그림의 바닥이 보이게 칠하지 않음 — 바닥선 · 눈금만)
            if (ArtLib.Bg == null)
            {
                p.fillColor = new Color(0.04f, 0.045f, 0.07f);
                p.BeginPath(); Rect(p, 0, gy, W, Mathf.Max(0, H - gy)); p.Fill();
            }
            float bar = w.BarPulse * w.BarPulse * w.BarPulse;
            p.strokeColor = new Color(0.45f, 0.8f, 1f, 0.35f + 0.5f * bar);
            p.lineWidth = 2 + 3 * bar;
            p.BeginPath(); p.MoveTo(new Vector2(0, gy)); p.LineTo(new Vector2(W, gy)); p.Stroke();
            // 바닥 눈금(2U 마다, 원근으로 퍼짐) — 박마다 밝아짐. 거리 감 잡기용.
            p.lineWidth = 1;
            p.strokeColor = new Color(0.4f, 0.6f, 1f, 0.10f + 0.15f * pulse);
            p.BeginPath();
            double tick = 2 * FightUnit.S;
            double x0 = Math.Floor((w.CamX - w.ViewW) / tick) * tick;
            for (double x = x0; x <= w.CamX + w.ViewW; x += tick)
            {
                if (Math.Abs(x) > w.D.Map.HalfW) continue;
                var a = S(w, x, 0, W, H, sh);
                p.MoveTo(a); p.LineTo(new Vector2(W / 2 + (a.x - W / 2) * 1.8f, H));
            }
            p.Stroke();
        }

        void Layer(Painter2D p, StageWorld w, float W, float H, float u, float gy, float par, float scale, float gap, Color c, float pulse, bool windows)
        {
            // 길이 단위 바꾸기(FightUnit.S): 배경 도시는 예전 모습 그대로 — 층 단위 = 예전 U
            float lu = u * scale * FightUnit.Sf;
            double cx = w.CamX * par / FightUnit.S;
            float baseY = gy - 0.2f * lu + (float)w.CamY * u * par;
            int i0 = (int)Math.Floor((cx - W / 2 / lu) / gap) - 1, i1 = (int)Math.Ceiling((cx + W / 2 / lu) / gap) + 1;
            for (int i = i0; i <= i1; i++)
            {
                float hsh = Hash(i * 7 + (windows ? 1 : 2));
                float bx = W / 2 + (float)(i * gap - cx) * lu;
                float bw = (0.9f + 0.9f * Hash(i * 3 + 11)) * lu;
                float bh = (windows ? 2.5f + 5 * hsh : 5f + 4 * hsh) * lu;
                p.fillColor = c;
                p.BeginPath(); Rect(p, bx, baseY - bh, bw, bh + 4); p.Fill();
                if (!windows) continue;
                p.fillColor = new Color(1f, 0.75f, 0.35f, 0.07f + 0.22f * pulse * ((i & 3) == 0 ? 1 : 0.3f));
                for (int k = 0; k < 4; k++) { p.BeginPath(); Rect(p, bx + bw * (0.18f + 0.44f * (k % 2)), baseY - bh + lu * (0.3f + 0.45f * (k / 2)), bw * 0.16f, lu * 0.2f); p.Fill(); }
            }
        }

        // ───────── 지형: 끝 벽 · 기둥 · 발판 ─────────
        static readonly Color WallFill = new Color(0.10f, 0.11f, 0.16f), WallEdge = new Color(0.5f, 0.8f, 1f);

        void DrawTerrain(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            var m = w.D.Map;
            float gy = S(w, 0, 0, W, H, sh).y;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float x = S(w, sd * m.HalfW, 0, W, H, sh).x;
                if (sd < 0 && x <= 0 || sd > 0 && x >= W) continue;
                p.fillColor = WallFill;
                p.BeginPath(); Rect(p, sd < 0 ? 0 : x, 0, sd < 0 ? x : W - x, H); p.Fill();
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = pass == 0 ? StageLook.A(WallEdge, 0.2f) : StageLook.A(WallEdge, 0.7f);
                    p.lineWidth = pass == 0 ? 10 : 3;
                    p.BeginPath(); p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, gy)); p.Stroke();
                }
            }
            if (ArtLib.Pillar == null) foreach (var b in m.Solids)
            {
                var a = S(w, b.X0, b.Y1, W, H, sh);
                var c = S(w, b.X1, b.Y0, W, H, sh);
                if (c.x < -20 || a.x > W + 20) continue;
                p.fillColor = WallFill;
                p.BeginPath(); Rect(p, a.x, a.y, c.x - a.x, c.y - a.y); p.Fill();
                p.strokeColor = StageLook.A(WallEdge, 0.45f);
                p.lineWidth = 2;
                p.BeginPath(); p.MoveTo(new Vector2(a.x, c.y)); p.LineTo(a); p.LineTo(new Vector2(c.x, a.y)); p.LineTo(c); p.Stroke();
                // 윗면(설 수 있음) 밝게
                p.strokeColor = StageLook.A(WallEdge, 0.9f);
                p.lineWidth = 3;
                p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(c.x, a.y)); p.Stroke();
            }
            if (ArtLib.Platform == null) foreach (var b in m.Platforms)
            {
                var a = S(w, b.X0, b.Y1, W, H, sh);
                var c = S(w, b.X1, b.Y0, W, H, sh);
                if (c.x < -20 || a.x > W + 20) continue;
                p.fillColor = new Color(0.13f, 0.12f, 0.2f);
                p.BeginPath(); Rect(p, a.x, a.y, c.x - a.x, Mathf.Max(4, c.y - a.y)); p.Fill();
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = pass == 0 ? new Color(0.75f, 0.6f, 1f, 0.2f) : new Color(0.85f, 0.75f, 1f, 0.9f);
                    p.lineWidth = pass == 0 ? 8 : 2.5f;
                    p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(c.x, a.y)); p.Stroke();
                }
                // 매달린 줄(발판이 떠 있다는 표시)
                p.strokeColor = new Color(0.6f, 0.6f, 0.8f, 0.18f);
                p.lineWidth = 1;
                p.BeginPath();
                p.MoveTo(new Vector2(a.x + 6, a.y)); p.LineTo(new Vector2(a.x + 6, 0));
                p.MoveTo(new Vector2(c.x - 6, a.y)); p.LineTo(new Vector2(c.x - 6, 0));
                p.Stroke();
            }
        }

        // ───────── 위험 구역(타격이 닿을 자리) ─────────
        void Zones(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            float us = u * FightUnit.Sf;      // 그림 크기(예전 U 기준 모양 그대로)
            var e = w.Foe;
            var T = w.D.T;
            if (e.Disabled || e.KO) return;
            var pat = e.Cur;
            // 카운터(자세 · 연타 대응 · 반격 상태): 카운터 공격 범위를 보라로
            if (w.D.NextThreat(out double cl, out _, out bool ctr) && ctr)
            {
                float k = e.InCounter ? 1 : Mathf.Clamp01(1 - (float)(cl / 2));
                var cb = e.CounterBox();
                var a0 = S(w, cb.X0, cb.Y1, W, H, sh);
                var c0 = S(w, cb.X1, cb.Y0, W, H, sh);
                p.fillColor = StageLook.A(StageLook.Stance, 0.05f + 0.15f * k);
                p.BeginPath(); Rect(p, a0.x, a0.y, c0.x - a0.x, c0.y - a0.y); p.Fill();
                p.strokeColor = StageLook.A(StageLook.Stance, 0.3f + 0.6f * k);
                p.lineWidth = e.InCounter ? 3 : 1.5f;
                p.BeginPath(); Rect(p, a0.x, a0.y, c0.x - a0.x, c0.y - a0.y); p.Stroke();
            }
            if (pat == null) return;
            if (pat.Counter)
            {
                // 위장 중: 보통 베기와 똑같은 위험 구역(반격 자세 시점을 타격처럼)
                if (ctr || !w.D.Disguised) return;
                var fake = Pattern.BASIC[0].Steps[0];
                double fl = T.CounterStanceBeats - (w.D.BeatOf(w.SongT) - e.CurStart);
                // 위장은 보통 베기처럼 보여야 하므로 그 패턴(BASIC[0])의 예고 · 판정 값을 쓴다
                var fp = Pattern.BASIC[0];
                double show0 = FightTune.Pos(w.D.ZoneShowOf(fp), 0.05);
                if (fl > show0 + 0.05 || fl < 0) return;
                float fk = Mathf.Clamp01(1 - (float)(fl / show0));
                double fr = fake.Reach * w.D.ReachMulOf(fp) + fake.Lunge * w.D.LungeMulOf(fp);
                var fa = S(w, e.Facing > 0 ? e.X : e.X - fr, e.Y + fake.Height, W, H, sh);
                var fc = S(w, e.Facing > 0 ? e.X + fr : e.X, e.Y + w.D.BoxBottomOf(fp), W, H, sh);
                var fcol = StageLook.KindColor(HitKind.Melee);
                p.fillColor = StageLook.A(fcol, Mathf.Clamp01((0.06f + (fl < w.D.ZoneHotOf(fp) ? 0.24f : 0.12f) * fk) * (float)T.ZoneAlphaMul));
                p.BeginPath(); Rect(p, fa.x, fa.y, fc.x - fa.x, fc.y - fa.y); p.Fill();
                p.strokeColor = StageLook.A(fcol, 0.3f + 0.6f * fk);
                p.lineWidth = 1.5f;
                p.BeginPath(); Rect(p, fa.x, fa.y, fc.x - fa.x, fc.y - fa.y); p.Stroke();
                return;
            }
            double rel = w.D.BeatOf(w.SongT) - e.CurStart;
            // 이 패턴의 값(패턴 탭에서 덮어쓰지 않았으면 공통값)
            double rm = w.D.ReachMulOf(pat), lm = w.D.LungeMulOf(pat), bb = w.D.BoxBottomOf(pat);
            double show = FightTune.Pos(w.D.ZoneShowOf(pat), 0.05), hotB = w.D.ZoneHotOf(pat);
            for (int i = 0; i < pat.Steps.Count; i++)
            {
                if (e.DoneSteps.Contains(i)) continue;
                var s = pat.Steps[i];
                double left = s.Beat - rel;
                if (left > show + 0.05 || left < -0.2) continue;
                float k = Mathf.Clamp01(1 - (float)(left / show));
                bool hot = left < hotB;
                var col = StageLook.KindColor(s.Kind);
                double x0, x1, y0, y1;
                if (s.Kind == HitKind.Projectile)
                {
                    // 조준선(탄은 플레이어를 노린다)
                    var from = S(w, e.X + e.Facing * w.D.ShotOriginXOf(pat), e.Y + w.D.ShotOriginYOf(pat), W, H, sh);
                    var to = S(w, w.Hero.X, w.Hero.Y + w.D.ShotAimYOf(pat), W, H, sh);
                    var d = to - from;
                    float len = d.magnitude;
                    if (len < 1) continue;
                    d /= len;
                    p.strokeColor = StageLook.A(col, 0.25f + 0.5f * k);
                    p.lineWidth = hot ? 3 : 2;
                    p.BeginPath();
                    for (float t = 0; t < len; t += 22) { p.MoveTo(from + d * t); p.LineTo(from + d * Mathf.Min(len, t + 11)); }
                    p.Stroke();
                    continue;
                }
                if (pat.Leap)
                {
                    double cx = e.LeapToX, ry = e.LeapToY, rr = s.Reach * rm;
                    x0 = cx - rr; x1 = cx + rr; y0 = ry; y1 = ry + s.Height;
                }
                else if (s.Kind == HitKind.Charge)
                {
                    x0 = e.Facing > 0 ? e.X : e.X - s.Reach; x1 = e.Facing > 0 ? e.X + s.Reach : e.X; y0 = e.Y; y1 = e.Y + s.Height;
                }
                else
                {
                    double reach = s.Reach * rm + s.Lunge * lm;
                    x0 = e.Facing > 0 ? e.X : e.X - reach; x1 = e.Facing > 0 ? e.X + reach : e.X;
                    y0 = e.Y + (s.Kind == HitKind.Low ? 0 : bb); y1 = e.Y + s.Height;
                }
                var a = S(w, x0, y1, W, H, sh);
                var c = S(w, x1, y0, W, H, sh);
                p.fillColor = StageLook.A(col, Mathf.Clamp01((0.06f + (hot ? 0.24f : 0.12f) * k) * (float)T.ZoneAlphaMul));
                p.BeginPath(); Rect(p, a.x, a.y, c.x - a.x, c.y - a.y); p.Fill();
                p.strokeColor = StageLook.A(col, 0.3f + 0.6f * k);
                p.lineWidth = hot ? 3 : 1.5f;
                p.BeginPath(); Rect(p, a.x, a.y, c.x - a.x, c.y - a.y); p.Stroke();
                if (s.Kind == HitKind.Charge)
                {
                    // 돌진 방향 겹화살
                    float cy = (a.y + c.y) / 2, step = 0.9f * us;
                    p.strokeColor = StageLook.A(col, 0.35f + 0.6f * k);
                    p.lineWidth = 3;
                    p.BeginPath();
                    for (float x = 0; x < c.x - a.x - step * 0.5f; x += step)
                    {
                        float bx = e.Facing > 0 ? a.x + x + step * 0.3f : c.x - x - step * 0.3f;
                        p.MoveTo(new Vector2(bx - e.Facing * 10, cy - 12)); p.LineTo(new Vector2(bx + e.Facing * 4, cy)); p.LineTo(new Vector2(bx - e.Facing * 10, cy + 12));
                    }
                    p.Stroke();
                }
            }
        }

        // ───────── 맵 패턴(전역 공격) ─────────
        static readonly Color Haz = new Color(1f, 0.48f, 0.18f), HazCore = new Color(1f, 0.92f, 0.75f);

        void Hazards(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            float us = u * FightUnit.Sf;      // 그림 크기(예전 U 기준 모양 그대로)
            double now = w.SongT;
            float gy = S(w, 0, 0, W, H, sh).y;
            foreach (var h in w.D.Hazards)
            {
                switch (h.Kind)
                {
                    case MapKind.Missiles:
                    {
                        var col = StageLook.KindColor(HitKind.Projectile);
                        var c = S(w, h.X, h.Y, W, H, sh);
                        var tail = S(w, h.X - h.Vx * 0.08, h.Y - h.Vy * 0.08, W, H, sh);
                        p.lineCap = LineCap.Round;
                        p.strokeColor = StageLook.A(col, 0.45f);
                        p.lineWidth = 0.3f * us;
                        p.BeginPath(); p.MoveTo(tail); p.LineTo(c); p.Stroke();
                        // 마름모 탄두(일반 탄과 구분) — 유도 중엔 둘레 고리
                        var d = new Vector2((float)h.Vx, -(float)h.Vy).normalized * 0.42f * us;
                        var n = new Vector2(-d.y, d.x) * 0.55f;
                        p.fillColor = Color.white;
                        p.BeginPath(); p.MoveTo(c + d); p.LineTo(c + n); p.LineTo(c - d * 0.6f); p.LineTo(c - n); p.ClosePath(); p.Fill();
                        p.strokeColor = col; p.lineWidth = 2;
                        p.BeginPath(); p.MoveTo(c + d); p.LineTo(c + n); p.LineTo(c - d * 0.6f); p.LineTo(c - n); p.ClosePath(); p.Stroke();
                        if (h.HomeT > 0) { p.strokeColor = StageLook.A(col, 0.5f); p.lineWidth = 1.5f; p.BeginPath(); p.Arc(c, 0.6f * us, A0, A360); p.Stroke(); }
                        break;
                    }
                    case MapKind.Columns:
                    {
                        var a = S(w, h.X - h.Half, 0, W, H, sh);
                        var b = S(w, h.X + h.Half, 0, W, H, sh);
                        if (b.x < -20 || a.x > W + 20) break;
                        float wpx = b.x - a.x;
                        if (now < h.ArmT)
                        {
                            float k = Mathf.Clamp01((float)((now - h.SpawnT) / Math.Max(0.01, h.ArmT - h.SpawnT)));
                            p.fillColor = StageLook.A(Haz, 0.05f + 0.2f * k);
                            p.BeginPath(); Rect(p, a.x, 0, wpx, gy); p.Fill();
                            p.strokeColor = StageLook.A(Haz, 0.35f + 0.6f * k);
                            p.lineWidth = k > 0.75f ? 3 : 1.5f;
                            p.BeginPath(); p.MoveTo(new Vector2(a.x, 0)); p.LineTo(new Vector2(a.x, gy)); p.MoveTo(new Vector2(b.x, 0)); p.LineTo(new Vector2(b.x, gy)); p.Stroke();
                            // 바닥 표식: 안쪽으로 닫혀 오는 막대(닿는 순간 = 타격)
                            float inner = wpx * 0.5f * (1 - k);
                            p.strokeColor = StageLook.A(HazCore, 0.6f + 0.4f * k);
                            p.lineWidth = 4;
                            p.BeginPath(); p.MoveTo(new Vector2(a.x + wpx * 0.5f - inner - 6, gy - 4)); p.LineTo(new Vector2(a.x + wpx * 0.5f + inner + 6, gy - 4)); p.Stroke();
                        }
                        else
                        {
                            float k = Mathf.Clamp01((float)((now - h.ArmT) / Math.Max(0.01, h.EndT + 0.25 - h.ArmT)));
                            p.fillColor = StageLook.A(Haz, 0.75f * (1 - k));
                            p.BeginPath(); Rect(p, a.x - 6, 0, wpx + 12, gy); p.Fill();
                            p.fillColor = StageLook.A(HazCore, 0.95f * (1 - k));
                            p.BeginPath(); Rect(p, a.x + wpx * 0.25f, 0, wpx * 0.5f, gy); p.Fill();
                        }
                        break;
                    }
                    case MapKind.Lasers:
                    {
                        var top = S(w, 0, h.Y + h.HalfH, W, H, sh).y;
                        var bot = S(w, 0, h.Y - h.HalfH, W, H, sh).y;
                        var l = S(w, -w.D.Map.HalfW, 0, W, H, sh).x;
                        var r = S(w, w.D.Map.HalfW, 0, W, H, sh).x;
                        l = Mathf.Max(0, l); r = Mathf.Min(W, r);
                        if (now < h.ArmT)
                        {
                            float k = Mathf.Clamp01((float)((now - h.SpawnT) / Math.Max(0.01, h.ArmT - h.SpawnT)));
                            p.fillColor = StageLook.A(Haz, 0.05f + 0.18f * k);
                            p.BeginPath(); Rect(p, l, top, r - l, bot - top); p.Fill();
                            p.strokeColor = StageLook.A(Haz, 0.35f + 0.6f * k);
                            p.lineWidth = k > 0.75f ? 2.5f : 1.5f;
                            p.BeginPath();
                            for (float x = l; x < r; x += 26) { p.MoveTo(new Vector2(x, top)); p.LineTo(new Vector2(Mathf.Min(r, x + 14), top)); p.MoveTo(new Vector2(x, bot)); p.LineTo(new Vector2(Mathf.Min(r, x + 14), bot)); }
                            p.Stroke();
                        }
                        else
                        {
                            float k = Mathf.Clamp01((float)((now - h.ArmT) / Math.Max(0.01, h.EndT + 0.25 - h.ArmT)));
                            p.fillColor = StageLook.A(Haz, 0.8f * (1 - k));
                            p.BeginPath(); Rect(p, l, top - 4, r - l, bot - top + 8); p.Fill();
                            p.fillColor = StageLook.A(HazCore, 0.95f * (1 - k));
                            float mid = (top + bot) / 2, hh = (bot - top) * 0.22f;
                            p.BeginPath(); Rect(p, l, mid - hh, r - l, hh * 2); p.Fill();
                        }
                        break;
                    }
                    case MapKind.Walls:
                    {
                        var c = S(w, h.X, 0, W, H, sh);
                        float tp = S(w, h.X, h.HalfH, W, H, sh).y;
                        float half = (float)h.Half * u;
                        if (now < h.ArmT)
                        {
                            // 예고: 들어오는 쪽 가장자리에 높이 표시 + 화살표(화면 밖이면 화면 끝에)
                            float k = Mathf.Clamp01((float)((now - h.SpawnT) / Math.Max(0.01, h.ArmT - h.SpawnT)));
                            float ex = Mathf.Clamp(c.x, 24, W - 24);
                            p.fillColor = StageLook.A(Haz, 0.1f + 0.3f * k);
                            p.BeginPath(); Rect(p, ex - half, tp, half * 2, gy - tp); p.Fill();
                            p.strokeColor = StageLook.A(HazCore, 0.5f + 0.5f * k);
                            p.lineWidth = 3;
                            float ay = tp - 18;
                            p.BeginPath(); p.MoveTo(new Vector2(ex - h.Dir * 12, ay - 10)); p.LineTo(new Vector2(ex + h.Dir * 6, ay)); p.LineTo(new Vector2(ex - h.Dir * 12, ay + 10)); p.Stroke();
                        }
                        else
                        {
                            if (c.x < -40 || c.x > W + 40) break;
                            p.fillColor = StageLook.A(Haz, 0.55f);
                            p.BeginPath(); Rect(p, c.x - half * 2.2f, tp - 6, half * 4.4f, gy - tp + 6); p.Fill();
                            p.fillColor = HazCore;
                            p.BeginPath(); Rect(p, c.x - half, tp, half * 2, gy - tp); p.Fill();
                        }
                        break;
                    }
                }
            }
        }

        // ───────── 탄 ─────────
        void DrawShots(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh)
        {
            float us = u * FightUnit.Sf;      // 그림 크기(예전 U 기준 모양 그대로)
            var col = StageLook.KindColor(HitKind.Projectile);
            foreach (var s in w.D.Shots)
            {
                var c = S(w, s.X, s.Y, W, H, sh);
                var tail = S(w, s.X - s.Vx * 0.06, s.Y - s.Vy * 0.06, W, H, sh);
                p.strokeColor = StageLook.A(col, 0.4f);
                float rk = Mathf.Max(0.05f, (float)(s.Radius / (0.3 * FightUnit.S)));     // 판정 반지름(패턴별)에 맞춘 크기 — 기준 0.3 도 지금 단위로
                p.lineWidth = 0.35f * us * rk;
                p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(tail); p.LineTo(c); p.Stroke();
                p.fillColor = StageLook.A(col, 0.35f);
                p.BeginPath(); p.Arc(c, 0.42f * us * rk, A0, A360); p.Fill();
                p.fillColor = Color.white;
                p.BeginPath(); p.Arc(c, 0.2f * us * rk, A0, A360); p.Fill();
            }
        }

        // ───────── 플레이어 ─────────
        /// <summary>layer 0 = 몸(그림이 없을 때) · 그림자 · 대쉬 잔상, layer 1 = 그림 위에 얹는 방패 · 별 · 휘두름(그림이 있을 때).</summary>
        void DrawHero(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh, int layer)
        {
            float us = u * FightUnit.Sf;      // 그림 크기(예전 U 기준 모양 그대로)
            var h = w.Hero;
            float sc = us / 100f * 1.55f;
            if (layer == 0) foreach (var g in w.Ghosts)
            {
                var gp = S(w, g.x, g.y, W, H, sh);
                Humanoid(p, gp, sc, g.facing, 0.6f, 0.5f, 0, 0, StageLook.A(StageLook.ACT[Act.DASH], g.a * 0.5f), new Color(0, 0, 0, 0), 0);
            }
            var at = S(w, h.X, h.Y, W, H, sh);
            float face = h.Facing;
            bool hurt = w.HeroHurtT < 0.3f || h.HurtT > 0;
            var line = hurt && Mathf.Repeat(w.HeroHurtT * 20, 1) < 0.5f ? StageLook.Hurt : StageLook.Hero;
            var st = h.State;
            bool down = st == PState.Stun || st == PState.Knockback || st == PState.WallBounce;
            float lean = st == PState.Dash ? 0.5f : down ? -0.6f : hurt ? -0.25f : st == PState.Attack ? 0.2f : st == PState.Ready ? 0.15f : 0;
            float run = st == PState.Move ? Mathf.Sin((float)w.SongT * 14) : Mathf.Sin((float)w.SongT * 2.2f) * 0.08f;
            float tuck = h.Grounded ? (st == PState.Ready ? 0.2f : 0) : 0.8f;
            float arm = st == PState.Ready ? -0.4f : 0.2f;
            if (st == PState.Attack) arm = h.ComboIndex % 2 == 1 ? -1.4f : 1.0f;
            if (st == PState.Counter) arm = -0.3f;
            if (down) arm = 1.2f;
            // 그림자(발 아래 면)
            double sup = w.D.Map.SupportBelow(h.X, h.Y + 0.01, PlayerFsm.HALF);
            var shp = S(w, h.X, sup, W, H, sh);
            float air = Mathf.Min(0.6f, (float)((h.Y - sup) / (4 * FightUnit.S)));
            if (layer == 0)
            {
                p.fillColor = new Color(0, 0, 0, 0.45f);
                p.BeginPath(); Ellipse(p, shp + new Vector2(0, 2), 0.42f * us * (1 - air), 0.09f * us); p.Fill();
                if (HeroTex == null) Humanoid(p, at, sc, face, run, lean, tuck, arm, line, StageLook.HeroFill, 1);
            }
            if (layer != (HeroTex != null ? 1 : 0)) return;
            var chest = at + new Vector2(0, -78 * sc);
            // 카운터 반격 상태: 자홍 방패(둘레 전체 — 카운터는 사방을 받는다)
            if (h.InCounter)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin((float)w.SongT * 40);
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = StageLook.A(pass == 0 ? StageLook.ACT[Act.CTR] : Color.white, pass == 0 ? 0.5f * pulse : 0.9f);
                    p.lineWidth = pass == 0 ? 12 : 2.5f;
                    p.BeginPath(); p.Arc(chest + new Vector2(0, 8 * sc), 1.1f * us, A0, A360); p.Stroke();
                }
                Shield(p, chest + new Vector2(face * 40 * sc, 0), new Vector2(face, 0), StageLook.ACT[Act.CTR], 0.9f, 52 * sc);
            }
            if (down) Stars(p, at + new Vector2(0, -130 * sc), sc, (float)w.SongT, StageLook.Hurt);
            // 동작 잔상(검기 · 기계 무기 · 카운터 공격)
            foreach (var fx in w.Swings)
            {
                float k = fx.age / fx.dur;
                if (k >= 1) continue;
                var c = S(w, fx.x, fx.y, W, H, sh);
                var dir = new Vector2(fx.dx, -fx.dy);
                if (fx.hasBox && ShowAtkRange)
                {
                    // 실제 판정 범위(잠깐 보임 — 거리 감각 잡기용)
                    var ba = S(w, fx.box.X0, fx.box.Y1, W, H, sh);
                    var bc = S(w, fx.box.X1, fx.box.Y0, W, H, sh);
                    var bcol = fx.kind == 2 ? StageLook.ACT[Act.CTR] : StageLook.ACT[Act.ATK];
                    p.fillColor = StageLook.A(bcol, 0.10f * (1 - k));
                    p.BeginPath(); Rect(p, ba.x, ba.y, bc.x - ba.x, bc.y - ba.y); p.Fill();
                    p.strokeColor = StageLook.A(bcol, 0.45f * (1 - k));
                    p.lineWidth = 1.5f;
                    p.BeginPath(); Rect(p, ba.x, ba.y, bc.x - ba.x, bc.y - ba.y); p.Stroke();
                }
                if (!ShowSwing) continue;
                if (fx.kind == 0) Slash(p, c, dir, StageLook.ACT[Act.ATK], 1 - k, (70 + 30 * k + 14 * fx.combo) * sc);
                else if (fx.kind == 1) { p.strokeColor = StageLook.A(new Color(0.9f, 0.95f, 1f), 1 - k); p.lineWidth = 5; p.BeginPath(); p.MoveTo(c - dir * 20 * sc); p.LineTo(c + dir * (60 + 20 * fx.combo) * sc); p.Stroke(); }
                else if (fx.kind == 3)
                {
                    // 동료 사격 광선: 총구 → 쏜 곳(강한 사격은 굵게 · 맞으면 끝에 고리)
                    var c2 = S(w, fx.x2, fx.y2, W, H, sh);
                    bool big = fx.combo >= 3;
                    var mc = new Color(0.62f, 0.88f, 1f);
                    p.strokeColor = StageLook.A(mc, (big ? 0.55f : 0.45f) * (1 - k)); p.lineWidth = big ? 16 * (1 - 0.5f * k) : 7;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c2); p.Stroke();
                    p.strokeColor = StageLook.A(Color.white, 1 - k); p.lineWidth = big ? 5 : 2.5f;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c2); p.Stroke();
                    if (fx.hit)
                    {
                        p.strokeColor = StageLook.A(big ? Color.white : mc, 1 - k); p.lineWidth = big ? 5 : 3;
                        p.BeginPath(); p.Arc(c2, (big ? 0.3f + 1.4f * k : 0.15f + 0.6f * k) * us, A0, A360); p.Stroke();
                    }
                }
                else { p.strokeColor = StageLook.A(StageLook.ACT[Act.CTR], 1 - k); p.lineWidth = 6; p.BeginPath(); p.Arc(c, (0.4f + 2.0f * k) * us, A0, A360); p.Stroke(); }
            }
        }

        static void Stars(Painter2D p, Vector2 hc, float sc, float t, Color c)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = t * 6 + i * 2.09f;
                var sp = hc + new Vector2(Mathf.Cos(a) * 22 * sc, Mathf.Sin(a) * 6 * sc);
                p.fillColor = c;
                p.BeginPath(); p.Arc(sp, 4 * sc, A0, A360); p.Fill();
            }
        }

        // ───────── 결투자 ─────────
        void DrawFoe(Painter2D p, StageWorld w, float W, float H, float u, Vector2 sh, int layer)
        {
            float us = u * FightUnit.Sf;      // 그림 크기(예전 U 기준 모양 그대로)
            var f = w.Foe;
            var d = w.D;
            bool rage = d.Phase == 2;
            float sc = us / 100f * (rage ? 1.9f : 1.75f);
            var at = S(w, f.X, f.Y, W, H, sh);
            float face = f.Facing;
            bool threat = d.NextThreat(out double left, out HitKind kind, out bool stance);
            Color tc = stance ? StageLook.Stance : StageLook.KindColor(kind);
            float telB = (float)FightTune.Pos(d.TelegraphOf(f.Cur), 0.05), tsz = Mathf.Max(0.1f, (float)d.T.TelegraphSize);
            float wind = threat ? Mathf.Clamp01(1 - (float)left / telB) : 0;
            if (f.InCounter) { tc = StageLook.Stance; wind = 1; }
            // 정체가 드러남(카운터 패턴 · 아직 자세 전): 베는 척을 멈추고 방패를 든다 — 색은 통째로 보라
            bool reveal = threat && stance && !f.InCounter && ((f.Cur != null && f.Cur.Counter) || d.CounterPrepping);
            if (reveal) { tc = StageLook.Stance; wind = 1; }
            Color baseC = rage ? StageLook.Boss : StageLook.Foe;
            Color line = Color.Lerp(baseC, tc, Mathf.Clamp01(wind * 1.6f));
            if (w.FoeHitT < 0.1f) line = Color.white;
            bool dis = f.Disabled && !f.KO;
            if (dis) line = Color.Lerp(baseC, new Color(0.7f, 0.7f, 0.8f), 0.5f + 0.5f * Mathf.Sin((float)w.SongT * 30));
            float alpha = f.KO ? 0.5f : 1;
            line = StageLook.A(line, alpha);
            var fill = StageLook.A(StageLook.FoeFill, alpha);
            // 그림자
            double sup = d.Map.SupportBelow(f.X, f.Y + 0.01, Duelist.HALF);
            var shp = S(w, f.X, sup, W, H, sh);
            if (layer == 0) p.fillColor = new Color(0, 0, 0, 0.45f);
            if (layer == 0) { p.BeginPath(); Ellipse(p, shp + new Vector2(0, 2), 0.55f * us * (1 - Mathf.Min(0.6f, (float)((f.Y - sup) / (4 * FightUnit.S)))), 0.1f * us); p.Fill(); }
            if (wind > 0 && layer == 0)
            {
                p.fillColor = StageLook.A(tc, 0.10f + 0.22f * wind);
                p.BeginPath(); Ellipse(p, at + new Vector2(0, -1.0f * us), 1.2f * us * (0.8f + 0.3f * wind), 1.4f * us * (0.8f + 0.3f * wind)); p.Fill();
            }
            float arm = reveal ? 1.0f : wind > 0 ? -1.3f + wind * 2.2f : 0.6f;     // 드러나면 칼을 내린다
            float lean = f.ChargeLeft > 0 ? 0.8f : kind == HitKind.Charge && wind > 0 ? 0.6f * wind : 0.3f;
            float tuck = !f.Grounded ? 0.6f : kind == HitKind.Low && wind > 0 ? 0.5f * wind : 0;
            if (dis) { arm = 1.2f; lean = -0.4f; }
            if (f.KO) { arm = 1.4f; lean = -1.2f; tuck = 0.9f; }
            float run = Math.Abs(f.Vx) > 0.1 || (!dis && f.Cur == null) ? Mathf.Sin((float)w.SongT * 7) * 0.4f : 0;
            if (layer == 0 && FoeTex == null) Humanoid(p, at, sc, face, run, lean, tuck, arm, line, fill, 3);
            if (layer != (FoeTex != null ? 1 : 0)) return;
            if (rage) Crown(p, at, sc, line);
            if (f.Invuln)
            {
                // 무적 보호막(맵 패턴 중): 금빛 고리 두 겹이 맥동
                float pulse = 0.6f + 0.4f * Mathf.Sin((float)w.SongT * 6);
                var cc = at + new Vector2(0, -90 * sc);
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = pass == 0 ? StageLook.A(new Color(1f, 0.85f, 0.4f), 0.35f * pulse) : StageLook.A(Color.white, 0.8f);
                    p.lineWidth = pass == 0 ? 16 : 2.5f;
                    p.BeginPath(); p.Arc(cc, 1.5f * us, A0, A360); p.Stroke();
                }
            }
            var chest = at + new Vector2(0, -78 * sc);
            // 반격 자세: 보라 방패 + 둘레가 맥동(때리면 반격당함)
            if (f.InCounter)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin((float)w.SongT * 18);
                for (int pass = 0; pass < 2; pass++)
                {
                    p.strokeColor = StageLook.A(pass == 0 ? StageLook.Stance : Color.white, pass == 0 ? 0.55f * pulse : 0.85f);
                    p.lineWidth = pass == 0 ? 14 : 3;
                    p.BeginPath(); p.Arc(chest + new Vector2(0, 10 * sc), 1.3f * us, A0, A360); p.Stroke();
                }
                Shield(p, chest + new Vector2(face * 46 * sc, 0), new Vector2(face, 0), StageLook.Stance, 1, 60 * sc);
            }
            if (reveal)
            {
                // 방패를 들어 올림 + 빠르게 깜빡이는 흰 테두리(자세까지 남은 시간 동안)
                float blink = 0.5f + 0.5f * Mathf.Sin((float)w.SongT * 28);
                p.strokeColor = StageLook.A(Color.white, 0.35f + 0.5f * blink);
                p.lineWidth = 4;
                p.BeginPath(); p.Arc(chest + new Vector2(0, 10 * sc), 1.25f * us, A0, A360); p.Stroke();
                Shield(p, chest + new Vector2(face * 46 * sc, -10 * sc), new Vector2(face, 0), StageLook.Stance, 0.7f + 0.3f * blink, 66 * sc);
            }
            if (dis) Stars(p, at + new Vector2(0, -132 * sc), sc, (float)w.SongT, new Color(1f, 0.9f, 0.4f));
            // 예고: 머리 위 표식 + 박 점(●○) + 박마다 줄어드는 원
            if (threat && !dis)
            {
                var top = at + new Vector2(0, -170 * sc - 40 * (tsz - 1));
                Glyph(p, top, kind, stance, tc, 0.6f + 0.4f * wind, tsz);
                int total = Mathf.Clamp(Mathf.CeilToInt((float)left - 1e-4f), 2, 8);   // 예고가 길면(카운터 자세 예고 등) 점도 늘어난다
                int lit = Mathf.Clamp(total - Mathf.CeilToInt((float)left - 1e-4f), 0, total);
                for (int i = 0; i < total; i++)
                {
                    bool on = lit > i;
                    var dp = top + new Vector2((i - (total - 1) * 0.5f) * 18 * tsz, -36 * tsz);
                    p.fillColor = on ? tc : StageLook.A(tc, 0.25f);
                    p.BeginPath(); p.Arc(dp, (on ? 6 : 5) * tsz, A0, A360); p.Fill();
                }
                float k = Mathf.Clamp01((float)left / Mathf.Max(total, telB));
                float rr = (26 + 84 * k) * tsz;
                p.strokeColor = StageLook.A(tc, 0.4f + 0.6f * wind);
                p.lineWidth = 2 + 3 * wind;
                p.BeginPath(); p.Arc(top, rr, A0, A360); p.Stroke();
                p.strokeColor = StageLook.A(Color.white, 0.35f + 0.5f * wind);
                p.lineWidth = 2f;
                p.BeginPath(); p.Arc(top, 26 * tsz, A0, A360); p.Stroke();
            }
            // 내려찍기: 떨어질 자리 표시
            if (f.Cur != null && f.Cur.Leap && !f.Grounded && !dis)
            {
                var lp = S(w, f.LeapToX, f.LeapToY, W, H, sh);
                p.strokeColor = StageLook.A(StageLook.KindColor(HitKind.Melee), 0.8f);
                p.lineWidth = 3;
                p.BeginPath(); Ellipse(p, lp, 0.9f * us, 0.18f * us); p.Stroke();
            }
            // 결투자 반격 횟수(발밑 마름모: 채움 = 쓸 수 있음)
            int caps = d.T.EnemyCtrCapI;
            for (int i = 0; i < caps; i++)
            {
                var dp = S(w, f.X + (i - (caps - 1) * 0.5) * 0.45 * FightUnit.S, sup - 0.35 * FightUnit.S, W, H, sh);
                bool on = f.CtrStack >= i + 1;
                p.fillColor = on ? StageLook.A(StageLook.Stance, f.CtrCd > 0 ? 0.45f : 0.9f) : StageLook.A(StageLook.Stance, 0.15f);
                p.BeginPath();
                float ds = Mathf.Max(0.2f, H / 900f);     // 화면 크기에 맞춰
                p.MoveTo(dp + new Vector2(0, -6) * ds); p.LineTo(dp + new Vector2(5, 0) * ds); p.LineTo(dp + new Vector2(0, 6) * ds); p.LineTo(dp + new Vector2(-5, 0) * ds); p.ClosePath();
                p.Fill();
            }
        }

        /// <summary>결투자 타격 종류 표식: 근접 = 마름모(카운터) · 낮은 공격 = 물결(점프) · 탄 = 점 셋(무빙) · 돌진 = 겹화살(대쉬) · 반격 자세 = ✕(때리지 말기).</summary>
        static void Glyph(Painter2D p, Vector2 c, HitKind kind, bool stance, Color col, float a, float sz = 1)
        {
            p.strokeColor = StageLook.A(col, a);
            p.fillColor = StageLook.A(col, a * 0.3f);
            p.lineWidth = 4 * Mathf.Max(0.6f, sz);
            p.lineCap = LineCap.Round;
            p.BeginPath();
            if (stance)
            {
                p.MoveTo(c + sz * new Vector2(-12, -12)); p.LineTo(c + sz * new Vector2(12, 12));
                p.MoveTo(c + sz * new Vector2(12, -12)); p.LineTo(c + sz * new Vector2(-12, 12));
                p.Stroke();
                return;
            }
            switch (kind)
            {
                case HitKind.Melee: p.MoveTo(c + sz * new Vector2(0, -16)); p.LineTo(c + sz * new Vector2(14, 0)); p.LineTo(c + sz * new Vector2(0, 16)); p.LineTo(c + sz * new Vector2(-14, 0)); p.ClosePath(); p.Fill(); break;
                case HitKind.Charge: p.MoveTo(c + sz * new Vector2(-16, -12)); p.LineTo(c + sz * new Vector2(-4, 0)); p.LineTo(c + sz * new Vector2(-16, 12)); p.MoveTo(c + sz * new Vector2(0, -12)); p.LineTo(c + sz * new Vector2(12, 0)); p.LineTo(c + sz * new Vector2(0, 12)); break;
                case HitKind.Projectile:
                    p.Stroke();
                    p.fillColor = StageLook.A(col, a);
                    for (int i = -1; i <= 1; i++) { p.BeginPath(); p.Arc(c + sz * new Vector2(i * 12, 0), 5 * sz, A0, A360); p.Fill(); }
                    return;
                default:
                    p.MoveTo(c + sz * new Vector2(-18, 4));
                    for (int i = 1; i <= 6; i++) p.LineTo(c + sz * new Vector2(-18 + i * 6, i % 2 == 0 ? 4 : -6));
                    break;
            }
            p.Stroke();
        }

        // ───────── 화면 밖 결투자 · 박자 ─────────
        void EdgeMark(Painter2D p, StageWorld w, float W, float H)
        {
            var at = w.ToScreen(w.Foe.X, w.Foe.Y + 1, W, H);
            if (at.x >= 0 && at.x <= W) return;
            int sd = at.x < 0 ? -1 : 1;
            float x = sd < 0 ? 18 : W - 18, y = Mathf.Clamp(at.y, 60, H - 60);
            p.fillColor = StageLook.Foe;
            p.BeginPath();
            p.MoveTo(new Vector2(x + sd * 10, y)); p.LineTo(new Vector2(x - sd * 8, y - 14)); p.LineTo(new Vector2(x - sd * 8, y + 14)); p.ClosePath();
            p.Fill();
        }

        /// <summary>화면 아래 가운데: 마디 박 점. 지금 박이 켜지고, 박이 오는 순간 크게 — 박에 맞춘 공격 = 추가 데미지.</summary>
        void Metronome(Painter2D p, StageWorld w, float W, float H)
        {
            if (w.SongT < w.D.Offset) return;
            int n = Math.Max(1, w.D.BeatsPerBar);
            float s = Mathf.Max(0.2f, H / 900f);       // 화면 크기에 맞춰(게임 화면 높이 900 기준) — 작은 미리보기에서 점이 커 보이지 않게
            float gap = 26 * s, y = H - 78 * s;
            float pulse = w.BeatPulse * w.BeatPulse * w.BeatPulse;
            for (int i = 0; i < n; i++)
            {
                var c = new Vector2(W / 2 + (i - (n - 1) * 0.5f) * gap, y);
                bool cur = i == ((w.BeatInBar % n) + n) % n;
                float r = (cur ? 6 + 4 * pulse : 4) * s;
                var col = i == 0 ? new Color(1f, 0.82f, 0.3f) : new Color(0.75f, 0.85f, 1f);
                p.fillColor = StageLook.A(col, cur ? 0.6f + 0.4f * pulse : 0.25f);
                p.BeginPath(); p.Arc(c, r, A0, A360); p.Fill();
            }
        }

        // ───────── 모양들 ─────────
        /// <summary>사람 모양(머리 · 몸통 · 팔 · 다리). run = 다리 흔들기, lean = 앞으로 기울기, tuck = 웅크림, arm = 팔 각도(−위 · +아래).</summary>
        static void Humanoid(Painter2D p, Vector2 foot, float s, float face, float run, float lean, float tuck, float arm, Color line, Color fill, int style)
        {
            float leg = 40 * s, torso = 42 * s, head = 13 * s;
            var hip = foot + new Vector2(lean * 14 * s * face, -leg * (1 - tuck * 0.35f));
            var neck = hip + new Vector2(lean * 22 * s * face, -torso);
            var hc = neck + new Vector2(lean * 6 * s * face, -head - 3 * s);
            var k1 = hip + new Vector2((run * 16 + tuck * 14) * s * face, leg * 0.5f * (1 - tuck * 0.4f));
            var k2 = hip + new Vector2((-run * 16 + tuck * 10) * s * face, leg * 0.5f * (1 - tuck * 0.4f));
            var f1 = k1 + new Vector2((run * 10 - tuck * 14) * s * face, leg * 0.5f * (1 - tuck));
            var f2 = k2 + new Vector2((-run * 10 - tuck * 18) * s * face, leg * 0.5f * (1 - tuck));
            var sh = neck + new Vector2(0, 6 * s);
            var hand = sh + new Vector2(Mathf.Cos(arm) * 34 * s * face, Mathf.Sin(arm) * 34 * s);
            var hand2 = sh + new Vector2(-Mathf.Cos(arm * 0.5f + 0.6f) * 24 * s * face, 26 * s);
            // 몸통(채움)
            if (fill.a > 0)
            {
                p.fillColor = fill;
                p.BeginPath();
                p.MoveTo(neck + new Vector2(-10 * s, 0)); p.LineTo(neck + new Vector2(10 * s, 0));
                p.LineTo(hip + new Vector2(8 * s, 0)); p.LineTo(hip + new Vector2(-8 * s, 0)); p.ClosePath();
                p.Fill();
            }
            // 빛나는 테두리(굵고 옅게 → 가늘고 밝게)
            for (int pass = 0; pass < 2; pass++)
            {
                p.strokeColor = pass == 0 ? StageLook.A(line, 0.25f) : line;
                p.lineWidth = (pass == 0 ? 9 : 3.2f) * Mathf.Max(0.8f, s * 0.8f);
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(f1); p.LineTo(k1); p.LineTo(hip); p.LineTo(k2); p.LineTo(f2);
                p.MoveTo(hip); p.LineTo(neck);
                p.MoveTo(sh); p.LineTo(hand);
                p.MoveTo(sh); p.LineTo(hand2);
                p.Stroke();
                p.BeginPath(); p.Arc(hc, head, A0, A360);
                if (pass == 1 && fill.a > 0) { p.fillColor = fill; p.Fill(); }
                p.Stroke();
            }
            // 눈(보는 쪽)
            p.fillColor = style == 1 ? new Color(0.4f, 0.95f, 1f, line.a) : StageLook.A(line, 1);
            p.BeginPath(); p.Arc(hc + new Vector2(5 * s * face, -1 * s), 2.6f * s, A0, A360); p.Fill();
            // 플레이어는 칼
            if (style == 1)
            {
                p.strokeColor = StageLook.A(new Color(0.75f, 0.95f, 1f), line.a);
                p.lineWidth = 3;
                p.BeginPath();
                p.MoveTo(hand); p.LineTo(hand + (hand - sh).normalized * 40 * s);
                p.Stroke();
            }
            // 졸개는 발톱
            if (style == 3)
            {
                p.strokeColor = line; p.lineWidth = 2.5f;
                var dir = (hand - sh).normalized;
                var n = new Vector2(-dir.y, dir.x);
                p.BeginPath();
                for (int i = -1; i <= 1; i++) { p.MoveTo(hand); p.LineTo(hand + dir * 14 * s + n * i * 6 * s); }
                p.Stroke();
            }
        }

        static void Crown(Painter2D p, Vector2 foot, float s, Color line)
        {
            var top = foot + new Vector2(0, -(40 + 42 + 26 + 10) * s);
            p.strokeColor = line; p.lineWidth = 3;
            p.BeginPath();
            p.MoveTo(top + new Vector2(-16 * s, 6 * s));
            for (int i = 0; i < 5; i++) p.LineTo(top + new Vector2((-16 + i * 8) * s, (i % 2 == 0 ? -10 : 2) * s));
            p.LineTo(top + new Vector2(16 * s, 6 * s));
            p.Stroke();
        }

        static void Shape(Painter2D p, Color line, Color fill, float lw, System.Action path)
        {
            p.fillColor = fill;
            p.BeginPath(); path(); p.Fill();
            p.strokeColor = StageLook.A(line, 0.25f); p.lineWidth = lw * 3;
            p.lineJoin = LineJoin.Round;
            p.BeginPath(); path(); p.Stroke();
            p.strokeColor = line; p.lineWidth = lw;
            p.BeginPath(); path(); p.Stroke();
        }

        static void Slash(Painter2D p, Vector2 c, Vector2 dir, Color col, float a, float len)
        {
            var n = new Vector2(-dir.y, dir.x);
            for (int pass = 0; pass < 2; pass++)
            {
                p.strokeColor = StageLook.A(pass == 0 ? col : Color.white, a * (pass == 0 ? 0.5f : 1));
                p.lineWidth = pass == 0 ? 12 : 3;
                p.BeginPath();
                p.MoveTo(c - n * len * 0.6f - dir * 10);
                p.QuadraticCurveTo(c + dir * 26, c + n * len * 0.6f - dir * 10);
                p.Stroke();
            }
        }

        static void Shield(Painter2D p, Vector2 c, Vector2 dir, Color col, float a, float r)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int pass = 0; pass < 2; pass++)
            {
                p.strokeColor = StageLook.A(pass == 0 ? col : Color.white, a * (pass == 0 ? 0.55f : 1));
                p.lineWidth = pass == 0 ? 14 : 3;
                p.BeginPath();
                p.Arc(c - dir * r * 0.4f, r, new Angle(ang - 70, AngleUnit.Degree), new Angle(ang + 70, AngleUnit.Degree));
                p.Stroke();
            }
        }

        static void Wave(Painter2D p, float x, float gy, Color col, float a, int dirn)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                p.strokeColor = StageLook.A(pass == 0 ? col : Color.white, a * (pass == 0 ? 0.45f : 0.9f));
                p.lineWidth = pass == 0 ? 10 : 2.5f;
                p.BeginPath();
                p.MoveTo(new Vector2(x - dirn * 70, gy));
                p.QuadraticCurveTo(new Vector2(x - dirn * 20, gy - 46), new Vector2(x, gy - 18));
                p.QuadraticCurveTo(new Vector2(x + dirn * 10, gy - 4), new Vector2(x + dirn * 24, gy));
                p.Stroke();
            }
        }

        static void Rect(Painter2D p, float x, float y, float w, float h)
        {
            p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath();
        }

        static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            const int N = 20;
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2 / N;
                var pt = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
        }

        static float Ease(float x) => 1 - (1 - x) * (1 - x);
    }
}
