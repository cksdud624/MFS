using System;
using System.Collections.Generic;

namespace Mori.Core.Fight
{
    /// <summary>
    /// 길이 단위 바꾸기: 예전 결투 수치는 캐릭터 그림(64px)을 3U 로 보던 기준. 지금은 1U = 그림 100px(MFS 와 같음)이라
    /// 64px 캐릭터 = 0.64U → 모든 길이 · 속도 · 가속도(U 가 든 값)에 S = 0.64 / 3 을 곱해 예전과 같은 감각을 유지한다.
    /// </summary>
    public static class FightUnit
    {
        public const double S = 0.64 / 3.0;
        public const float Sf = (float)S;
    }

    /// <summary>튜닝 패널 표시용: 묶음 · 이름 · 명세서 항목명 · 범위. NextRound = 다음 판부터 적용(체력 등).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TuneAttribute : Attribute
    {
        public readonly string Group, Name, Spec;
        public readonly double Min, Max, Step;
        public bool NextRound;
        public TuneAttribute(string group, string name, string spec, double min, double max, double step)
        {
            Group = group; Name = name; Spec = spec; Min = min; Max = max; Step = step;
        }
    }

    /// <summary>
    /// 전투 수치(단위 U · 초). 기본값 = 명세서(이동 v0.0.2 · 기본 공격 · 카운터 · 넉백 · 월 바운스 · 경직 v0.0.1)의 Prototype Initial Value.
    /// 튜닝 항목 분류 = ‘대분류/탭/묶음’(패널 F2 의 테두리 상자). 명세서 항목은 설명에 ‘명세: 영문명’, 이름 끝 괄호 = 단위(U · 초 · 박 · 배 · 0 ~ 1 · 1 켬 · 0 끔).
    /// </summary>
    public sealed class FightTune
    {
        public int V = 10;                // 2 = 길이 단위를 1U = 그림 100px(64px 캐릭터 기준)으로 줄인 값(FightUnit.S) · 3 = 넓이 배율 기본 1 · 4 = 궁극기 시간 · 게이지 수급 새 기본값 · 5 = 크래시 굿 판정 창 0.4박 · 6 ~ 8 = 동료 값 새 기본 · 9 = 동료 기본 끔(실험) · 10 = 준비 루프 1.5초

        // ── 카메라 ──
        [Tune("화면/카메라/보이는 넓이", "넓이 배율(배)", "1 = 유니티 표준(1U = 128px, 1080p 에서 가로 15U) · 0.5 = 반만 보임(가까이) · 2 = 가로 2배(30U)", 0, 4, 0.05)] public double CamZoom = 1;
        [Tune("화면/카메라/따라가기", "결투자 쪽으로 보기(0 ~ 1)", "0 = 플레이어 가운데 · 1 = 둘 사이 끝까지", 0, 1, 0.05)] public double CamLook = 0.5;
        [Tune("화면/카메라/보이는 넓이", "바닥 화면 높이(비율)", "화면 위에서부터 0 ~ 1", 0.6, 1, 0.05)] public double CamGround = 0.8;
        [Tune("화면/카메라/따라가기", "위로 따라가기 시작 높이(U)", "플레이어가 이보다 높으면 카메라가 올라감", 0, 6 * FightUnit.S, 0.25 * FightUnit.S)] public double CamFollowY = 3 * FightUnit.S;

        // ── 몸 · 결투장 ──
        [Tune("플레이어/몸 · 체력/몸", "몸 폭(± U)", "몸판정 가로 ±", 0.05 * FightUnit.S, 0.65 * FightUnit.S, 0.01 * FightUnit.S, NextRound = true)] public double PlayerHalf = 0.35 * FightUnit.S;
        [Tune("플레이어/몸 · 체력/몸", "몸 높이(U)", "", 0.6 * FightUnit.S, 3 * FightUnit.S, 0.05 * FightUnit.S, NextRound = true)] public double PlayerHeight = 1.8 * FightUnit.S;
        [Tune("결투자/몸 · 체력 · 단계/몸", "몸 폭(± U)", "", 0.05 * FightUnit.S, 0.85 * FightUnit.S, 0.01 * FightUnit.S, NextRound = true)] public double EnemyHalf = 0.45 * FightUnit.S;
        [Tune("결투자/몸 · 체력 · 단계/몸", "몸 높이(U)", "", 0.5 * FightUnit.S, 3.5 * FightUnit.S, 0.05 * FightUnit.S, NextRound = true)] public double EnemyHeight = 2 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/결투장/크기", "반폭(U)", "가운데 → 양 끝 벽", 10 * FightUnit.S, 50 * FightUnit.S, 1 * FightUnit.S, NextRound = true)] public double ArenaHalfW = 30 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/결투장/크기", "천장 높이(U)", "", 5 * FightUnit.S, 17 * FightUnit.S, 0.5 * FightUnit.S, NextRound = true)] public double ArenaCeil = 11 * FightUnit.S;

        // ── 이동 ──
        [Tune("플레이어/이동/달리기", "최고 속도(U/s)", "명세: Move Speed", 0, 8.95 * FightUnit.S, 0.025 * FightUnit.S)] public double MoveSpeed = 4.475 * FightUnit.S;
        [Tune("플레이어/이동/달리기", "가속(U/s²)", "명세: Acceleration", 6 * FightUnit.S, 94 * FightUnit.S, 2 * FightUnit.S)] public double Accel = 50 * FightUnit.S;
        [Tune("플레이어/이동/달리기", "감속(U/s²)", "명세: Deceleration", 5 * FightUnit.S, 65 * FightUnit.S, 2 * FightUnit.S)] public double Decel = 35 * FightUnit.S;
        [Tune("플레이어/이동/방향 전환", "전환 가감속(U/s²)", "명세: Turn Acceleration", 5 * FightUnit.S, 175 * FightUnit.S, 5 * FightUnit.S)] public double TurnAccel = 90 * FightUnit.S;
        [Tune("플레이어/이동/방향 전환", "전환 판정 시간(초)", "반대 방향을 이만큼 누르면 방향 전환 · 명세: Turn 입력 ≥ (s)", 0, 0.2, 0.005)] public double TurnHold = 0.1;
        [Tune("플레이어/이동/준비 자세", "유지 시간(초)", "명세: Ready Duration", 0, 1.2, 0.025)] public double ReadyDuration = 0.6;

        // ── 점프 ──
        [Tune("공통 규칙/중력/중력", "공통 중력(U/s²)", "모든 ‘중력 배율’ 의 기준 · 명세: Gravity", 20 * FightUnit.S, 220 * FightUnit.S, 5 * FightUnit.S)] public double Gravity = 120 * FightUnit.S;
        [Tune("공통 규칙/중력/중력", "최대 낙하 속도(U/s)", "명세: Max Fall Speed", 5 * FightUnit.S, 31 * FightUnit.S, 0.5 * FightUnit.S)] public double MaxFall = 18 * FightUnit.S;
        [Tune("플레이어/점프/점프", "점프 초속(U/s)", "명세: Initial Jump Velocity", 5.5 * FightUnit.S, 43.5 * FightUnit.S, 1 * FightUnit.S)] public double JumpV = 24.5 * FightUnit.S;
        [Tune("플레이어/점프/더블 점프", "가로 속도(U/s)", "명세: Horizontal Velocity", 0, 20 * FightUnit.S, 0.5 * FightUnit.S)] public double DJumpH = 10 * FightUnit.S;
        [Tune("플레이어/점프/더블 점프", "세로 속도(U/s)", "명세: Vertical Velocity", 0, 24 * FightUnit.S, 0.5 * FightUnit.S)] public double DJumpV = 12 * FightUnit.S;
        [Tune("플레이어/점프/더블 점프", "지속 시간(초)", "명세: Duration", 0, 0.36, 0.01)] public double DJumpTime = 0.18;
        [Tune("플레이어/점프/더블 점프", "지속 중 중력 배율(배)", "× 공통 중력", 0, 1, 0.025)] public double DJumpGravityMul = 0.5;
        [Tune("플레이어/점프/체공 느낌", "정점 판정 세로 속도(U/s)", "|세로 속도| 가 이보다 작으면 정점", 0, 6 * FightUnit.S, 0.125 * FightUnit.S)] public double ApexBand = 3 * FightUnit.S;
        [Tune("플레이어/점프/체공 느낌", "정점 중력 배율(배)", "정점에서 잠깐 떠 있는 느낌 · × 공통 중력", 0, 1.8, 0.02)] public double ApexGravityMul = 0.9;
        [Tune("플레이어/점프/체공 느낌", "낙하 중력 배율(배)", "떨어질 때 × 공통 중력(1 = 올라갈 때와 같음)", 0, 0.68, 0.005)] public double FallGravityMul = 0.34;

        // ── 대쉬 ──
        [Tune("플레이어/대쉬/대쉬", "거리(U)", "명세: Dash Distance", 0, 8.2 * FightUnit.S, 0.025 * FightUnit.S)] public double DashDist = 4.1 * FightUnit.S;
        [Tune("플레이어/대쉬/대쉬", "시간(초)", "명세: Dash Duration", 0.036, 0.104, 0.002)] public double DashTime = 0.07;
        [Tune("플레이어/대쉬/충전", "최대 충전 횟수(회)", "명세: Maximum Stack Capacity", 0, 4, 1)] public double DashCap = 2;
        [Tune("플레이어/대쉬/충전", "1회 충전 시간(초)", "명세: Stack Recharge Time", 0, 20, 0.05)] public double DashRecharge = 10;
        [Tune("플레이어/대쉬/충전", "대쉬 뒤 재사용 대기(초)", "대쉬가 끝난 뒤 이 시간 동안은 충전이 남아 있어도 다음 대쉬가 안 나감 · 0 = 바로(연속 대쉬) · 작업실 ‘쿨타임 없음’ 에서도 걸림", 0, 1, 0.025)] public double DashGap = 0;
        [Tune("플레이어/대쉬/대쉬", "대쉬 중 중력 배율(배)", "× 공통 중력 · 명세: 공통 중력 적용 = 1", 0, 0.6, 0.02)] public double DashGravityMul = 0.3;
        [Tune("플레이어/대쉬/대쉬 뒤", "공중 대쉬 뒤 떠 있는 시간(초)", "", 0, 0.4, 0.005)] public double PostDashHang = 0.2;
        [Tune("플레이어/대쉬/대쉬 뒤", "공중 대쉬 뒤 중력 배율(배)", "× 공통 중력", 0, 0.6, 0.02)] public double PostDashGravityMul = 0.3;
        [Tune("플레이어/대쉬/대쉬 뒤", "속도 유지 배율(배)", "× 이동 최고 속도", 0.675, 2.025, 0.025)] public double DashCarry = 1.35;
        [Tune("플레이어/대쉬/대쉬", "무적(1 켬 · 0 끔)", "대쉬하는 동안 맞지 않음", 0, 1, 1)] public double DashInvuln = 1;

        // ── 기본 공격 ──
        [Tune("플레이어/기본 공격/공통", "기본 데미지", "모든 데미지 배율의 기준 · 명세: 기본 데미지(배율의 기준)", 0, 50, 1)] public double BaseDamage = 25;
        [Tune("플레이어/기본 공격/공통", "전진 도약 거리(U)", "명세: 전진 도약 거리", 0, 1 * FightUnit.S, 0.025 * FightUnit.S)] public double AtkStep = 0.5 * FightUnit.S;
        [Tune("플레이어/기본 공격/공통", "위치 고정(초)", "명세: Position Lock", 0.05, 0.45, 0.01)] public double AtkLock = 0.25;
        [Tune("플레이어/기본 공격/타별 위치 고정", "1타 위치 고정(초)", "0 = 공통 ‘위치 고정’ 그대로 · 그림(hero-attack1)도 이 시간 동안 보임(60fps: 초 × 60 = 프레임)", 0, 0.8, 0.01)] public double AtkLock1 = 0;
        [Tune("플레이어/기본 공격/타별 위치 고정", "2타 위치 고정(초)", "0 = 공통 ‘위치 고정’ 그대로 · 그림(hero-attack2)도 이 시간 동안 보임", 0, 0.8, 0.01)] public double AtkLock2 = 0;
        [Tune("플레이어/기본 공격/타별 위치 고정", "3타 위치 고정(초)", "0 = 공통 ‘위치 고정’ 그대로 · 마지막 타(피니시)만 길게 주면 묵직한 여운", 0, 0.8, 0.01)] public double AtkLock3 = 0;

        /// <summary>이 타(1 ~ 3)의 위치 고정 시간: 타별 값이 0 보다 크면 그것, 아니면 공통.</summary>
        // ───────── 궁극기(궤도 폭격): 게이지가 차면 C — 7초 고정 · 무적, 레이저가 결투자를 따라가며 박마다 타격 ─────────
        [Tune("플레이어/궁극기/게이지", "게이지 최대", "게이지가 찰 수 있는 최대치", 10, 500, 5)] public double UltMax = 100;
        [Tune("플레이어/궁극기/게이지", "사용에 필요한 양", "이만큼 있으면 쓸 수 있고, 쓰면 이만큼 줄어듦(최대보다 크면 최대)", 1, 500, 5)] public double UltCost = 100;
        [Tune("플레이어/궁극기/게이지", "시작 게이지", "결투(라운드)를 시작할 때 들어 있는 양", 0, 500, 5)] public double UltStartGauge = 0;
        [Tune("플레이어/궁극기/게이지", "맞을 때 채움", "피격 한 번에(반격당함 포함)", 0, 100, 0.5)] public double UltGainHurt = 5;
        [Tune("플레이어/궁극기/게이지", "기본 공격 적중 채움", "검기 · 기계 공격이 맞을 때마다", 0, 20, 0.5)] public double UltGainHit = 1;
        [Tune("플레이어/궁극기/게이지", "카운터 공격 적중 채움", "카운터를 눌러 나간 공격이 맞을 때마다(성공 · 크래시는 아래 따로)", 0, 20, 0.5)] public double UltGainCtrAtk = 2;
        [Tune("플레이어/궁극기/게이지", "카운터 성공 채움", "카운터 성공 한 번에", 0, 100, 0.5)] public double UltGainCounter = 4;
        [Tune("플레이어/궁극기/게이지", "근접 카운터 추가", "근접(초근접) 카운터면 위에 더함", 0, 100, 0.5)] public double UltGainNear = 2;
        [Tune("플레이어/궁극기/게이지", "카운터 크래시 채움", "크래시 한 번에", 0, 100, 0.5)] public double UltGainCrash = 8;
        [Tune("플레이어/궁극기/시전", "지속(초)", "폭격이 이어지는 전체 시간(컷씬 포함) — 고정 · 무적 시간은 아래에서 따로", 1, 20, 0.25)] public double UltTime = 9;
        [Tune("플레이어/궁극기/시전", "위치 고정(초)", "시작부터 이 시간 동안 그 자리에 완전히 고정(공중이면 공중에) · 지속보다 짧으면 폭격 도중 움직일 수 있음", 0, 20, 0.25)] public double UltLock = 9;
        [Tune("플레이어/궁극기/시전", "무적(초)", "시작부터 이 시간 동안 무적", 0, 20, 0.25)] public double UltInvuln = 9;
        [Tune("플레이어/궁극기/시전", "컷씬(초)", "폭격 요청 → 궤도 전함 → 조준 포착 — 이 동안은 타격 없음(지속 시간 안에 포함)", 0, 10, 0.1)] public double UltCut = 3.5;
        [Tune("플레이어/궁극기/시전", "끝난 뒤 무적(초)", "지속이 끝난 뒤에도 이만큼 더 무적", 0, 3, 0.05)] public double UltAfter = 0.5;
        [Tune("플레이어/궁극기/레이저", "타격 간격(박)", "레이저가 몇 박마다 데미지를 주는지(박자에 맞춰 터짐)", 0.25, 2, 0.25)] public double UltTickBeats = 1;
        [Tune("플레이어/궁극기/레이저", "한 번 데미지", "레이저 한 번 맞을 때", 0, 2000, 10)] public double UltDmg = 180;
        [Tune("플레이어/궁극기/레이저", "박 보너스 · 그루브 적용(1 켬 · 0 끔)", "켜면 레이저 데미지에도 박 보너스 · 그루브 배율이 곱해짐", 0, 1, 1)] public double UltBonus = 0;
        [Tune("플레이어/궁극기/레이저", "레이저 반폭(U)", "가운데 → 가장자리, 결투자 몸이 이 안에 걸치면 맞음", 0.2 * FightUnit.S, 6 * FightUnit.S, 0.1 * FightUnit.S)] public double UltWidth = 1.4 * FightUnit.S;
        [Tune("플레이어/궁극기/레이저", "따라가는 늦음(초)", "레이저가 결투자 위치를 따라가는 느림 — 클수록 늦게 따라감(나중에 결투자가 피할 틈)", 0.02, 2, 0.02)] public double UltFollow = 0.35;
        [Tune("플레이어/궁극기/연출", "주변 폭격 개수", "맵 곳곳에 떨어지는 작은 폭격(연출만 · 데미지 없음) — 박마다 자리가 바뀜", 0, 20, 1)] public double UltScatter = 5;
        [Tune("플레이어/궁극기/연출", "컷씬 어둡기(0 ~ 1)", "조준 컷씬 동안 화면을 얼마나 어둡게", 0, 1, 0.05)] public double UltDim = 0.45;
        [Tune("플레이어/궁극기/연출", "소리 크기(배)", "요청 · 레이저 · 끝 소리(효과음 음량에 한 번 더 곱함)", 0, 2, 0.05)] public double UltSfx = 1;
        // ───────── 동료(기계 공격 담당 여주인공 · 인간형): 땅에 서서 플레이어 뒤를 따라 걷다가 공격 때 기계 공격을 대신 쏨 ─────────
        [Tune("동료(기계)/공통/켬", "동료 켬(1 켬 · 0 끔)", "실험 기능(기본 끔) — 켜면 기계 공격 판정이 동료(저격수)의 사격으로 옮겨 감(데미지 합은 그대로). 끄면 예전처럼 플레이어 판정 그대로. 작업실 미리보기의 ‘저격수’ 버튼과 같은 값", 0, 1, 1)] public double AllyOn = 0;
        [Tune("동료(기계)/엄호 자리/거리", "최대 교전 거리(U)", "적에게서 될 수 있는 한 이만큼 멀리(중장거리 사격수) — 사거리 · 플레이어에서 최대 · 화면 가장자리 중 가까운 한계까지만", 0, 40 * FightUnit.S, 0.25 * FightUnit.S)] public double AllyPrefDist = 18 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/거리", "플레이어보다 최소 뒤(U)", "적과의 거리가 플레이어보다 적어도 이만큼은 멂 — 최전선은 플레이어", 0, 6 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyBehindMin = 1.5 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/거리", "플레이어에서 최대(U)", "멀리 서더라도 플레이어에게서 이보다는 안 멀어짐", 0, 30 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyMaxBehind = 14 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/거리", "화면 가장자리 여유(U)", "화면 끝에서 이만큼 안쪽까지만 — 화면 밖으로 안 나감", 0, 6 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyScreenMargin = 1 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/높은 곳", "높은 곳 선호(배)", "엄호 범위 안의 발판 · 기둥 윗면을 얼마나 좋아하는지 — 클수록 1층(바닥)을 거의 안 밟음 · 0 = 바닥만", 0, 4, 0.05)] public double AllyHighPref = 1.2;
        [Tune("동료(기계)/엄호 자리/높은 곳", "최대 높이(U)", "플레이어가 선 면보다 이보다 높은 곳은 안 올라감(화면 위로 안 나가게)", 0.5 * FightUnit.S, 12 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyPerchMaxH = 6 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/구석 · 고립", "구석 거리(U)", "결투장 벽에서 이 안쪽은 구석 — 가까울수록 피함", 0, 20 * FightUnit.S, 0.25 * FightUnit.S)] public double AllyCornerDist = 6 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/구석 · 고립", "구석 피하기 세기", "클수록 구석을 강하게 피함(벽에 딱 붙은 자리 = 이만큼 감점)", 0, 4, 0.05)] public double AllyCornerW = 1.2;
        [Tune("동료(기계)/엄호 자리/구석 · 고립", "고립 거리(U)", "플레이어에게서 이보다 멀면 고립 — 멀수록 피함", 1 * FightUnit.S, 30 * FightUnit.S, 0.25 * FightUnit.S)] public double AllyIsoDist = 9 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/구석 · 고립", "고립 피하기 세기", "클수록 플레이어 가까이(적 너머 · 혼자 떨어진 자리를 피함)", 0, 4, 0.05)] public double AllyIsoW = 0.8;
        [Tune("동료(기계)/엄호 자리/구석 · 고립", "나쁜 자리 버티는 시간(초)", "구석 · 고립 자리에 이만큼 있으면 더 나은 자리로 옮김(드론 쉬는 시간은 지킴)", 0, 10, 0.1)] public double AllyBadSpotTime = 1.5;
        [Tune("동료(기계)/엄호 자리/자리 지키기", "자리 지키는 범위(U)", "다시 자리를 잡을 때 이 안의 차이는 무시", 0.05 * FightUnit.S, 6 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyHoldBand = 1.5 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/자리 지키기", "반대편으로 도는 시간(초)", "적이 플레이어를 넘어가면 이만큼 지켜본 뒤 반대편으로 돌아 들어감", 0, 2, 0.05)] public double AllySideDelay = 0.35;
        [Tune("동료(기계)/엄호 자리/자리 지키기", "돌아서서 뛰는 거리(U)", "옮길 거리가 이보다 멀면 돌아서서 뜀, 가까우면 적을 보며 뒷걸음 · 옆걸음", 0.2 * FightUnit.S, 20 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyTurnRunDist = 4 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/적이 없을 때", "뒤쪽 거리(U)", "적이 없으면(K.O. 등) 플레이어가 보는 방향의 반대쪽 뒤에 섬", 0, 6 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyBackX = 1.4 * FightUnit.S;
        [Tune("동료(기계)/엄호 자리/자리 지키기", "멈추는 범위(U)", "자리에서 이 안의 작은 차이는 무시하고 서 있음(작은 움직임에 떨지 않게)", 0.02 * FightUnit.S, 3 * FightUnit.S, 0.02 * FightUnit.S)] public double AllyTol = 0.3 * FightUnit.S;
        [Tune("동료(기계)/따라가기/걷기 · 뛰기", "뛰는 그림 기준 속도(U/s)", "이 속도보다 빠르면 뛰는 그림(ally-run) — 그림 고르는 기준", 0.2 * FightUnit.S, 20 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyWalkSpeed = 4 * FightUnit.S;
        [Tune("동료(기계)/따라가기/걷기 · 뛰기", "최대 속도(U/s)", "아무리 멀어도 이보다 빨리는 안 뜀", 0.2 * FightUnit.S, 40 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyRunSpeed = 11 * FightUnit.S;
        [Tune("동료(기계)/따라가기/부드럽게", "따라붙는 세기(/초)", "남은 거리 × 이 값 = 따라가는 속도 — 멀수록 빨라지고 가까울수록 천천히 멈춤(뚝뚝 끊김 없이)", 0.2, 20, 0.1)] public double AllyGain = 3;
        [Tune("동료(기계)/따라가기/부드럽게", "플레이어 속도 맞추기(배)", "플레이어가 꾸준히 걸어가면 그 속도만큼 같이 걸음 — 1 = 똑같이 · 0 = 거리만 보고 따라감", 0, 1.5, 0.05)] public double AllyMatch = 0.9;
        [Tune("동료(기계)/따라가기/부드럽게", "자리 옮기는 빠르기(/초)", "자리를 옮길 때 목표가 미끄러지듯 옮겨 감 — 작을수록 천천히 · 부드럽게", 0.2, 30, 0.1)] public double AllyTargetSmooth = 4;
        [Tune("동료(기계)/따라가기/걷기 · 뛰기", "가속(U/s²)", "출발 · 멈춤의 부드러움 — 작을수록 부드럽게(너무 작으면 늦게 따라옴)", 1 * FightUnit.S, 300 * FightUnit.S, 1 * FightUnit.S)] public double AllyAccel = 35 * FightUnit.S;
        [Tune("동료(기계)/따라가기/걷기 · 뛰기", "돌아서기 시간(초)", "바라보는 쪽이 바뀔 때 ally-turn 을 보여 주는 시간(그림이 없으면 바로 뒤집힘)", 0, 0.6, 0.01)] public double AllyTurnTime = 0.12;
        [Tune("동료(기계)/따라가기/길 찾기", "뛰어넘기 · 올라가기(1 켬 · 0 끔)", "기둥에 막히면 뛰어넘고, 플레이어가 발판에 서면 따라 올라감(아래면 뚫고 내려감). 플레이어가 떠 있는 동안은 땅에서 기다림", 0, 1, 1)] public double AllyJumpOn = 1;
        [Tune("동료(기계)/따라가기/길 찾기", "최대 점프 높이(U)", "이보다 높은 곳은 못 올라감 → 아래 ‘막힘 시간’ 뒤 드론으로 날아감", 0.3 * FightUnit.S, 12 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyMaxJumpH = 4 * FightUnit.S;
        [Tune("동료(기계)/따라가기/길 찾기", "막힘 시간(초)", "이만큼 못 움직이면(또는 못 올라가면) 드론에 매달려 날아감", 0.1, 5, 0.05)] public double AllyStuckTime = 0.8;
        [Tune("동료(기계)/드론 비행/자제", "드론 쉬는 시간(초)", "한 번 내려앉으면 이 시간 동안은 자리 옮기려고 날지 않음(화면 밖 · 최전선 · 적 접근 같은 급한 경우는 예외)", 0, 10, 0.1)] public double AllyFlyCd = 8;
        [Tune("동료(기계)/드론 비행/자제", "옮기는 거리 벌점", "자리를 다시 잡을 때 가까운 자리를 얼마나 좋아하는지 — 클수록 짧게 옮기고 빨리 자리 잡음(멀리 · 높이 가려고 길게 날지 않음)", 0, 4, 0.05)] public double AllyTravelW = 1.2;
        [Tune("동료(기계)/엄호 자리/자리 지키기", "사거리 밖 버티는 시간(초)", "적이 사거리 밖에 이만큼 있으면 그때 자리를 다시 잡음 · 0 = 사거리 때문에는 안 옮김(저격수 — 기본)", 0, 10, 0.1)] public double AllyOutRangeTime = 0;
        [Tune("동료(기계)/드론 비행/위험", "적 접근 한계(U)", "적과 가로 거리가 이보다 가까워지면(겹침 · 접촉 금지) 바로 드론에 매달려 날아올라 다시 자리 잡음 — 내려앉을 땐 이 거리의 1.3배 이상", 0.3 * FightUnit.S, 10 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyDangerDist = 2.2 * FightUnit.S;
        [Tune("동료(기계)/드론 비행/움직임", "비행 속도(U/s)", "", 1 * FightUnit.S, 60 * FightUnit.S, 0.5 * FightUnit.S)] public double AllyFlySpeed = 30 * FightUnit.S;
        [Tune("동료(기계)/드론 비행/움직임", "비행 가속(U/s²)", "작을수록 부드럽게 떠오르고 내려앉음", 1 * FightUnit.S, 400 * FightUnit.S, 1 * FightUnit.S)] public double AllyFlyAccel = 160 * FightUnit.S;
        [Tune("동료(기계)/드론 비행/움직임", "순항 높이(U)", "땅(또는 플레이어 발) 위로 이만큼 — 적 너머로 갈 땐 적 머리보다 높게", 0.5 * FightUnit.S, 10 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyFlyHeight = 1.4 * FightUnit.S;
        [Tune("동료(기계)/따라가기/길 찾기", "너무 멀면 드론 비행(U)", "플레이어에게서 이만큼 멀어지면 드론에 매달려 날아감", 2 * FightUnit.S, 60 * FightUnit.S, 0.5 * FightUnit.S)] public double AllyWarpDist = 30 * FightUnit.S;
        [Tune("동료(기계)/사격/판정", "사거리(U)", "동료 총구에서 결투자까지 거리(대각선 포함)가 이 안이면 맞음", 0.5 * FightUnit.S, 80 * FightUnit.S, 0.25 * FightUnit.S)] public double AllyRange = 40 * FightUnit.S;
        [Tune("동료(기계)/사격/판정", "위아래 허용(± U)", "총구와 결투자 가운데의 높이 차 — 높은 곳에서 내려 쏘려면 넉넉하게", 0.2 * FightUnit.S, 40 * FightUnit.S, 0.1 * FightUnit.S)] public double AllyRangeH = 20 * FightUnit.S;
        [Tune("동료(기계)/사격/판정", "쏘는 높이(U)", "동료 발에서 총구까지 — 광선이 나가는 높이", 0, 3 * FightUnit.S, 0.05 * FightUnit.S)] public double AllyChestY = 1.1 * FightUnit.S;
        [Tune("동료(기계)/사격/데미지", "데미지 배율(배)", "× 기본 데미지 × 그 타의 배율 — 1 = 예전 기계 공격과 같음", 0, 3, 0.05)] public double AllyDmgMul = 1;
        [Tune("동료(기계)/사격/연출", "쏘는 모습 시간(초)", "쏜 뒤 이 시간 동안 쏜 쪽을 봄 · ally-shoot 그림은 그림 길이와 이 값 중 긴 쪽", 0.05, 1, 0.01)] public double AllyShootTime = 0.22;
        [Tune("동료(기계)/강한 사격/켬", "3타는 강한 사격(1 켬 · 0 끔)", "3타의 기계 공격은 뒤에서 버티고 서서 굵은 한 방(앞으로 나가지 않음)", 0, 1, 1)] public double AllyStrike3 = 1;
        [Tune("동료(기계)/강한 사격/켬", "카운터 성공 · 크래시 때도(1 켬 · 0 끔)", "카운터가 터지면 동료도 뒤에서 엄호 사격 한 방(데미지는 아래 배율)", 0, 1, 1)] public double AllyOnCounter = 1;
        [Tune("동료(기계)/강한 사격/데미지", "3타 강한 사격 배율(배)", "× 동료 사격 데미지", 0, 4, 0.05)] public double AllyStrikeMul = 1.3;
        [Tune("동료(기계)/강한 사격/데미지", "카운터 때 데미지(배)", "× 기본 데미지 — 0 = 연출만(밸런스 그대로)", 0, 3, 0.05)] public double AllyCtrDmgMul = 0;
        [Tune("동료(기계)/강한 사격/판정", "사거리(U)", "강한 사격은 보통 사격보다 멀리 — 동료가 선 자리에서 결투자까지 가로 거리", 0.5 * FightUnit.S, 100 * FightUnit.S, 0.25 * FightUnit.S)] public double AllyStrikeRange = 50 * FightUnit.S;
        [Tune("동료(기계)/강한 사격/버티기", "반동으로 밀림(U)", "쏘는 순간 뒤로 미끄러지는 거리", 0, 2 * FightUnit.S, 0.02 * FightUnit.S)] public double AllyKick = 0.35 * FightUnit.S;
        [Tune("동료(기계)/강한 사격/버티기", "버티는 시간(초)", "쏘고 나서 자리에 굳어 있는 시간(ally-shoot-strong) — 그 뒤 다시 따라감", 0, 0.8, 0.01)] public double AllyHoldTime = 0.22;
        [Tune("동료(기계)/강한 사격/버티기", "결투자 움찔(초)", "강한 사격에 결투자가 굳는 시간", 0, 0.6, 0.01)] public double AllyStrikeFlinch = 0.25;
        [Tune("동료(기계)/컷인/켬", "컷인(1 켬 · 0 끔)", "카운터 크래시 · 궁극기 시작 때 여주인공 얼굴(ally-cutin 그림이 있을 때만 — 글자 알림은 없음) · 기본 끔", 0, 1, 1)] public double AllyCutIn = 0;
        [Tune("동료(기계)/컷인/켬", "3타 강한 사격에도 컷인(1 켬 · 0 끔)", "자주 나오면 질릴 수 있어 기본 끔", 0, 1, 1)] public double AllyCutIn3 = 0;
        [Tune("동료(기계)/컷인/모양", "보이는 시간(초)", "", 0.2, 3, 0.05)] public double AllyCutInTime = 0.8;
        [Tune("화면/보이는 크기/따로", "동료 그림 배율(배)", "전체 배율에 한 번 더 곱함", 0, 2, 0.05)] public double ImgScaleAlly = 1;

        public double AtkLockOf(int combo)
        {
            double v = combo == 1 ? AtkLock1 : combo == 2 ? AtkLock2 : combo == 3 ? AtkLock3 : 0;
            return v > 0 ? v : AtkLock;
        }
        [Tune("플레이어/기본 공격/공통", "콤보 입력 시간(초)", "명세: Combo Input Window", 0.05, 0.55, 0.01)] public double ComboWindow = 0.3;
        [Tune("플레이어/기본 공격/공통", "공격 뒤 재사용 대기(초)", "위치 고정이 끝난 뒤 이 시간 동안은 다음 공격이 안 나감(그 사이 누르면 기다렸다 나감) · 0 = 바로. 콤보 입력 시간보다 길면 콤보가 1타로 돌아가요", 0, 1, 0.025)] public double AtkGap = 0;
        [Tune("플레이어/기본 공격/박에 붙이기", "박에 붙이기(0 끔 · 1 4분 · 2 8분 · 3 16분)", "누른 공격을 가장 가까운 다음 박 칸에 맞춰 냄 — 모션 · 소리가 같은 순간, 연타하면 박 칸마다 일정하게(드럼 롤). 위치 고정 · 재사용 대기가 남아 있으면 그 뒤 첫 박 칸에", 0, 3, 1)] public double AtkQuant = 2;
        [Tune("플레이어/기본 공격/박에 붙이기", "늦게 눌러도 그 칸(초)", "박 칸을 이만큼 지나서 눌러도 기다리지 않고 바로 냄(살짝 늦은 입력이 한 칸 밀리지 않게)", 0, 0.08, 0.005)] public double AtkQuantLate = 0.03;
        [Tune("플레이어/기본 공격/공통", "마법 → 기계 지연(초)", "명세: 마법 → 기계 공격 지연", 0, 0.3, 0.01)] public double MechDelay = 0.15;
        [Tune("플레이어/기본 공격/1타", "데미지 배율(배)", "× 기본 데미지(검기 · 기계 각각) · 명세: 1타 데미지", 0, 1, 0.025)] public double Atk1Mul = 0.5;
        [Tune("플레이어/기본 공격/2타", "데미지 배율(배)", "× 기본 데미지(검기 · 기계 각각) · 명세: 2타 데미지", 0, 1.2, 0.025)] public double Atk2Mul = 0.6;
        [Tune("플레이어/기본 공격/3타", "데미지 배율(배)", "× 기본 데미지(검기 · 기계 각각) · 명세: 3타 데미지", 0, 1.7, 0.05)] public double Atk3Mul = 0.85;
        [Tune("플레이어/기본 공격/1타", "판정 앞쪽(U)", "X + · 명세: 1타 X +", 0.6 * FightUnit.S, 9.6 * FightUnit.S, 0.25 * FightUnit.S)] public double Atk1Reach = 5.1 * FightUnit.S;
        [Tune("플레이어/기본 공격/2타", "판정 앞쪽(U)", "X + · 명세: 2타 X +", 0.6 * FightUnit.S, 10.6 * FightUnit.S, 0.25 * FightUnit.S)] public double Atk2Reach = 5.6 * FightUnit.S;
        [Tune("플레이어/기본 공격/3타", "판정 앞쪽(U)", "X + · 명세: 3타 X +", 0.7 * FightUnit.S, 11.7 * FightUnit.S, 0.25 * FightUnit.S)] public double Atk3Reach = 6.2 * FightUnit.S;
        [Tune("플레이어/기본 공격/공통", "판정 뒤쪽(U)", "명세: X −", 0, 1 * FightUnit.S, 0.025 * FightUnit.S)] public double AtkBack = 0.5 * FightUnit.S;
        [Tune("플레이어/기본 공격/1타", "판정 위아래(± U)", "Y ± · 명세: 1타 Y ±", 0.2 * FightUnit.S, 3 * FightUnit.S, 0.05 * FightUnit.S)] public double Atk1HalfH = 1.6 * FightUnit.S;
        [Tune("플레이어/기본 공격/2타", "판정 위아래(± U)", "Y ± · 명세: 2타 Y ±", 0.2 * FightUnit.S, 3.4 * FightUnit.S, 0.1 * FightUnit.S)] public double Atk2HalfH = 1.8 * FightUnit.S;
        [Tune("플레이어/기본 공격/3타", "판정 위아래(± U)", "Y ± · 명세: 3타 Y ±", 0.2 * FightUnit.S, 4.2 * FightUnit.S, 0.1 * FightUnit.S)] public double Atk3HalfH = 2.2 * FightUnit.S;

        // ── 카운터 ──
        [Tune("플레이어/카운터/자세 · 공격", "위치 고정(초)", "명세: Counter Position Lock", 0.05, 0.55, 0.01)] public double CtrLock = 0.3;
        [Tune("플레이어/카운터/자세 · 공격", "카운터 자세 시간(초)", "이 동안 맞으면 카운터 성공 · 명세: Counter State Duration", 0.05, 1.95, 0.05)] public double CtrState = 1;
        [Tune("플레이어/카운터/자세 · 공격", "카운터 공격 지연(초)", "자세 시작 → 카운터 공격 · 명세: Counter Attack Delay", 0, 0.2, 0.005)] public double CtrAtkDelay = 0.1;
        [Tune("플레이어/카운터/자세 · 공격", "카운터 공격 판정 가로(± U)", "명세: X ±", 0.5 * FightUnit.S, 4.3 * FightUnit.S, 0.1 * FightUnit.S)] public double CtrHalfW = 2.4 * FightUnit.S;
        [Tune("플레이어/카운터/자세 · 공격", "카운터 공격 판정 세로(± U)", "명세: Y ±", 0.5 * FightUnit.S, 3.5 * FightUnit.S, 0.1 * FightUnit.S)] public double CtrHalfH = 2 * FightUnit.S;
        [Tune("플레이어/카운터/데미지", "카운터 공격 배율(배)", "× 기본 데미지(검기 · 기계 각각) · 명세: 카운터 공격 데미지", 0, 8, 0.05)] public double CtrAtkMul = 4;
        [Tune("플레이어/카운터/데미지", "카운터 성공 배율(배)", "카운터 성공 때 × 기본 데미지 · 명세: Counter Damage", 0, 20, 0.1)] public double CtrSuccessMul = 10;
        [Tune("플레이어/카운터/충전", "최대 충전 횟수(회)", "명세: Counter Maximum Stack Capacity", 1, 3, 1)] public double CtrCap = 2;
        [Tune("플레이어/카운터/충전", "1회 충전 시간(초)", "명세: Counter Stack Recharge Time", 0.25, 5.75, 0.125)] public double CtrRecharge = 3;
        [Tune("플레이어/카운터/충전", "사용 후 쿨타임(초)", "", 0, 2, 0.05)] public double CtrCooldown = 1;
        [Tune("플레이어/카운터/충전", "카운터 뒤 재사용 대기(초)", "위치 고정이 끝난 뒤 이 시간 동안은 다음 카운터가 안 나감 · 0 = 바로. ‘사용 후 쿨타임’ 과 달리 작업실 ‘쿨타임 없음’ 에서도 걸림", 0, 2, 0.025)] public double CtrGap = 0;
        [Tune("공통 규칙/경직/카운터 성공 경직", "근접 카운터 거리(U)", "이 안에서 성공하면 ‘근접’ · 명세: 근접 카운터 판정 거리", 0, 4 * FightUnit.S, 0.1 * FightUnit.S)] public double NearCounterDist = 2 * FightUnit.S;

        // ── 경직 · 넉백 · 월 바운스 ──
        [Tune("공통 규칙/경직/카운터 성공 경직", "기본(초)", "카운터 성공으로 상대가 굳는 시간 · 명세: 기본 카운터 성공 경직", 0, 4, 0.1)] public double StunBase = 2;
        [Tune("공통 규칙/경직/카운터 성공 경직", "근접 카운터 추가(초)", "명세: 근접 카운터 추가 경직", 0, 4, 0.1)] public double StunNear = 2;
        [Tune("공통 규칙/경직/카운터 성공 경직", "월 바운스 추가(초)", "명세: 월 바운스 추가 경직", 0, 4, 0.1)] public double StunWall = 2;
        [Tune("공통 규칙/경직/카운터 성공 경직", "근접 월 바운스 추가(초)", "명세: 근접 월 바운스 추가 경직", 0, 4, 0.1)] public double StunNearWall = 2;
        [Tune("공통 규칙/카운터 크래시/크래시(굿)", "결투자 경직(초)", "크래시 성공 때 결투자가 굳는 시간 · 명세: 카운터 크래시 경직", 0, 4, 0.1)] public double CrashStun = 2;
        [Tune("공통 규칙/경직/한계 · 맞은 뒤", "최대 누적 경직(초)", "명세: 최대 누적 경직 시간", 0.25, 9.75, 0.25)] public double StunCap = 5;
        [Tune("공통 규칙/경직/카운터 성공 경직", "근접 월 바운스 거리(U)", "벽까지 이 안이면 ‘근접’ · 명세: 근접 월 바운스 판정 거리", 0, 12 * FightUnit.S, 0.25 * FightUnit.S)] public double NearWallDist = 6 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "거리 배율(배)", "× 두 몸 중심 거리 · 명세: 중심 거리 ×", 0, 4, 0.1)] public double KnockMul = 2;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "최소 거리(U)", "명세: 넉백 최소 거리", 0, 4.4 * FightUnit.S, 0.1 * FightUnit.S)] public double KnockMin = 2.2 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "최대 거리(U)", "명세: 넉백 최대 거리", 0, 12 * FightUnit.S, 0.25 * FightUnit.S)] public double KnockMax = 6 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "속도(U/s)", "명세: 넉백 속도", 5 * FightUnit.S, 55 * FightUnit.S, 1 * FightUnit.S)] public double KnockSpeed = 30 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/월 바운스 반동", "반동 속도(U/s)", "명세: Wall Bounce Velocity", 0.5 * FightUnit.S, 30.5 * FightUnit.S, 1 * FightUnit.S)] public double BounceSpeed = 15.5 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/월 바운스 반동", "바닥 마찰(U/s²)", "자연스럽게 멈춤", 2 * FightUnit.S, 78 * FightUnit.S, 2 * FightUnit.S)] public double BounceFriction = 40 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "위로 뜨는 정도", "세로 방향 +", 0, 0.3, 0.01)] public double KnockLift = 0.15;
        [Tune("공통 규칙/넉백 · 월 바운스/넉백", "세로 성분 한계(±)", "±", 0, 1.2, 0.02)] public double KnockLiftCap = 0.6;
        [Tune("공통 규칙/경직/한계 · 맞은 뒤", "맞은 뒤 무적(초)", "", 0, 0.9, 0.025)] public double HurtIFrames = 0.45;

        // ── 결투자 ──
        [Tune("결투자/몸 · 체력 · 단계/체력", "체력", "", 500, 19500, 500, NextRound = true)] public double EnemyHp = 10000;
        [Tune("플레이어/몸 · 체력/체력", "체력", "", 0, 1000, 50, NextRound = true)] public double PlayerHp = 500;
        // ───────── 결투자 자연 이동: 차분하게 간격을 재다가 플레이어가 방심하면 몰아붙임 · 점프 · 더블 점프 · 대쉬(위치 잡기 + 회피) ─────────
        [Tune("결투자/이동 · 반응/자연 이동", "이동 방식(1 자연 · 0 예전 박 스텝)", "자연 = 플레이어처럼 가속 · 감속하며 걷고 점프 · 대쉬 · 의도를 몇 박씩 유지(들썩이지 않게) · 예전 = 박마다 짧은 스텝", 0, 1, 1)] public double EnemyMoveMode = 1;
        [Tune("결투자/이동 · 반응/자연 이동", "점프 · 대쉬 난이도(1 ~ 3)", "1 = 가끔 · 느린 반응 · 더블 점프 없음 / 2 = 보통 · 더블 점프 / 3 = 자주 · 빠른 반응 · 공중 대쉬", 1, 3, 1)] public double EnemyAgility = 2;
        [Tune("결투자/이동 · 반응/자연 이동", "가속(U/s²)", "걷기 시작 · 방향 바꿀 때", 2 * FightUnit.S, 120 * FightUnit.S, 1 * FightUnit.S)] public double EnemyAccel = 22 * FightUnit.S;
        [Tune("결투자/이동 · 반응/자연 이동", "거리 여유 폭(U)", "유지 거리 ± 이 안이면 가만히(경계에서 왔다 갔다 하지 않게)", 0, 6 * FightUnit.S, 0.1 * FightUnit.S)] public double EnemyHoldBand = 1.0 * FightUnit.S;
        [Tune("결투자/이동 · 반응/자연 이동", "의도 유지(박)", "다가가기 · 물러나기 · 버티기를 정하면 최소 이만큼 유지", 0.5, 8, 0.5)] public double EnemyCommitBeats = 2;
        [Tune("결투자/이동 · 반응/자연 이동", "반응 늦음(초)", "회피 · 몰아붙이기를 결정하고 실제로 움직이기까지 · 0 = 난이도 기본(1: 0.35 · 2: 0.25 · 3: 0.15)", 0, 1, 0.01)] public double EnemyReact = 0;
        [Tune("결투자/이동 · 반응/자연 이동", "방심 노리기(1 켬 · 0 끔)", "플레이어가 가만히 있거나 · 카운터가 빠졌거나 · 공격 뒤 틈이면 대쉬로 붙어 찌름", 0, 1, 1)] public double EnemyPunishOpen = 1;
        [Tune("결투자/이동 · 반응/자연 이동", "방심: 가만히 있는 시간(초)", "플레이어가 이만큼 움직이지 않으면 방심으로 봄", 0.3, 5, 0.1)] public double EnemyOpenIdle = 1.4;
        [Tune("결투자/이동 · 반응/자연 이동", "몰아붙이기 쿨타임(초)", "방심을 노린 뒤 다시 노리기까지", 0.5, 15, 0.5)] public double EnemyPunishCd = 4;
        [Tune("결투자/이동 · 반응/자연 이동", "회피 확률 배율(배)", "난이도별 회피 확률(1: 15% · 2: 30% · 3: 50%)에 곱함", 0, 3, 0.05)] public double EnemyEvadeMul = 1;
        [Tune("결투자/이동 · 반응/자연 이동", "대쉬 거리(U)", "", 0.5 * FightUnit.S, 10 * FightUnit.S, 0.1 * FightUnit.S)] public double EnemyDashDist = 3.2 * FightUnit.S;
        [Tune("결투자/이동 · 반응/자연 이동", "대쉬 시간(초)", "", 0.04, 0.5, 0.01)] public double EnemyDashTime = 0.12;
        [Tune("결투자/이동 · 반응/자연 이동", "점프 초속(U/s)", "", 4 * FightUnit.S, 40 * FightUnit.S, 0.5 * FightUnit.S)] public double EnemyJumpV = 20 * FightUnit.S;
        [Tune("결투자/이동 · 반응/자연 이동", "더블 점프 초속(U/s)", "난이도 2 이상", 2 * FightUnit.S, 30 * FightUnit.S, 0.5 * FightUnit.S)] public double EnemyDJumpV = 13 * FightUnit.S;
        [Tune("결투자/이동 · 반응/자연 이동", "멀 때 대쉬로 붙기(배)", "유지 거리 바깥 폭 × 이 값보다 멀면 대쉬로 거리 좁히기(위치 잡기)", 1, 6, 0.1)] public double EnemyDashFarMul = 2.2;
        [Tune("결투자/이동 · 반응/시선", "시선(1 자연 · 0 항상 플레이어)", "자연 = 교전 거리 안에선 플레이어를 보고, 멀리 자리를 옮길 땐 가는 쪽을 봄(가끔 뒤돌아봄) · 공격 · 예고 · 카운터 땐 언제나 플레이어", 0, 1, 1)] public double EnemyLookMode = 1;
        [Tune("결투자/이동 · 반응/시선", "교전 거리(배)", "(유지 거리 + 여유 폭) × 이 값 안이면 늘 플레이어를 봄(뒷걸음)", 1, 5, 0.1)] public double EnemyEngageMul = 1.5;
        [Tune("결투자/이동 · 반응/시선", "등 돌리는 기준(박)", "교전 거리 밖에서 플레이어 반대쪽으로 이만큼 계속 움직이면 돌아서서 걸어감", 0.25, 8, 0.25)] public double EnemyTurnAfterBeats = 1;
        [Tune("결투자/이동 · 반응/시선", "뒤돌아보기 간격(박)", "가는 쪽을 보며 걷는 동안 이 간격마다 잠깐 플레이어를 돌아봄 · 0 = 안 함", 0, 16, 0.5)] public double EnemyGlanceBeats = 3;
        [Tune("결투자/이동 · 반응/시선", "뒤돌아보기 시간(초)", "", 0.1, 2, 0.05)] public double EnemyGlanceTime = 0.4;
        [Tune("결투자/이동 · 반응/시선", "방향 유지 최소(초)", "한 번 돌아서면 이만큼은 그 방향(휙휙 돌지 않게)", 0, 2, 0.05)] public double EnemyFaceHold = 0.35;
        [Tune("결투자/이동 · 반응/시선", "돌아서기 그림 시간(초)", "돌아설 때 foe-turn 그림을 보여 주는 시간(그림이 없으면 바로 뒤집힘)", 0, 1, 0.02)] public double EnemyTurnTime = 0.12;
        [Tune("결투자/이동 · 반응/이동", "이동 속도(U/s)", "", 0.525 * FightUnit.S, 6.275 * FightUnit.S, 0.125 * FightUnit.S)] public double EnemySpeed = 3.4 * FightUnit.S;
        [Tune("결투자/몸 · 체력 · 단계/단계", "단계마다 빨라지는 속도(U/s)", "", 0, 3.2 * FightUnit.S, 0.1 * FightUnit.S)] public double EnemySpeedPhase = 1.6 * FightUnit.S;
        [Tune("결투자/공격 공통/모든 패턴 배율", "사거리 배율(배)", "패턴 거리 · 사거리 × — 패턴 탭에서 패턴마다 덮어쓸 수 있음", 0.5, 5.5, 0.125)] public double EnemyReachMul = 3;
        [Tune("결투자/공격 공통/모든 패턴 배율", "돌진 배율(배)", "타격 때 앞으로 나가는 거리 × — 패턴마다 덮어쓰기 가능", 0, 2.8, 0.05)] public double EnemyLungeMul = 1.4;
        [Tune("결투자/공격 공통/모든 패턴 배율", "데미지 배율(배)", "패턴 · 맵 패턴 데미지 ×", 0, 2, 0.05)] public double EnemyDamageMul = 1;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "쉬는 마디 확률 · 1단계(0 ~ 1)", "2 · 3단계는 줄어듦", 0, 2, 0.05)] public double EnemyRest = 1;
        [Tune("결투자/카운터/충전", "최대 충전 횟수(회)", "", 0, 6, 1)] public double EnemyCtrCap = 3;
        [Tune("결투자/카운터/충전", "1회 충전 시간(초)", "", 0.5, 36.5, 1)] public double EnemyCtrRecharge = 18.5;
        [Tune("결투자/카운터/충전", "사용 후 쿨타임(초)", "", 0, 5, 0.125)] public double EnemyCtrCooldown = 2.5;
        [Tune("결투자/카운터/자세 · 공격", "카운터 공격 판정 가로(± U)", "", 0.65 * FightUnit.S, 14.15 * FightUnit.S, 0.25 * FightUnit.S)] public double EnemyCtrHalfW = 7.4 * FightUnit.S;
        [Tune("결투자/카운터/자세 · 공격", "카운터 공격 판정 세로(± U)", "", 0.55 * FightUnit.S, 12.05 * FightUnit.S, 0.25 * FightUnit.S)] public double EnemyCtrHalfH = 6.3 * FightUnit.S;
        [Tune("결투자/카운터/데미지", "카운터 성공 배율(배)", "× 기본 데미지", 0, 8, 0.2)] public double EnemyCtrSuccessMul = 4;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "유지 거리(배)", "× 공격 사거리 배율", 0, 4.4, 0.1)] public double EnemyIdleReach = 2.2;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "멀다고 보는 거리(U)", "", 0, 18 * FightUnit.S, 0.5 * FightUnit.S)] public double EnemyFarDist = 9 * FightUnit.S;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "멀 때 다가갈 거리(U)", "", 0, 12 * FightUnit.S, 0.5 * FightUnit.S)] public double EnemyFarWant = 6 * FightUnit.S;
        [Tune("결투자/카운터/데미지", "가만히 맞을 때 데미지 배율(배)", "카운터를 안 누르고 결투자 카운터 공격을 맞으면 × 기본 데미지", 0, 2.4, 0.1)] public double EnemyCtrHitMul = 1.2;
        [Tune("결투자/몸 · 체력 · 단계/단계", "격노 데미지 배율(배)", "3단계(격노)에서 ×", 0, 2.4, 0.05)] public double RageDamageMul = 1.2;
        [Tune("결투자/몸 · 체력 · 단계/단계", "2단계 체력 기준(비율)", "체력이 이 비율 아래면 2단계", 0, 1.32, 0.02)] public double Phase1Hp = 0.66;
        [Tune("결투자/몸 · 체력 · 단계/단계", "3단계(격노) 체력 기준(비율)", "체력이 이 비율 아래면 3단계(격노)", 0, 0.66, 0.01)] public double Phase2Hp = 0.33;
        [Tune("결투자/공격 공통/근접 판정", "판정 유지(초)", "패턴마다 덮어쓰기 가능", 0, 0.24, 0.01)] public double MeleeActive = 0.12;
        [Tune("결투자/공격 공통/에너지탄", "수명(초)", "패턴마다 덮어쓰기 가능", 0, 8, 0.25)] public double ShotLife = 4;
        [Tune("결투자/카운터/자세 · 공격", "카운터 공격까지(박)", "카운터 자세 → 카운터 공격. 이 순간 같이 카운터하면 크래시", 0, 4, 0.05)] public double EnemyCtrAtkBeats = 2;
        [Tune("결투자/반응 패턴/연타 대응", "맞은 횟수(회)", "이 시간 안에 이만큼 맞으면 대응", 2, 8, 1)] public double SpamHits = 5;
        [Tune("결투자/반응 패턴/연타 대응", "시간 창(초)", "", 0.3, 2.9, 0.05)] public double SpamWindow = 1.6;

        // ── 결투자 AI ──
        [Tune("결투자/이동 · 반응/박 스텝", "스텝 거리(U)", "쉬는 동안 박에 맞춰 스텝으로 거리 조절", 0, 2.4 * FightUnit.S, 0.05 * FightUnit.S)] public double EnemyStepDist = 1.2 * FightUnit.S;
        [Tune("결투자/반응 패턴/추격", "추격(1 켬 · 0 끔)", "결투자 카운터 성공 뒤 따라가서 침", 0, 1, 1)] public double EnemyPunish = 1;
        [Tune("결투자/반응 패턴/추격", "추격 속도 배율(배)", "× 이동 속도", 1, 3.4, 0.05)] public double EnemyPunishChase = 2.2;
        [Tune("결투자/반응 패턴/빈틈 찌르기", "확률(0 ~ 1)", "플레이어 공격 · 카운터가 빗나가면", 0, 1, 0.025)] public double EnemyWhiffPunish = 0.5;
        [Tune("결투자/이동 · 반응/회피", "회피 확률(0 ~ 1)", "플레이어가 공격하면 뒤로 스텝", 0, 0.4, 0.01)] public double EnemyDodge = 0.2;
        [Tune("결투자/이동 · 반응/경직 풀린 뒤", "물러나기 확률(0 ~ 1)", "", 0, 1.2, 0.025)] public double EnemyRecoverEvade = 0.6;
        [Tune("결투자/이동 · 반응/박 스텝", "스텝 · 회피 시간(초)", "", 0, 0.32, 0.01)] public double StepTime = 0.16;
        [Tune("결투자/이동 · 반응/회피", "회피 뒤 쿨타임(초)", "", 0, 2.4, 0.1)] public double DodgeCooldown = 1.2;
        [Tune("결투자/반응 패턴/빈틈 찌르기", "쿨타임(초)", "", 0, 3, 0.1)] public double WhiffCooldown = 1.5;
        [Tune("결투자/반응 패턴/빈틈 찌르기", "추가 거리(U)", "기본 거리 + 이만큼 안이면", 0, 5 * FightUnit.S, 0.1 * FightUnit.S)] public double WhiffRange = 2.5 * FightUnit.S;
        [Tune("결투자/이동 · 반응/경직 풀린 뒤", "반응 거리(U)", "플레이어가 이 안이면", 0, 8 * FightUnit.S, 0.25 * FightUnit.S)] public double RecoverDist = 4 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "발판 따라 점프(1 켬 · 0 끔)", "", 0, 1, 1)] public double EnemyPlatformJump = 1;

        // ── 맵 패턴(전역 공격) ──
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/일정", "맵 패턴(1 켬 · 0 끔)", "", 0, 1, 1)] public double MapEnable = 1;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/일정", "첫 맵 패턴(마디)", "", 0, 8, 1)] public double MapStartBar = 4;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/일정", "간격(마디)", "", 1, 11, 1)] public double MapEveryBars = 6;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/일정", "데미지", "× 결투자 데미지 배율", 0, 24, 1)] public double MapDamage = 12;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/결투자 무적 비행", "떠 있는 높이(U)", "", 0, 13 * FightUnit.S, 0.25 * FightUnit.S)] public double EnemyHoverY = 6.5 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "유도 뒤 수명(초)", "", 0, 7, 0.25)] public double MisLife = 3.5;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "타격 유지(초)", "", 0, 0.36, 0.01)] public double ColActive = 0.18;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "타격 유지(초)", "", 0, 0.36, 0.01)] public double LasActive = 0.18;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "높은 띠 아래쪽(U)", "바닥에 서 있으면 안 맞는 높이", 0, 4.2 * FightUnit.S, 0.1 * FightUnit.S)] public double LasHighY = 2.1 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "예고(박)", "", 0, 2, 0.25)] public double WallTelegraph = 1;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "사용(1 켬 · 0 끔)", "", 0, 1, 1)] public double MissileOn = 1;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "결투자 무적(1 켬 · 0 끔)", "", 0, 1, 1)] public double MissileInvuln = 0;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "개수(개)", "", 1, 11, 1)] public double MisCount = 6;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "발사 간격(박)", "", 0.14, 0.86, 0.02)] public double MisInterval = 0.5;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "속도(U/s)", "", 1 * FightUnit.S, 15 * FightUnit.S, 0.25 * FightUnit.S)] public double MisSpeed = 8 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "선회력(rad/s)", "", 0, 5, 0.125)] public double MisTurn = 2.5;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "유도 시간(초)", "이후 직진", 0, 3.2, 0.1)] public double MisHoming = 1.6;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "사용(1 켬 · 0 끔)", "세로", 0, 1, 1)] public double ColumnOn = 1;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "결투자 무적(1 켬 · 0 끔)", "", 0, 1, 1)] public double ColumnInvuln = 1;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "개수(개)", "", 1, 15, 1)] public double ColCount = 8;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "간격(박)", "", 0.14, 0.86, 0.02)] public double ColInterval = 0.5;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "예고(박)", "", 0.25, 1.75, 0.05)] public double ColTelegraph = 1;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "폭(U)", "", 0.2 * FightUnit.S, 2.2 * FightUnit.S, 0.05 * FightUnit.S)] public double ColWidth = 1.2 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "사용(1 켬 · 0 끔)", "가로", 0, 1, 1)] public double LaserOn = 1;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "결투자 무적(1 켬 · 0 끔)", "", 0, 1, 1)] public double LaserInvuln = 1;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "개수(개)", "낮게 · 높게 번갈아", 1, 7, 1)] public double LasCount = 4;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "간격(박)", "", 0.25, 1.75, 0.05)] public double LasInterval = 1;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "예고(박)", "", 0.25, 1.75, 0.05)] public double LasTelegraph = 1;
        [Tune("결투장 · 맵 패턴/가로 레이저/가로 레이저", "두께(± U)", "", 0.1 * FightUnit.S, 1.1 * FightUnit.S, 0.025 * FightUnit.S)] public double LasThick = 0.6 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "사용(1 켬 · 0 끔)", "가로로 지나감", 0, 1, 1)] public double WallOn = 1;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "결투자 무적(1 켬 · 0 끔)", "", 0, 1, 1)] public double WallInvuln = 0;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "개수(개)", "양쪽 번갈아", 1, 5, 1)] public double WallCount = 3;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "간격(박)", "", 0.3, 3.7, 0.1)] public double WallInterval = 2;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "속도(U/s)", "", 1 * FightUnit.S, 23 * FightUnit.S, 0.5 * FightUnit.S)] public double WallSpeed = 12 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "높이(U)", "점프로 넘기", 0.2 * FightUnit.S, 3 * FightUnit.S, 0.05 * FightUnit.S)] public double WallHeight = 1.6 * FightUnit.S;

        // ── 리듬 ──
        [Tune("리듬 · 예고/박 · 그루브/박 판정", "박 판정 폭(± 초)", "", 0.02, 0.16, 0.005)] public double BeatWindow = 0.09;
        [Tune("리듬 · 예고/박 · 그루브/박 판정", "박 보너스(+ 비율)", "박에 맞춘 공격 데미지 +", 0, 0.6, 0.02)] public double BeatBonus = 0.3;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "박 맞춤마다 +", "", 0, 0.2, 0.005)] public double GrooveGain = 0.1;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "엇박마다 −", "", 0, 0.12, 0.002)] public double GrooveLoss = 0.06;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "맞으면 −", "", 0.01, 0.69, 0.02)] public double GrooveHurt = 0.35;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "초당 감소", "", 0, 0.08, 0.002)] public double GrooveDecay = 0.04;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "최대일 때 데미지 +(비율)", "", 0, 1, 0.025)] public double GrooveDamage = 0.5;
        [Tune("리듬 · 예고/박 · 그루브/그루브", "최대일 때 카운터 충전 가속(+ 비율)", "", 0, 2, 0.05)] public double GrooveCtrRecharge = 1;
        [Tune("리듬 · 예고/박 · 그루브/음악 필터", "그루브 음악 필터(1 켬 · 0 끔)", "", 0, 1, 1)] public double GrooveFilter = 1;
        [Tune("리듬 · 예고/박 · 그루브/음악 필터", "그루브 0 일 때 필터(Hz)", "낮을수록 먹먹", 0, 10000, 250)] public double GrooveFilterLow = 5000;
        [Tune("리듬 · 예고/박 · 그루브/음악 필터", "음악이 다 열리는 그루브(0 ~ 1)", "", 0, 1.2, 0.05)] public double GrooveFilterFull = 0.6;

        // ── 세부(그동안 코드에 박혀 있던 값) ──
        [Tune("공통 규칙/중력/중력", "결투자 중력 배율(배)", "× 공통 중력", 0, 2, 0.125)] public double EnemyGravityMul = 1;
        [Tune("공통 규칙/넉백 · 월 바운스/월 바운스 반동", "반동 · 경직 중 중력 배율(배)", "× 공통 중력", 0, 2, 0.125)] public double BounceGravityMul = 1;
        [Tune("플레이어/이동/달리기", "공중 감속 배율(배)", "공중에서 방향키를 뗐을 때 × 감속", 0, 1, 0.05)] public double AirDecelMul = 0.5;
        [Tune("플레이어/이동/달리기", "과속 감속 배율(배)", "대쉬 · 더블 점프로 최고 속도를 넘었을 때 풀리는 정도", 0, 1.2, 0.05)] public double OverSpeedDecelMul = 0.6;
        [Tune("플레이어/점프/발판 내려가기", "통과 시간(초)", "↓ + 점프", 0, 0.5, 0.025)] public double DropThroughTime = 0.25;
        [Tune("플레이어/점프/발판 내려가기", "내려가는 초속(U/s)", "", 0, 4 * FightUnit.S, 0.25 * FightUnit.S)] public double DropThroughVy = 2 * FightUnit.S;
        [Tune("플레이어/점프/착지 감속", "착지 감속(1 켬 · 0 끔)", "떨어진 높이를 3단계로 나눠 단계마다 착지 순간 가로 속도를 줄이고, 잠깐 최고 속도를 낮췄다가 서서히 돌려놓음(관성 그대로 미끄러지지 않게)", 0, 1, 1)] public double LandSlowOn = 1;
        [Tune("플레이어/점프/착지 감속", "1단계 높이(U)", "떨어진 높이(공중 최고점 − 착지한 곳)가 이 이상이면 1단계 · 이보다 낮으면 감속 없음(작은 점프 · 턱 내려오기)", 0, 20 * FightUnit.S, 0.05 * FightUnit.S)] public double LandT1H = 0.6 * FightUnit.S;
        [Tune("플레이어/점프/착지 감속", "2단계 높이(U)", "이 이상이면 2단계", 0, 20 * FightUnit.S, 0.05 * FightUnit.S)] public double LandT2H = 1.8 * FightUnit.S;
        [Tune("플레이어/점프/착지 감속", "3단계 높이(U)", "이 이상이면 3단계(가장 셈)", 0, 30 * FightUnit.S, 0.05 * FightUnit.S)] public double LandT3H = 3.5 * FightUnit.S;
        [Tune("플레이어/점프/착지 감속 · 1단계", "감속(비율)", "착지 순간 줄이는 가로 속도 비율 — 0.1 = 10% 줄임", 0, 1, 0.01)] public double LandT1Slow = 0.12;
        [Tune("플레이어/점프/착지 감속 · 1단계", "회복 시간(초)", "줄어든 최고 속도가 원래대로 돌아오는 시간 — 그동안 입력해도 조금 무거움", 0, 1.5, 0.01)] public double LandT1Time = 0.1;
        [Tune("플레이어/점프/착지 감속 · 2단계", "감속(비율)", "착지 순간 줄이는 가로 속도 비율", 0, 1, 0.01)] public double LandT2Slow = 0.25;
        [Tune("플레이어/점프/착지 감속 · 2단계", "회복 시간(초)", "", 0, 1.5, 0.01)] public double LandT2Time = 0.18;
        [Tune("플레이어/점프/착지 감속 · 3단계", "감속(비율)", "착지 순간 줄이는 가로 속도 비율", 0, 1, 0.01)] public double LandT3Slow = 0.4;
        [Tune("플레이어/점프/착지 감속 · 3단계", "회복 시간(초)", "", 0, 1.5, 0.01)] public double LandT3Time = 0.3;
        [Tune("플레이어/대쉬/대쉬 뒤", "위 속도 한계(배)", "× 점프 초속", 0, 1, 0.05)] public double DashEndUpMul = 0.5;
        [Tune("플레이어/대쉬/대쉬 뒤", "아래 속도 한계(U/s)", "", 0, 4 * FightUnit.S, 0.25 * FightUnit.S)] public double DashEndFall = 2 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/월 바운스 반동", "멈춤 속도(U/s)", "바닥에서 이보다 느리면 멈춤", 0, 0.4 * FightUnit.S, 0.025 * FightUnit.S)] public double BounceStopSpeed = 0.2 * FightUnit.S;
        [Tune("공통 규칙/넉백 · 월 바운스/월 바운스 반동", "최대 시간(초)", "", 0, 2.4, 0.1)] public double BounceMaxTime = 1.2;
        [Tune("결투자/카운터/자세 · 공격", "카운터 자세까지(박)", "카운터 패턴 시작 → 카운터 자세", 0, 8, 0.25)] public double CounterStanceBeats = 4;
        [Tune("결투자/반응 패턴/연타 대응", "카운터 시작 지연(박)", "최소 이만큼 뒤 박 단위 자리에서 카운터 자세", 0, 2, 0.125)] public double ReactLeadBeats = 1;
        [Tune("결투자/반응 패턴/연타 대응", "반응 박 단위(박)", "연타 대응 · 추격이 시작하는 박 간격", 0, 1, 0.05)] public double ReactQuantBeats = 0.5;
        [Tune("결투자/카운터/자세 · 공격", "카운터 공격 뒤 남는 자세(초)", "카운터 공격이 나간 뒤 이만큼만 자세 유지 — 짧을수록 늦은 카운터가 덜 억울함", 0, 0.2, 0.0125)] public double EnemyCtrStatePad = 0.1;
        [Tune("결투자/패턴 진행/고르기", "시작 허용(박)", "마디 첫 박에서 이만큼 안이면 패턴 시작", 0, 0.5, 0.025)] public double PatternStartWindow = 0.25;
        [Tune("결투자/패턴 진행/접근", "다가가기 멈춤(박)", "첫 타격 이만큼 전부터 멈춤", 0, 1, 0.05)] public double ApproachStopBeats = 0.5;
        [Tune("결투자/패턴 진행/접근", "접근 속도 배율(배)", "× 이동 속도", 0, 2.3, 0.05)] public double ApproachMul = 1.15;
        [Tune("결투자/패턴 진행/접근", "거리 오차(U)", "", 0, 0.4 * FightUnit.S, 0.025 * FightUnit.S)] public double ApproachDeadZone = 0.2 * FightUnit.S;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "패턴 끝 여유(박)", "", 0, 1.2, 0.05)] public double PatternEndPad = 0.6;
        [Tune("결투자/공격 공통/돌진 · 내려찍기", "내려찍기 점프 높이(U)", "", 0, 9 * FightUnit.S, 0.5 * FightUnit.S)] public double LeapHeight = 4.5 * FightUnit.S;
        [Tune("결투자/공격 공통/돌진 · 내려찍기", "내려찍기 체공(박)", "", 0, 2, 0.125)] public double LeapBeats = 1;
        [Tune("결투자/공격 공통/근접 판정", "판정 아래 끝(U)", "낮은 공격은 0 · 패턴마다 덮어쓰기 가능", 0, 0.4 * FightUnit.S, 0.025 * FightUnit.S)] public double MeleeBoxBottom = 0.2 * FightUnit.S;
        [Tune("결투자/패턴 진행/고르기", "근접 패턴 최대 거리 여유(U)", "", 0, 3 * FightUnit.S, 0.125 * FightUnit.S)] public double MeleeMaxExtra = 1.5 * FightUnit.S;
        [Tune("결투자/패턴 진행/고르기", "근접 패턴 높이 차 한계(U)", "", 0, 5 * FightUnit.S, 0.25 * FightUnit.S)] public double MeleeMaxHeightDiff = 2.5 * FightUnit.S;
        [Tune("결투자/패턴 진행/고르기", "곡이 크다고 보는 세기(0 ~ 1)", "0 ~ 1", 0, 1.4, 0.05)] public double EnergyLoud = 0.7;
        [Tune("결투자/패턴 진행/고르기", "곡이 클 때 연타 비중 배율(배)", "", 0, 3, 0.125)] public double EnergyLoudMul = 1.5;
        [Tune("결투자/패턴 진행/고르기", "곡이 조용하다고 보는 세기(0 ~ 1)", "0 ~ 1", 0, 0.6, 0.025)] public double EnergyQuiet = 0.3;
        [Tune("결투자/패턴 진행/고르기", "조용할 때 견제 비중 배율(배)", "", 0, 3, 0.125)] public double EnergyQuietMul = 1.5;
        [Tune("결투자/패턴 진행/고르기", "격노 때 카운터 패턴 비중 배율(배)", "", 0, 3.2, 0.2)] public double RageCounterMul = 1.6;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "2단계 쉬는 마디 배율(배)", "× 1단계 확률", 0, 1.2, 0.05)] public double EnemyRestPhase2 = 0.6;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "3단계 쉬는 마디 배율(배)", "× 1단계 확률", 0, 0.6, 0.025)] public double EnemyRestPhase3 = 0.3;
        [Tune("결투자/공격 공통/에너지탄", "나가는 앞쪽 거리(U)", "", 0, 1.2 * FightUnit.S, 0.05 * FightUnit.S)] public double ShotOriginX = 0.6 * FightUnit.S;
        [Tune("결투자/공격 공통/에너지탄", "나가는 높이(U)", "", 0, 2.6 * FightUnit.S, 0.1 * FightUnit.S)] public double ShotOriginY = 1.3 * FightUnit.S;
        [Tune("결투자/공격 공통/에너지탄", "판정 반지름(U)", "", 0, 0.6 * FightUnit.S, 0.025 * FightUnit.S)] public double ShotRadius = 0.3 * FightUnit.S;
        [Tune("결투자/공격 공통/돌진 · 내려찍기", "돌진 판정 폭(± U)", "", 0, 1.2 * FightUnit.S, 0.05 * FightUnit.S)] public double ChargeHalf = 0.6 * FightUnit.S;
        [Tune("결투자/이동 · 반응/박 스텝", "스텝을 밟는 거리 차(U)", "", 0, 0.8 * FightUnit.S, 0.05 * FightUnit.S)] public double StepThreshold = 0.4 * FightUnit.S;
        [Tune("결투자/이동 · 반응/박 스텝", "반대 스텝 확률(0 ~ 1)", "거리 흔들기", 0, 0.36, 0.02)] public double StepFeint = 0.18;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "걷는 속도 배율(배)", "× 이동 속도(박 스텝이 켜져 있을 때)", 0, 0.9, 0.05)] public double IdleWalkMul = 0.45;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "흔들림 크기(U/s)", "", 0, 0.8 * FightUnit.S, 0.05 * FightUnit.S)] public double IdleWobble = 0.4 * FightUnit.S;
        [Tune("결투자/이동 · 반응/회피", "회피 거리(U)", "", 0, 4.8 * FightUnit.S, 0.2 * FightUnit.S)] public double DodgeDist = 2.4 * FightUnit.S;
        [Tune("결투자/이동 · 반응/경직 풀린 뒤", "물러나는 거리(U)", "", 0, 6 * FightUnit.S, 0.25 * FightUnit.S)] public double RecoverEvadeDist = 3 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "따라 점프 높이 차(U)", "", 0, 3 * FightUnit.S, 0.125 * FightUnit.S)] public double PlatformJumpHeight = 1.5 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "따라 점프 가로 거리(U)", "", 0, 10 * FightUnit.S, 0.5 * FightUnit.S)] public double PlatformJumpRange = 5 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "기둥 넘기 여유 높이(U)", "", 0, 1.2 * FightUnit.S, 0.05 * FightUnit.S)] public double VaultMargin = 0.6 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/결투자 무적 비행", "플레이어와 떨어지는 거리(U)", "", 0, 10 * FightUnit.S, 0.5 * FightUnit.S)] public double HoverOffset = 5 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "퍼지는 각(rad)", "", 0, 1, 0.05)] public double MisSpread = 0.5;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "나가는 높이(U)", "", 0, 3.2 * FightUnit.S, 0.2 * FightUnit.S)] public double MisOriginY = 1.6 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "판정 반지름(U)", "", 0, 0.6 * FightUnit.S, 0.025 * FightUnit.S)] public double MisRadius = 0.3 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/낙뢰 기둥/낙뢰 기둥", "위치 흔들림(U)", "", 0, 1.6 * FightUnit.S, 0.1 * FightUnit.S)] public double ColJitter = 0.8 * FightUnit.S;
        [Tune("결투장 · 맵 패턴/충격파 장벽/충격파 장벽", "두께(U)", "", 0, 1.6 * FightUnit.S, 0.1 * FightUnit.S)] public double WallThick = 0.8 * FightUnit.S;

        [Tune("리듬 · 예고/예고 공통/예고음", "시점(박)", "결투자 타격 몇 박 전에 ‘딱’ — 설정 › 예고음 켜기 · 패턴마다 덮어쓰기 가능", 0, 2, 0.125)] public double CueLeadBeats = 1;
        [Tune("리듬 · 예고/예고 공통/예고음", "크기(0 ~ 1)", "", 0, 0.9, 0.05)] public double CueVolume = 0.45;
        [Tune("리듬 · 예고/예고 공통/위험 구역", "보이기 시작(박)", "타격 몇 박 전부터 바닥 · 공중에 칠함 — 설정 › 위험 구역 표시 · 패턴마다 덮어쓰기 가능", 0, 4, 0.25)] public double ZoneShowBeats = 2;
        [Tune("리듬 · 예고/예고 공통/위험 구역", "진해지는 시점(박)", "패턴마다 덮어쓰기 가능", 0, 1, 0.05)] public double ZoneHotBeats = 0.5;
        [Tune("리듬 · 예고/예고 공통/위험 구역", "진하기 배율(배)", "", 0, 2, 0.125)] public double ZoneAlphaMul = 1;
        [Tune("리듬 · 예고/예고 공통/색 변화 · 표식", "색 변화 · 줄어드는 원 길이(박)", "타격 몇 박 전부터 결투자가 공격 색으로 물들고 원이 줄어듦 — 패턴마다 덮어쓰기 가능", 0, 4, 0.25)] public double TelegraphBeats = 2;
        [Tune("리듬 · 예고/예고 공통/색 변화 · 표식", "머리 위 표식 크기 배율(배)", "표식 · 박 점 · 줄어드는 원", 0, 2, 0.125)] public double TelegraphSize = 1;
        [Tune("화면/타격감/히트스톱", "길이 배율(배)", "타격 순간 멈칫하는 길이 · 0 = 끔", 0, 2, 0.125)] public double HitStopMul = 1;
        [Tune("화면/타격감/히트스톱", "멈춘 동안 시간 배율(배)", "0 = 완전히 멈춤 · 1 = 멈추지 않음", 0, 0.24, 0.01)] public double HitStopSlow = 0.12;
        [Tune("화면/타격감/흔들림 · 섬광", "화면 흔들림 배율(배)", "", 0, 2, 0.125)] public double ShakeMul = 1;
        [Tune("화면/타격감/흔들림 · 섬광", "화면 섬광 배율(배)", "", 0, 2, 0.125)] public double FlashMul = 1;
        [Tune("결투자/반응 패턴/빈틈 찌르기", "시작 지연(박)", "헛친 뒤 이만큼 지나 박 단위 자리에서 시작(+ 1타 박 뒤 타격)", 0, 1, 0.05)] public double WhiffLeadBeats = 0.5;
        [Tune("결투자/반응 패턴/추격", "시작 지연(박)", "결투자 카운터 성공 뒤(+ 1타 박 뒤 타격)", 0, 1, 0.05)] public double PunishLeadBeats = 0.5;
        [Tune("결투자/반응 패턴/연타 대응", "밀쳐내기 시작 지연(박)", "카운터가 없을 때(+ 1타 박 뒤 타격)", 0, 2, 0.125)] public double ShoveLeadBeats = 1;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "첫 패턴 전 쉬는 마디(마디)", "", 0, 2, 0.125)] public double FirstRestBars = 1;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "1단계 패턴 뒤 한 마디 쉴 확률(0 ~ 1)", "", 0, 1, 0.05)] public double RestAfter1 = 0.5;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "2단계 패턴 뒤 한 마디 쉴 확률(0 ~ 1)", "", 0, 0.6, 0.025)] public double RestAfter2 = 0.3;
        [Tune("결투자/패턴 진행/끝 · 쉬는 마디", "3단계 패턴 뒤 한 마디 쉴 확률(0 ~ 1)", "", 0, 1, 0.05)] public double RestAfter3 = 0;
        [Tune("결투자/공격 공통/에너지탄", "조준 높이(U)", "플레이어 발에서 위로", 0, 1.8 * FightUnit.S, 0.1 * FightUnit.S)] public double ShotAimY = 0.9 * FightUnit.S;
        [Tune("결투자/반응 패턴/빈틈 찌르기", "기본 거리(배)", "× 공격 사거리 배율", 0, 4, 0.25)] public double WhiffBaseRange = 2;
        [Tune("결투자/이동 · 반응/지형 따라가기", "기둥 찾는 앞쪽 거리(U)", "", 0, 2 * FightUnit.S, 0.125 * FightUnit.S)] public double VaultLook = 1 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "기둥 찾는 범위(U)", "", 0, 3.2 * FightUnit.S, 0.2 * FightUnit.S)] public double VaultRange = 1.6 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "따라 점프 여유 높이(U)", "", 0, 2 * FightUnit.S, 0.125 * FightUnit.S)] public double PlatformJumpMargin = 1 * FightUnit.S;
        [Tune("결투자/이동 · 반응/지형 따라가기", "따라 점프 가로 이동 시간(초)", "", 0, 0.9, 0.05)] public double PlatformJumpTime = 0.45;
        [Tune("결투자/이동 · 반응/지형 따라가기", "따라 점프 가로 속도 배율(배)", "× 이동 속도", 0, 3, 0.125)] public double PlatformJumpSpeedMul = 1.5;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "흔들림 빠르기(Hz)", "", 0, 3.4, 0.1)] public double IdleWobbleFreq = 1.7;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "거리 오차(U)", "이 안이면 걷지 않음", 0, 0.6 * FightUnit.S, 0.02 * FightUnit.S)] public double IdleDeadZone = 0.3 * FightUnit.S;
        [Tune("결투자/카운터/자세 · 공격", "자세 전 접근 속도 배율(배)", "× 이동 속도", 0, 2, 0.05)] public double CounterApproachMul = 1;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/일정", "시작 → 첫 위험물(박)", "맵 패턴 시작 뒤 이만큼 + 각 위험물 예고", 0, 2, 0.125)] public double MapIntroBeats = 1;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/결투자 무적 비행", "따라오는 빠르기", "", 0, 3, 0.125)] public double HoverFollow = 1.5;
        [Tune("결투장 · 맵 패턴/맵 패턴 공통/결투자 무적 비행", "떠오르는 빠르기", "", 0, 6, 0.25)] public double HoverRise = 3;
        [Tune("결투장 · 맵 패턴/유도 미사일/유도 미사일", "퍼지는 각 증가(rad)", "", 0, 0.24, 0.01)] public double MisSpreadStep = 0.12;
        [Tune("플레이어/몸 · 체력/몸", "시작 위치(X)", "", -8 * FightUnit.S, 0, 0.5 * FightUnit.S, NextRound = true)] public double PlayerStartX = -4 * FightUnit.S;
        [Tune("결투자/몸 · 체력 · 단계/몸", "시작 위치(X)", "", 0, 10 * FightUnit.S, 0.5 * FightUnit.S, NextRound = true)] public double EnemyStartX = 5 * FightUnit.S;

        [Tune("결투자/카운터/위장 예고", "위장(1 켬 · 0 끔)", "카운터 패턴이 처음엔 보통 베기처럼 보이다가 직전에만 진짜 신호", 0, 1, 1)] public double CounterDisguise = 1;
        [Tune("결투자/카운터/위장 예고", "진짜 신호 시점(박)", "카운터 자세 몇 박 전에 방패 번쩍임 · 금속음", 0, 1, 0.05)] public double CounterTellBeats = 0;
        [Tune("결투자/카운터/위장 예고", "위장 시작 단계", "0 = 1단계 · 1 = 2단계 · 2 = 3단계(격노) — 그 전에는 처음부터 카운터 자세로 보임", 0, 2, 0.125)] public double CounterDisguiseMinPhase = 1;
        [Tune("공통 규칙/카운터 크래시/판정", "저스트 판정(1 켬 · 0 끔)", "결투자 카운터 공격 순간 ± 판정 창 안에서 카운터를 ‘시작’ 해야 크래시. 0 = 둘이 자세만 겹치면 크래시", 0, 2, 1)] public double JustCrashOn = 1;
        [Tune("공통 규칙/카운터 크래시/퍼펙트 크래시", "플레이어 경직 배율(배)", "× 결투자 경직(크래시)", 0, 0.6, 0.025)] public double CrashWinStunMul = 0.3;
        [Tune("공통 규칙/카운터 크래시/퍼펙트 크래시", "결투자 추가 경직(초)", "", 0, 0.8, 0.05)] public double CrashWinBonus = 0.4;
        [Tune("공통 규칙/카운터 크래시/크래시 뒤", "크래시 뒤 결투자 카운터 휴식(초)", "", 0, 12, 0.5)] public double CrashEnemyCooldown = 6;
        [Tune("공통 규칙/카운터 크래시/크래시 뒤", "크래시 뒤 결투자 충전 처음부터(1 켬 · 0 끔)", "플레이어 충전은 그대로", 0, 2, 1)] public double CrashResetCharge = 1;
        // ───────── 카운터 크래시 개편(stage14): 판정은 박 단위, 크래시 = 플레이어 승리, 빗나감 = 튕김 ─────────
        [Tune("공통 규칙/카운터 크래시/판정", "굿 판정 창(± 박)", "결투자 카운터 공격 순간 ± 이 안에서 카운터 시작 = 크래시. 늦게 눌러도 이 안이면 인정", 0, 1, 0.0125)] public double CrashGoodBeats = 0.4;
        [Tune("공통 규칙/카운터 크래시/판정", "퍼펙트 판정 창(± 박)", "이 안이면 퍼펙트 크래시", 0, 0.25, 0.00625)] public double CrashPerfectBeats = 0.125;
        [Tune("공통 규칙/카운터 크래시/크래시(굿)", "플레이어 경직 배율(배)", "× 결투자 경직 — 1 보다 작을수록 플레이어가 먼저 움직임", 0, 0.7, 0.025)] public double CrashPlayerStunMul = 0.35;
        [Tune("공통 규칙/카운터 크래시/크래시(굿)", "결투자 데미지 배율(배)", "× 기본 데미지", 0, 6, 0.25)] public double CrashGoodDmgMul = 3;
        [Tune("공통 규칙/카운터 크래시/퍼펙트 크래시", "결투자 데미지 배율(배)", "× 기본 데미지", 0, 12, 0.5)] public double CrashPerfectDmgMul = 6;
        [Tune("공통 규칙/카운터 크래시/빗나감", "빨리 누르면 튕김(1 켬 · 0 끔)", "판정 창보다 빨리 카운터하면 반격 대신 양쪽이 튕겨 나감. 0 = 예전처럼 반격당함", 0, 1, 1)] public double EarlyBounce = 1;
        [Tune("공통 규칙/카운터 크래시/타이밍 표시", "타이밍 링(1 켬 · 0 끔)", "결투자 카운터 공격 직전, 결투자 둘레에 링이 좁혀 들어와 공격 순간에 딱 닿음 — 닿을 때 카운터를 누르면 크래시(‘표시 › 범위’ 가 켜져 있을 때)", 0, 1, 1)] public double CrashRingOn = 1;
        [Tune("공통 규칙/카운터 크래시/타이밍 표시", "링 시작(박 전)", "카운터 공격 몇 박 전부터 링이 좁혀 오기 시작하는지", 0.25, 4, 0.25)] public double CrashRingLead = 1;
        [Tune("공통 규칙/카운터 크래시/타이밍 표시", "링 시작 크기(배)", "처음 링 크기 = 결투자 몸 크기 × 이 값", 1.2, 8, 0.1)] public double CrashRingSize = 3;
        [Tune("공통 규칙/카운터 크래시/타이밍 표시", "빠름 · 늦음 글자(1 켬 · 0 끔)", "튕겼을 때 몇 박 빨랐는지 · 늦었는지 글자로", 0, 1, 1)] public double CrashTimingText = 1;
        [Tune("공통 규칙/카운터 크래시/빗나감", "튕김: 양쪽 경직(초)", "", 0, 0.7, 0.025)] public double BounceOffStun = 0.35;
        [Tune("공통 규칙/카운터 크래시/빗나감", "엇박도 크래시 모션(1 켬 · 0 끔)", "박자가 엇나가 튕겨도 hero-counter-crash 모션을 끝까지 보여 줌(둘이 마주 봄) — 보상(데미지 · 이펙트 · 글자)은 튕김 그대로", 0, 1, 1)] public double OffCrashMotion = 1;
        [Tune("공통 규칙/카운터 크래시/빗나감", "엇박 데미지 배율(배)", "튕겼을 때 결투자가 받는 데미지 × 기본 데미지 — 0 = 데미지 없음", 0, 3, 0.05)] public double OffCrashDmgMul = 0;
        [Tune("결투자/카운터/데미지", "가만히 맞을 때 경직(초)", "카운터를 안 누르고 맞으면 짧게 밀리며 굳음 — 버티기가 공짜가 아니게", 0, 0.8, 0.025)] public double EnemyCtrHitStun = 0.4;
        [Tune("결투자/카운터/자세 · 공격", "반응 카운터 준비 시간(박)", "습관 읽기 · 연타 대응 · 경직 회복 카운터: 하던 공격을 멈추고 방패를 드는 동작을 최소 이만큼 보여 준 뒤 자세 — 이 동안 때리면 보통 타격(멈출 틈)", 0, 2, 0.125)] public double EnemyCtrPrepBeats = 1;
        // ───────── 페이즈(stage15): 체력바 하나 = 페이즈 하나. 다 깎으면 다음 페이즈(새 체력바, 더 어려움). 단계(1 · 2 · 격노)는 페이즈 안의 체력 구간 ─────────
        [Tune("결투자/페이즈/공통", "페이즈 수", "1 ~ 3 — 마지막 페이즈 체력을 다 깎아야 K.O.", 1, 5, 1)] public double PhaseCount = 3;
        [Tune("결투자/페이즈/공통", "전환 시간(박)", "체력바가 바뀌는 동안 결투자 무적 · 가운데로 도약", 0, 8, 0.5)] public double PhaseBreakBeats = 4;
        [Tune("결투자/페이즈/공통", "전환 때 가운데로(1 켬 · 0 끔)", "구석에서 끝나도 다음 페이즈는 결투장 가운데에서", 0, 1, 1)] public double PhaseToCenter = 1;
        [Tune("결투자/페이즈/공통", "전환 때 플레이어 회복(비율)", "× 플레이어 최대 체력", 0, 0.3, 0.025)] public double PhaseHealPlayer = 0.15;
        [Tune("결투자/페이즈/2페이즈", "체력 배율(배)", "× 결투자 체력", 0, 2, 0.05)] public double Phase2HpMul = 1;
        [Tune("결투자/페이즈/2페이즈", "단계 가산", "페이즈 안 단계 + 이만큼(0 = 1단계부터 · 1 = 2단계부터 · 2 = 처음부터 격노) — 패턴 · 위장 · 습관 읽기 시작 단계에 쓰임", 0, 2, 1)] public double Phase2StageAdd = 1;
        [Tune("결투자/페이즈/2페이즈", "이동 속도 배율(배)", "", 0, 2.3, 0.05)] public double Phase2SpeedMul = 1.15;
        [Tune("결투자/페이즈/2페이즈", "데미지 배율(배)", "결투자 모든 공격 ×", 0, 2.4, 0.05)] public double Phase2DmgMul = 1.2;
        [Tune("결투자/페이즈/2페이즈", "쉬는 마디 배율(배)", "쉬는 마디 · 패턴 뒤 쉴 확률 × (작을수록 쉬지 않음)", 0, 1.5, 0.05)] public double Phase2RestMul = 0.75;
        [Tune("결투자/페이즈/2페이즈", "카운터 충전 시간 배율(배)", "× 결투자 1회 충전 시간(작을수록 자주)", 0, 1.6, 0.05)] public double Phase2CtrRechargeMul = 0.8;
        [Tune("결투자/페이즈/2페이즈", "맵 패턴 간격 배율(배)", "× 맵 패턴 간격(작을수록 자주)", 0, 1.6, 0.05)] public double Phase2MapEveryMul = 0.8;
        [Tune("결투자/페이즈/2페이즈", "예고 길이 배율(배)", "색 변화 · 위험 구역 · 예고음 시점 ×(작을수록 어려움)", 0, 2, 0.05)] public double Phase2TellMul = 1;
        [Tune("결투자/페이즈/3페이즈", "체력 배율(배)", "× 결투자 체력", 0, 2, 0.05)] public double Phase3HpMul = 1;
        [Tune("결투자/페이즈/3페이즈", "단계 가산", "페이즈 안 단계 + 이만큼", 0, 2, 1)] public double Phase3StageAdd = 1;
        [Tune("결투자/페이즈/3페이즈", "이동 속도 배율(배)", "", 0, 2.6, 0.05)] public double Phase3SpeedMul = 1.3;
        [Tune("결투자/페이즈/3페이즈", "데미지 배율(배)", "결투자 모든 공격 ×", 0, 2.9, 0.05)] public double Phase3DmgMul = 1.45;
        [Tune("결투자/페이즈/3페이즈", "쉬는 마디 배율(배)", "", 0, 1, 0.05)] public double Phase3RestMul = 0.5;
        [Tune("결투자/페이즈/3페이즈", "카운터 충전 시간 배율(배)", "", 0, 1.2, 0.05)] public double Phase3CtrRechargeMul = 0.6;
        [Tune("결투자/페이즈/3페이즈", "맵 패턴 간격 배율(배)", "", 0, 1.3, 0.05)] public double Phase3MapEveryMul = 0.65;
        [Tune("결투자/페이즈/3페이즈", "예고 길이 배율(배)", "", 0, 2, 0.05)] public double Phase3TellMul = 1;

        // ───────── 구석 탈출(stage15): 결투자가 한쪽 벽으로 몰리지 않게 플레이어를 뛰어넘어 반대편으로 ─────────
        [Tune("결투자/이동 · 반응/구석 탈출", "사용(1 켬 · 0 끔)", "", 0, 1, 1)] public double CornerOn = 1;
        [Tune("결투자/이동 · 반응/구석 탈출", "구석으로 보는 벽까지 거리(U)", "등 뒤 벽까지 이보다 가까우면 구석", 0, 16 * FightUnit.S, 0.5 * FightUnit.S)] public double CornerDist = 8 * FightUnit.S;
        [Tune("결투자/이동 · 반응/구석 탈출", "플레이어가 이 안이면(U)", "", 0, 16 * FightUnit.S, 0.5 * FightUnit.S)] public double CornerTrigger = 8 * FightUnit.S;
        [Tune("결투자/이동 · 반응/구석 탈출", "박마다 확률(0 ~ 1)", "쉬는 동안 박마다", 0, 1, 0.05)] public double CornerChance = 0.5;
        [Tune("결투자/이동 · 반응/구석 탈출", "넘어가 착지할 거리(U)", "플레이어 너머로", 0, 10 * FightUnit.S, 0.25 * FightUnit.S)] public double CornerLand = 5 * FightUnit.S;
        [Tune("결투자/이동 · 반응/구석 탈출", "점프 높이(U)", "플레이어 머리 위로", 0, 9 * FightUnit.S, 0.25 * FightUnit.S)] public double CornerJumpHeight = 4.5 * FightUnit.S;
        [Tune("결투자/이동 · 반응/구석 탈출", "쿨타임(초)", "", 0, 8, 0.25)] public double CornerCooldown = 4;
        [Tune("결투자/이동 · 반응/구석 탈출", "회피 · 물러나기 대신 넘어가기(1 켬 · 0 끔)", "구석에서 뒤로 빠질 자리가 없으면 넘어간다", 0, 1, 1)] public double CornerOnEvade = 1;
        [Tune("결투자/이동 · 반응/쉴 때 거리 두기", "가운데로 끌리는 정도(0 ~ 1)", "쉬는 동안 결투장 가운데 쪽으로 걷는 힘", 0, 0.6, 0.025)] public double CenterPull = 0.3;

        // ───────── 캐릭터 그림(stage16): Resources/Fight 의 png 가 있으면 코드 그림 대신 그 그림 ─────────
        [Tune("화면/캐릭터 그림/플레이어", "그림 높이(U)", "발바닥 ~ 그림 위 끝. 몸판정 높이는 1.8", 0, 6 * FightUnit.S, 0.1 * FightUnit.S)] public double HeroImgH = 3 * FightUnit.S;
        [Tune("화면/캐릭터 그림/플레이어", "아래로 내리기(U)", "그림 아래 여백만큼 — 발이 바닥에 닿게(음수 = 올리기)", 0, 1 * FightUnit.S, 0.05 * FightUnit.S)] public double HeroImgDown = 0;
        [Tune("화면/캐릭터 그림/결투자", "그림 높이(U)", "몸판정 높이는 2", 0, 7 * FightUnit.S, 0.1 * FightUnit.S)] public double FoeImgH = 3.5 * FightUnit.S;
        [Tune("화면/캐릭터 그림/결투자", "아래로 내리기(U)", "음수 = 올리기", 0, 1 * FightUnit.S, 0.05 * FightUnit.S)] public double FoeImgDown = 0;
        [Tune("화면/캐릭터 그림/결투자", "격노 때 크기 배율(배)", "", 0, 2.2, 0.05)] public double FoeImgRage = 1.1;
        // ───────── 보이는 크기(stage20i): 그림만 — 판정 · 이동 · 지형은 그대로 ─────────
        [Tune("화면/보이는 크기/전체", "넓이 배율 따라가기(0 ~ 1)", "1 = 넓이 배율을 높이면 그만큼 그림도 작아짐(기본, 판정과 같은 비율) · 0 = 넓이를 바꿔도 그림의 화면 크기 그대로 · 0.5 = 반만(1 넘으면 1)", 0, 2, 0.05)] public double ImgFollowZoom = 1;
        [Tune("화면/보이는 크기/전체", "그림 크기 배율(배)", "캐릭터 · 배경 그림 전체에 곱함(판정은 그대로)", 0, 2, 0.05)] public double ImgScaleAll = 1;
        [Tune("화면/보이는 크기/따로", "플레이어 그림 배율(배)", "전체 배율에 한 번 더 곱함", 0, 2, 0.05)] public double ImgScaleHero = 1;
        [Tune("화면/보이는 크기/따로", "결투자 그림 배율(배)", "", 0, 2, 0.05)] public double ImgScaleFoe = 1;
        [Tune("화면/보이는 크기/따로", "배경 그림 배율(배)", "따라 움직이는 배경 층(고정 층은 화면을 꽉 채우는 그대로) — 바닥선 기준", 0, 2, 0.05)] public double ImgScaleBg = 1;
        [Tune("화면/캐릭터 그림/공통", "그림이 보는 방향(1 오른쪽 · 0 왼쪽)", "그린 그림이 왼쪽을 보고 있으면 0", 0, 1, 1)] public double ImgFaceRight = 1;
        [Tune("화면/캐릭터 그림/공통", "예고 색 입히기(0 ~ 1)", "결투자 그림에 공격 종류 색을 얼마나 입힐지", 0, 1.1, 0.05)] public double ImgTint = 0.55;
        [Tune("화면/캐릭터 그림/걷기", "걷기 속도 맞춤(1 켬 · 0 끔)", "걷기 그림 재생 속도를 실제 이동 속도에 맞춤 — 발이 미끄러져 보이지 않게(결투자)", 0, 1, 1)] public double WalkSync = 1;
        [Tune("화면/캐릭터 그림/걷기", "기준 이동 속도(U/s)", "이 속도일 때 걷기 그림이 원래 속도로 재생 · 0 = 결투자 이동 속도", 0, 20 * FightUnit.S, 0.1 * FightUnit.S)] public double WalkRefSpeed = 0;
        [Tune("화면/캐릭터 그림/걷기", "뒷걸음 그림 없을 때 거꾸로(1 켬 · 0 끔)", "플레이어에게서 멀어질 때 foe-walk-back 이 없으면 foe-walk 를 거꾸로 재생(문워크 방지)", 0, 1, 1)] public double WalkBackReverse = 1;
        // ───────── 캐릭터 흐름(그림만 — 판정 · 조작은 그대로, 입력이 오면 언제나 즉시 끊김) ─────────
        [Tune("화면/캐릭터 흐름/켜기", "흐름 쓰기(1 켬 · 0 끔)", "끄면 연결 · 준비 · 긴 대기 없이 예전처럼 상태 그림만", 0, 1, 1)] public double FlowOn = 1;
        [Tune("화면/캐릭터 흐름/준비 · 대기", "준비 시간(초)", "루프로 정한 준비 그림의 기본 시간 — 이만큼 되풀이한 뒤 준비 → 대기 연결(-ready-end) → 대기. 그림별 루프 · 1회 · 시간은 아래 ‘준비 그림별 재생’ 표 · 0 = 그림 길이만큼", 0, 10, 0.05)] public double ReadySec = 1.5;
        [Tune("화면/캐릭터 흐름/준비 · 대기", "긴 대기 간격(초)", "대기(-idle)가 이만큼 이어지면 긴 대기(-idle-long)를 한 번 재생하고 다시 대기", 1, 30, 0.5)] public double LongWaitSec = 8;
        [Tune("화면/캐릭터 흐름/준비 · 대기", "긴 대기 고르기(1 무작위 · 0 순서)", "긴 대기를 여러 개(-idle-long · -idle-long2 …) 그렸을 때", 0, 1, 1)] public double LongWaitRandom = 0;
        [Tune("화면/캐릭터 흐름/공중", "긴 낙하로 넘어가기(초)", "공중 몸통(hero-air)이 마지막 프레임(최대 낙하 속도 근처)에 이만큼 머물면 긴 낙하 루프(hero-fall)로 · 0 = 닿자마자", 0, 1, 0.05)] public double FallLoopSec = 0.1;
        [Tune("결투자/카운터/습관 읽기", "습관 읽기(1 켬 · 0 끔)", "같은 간격으로 계속 치면 다음 타이밍에 카운터", 0, 1, 1)] public double HabitOn = 1;
        [Tune("결투자/카운터/습관 읽기", "연속 공격 횟수(회)", "이만큼 같은 간격이면 읽힘", 0, 8, 0.5)] public double HabitCount = 4;
        [Tune("결투자/카운터/습관 읽기", "간격 오차(초)", "", 0, 0.12, 0.005)] public double HabitTol = 0.06;
        [Tune("결투자/카운터/습관 읽기", "예상 타이밍보다 먼저 자세(초)", "", 0, 0.2, 0.0125)] public double HabitLead = 0.1;
        [Tune("결투자/카운터/습관 읽기", "습관 카운터 뒤 휴식(초)", "", 0, 8, 0.5)] public double HabitCooldown = 4;
        [Tune("결투자/카운터/습관 읽기", "시작 단계", "0 = 1단계 · 1 = 2단계 · 2 = 3단계(격노)", 0, 1, 0.125)] public double HabitMinPhase = 0;
        [Tune("결투자/카운터/엇박 카운터", "확률(0 ~ 1)", "카운터 공격이 박에서 밀려 나옴", 0, 0.6, 0.025)] public double OffbeatChance = 0.3;
        [Tune("결투자/카운터/엇박 카운터", "밀림(박)", "", 0, 1, 0.05)] public double OffbeatShift = 0.5;
        [Tune("결투자/카운터/엇박 카운터", "시작 단계", "0 = 1단계 · 1 = 2단계 · 2 = 3단계(격노)", 0, 2, 0.125)] public double OffbeatMinPhase = 1;

        // ── 계산 ──
        public double AtkMul(int combo) => combo >= 3 ? Atk3Mul : combo == 2 ? Atk2Mul : Atk1Mul;
        public double AtkReach(int combo) => combo >= 3 ? Atk3Reach : combo == 2 ? Atk2Reach : Atk1Reach;
        public double AtkHalfH(int combo) => combo >= 3 ? Atk3HalfH : combo == 2 ? Atk2HalfH : Atk1HalfH;
        public int DashCapI => Math.Max(1, (int)Math.Round(DashCap));
        public int CtrCapI => Math.Max(1, (int)Math.Round(CtrCap));
        public int EnemyCtrCapI => Math.Max(0, (int)Math.Round(EnemyCtrCap));

        public FightTune Clone() => MoriJson.Deserialize<FightTune>(MoriJson.Serialize(this));

        /// <summary>
        /// NaN · 무한대만 기본값으로 바로잡는다(불러온 파일이 망가져도 게임이 서도록). 값의 한계는 두지 않는다 —
        /// 0 이하가 되면 멈추거나 나눗셈이 깨지는 시간 · 속도 값은 쓰는 곳에서 아주 작은 양수로 받친다.
        /// </summary>
        public void Clamp()
        {
            var def = new FightTune();
            foreach (var f in typeof(FightTune).GetFields())
            {
                if (f.FieldType != typeof(double)) continue;
                double v = (double)f.GetValue(this);
                if (double.IsNaN(v) || double.IsInfinity(v)) f.SetValue(this, (double)f.GetValue(def));
            }
        }

        /// <summary>이 튜닝 값이 길이 단위(U · U/s · U/s² · 위치 X · 카메라 넓이)인지 — 단위 바꾸기에 씀.</summary>
        public static bool IsLength(string field, string name)
        {
            if (field == nameof(CamZoom)) return false;        // 넓이 배율은 단위 없는 배율(기본 1)
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("(U)") || name.Contains("± U)") || name.Contains("U/s") || name.Contains("(X)");
        }

        /// <summary>예전 단위(V 1)로 저장된 값을 지금 단위로: 길이 값에 s 를 곱함.</summary>
        public void ScaleLengths(double s)
        {
            foreach (var f in typeof(FightTune).GetFields())
            {
                if (f.FieldType != typeof(double)) continue;
                var a = (TuneAttribute)Attribute.GetCustomAttribute(f, typeof(TuneAttribute));
                if (a == null || !IsLength(f.Name, a.Name)) continue;
                f.SetValue(this, (double)f.GetValue(this) * s);
            }
        }

        /// <summary>0 이하 · 아주 작은 값이 들어와도 멈추지 않게(충전 시간 0 = 무한 루프, 대쉬 시간 0 = 나눗셈) 받치는 하한.</summary>
        public static double Pos(double v, double floor = 0.01) => v > floor ? v : floor;
    }

    /// <summary>명세서 FSM 의 13 상태.</summary>
    public enum PState { Idle, Ready, Move, Jump, DoubleJump, Fall, Dash, Attack, Counter, Stun, Knockback, WallBounce, EventLock }

    public enum FsmEv { Slash, Mech, CounterAtk, CounterStart, Dash, BackDash, Jump, DoubleJump, Land, FaceTurn, Turn, WallBounce, NearWallBounce, Recovered }

    /// <summary>
    /// 플레이어 · 결투자 공통: 몸(위치 · 속도 · 몸판정), 카운터 스택 · 반격 상태, 경직 · 넉백 · 월 바운스(명세서 그대로 — 양쪽 같은 규칙).
    ///  · 넉백: 시작 때 한 번 정한 방향(중심 → 중심), 거리 = 중심 거리 × 배율(최소 · 최대), 일정 속도. 거리를 다 가거나 충돌하면 끝.
    ///  · 월 바운스: 넉백 중 벽 · 바닥 · 천장 · 기둥에 처음 닿으면 속도 초기화 → 반사 방향으로 반동 속도, 경직 추가(+근접 월 바운스). 반동은 중력 · 바닥 마찰로 자연스럽게 멈춤.
    ///  · 경직: 한 사이클(카운터 한 번)에 최대치까지 누적. 넉백 · 월 바운스가 끝난 뒤 흐른다(그동안은 상위 상태).
    /// </summary>
    public class Fighter
    {
        public readonly FightTune T;
        public Arena Map;
        public readonly double Half, Height;
        public double X, Y, Vx, Vy;
        public int Facing = 1;
        public bool Grounded = true;
        public double StunT, StunCycle;
        public CombatRules.Knock Knock;
        public bool Bouncing;
        public double CtrStack, CtrRech, CtrCd;
        protected double ctrStateT, bounceT;

        public Fighter(FightTune t, Arena map, double half, double height)
        {
            T = t; Map = map ?? Arena.Standard(); Half = half; Height = height;
        }

        public Box Hurtbox => new Box(X - Half, X + Half, Y, Y + Height);
        public double Cy => Y + Height / 2;
        public bool InCounter => ctrStateT > 0;
        public bool Knocked => Knock != null && !Knock.Done;
        public bool Disabled => Knocked || Bouncing || StunT > 0;
        public bool CounterReady => CtrStack >= 1 && CtrCd <= 0;

        /// <summary>카운터 스택 충전(speed = 그루브 가속 등).</summary>
        public void ChargeCounter(double dt, int cap, double recharge, double speed = 1)
        {
            if (CtrCd > 0) CtrCd -= dt;
            recharge = FightTune.Pos(recharge);
            if (CtrStack < cap) { CtrRech += dt * Math.Max(0, speed); while (CtrRech >= recharge && CtrStack < cap) { CtrRech -= recharge; CtrStack += 1; } }
            else { CtrRech = 0; CtrStack = Math.Min(CtrStack, cap); }
        }

        /// <summary>하던 동작을 모두 끊는다(넉백 · 경직이 시작될 때).</summary>
        protected virtual void Interrupt() { ctrStateT = 0; }

        /// <summary>넉백 + 경직 사이클 시작(카운터 성공 · 크래시 · 결투자 카운터). (ax, ay) = 때린 쪽 중심.</summary>
        public void StartReaction(double ax, double ay, double stun)
        {
            Interrupt();
            Vx = Vy = 0;
            Bouncing = false;
            StunCycle = 0;
            StunT = 0;
            AddStun(stun);
            Knock = CombatRules.Start(ax, ay, X, Cy, X < ax ? -1 : 1, T);
        }

        public double AddStun(double s)
        {
            double a = CombatRules.AddStun(ref StunCycle, s, T.StunCap);
            StunT += a;
            return a;
        }

        /// <summary>넉백 · 월 바운스 · 경직 한 프레임. 그동안 다른 행동은 못 함 → true.</summary>
        public bool TickReaction(double dt, out bool bounced, out bool nearWall, out bool recovered)
        {
            bounced = nearWall = recovered = false;
            if (Knocked)
            {
                double x = X, y = Cy;
                CombatRules.Step(ref x, ref y, Knock, dt, Map, Half, Height, out bool hitX, out bool hitY);
                X = x; Y = Math.Max(0, y - Height / 2);
                if (hitX || hitY)
                {
                    // 월 바운스(넉백 1회당 처음 충돌 1회): 속도 초기화 → 반사 방향 × 반동 속도
                    bounced = true;
                    double dx = Knock.Dx, dy = Knock.Dy;
                    if (hitX) dx = -dx; else dy = -dy;
                    Vx = dx * T.BounceSpeed;
                    Vy = dy * T.BounceSpeed;
                    Grounded = false;
                    Bouncing = true;
                    bounceT = 0;
                    AddStun(T.StunWall);
                    double cx = X - Knock.StartX, cy = Cy - Knock.StartY;
                    nearWall = Math.Sqrt(cx * cx + cy * cy) <= T.NearWallDist;
                    if (nearWall) AddStun(T.StunNearWall);
                }
                else if (Knock.Done) { Vx = Vy = 0; Grounded = false; }
                return true;
            }
            if (Bouncing)
            {
                bounceT += dt;
                Body(dt, T.BounceGravityMul, false, out _);
                if (Grounded) Vx = Toward(Vx, 0, T.BounceFriction * dt);
                if ((Grounded && Math.Abs(Vx) < T.BounceStopSpeed) || bounceT > T.BounceMaxTime) { Bouncing = false; Vx = 0; }
                return true;
            }
            if (StunT > 0)
            {
                StunT -= dt;
                Body(dt, T.BounceGravityMul, false, out _);
                if (Grounded) Vx = Toward(Vx, 0, T.BounceFriction * dt);
                if (StunT <= 0) { StunT = 0; StunCycle = 0; recovered = true; }
                return true;
            }
            return false;
        }

        /// <summary>중력 · 가로 이동 · 착지. landed = 이번 프레임에 땅에 닿음.</summary>
        /// <summary>이 몸에만 곱하는 중력 배율(결투자 = 결투자 중력 배율).</summary>
        protected virtual double OwnGravity => 1;

        protected void Body(double dt, double gravityMul, bool dropThrough, out bool landed)
        {
            landed = false;
            if (Vx != 0)
            {
                X = Map.MoveX(X, Y, Vx * dt, Half, Height, out bool wall);
                if (wall) Vx = 0;
            }
            if (Grounded && Vy <= 0 && Map.OnSupport(X, Y, Half)) { Vy = 0; return; }
            Grounded = false;
            Vy = Math.Max(Vy - T.Gravity * OwnGravity * gravityMul * FeelMul(Vy) * dt, -T.MaxFall);
            Y = Map.MoveY(X, Y, Vy * dt, Half, Height, dropThrough, out bool l, out bool bonk);
            if (bonk) Vy = 0;
            if (l) { Vy = 0; Grounded = true; landed = true; }
        }

        /// <summary>올라갈 때 = 공통 중력, 정점 근처 = 가볍게(떠 있는 느낌), 떨어질 때 = 낙하 중력 배율(자체 — 낙하가 너무 급하지 않게).</summary>
        protected double FeelMul(double vy) => vy > T.ApexBand ? 1 : vy > -T.ApexBand ? T.ApexGravityMul : T.FallGravityMul;

        protected static double Toward(double v, double target, double step) => v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
    }

    /// <summary>
    /// 플레이어 상태 기계(명세서 FSM v0.0.1). 상태 우선순위: Event Lock &gt; Wall Bounce &gt; Knockback &gt; Stun &gt; Counter &gt; Dash &gt; Attack &gt; Double Jump &gt; Jump &gt; Fall &gt; Move &gt; Ready &gt; Idle.
    /// 위치 고정 · 콤보 입력 시간 · 반격 상태 · 카운터 공격 지연 · 대쉬는 서로 독립된 타이머.
    ///  · 공격: 속도 무시 → 바라보는 쪽 0.2U 도약(공중도) → 위치 고정 0.25초, 검기 즉시 · 기계 0.15초 뒤. 맞혔을 때만 콤보(사이클당 1회). 대쉬 · 카운터가 캔슬.
    ///  · 카운터: 위치 고정 0.3초 · 반격 상태 0.3초 · 0.1초 뒤 카운터 공격 1회. 대쉬 · 공격을 캔슬. 카운터 중에는 대쉬 불가.
    ///  · 대쉬: 속도 무시, 8방향(입력 없으면 바라보는 쪽), 공통 중력 적용, 바라보는 방향은 그대로(뒤로 누르면 백대쉬). 대쉬 중 입력은 카운터만(공격은 버퍼).
    ///  · 점프: X 속도 유지. 더블 점프: 입력 방향(없으면 바라보는 쪽) 수평 추진 우선. ↓ + 점프 = 발판 내려가기(자체).
    /// </summary>
    public sealed class PlayerFsm : Fighter
    {
        public const double HALF = 0.35 * FightUnit.S, HEIGHT = 1.8 * FightUnit.S;
        public int ComboIndex, AtkDir = 1;
        public double DashStack, DashRech, HurtT;
        /// <summary>재사용 대기 남은 시간(초): 행동이 끝난 뒤 다음 같은 행동까지(튜닝 ‘재사용 대기’). ‘쿨타임 없음’ 과 상관없이 걸림.</summary>
        public double DashGapT, AtkGapT, CtrGapT;
        public double CtrRechargeSpeed = 1;
        public bool EventLock;
        public PState State { get; private set; } = PState.Idle;
        public bool Turning => oppHold >= T.TurnHold && State == PState.Move;
        public readonly List<(FsmEv ev, int combo)> Events = new List<(FsmEv, int)>();

        double lockT, ctrLockT, ctrAtkT = -1, mechT = -1, comboT, dashT, djT, readyT, oppHold, dropT, hangT;
        bool djUsed, bufAtk, cycleHit;
        /// <summary>공중에서 가장 높았던 발 높이(착지 감속) · 착지 감속 남은 시간 · 세기(0 ~ 1).</summary>
        double airTop, landSlowT, landSlowDur, landSlowK;
        /// <summary>지금 최고 속도 배율(착지 감속 중엔 1보다 작음, 서서히 1로).</summary>
        double LandMul => landSlowT > 0 && landSlowDur > 0 ? 1 - landSlowK * (landSlowT / landSlowDur) : 1;
        /// <summary>마지막 착지의 단계(0 = 감속 없음 · 1 ~ 3) — 화면 · 연결 그림에서 쓸 수 있게.</summary>
        public int LandTier { get; private set; }
        double MoveCap => T.MoveSpeed * LandMul;

        public PlayerFsm(FightTune t, Arena map = null) : base(t, map, FightTune.Pos(t.PlayerHalf, 0.05 * FightUnit.S), FightTune.Pos(t.PlayerHeight, 0.2 * FightUnit.S))
        {
            DashStack = t.DashCapI;
            CtrStack = t.CtrCapI;
        }

        public bool Locked => lockT > 0 || ctrLockT > 0;
        public bool Dashing => dashT > 0;
        public bool Invuln => (dashT > 0 && T.DashInvuln >= 0.5) || HurtT > 0 || EventLock || UltInvT > 0 || UltInvulnT > 0;
        /// <summary>궁극기: 게이지 · 남은 시전(폭격) 시간 · 남은 위치 고정 · 남은 무적(시작부터) · 끝난 뒤 남은 무적.</summary>
        public double Ult, UltT, UltLockT, UltInvT, UltInvulnT;
        public bool CanAct => !EventLock && !Disabled;
        /// <summary>지금 누르면 공격이 바로 나가는지(위치 고정 · 재사용 대기 · 대쉬 · 카운터 고정이 없음).</summary>
        public bool AttackFree => CanAct && ctrLockT <= 0 && dashT <= 0 && lockT <= 0 && AtkGapT <= 0;
        /// <summary>공격 입력을 받을 수 있는지(곧 나갈 수 있음 — 카운터 고정 중엔 안 받음).</summary>
        public bool AttackAccept => CanAct && ctrLockT <= 0;
        public double ComboLeft => comboT;

        /// <summary>기본 공격 판정 상자(몸 중심 기준, 방향 = 공격 입력 순간의 바라보는 방향).</summary>
        public Box AttackBox(int combo)
        {
            double cy = Cy, r = T.AtkReach(combo), hh = T.AtkHalfH(combo);
            return AtkDir > 0 ? new Box(X - T.AtkBack, X + r, cy - hh, cy + hh) : new Box(X - r, X + T.AtkBack, cy - hh, cy + hh);
        }

        public Box CounterBox() => new Box(X - T.CtrHalfW, X + T.CtrHalfW, Cy - T.CtrHalfH, Cy + T.CtrHalfH);

        // ───────── 입력 ─────────
        public bool Attack()
        {
            if (!CanAct || ctrLockT > 0) return false;
            if (dashT > 0 || lockT > 0 || AtkGapT > 0) { bufAtk = true; return true; }     // 대쉬 · 위치 고정 · 재사용 대기 중: 끝나면 이어서(입력 버퍼)
            DoAttack();
            return true;
        }

        void DoAttack()
        {
            bufAtk = false;
            ComboIndex = comboT > 0 && cycleHit ? Math.Min(3, ComboIndex + 1) : 1;
            comboT = 0;
            cycleHit = false;
            AtkDir = Facing;
            Vx = Vy = 0;                                        // 현재 X/Y 속도 무시
            X = Map.MoveX(X, Y, Facing * T.AtkStep, Half, Height, out _);   // 즉시 전진 도약(벽이면 거기까지)
            lockT = T.AtkLockOf(ComboIndex);                    // 타별 위치 고정(0 이면 공통)
            AtkGapT = Math.Max(0, lockT) + Math.Max(0, T.AtkGap);
            mechT = T.MechDelay;
            readyT = 0;
            State = PState.Attack;
            Events.Add((FsmEv.Slash, ComboIndex));
        }

        /// <summary>이번 공격 사이클의 판정(검기 · 기계)이 맞음: 그 시점부터 콤보 입력 시간이 흐른다. 콤보 단계는 사이클당 한 번만 오른다.</summary>
        public void HitLanded()
        {
            cycleHit = true;
            comboT = T.ComboWindow;
        }

        public bool Counter()
        {
            if (!CanAct || !CounterReady || ctrLockT > 0 || CtrGapT > 0) return false;
            lockT = 0; dashT = 0; mechT = -1; bufAtk = false;      // 공격 · 대쉬 캔슬
            CtrStack -= 1;
            CtrCd = T.CtrCooldown;
            Vx = Vy = 0;
            ctrLockT = T.CtrLock;
            CtrGapT = Math.Max(0, T.CtrLock) + Math.Max(0, T.CtrGap);
            ctrStateT = T.CtrState;
            ctrAtkT = T.CtrAtkDelay;
            readyT = 0;
            State = PState.Counter;
            Events.Add((FsmEv.CounterStart, 0));
            return true;
        }

        /// <summary>대쉬(8방향). dx · dy = 누른 방향(−1 · 0 · +1, 위 = +1). 없으면 바라보는 쪽. 바라보는 방향은 바뀌지 않는다.</summary>
        public bool Dash(int dx, int dy)
        {
            if (!CanAct || DashStack < 1 || ctrLockT > 0 || DashGapT > 0) return false;
            DashStack -= 1;
            lockT = 0; mechT = -1;                               // 공격 위치 고정 캔슬
            if (dx == 0 && dy == 0) dx = Facing;
            double n = Math.Sqrt(dx * dx + dy * dy), v = T.DashDist / FightTune.Pos(T.DashTime, 0.005);
            Vx = dx / n * v;                                     // 현재 속도 무시
            Vy = dy / n * v;
            dashT = T.DashTime;
            DashGapT = Math.Max(0, T.DashTime) + Math.Max(0, T.DashGap);     // 대쉬 시간 + 재사용 대기
            readyT = 0;
            State = PState.Dash;
            Events.Add((dx == -Facing && dy == 0 ? FsmEv.BackDash : FsmEv.Dash, 0));
            return true;
        }

        /// <summary>점프 · 더블 점프. dir = 누른 좌우, down = ↓ 를 누른 채(발판이면 내려가기).</summary>
        public bool Jump(int dir, bool down = false)
        {
            if (!CanAct || lockT > 0 || ctrLockT > 0 || dashT > 0) return false;
            if (Grounded)
            {
                if (down && Y > 0.01 * FightUnit.S && !OnSolid()) { dropT = T.DropThroughTime; Grounded = false; Vy = -Math.Abs(T.DropThroughVy); return true; }
                Vy = T.JumpV;                                    // 지상 X 속도 유지
                Grounded = false;
                readyT = 0;
                Events.Add((FsmEv.Jump, 0));
                return true;
            }
            if (djUsed) return false;
            djUsed = true;
            int d = dir != 0 ? dir : Facing;
            if (dir != 0) Facing = dir;
            Vx += d * T.DJumpH;                                  // 현재 속도에 추진력을 더한다(수평 우선)
            Vy = Math.Max(Vy, 0) + T.DJumpV;
            djT = T.DJumpTime;
            Events.Add((FsmEv.DoubleJump, 0));
            return true;
        }

        bool OnSolid()
        {
            foreach (var s in Map.Solids) if (X + Half > s.X0 && X - Half < s.X1 && Math.Abs(s.Y1 - Y) < 1e-3) return true;
            return false;
        }

        /// <summary>보통 공격에 맞음: 데미지는 바깥에서, 여기선 잠깐 무적만(넉백 · 경직 없음).</summary>
        public void Hurt() => HurtT = T.HurtIFrames;

        protected override void Interrupt()
        {
            base.Interrupt();
            lockT = ctrLockT = dashT = djT = 0;
            ctrAtkT = mechT = -1;
            bufAtk = false;
        }

        /// <summary>결투자의 카운터 · 카운터 크래시: 넉백 + 경직(플레이어 넉백 · 경직은 이것뿐).</summary>
        public void KnockedBy(double ax, double ay, double stun) => StartReaction(ax, ay, stun);

        // ───────── 매 프레임 ─────────
        /// <summary>dir = 누른 좌우(−1 · 0 · +1).</summary>
        public void Tick(double dt, int dir)
        {
            if (HurtT > 0) HurtT -= dt;
            if (dropT > 0) dropT -= dt;
            if (landSlowT > 0) landSlowT -= dt;
            airTop = Grounded ? Y : Math.Max(airTop, Y);
            if (DashGapT > 0) DashGapT -= dt;
            if (AtkGapT > 0) AtkGapT -= dt;
            if (CtrGapT > 0) CtrGapT -= dt;
            double dr = FightTune.Pos(T.DashRecharge);
            if (DashStack < T.DashCapI) { DashRech += dt; while (DashRech >= dr && DashStack < T.DashCapI) { DashRech -= dr; DashStack += 1; } }
            else { DashRech = 0; DashStack = Math.Min(DashStack, T.DashCapI); }
            ChargeCounter(dt, T.CtrCapI, T.CtrRecharge, CtrRechargeSpeed);
            // 콤보 입력 시간(위치 고정과 독립)
            if (comboT > 0) { comboT -= dt; if (comboT <= 0) { comboT = 0; if (lockT <= 0 && !bufAtk) ComboIndex = 0; } }
            if (bufAtk && AtkGapT <= 0 && lockT <= 0 && dashT <= 0 && ctrLockT <= 0 && CanAct) DoAttack();     // 재사용 대기가 끝나면 눌러 둔 공격

            if (UltLockT > 0)
            {
                // 궁극기 시전: 그 자리에 완전히 고정(미끄러짐 · 중력 없음 — 공중이면 공중에 뜬 채로)
                Vx = Vy = 0;
                State = PState.EventLock;
                return;
            }
            if (EventLock)
            {
                Vx = Toward(Vx, 0, T.Decel * dt);
                Body(dt, 1, false, out _);
                State = PState.EventLock;
                return;
            }
            // Damage 상태군
            if (TickReaction(dt, out bool bounced, out bool nearWall, out bool recovered))
            {
                if (bounced) Events.Add((nearWall ? FsmEv.NearWallBounce : FsmEv.WallBounce, 0));
                State = Bouncing ? PState.WallBounce : Knocked ? PState.Knockback : PState.Stun;
                if (recovered) { Events.Add((FsmEv.Recovered, 0)); readyT = T.ReadyDuration; }
                return;
            }
            // 카운터(위치 고정 · 반격 상태 · 카운터 공격 지연 — 각각 독립)
            if (ctrStateT > 0) ctrStateT -= dt;
            if (ctrAtkT > 0) { ctrAtkT -= dt; if (ctrAtkT <= 0) { ctrAtkT = -1; Events.Add((FsmEv.CounterAtk, 0)); } }
            if (ctrLockT > 0)
            {
                ctrLockT -= dt;
                Vx = Vy = 0;
                State = PState.Counter;
                if (ctrLockT <= 0) { ctrLockT = 0; readyT = T.ReadyDuration; }
                return;
            }
            // 기계 무기(검기 0.15초 뒤)
            if (mechT > 0) { mechT -= dt; if (mechT <= 0) { mechT = -1; Events.Add((FsmEv.Mech, ComboIndex)); } }
            if (dashT > 0)
            {
                dashT -= dt;
                Vy -= T.Gravity * T.DashGravityMul * dt;
                X = Map.MoveX(X, Y, Vx * dt, Half, Height, out bool w);
                if (w) Vx = 0;
                Y = Map.MoveY(X, Y, Vy * dt, Half, Height, false, out bool landed, out bool bonk);
                if (bonk) Vy = 0;
                if (landed && Vy <= 0) { Vy = 0; if (!Grounded) Land(); }
                else if (!landed && Y > 0 && !Map.OnSupport(X, Y, Half)) Grounded = false;
                State = PState.Dash;
                if (dashT <= 0)
                {
                    dashT = 0;
                    double cap = T.MoveSpeed * T.DashCarry;
                    Vx = Math.Sign(Vx) * Math.Min(Math.Abs(Vx), cap);
                    Vy = Vy > 0 ? Math.Min(Vy, T.JumpV * T.DashEndUpMul) : Math.Max(Vy, -Math.Abs(T.DashEndFall));
                    if (!Grounded) hangT = T.PostDashHang;
                    readyT = T.ReadyDuration;
                    if (bufAtk && AtkGapT <= 0) DoAttack();
                }
                return;
            }
            if (lockT > 0)
            {
                lockT -= dt;
                // 위치 고정 중에도 바라보기 전환은 가능(판정 방향은 그대로)
                if (dir != 0 && dir != Facing) { Facing = dir; Events.Add((FsmEv.FaceTurn, 0)); }
                Vx = Vy = 0;
                State = PState.Attack;
                if (lockT <= 0) { lockT = 0; readyT = T.ReadyDuration; if (bufAtk && AtkGapT <= 0) DoAttack(); }
                return;
            }
            if (djT > 0) djT -= dt;
            // 이동 · 방향 전환(Turn ≥ TurnHold) · 바라보기 전환(Facing Turn &lt; TurnHold)
            int movingDir = Math.Abs(Vx) > 0.5 * FightUnit.S ? Math.Sign(Vx) : 0;
            if (dir == 0) { oppHold = 0; Vx = Toward(Vx, 0, (Grounded ? T.Decel : T.Decel * T.AirDecelMul) * dt); }
            else if (movingDir != 0 && dir != movingDir)
            {
                if (Facing != dir) { Facing = dir; Events.Add((FsmEv.FaceTurn, 0)); }
                oppHold += dt;
                if (oppHold >= T.TurnHold)
                {
                    if (oppHold - dt < T.TurnHold) Events.Add((FsmEv.Turn, 0));
                    Vx = Toward(Vx, dir * MoveCap, T.TurnAccel * dt);
                }
            }
            else
            {
                oppHold = 0;
                Facing = dir;
                if (Math.Abs(Vx) <= MoveCap) Vx = Toward(Vx, dir * MoveCap, T.Accel * dt);
            }
            if (Math.Abs(Vx) > MoveCap && djT <= 0) Vx = Toward(Vx, Math.Sign(Vx) * MoveCap, T.Decel * (landSlowT > 0 ? 1 : T.OverSpeedDecelMul) * dt);   // 대쉬 · 더블 점프 가속이 서서히 풀림 · 착지 감속 중엔 낮아진 최고 속도로   // 대쉬 · 더블 점프 가속이 서서히 풀림
            bool wasAir = !Grounded;
            if (hangT > 0) hangT -= dt;
            Body(dt, djT > 0 ? T.DJumpGravityMul : hangT > 0 ? T.PostDashGravityMul : 1, dropT > 0, out bool l2);
            if (l2 && wasAir) Land();
            // 상태 결정(우선순위 순)
            if (!Grounded) State = djT > 0 || (djUsed && Vy > 0) ? PState.DoubleJump : Vy > 0 ? PState.Jump : PState.Fall;
            else if (dir != 0 || Math.Abs(Vx) > 0.3 * FightUnit.S) { State = PState.Move; readyT = 0; }
            else if (readyT > 0) { readyT -= dt; State = PState.Ready; }
            else State = PState.Idle;
        }

        void Land()
        {
            Events.Add((FsmEv.Land, 0));
            Grounded = true;
            // 착지 감속: 떨어진 높이(공중 최고점 − 착지점)를 3단계로 나눠 가로 속도를 줄이고 잠깐 최고 속도를 낮춤
            double fallH = Math.Max(0, airTop - Y);
            airTop = Y;
            // 3단계: 높이가 단계 기준 이상이면 그 단계(가장 높은 단계부터 확인)
            LandTier = T.LandSlowOn < 0.5 ? 0 : fallH >= T.LandT3H ? 3 : fallH >= T.LandT2H ? 2 : fallH >= T.LandT1H ? 1 : 0;
            if (LandTier > 0)
            {
                double slow = LandTier == 3 ? T.LandT3Slow : LandTier == 2 ? T.LandT2Slow : T.LandT1Slow;
                double time = LandTier == 3 ? T.LandT3Time : LandTier == 2 ? T.LandT2Time : T.LandT1Time;
                slow = Math.Max(0, Math.Min(1, slow));
                Vx *= 1 - slow;
                landSlowK = slow;
                landSlowDur = landSlowT = Math.Max(0, time);
            }
            djUsed = false;
            djT = 0;
            readyT = T.ReadyDuration;
        }
    }

    public static class CombatRules
    {
        /// <summary>넉백 거리 = 중심 사이 거리 × 배율, 최소 · 최대로 자른다.</summary>
        public static double KnockDist(double centerDist, FightTune t) => Math.Max(t.KnockMin, Math.Min(t.KnockMax, centerDist * t.KnockMul));

        public sealed class Knock
        {
            public double Dx, Dy, Left, Speed, StartX, StartY;
            public bool Done;
        }

        /// <summary>넉백 시작: 방향 = 때린 쪽 중심 → 맞은 쪽 중심(시작할 때 한 번). 너무 위로 뜨지 않게 세로 성분은 줄인다(자체).</summary>
        public static Knock Start(double px, double py, double ex, double ey, int fallbackDir, FightTune t)
        {
            double dx = ex - px, dy = ey - py, d = Math.Sqrt(dx * dx + dy * dy);
            double ux, uy;
            if (d < 1e-6 || Math.Abs(dx) < 1e-6) { ux = fallbackDir; uy = 0.2; }
            else { ux = dx / d; uy = dy / d; }
            uy = Math.Max(-t.KnockLiftCap, Math.Min(t.KnockLiftCap, uy + t.KnockLift));
            double n = Math.Sqrt(ux * ux + uy * uy);
            return new Knock { Dx = ux / n, Dy = uy / n, Left = KnockDist(d, t), Speed = FightTune.Pos(t.KnockSpeed, 0.1 * FightUnit.S), StartX = ex, StartY = ey };
        }

        /// <summary>
        /// 넉백 한 걸음(중심 좌표, 일정 속도). 결투장 끝 벽 · 기둥 옆면 = hitX, 바닥 · 천장 · 기둥 윗면 = hitY. 충돌하거나 거리를 다 가면 Done.
        /// </summary>
        public static void Step(ref double x, ref double y, Knock k, double dt, Arena map, double half, double h, out bool hitX, out bool hitY)
        {
            hitX = hitY = false;
            if (k.Done) return;
            double step = Math.Min(k.Left, k.Speed * dt);
            double nx = x + k.Dx * step, ny = y + k.Dy * step;
            k.Left -= step;
            if (nx < -map.HalfW + half) { nx = -map.HalfW + half; hitX = true; }
            if (nx > map.HalfW - half) { nx = map.HalfW - half; hitX = true; }
            if (ny - h / 2 < 0) { ny = h / 2; if (k.Dy < 0) hitY = true; }
            if (!map.OpenTop && ny + h / 2 > map.Ceil) { ny = map.Ceil - h / 2; hitY = true; }
            foreach (var s in map.Solids)
            {
                var body = new Box(nx - half, nx + half, ny - h / 2 + 0.02 * FightUnit.S, ny + h / 2);
                if (!s.Overlaps(body)) continue;
                if (y - h / 2 >= s.Y1 - 1e-3) { ny = s.Y1 + h / 2; hitY = true; }
                else { nx = x < s.Cx ? s.X0 - half - 1e-4 : s.X1 + half + 1e-4; hitX = true; }
            }
            if (hitX) hitY = false;                                          // 모서리는 벽(X) 우선
            x = nx; y = ny;
            if (hitX || hitY || k.Left <= 1e-9) k.Done = true;
        }

        /// <summary>경직 한 사이클: 쌓이되 최대치까지. 실제로 더해진 초.</summary>
        public static double AddStun(ref double cycle, double add, double cap)
        {
            double room = Math.Max(0, cap - cycle);
            double a = Math.Min(room, add);
            cycle += a;
            return a;
        }
    }
}
