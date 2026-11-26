using System;
using System.Collections.Generic;
using Mori.Core.Fight;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>색 약속. 동작 키 색(ACT) · 결투자 공격 종류 색(KindColor) — 어두운 배경 위에서 서로 구분되도록 채도 · 밝기를 띄웠다.</summary>
    public static class StageLook
    {
        public static readonly Color[] ACT =
        {
            new Color(0.25f, 0.88f, 1f),      // 공격(Ctrl) 청록
            new Color(1f, 0.32f, 0.86f),      // 카운터(Z) 자홍
            new Color(1f, 0.80f, 0.22f),      // 대쉬(Space) 금빛
            new Color(0.58f, 1f, 0.32f),      // 점프(Alt) 연두
        };
        public static readonly Color Foe = new Color(1f, 0.36f, 0.40f);
        public static readonly Color FoeFill = new Color(0.06f, 0.05f, 0.08f);
        public static readonly Color Hero = new Color(0.92f, 0.96f, 1f);
        public static readonly Color HeroFill = new Color(0.10f, 0.12f, 0.17f);
        public static readonly Color Hurt = new Color(1f, 0.25f, 0.3f);
        public static readonly Color Boss = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Stance = new Color(0.56f, 0.52f, 1f);   // 결투자 카운터 자세(푸른 보라) — 근접 예고 자홍과 한눈에 구분되게. 때리면 반격당함

        /// <summary>결투자 공격 종류 색 = 대응 방법: 근접(카운터) 자홍 · 낮은 공격(점프) 연두 · 탄(무빙) 청록 · 돌진(대쉬) 금빛.</summary>
        public static Color KindColor(HitKind k) =>
            k == HitKind.Melee ? ACT[Act.CTR] : k == HitKind.Low ? ACT[Act.JUMP] : k == HitKind.Projectile ? ACT[Act.ATK] : ACT[Act.DASH];

        /// <summary>패턴의 대표 색(첫 타격 종류, 반격 자세면 보라).</summary>
        public static Color ThreatColor(Pattern p)
        {
            if (p == null) return Foe;
            if (p.Counter) return Stance;
            return p.Steps.Count > 0 ? KindColor(p.Steps[0].Kind) : Foe;
        }

        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);
    }

    /// <summary>
    /// 무대 = Core 결투(Duel)를 화면에 옮기는 얇은 껍데기: 카메라 · 히트스톱 · 흔들림 · 잔상. 판정 · 이동 · 데미지는 모두 Duel 이 한다.
    /// 화면 배율은 유니티 표준(명세서 1U = 128px, 1080p 기준 — 화면 높이에 비례). 1920×1080 이면 가로 15U 가 보인다.
    /// 카메라는 플레이어를 따라가며 결투자 쪽을 조금 더 보여 주고(결투자가 화면 밖이면 가장자리 화살표), 결투장 끝 밖은 보이지 않게.
    /// </summary>
    public sealed class StageWorld
    {
        public readonly Duel D;
        public PlayerFsm Hero => D.P;
        public Duelist Foe => D.E;
        public float Shake, HitStop, BeatPulse, BarPulse, HeroHurtT = 9, FoeHitT = 9;
        public double SongT;
        public int BeatInBar;
        public readonly List<(double x, double y, float a, int facing)> Ghosts = new List<(double, double, float, int)>();

        // 카메라(U): 가운데 · 바닥 높이. 화면 크기는 그림 쪽이 알려 준다.
        /// <summary>카메라 = 따라가는 기본 자리 + 연출 줌 중심 보정(줌 동안 그 점이 화면에서 제자리에 있게).</summary>
        public double CamX => camX0 + FxCamDX;
        public double CamY => camY0 + FxCamDY;
        double camX0, camY0;
        public double FxCamDX, FxCamDY;
        /// <summary>연출 카드가 주는 화면 값: 줌(1 = 그대로) · 시간 배율(히트스톱, 1 = 보통). 매 프레임 그림 쪽이 채움.</summary>
        public float FxZoom = 1, FxSlow = 1;
        /// <summary>미리보기 확대(킷 작업실): 카메라가 실제로 다가감(1 = 게임 카메라 넓이). 플레이어를 따라가는 카메라는 그대로.</summary>
        public float ViewZoom = 1;
        public float ScreenW = 1920, ScreenH = 1080;
        public const float PPU = 128, REF_H = 1080;
        public double ViewW => ScreenW / PxU(ScreenW, ScreenH) * Mathf.Clamp(FxZoom, 0.2f, 5f);      // 연출 줌과 상관없는 보이는 넓이(카메라가 줌 때문에 흔들리지 않게)

        /// <summary>연출 줌: 배율 z, at = 줌 중심(게임 단위, 없으면 화면 가운데). 중심점이 화면에서 제자리에 머물게 카메라를 비켜 놓음.</summary>
        public void SetFxZoom(float z, bool hasAt, Vector2 at)
        {
            FxZoom = Mathf.Clamp(z, 0.2f, 5f);
            if (hasAt && Mathf.Abs(FxZoom - 1) > 1e-4f)
            {
                double k = 1 - 1 / FxZoom;
                FxCamDX = (at.x - camX0) * k;
                FxCamDY = Math.Max(-camY0, (at.y - camY0) * k);
            }
            else FxCamDX = FxCamDY = 0;
        }

        /// <summary>휘두름 잔상: 0 마법 검기 · 1 기계 무기 · 2 카운터 공격 · 3 동료 사격.</summary>
        /// <summary>3 = 동료 사격 광선(x · y → x2 · y2, combo 3 = 돌진 한 방).</summary>
        public sealed class Swing { public int kind, combo; public double x, y, x2, y2; public float dx, dy, age, dur = 0.22f; public Box box; public bool hasBox, hit; }
        public readonly List<Swing> Swings = new List<Swing>();

        public StageWorld(Duel d)
        {
            D = d;
            camX0 = TargetCamX();
        }

        /// <summary>sim = false 면 결투는 멈추고 연출만(결과 화면 · 일시정지에서 돌아오는 중).</summary>
        public void Tick(float dt, double songT, int dir, bool sim = true)
        {
            SongT = songT;
            // 히트스톱: 길이 배율 · 그동안의 시간 배율(패널 ‘예고 · 연출’)
            HitStop = Mathf.Max(0, HitStop - dt / Mathf.Max(0.01f, (float)D.T.HitStopMul));
            float wdt = HitStop > 0 && D.T.HitStopMul > 0 ? dt * Mathf.Clamp01((float)D.T.HitStopSlow) : dt;
            wdt *= Mathf.Clamp01(FxSlow);
            Shake *= Mathf.Exp(-dt * 9);
            HeroHurtT += dt;
            FoeHitT += dt;
            double bp = D.BeatOf(songT);
            BeatPulse = 1 - (float)(bp - Math.Floor(bp));
            double bar = bp / D.BeatsPerBar;
            BarPulse = 1 - (float)(bar - Math.Floor(bar));
            BeatInBar = (int)Math.Floor(bp - Math.Floor(bar) * D.BeatsPerBar);
            { double vh = ViewW / 2; D.A.ViewL = CamX - vh; D.A.ViewR = CamX + vh; }     // 동료: 화면 밖으로 안 나가게
            if (sim) D.Tick(wdt, songT, dir);
            if (Hero.Dashing) Ghosts.Add((Hero.X, Hero.Y, 0.55f, Hero.Facing));
            for (int i = Ghosts.Count - 1; i >= 0; i--)
            {
                var g = Ghosts[i];
                g.a -= dt * 3.2f;
                if (g.a <= 0) Ghosts.RemoveAt(i); else Ghosts[i] = g;
            }
            for (int i = Swings.Count - 1; i >= 0; i--) { Swings[i].age += dt; if (Swings[i].age >= Swings[i].dur) Swings.RemoveAt(i); }
            camX0 += (TargetCamX() - camX0) * (1 - Mathf.Exp(-dt * 6f));
            double ty = Math.Max(0, Hero.Y - D.T.CamFollowY);
            camY0 += (ty - camY0) * (1 - Mathf.Exp(-dt * 5f));
        }

        double TargetCamX()
        {
            double view = ViewW, half = view / 2;
            // 플레이어 중심 + 결투자 쪽으로 둘 사이의 절반만큼(플레이어는 화면 가장자리 2.5U 안쪽에 남는다)
            double margin = Math.Min(half * 0.8, 2.5 * FightUnit.S * Math.Max(1, D.T.CamZoom / FightUnit.S * 0.5));
            double look = Math.Max(-(half - margin), Math.Min(half - margin, (Foe.X - Hero.X) * D.T.CamLook));
            double target = Hero.X + look;
            double lim = D.Map.HalfW + 1.0 * FightUnit.S - half;
            return lim <= 0 ? 0 : Math.Max(-lim, Math.Min(lim, target));
        }

        // ───────── 화면 좌표 ─────────
        /// <summary>1U 의 화면 픽셀: 유니티 표준(1080p 에서 128px)을 넓이 배율로 나눈다(배율 4 = 4배 넓게).</summary>
        public float PxU(float w, float h) => PPU * h / REF_H / (float)FightTune.Pos(D.T.CamZoom, 0.05 * FightUnit.S) * Mathf.Clamp(FxZoom, 0.2f, 5f) * Mathf.Clamp(ViewZoom, 0.25f, 8f);
        /// <summary>CamY = 0 일 때 바닥이 놓이는 화면 높이.</summary>
        public float Ground(float h) => h * Mathf.Clamp((float)D.T.CamGround, 0.1f, 1f);
        public Vector2 ToScreen(double x, double y, float w, float h)
        {
            float u = PxU(w, h);
            return new Vector2(w * 0.5f + (float)(x - CamX) * u, Ground(h) - (float)(y - CamY) * u);
        }
    }
}
