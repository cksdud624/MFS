using System;
using System.Collections.Generic;
using System.Linq;
using Mori.Core.Rhythm;

namespace Mori.Core.Fight
{
    /// <summary>결투자 공격 종류 — 피하는 법이 다르다. 투사체를 뺀 모든 공격은 카운터하면 카운터 성공(경직 · 넉백). 투사체는 카운터로 무효화만.</summary>
    public enum HitKind
    {
        Melee,       // 근접 베기: 무빙(거리)으로 피하거나 카운터
        Low,         // 바닥 쓸기 · 충격파: 점프로 피하거나 카운터
        Projectile,  // 에너지탄: 무빙으로 피함. 카운터 = 무효화만(보상 없음)
        Charge,      // 돌진: 대쉬 무적 · 뛰어넘기, 또는 카운터
    }

    /// <summary>패턴의 한 타격. Beat = 패턴 시작(마디 첫 박)부터 몇 박 뒤. Reach · Lunge 는 결투자 사거리 · 돌진 배율이 곱해진다.</summary>
    public sealed class PatStep
    {
        public double Beat, Reach = 2.2 * FightUnit.S, Lunge = 0.8 * FightUnit.S, Damage = 10, Height = 1.9 * FightUnit.S, Speed = 10 * FightUnit.S;
        public HitKind Kind = HitKind.Melee;
        public bool Counterable => Kind != HitKind.Projectile;
    }

    /// <summary>
    /// 결투자 패턴(기본 세트). 마디 첫 박에 시작해 박에 맞춰 친다. 첫 타격은 2박 뒤 — 읽고 판단할 시간.
    /// 근접 패턴의 거리(MinD · MaxD · Pref)는 결투자 사거리 배율이 곱해진다. 원거리(탄 · 돌진 · 내려찍기)는 그대로.
    /// </summary>
    public sealed class Pattern
    {
        public string Id, Name, Hint;
        public double MinD, MaxD, Pref, Weight = 1;
        public int MinPhase;
        public bool Counter, Leap, Charge, Ranged;
        public List<PatStep> Steps = new List<PatStep>();

        // 패턴별 덮어쓰기(−1 = 공통값 사용) — 튜닝 패널 ‘패턴 · 이름’ 탭
        public double Telegraph = -1, ZoneShow = -1, ZoneHot = -1, CueLead = -1;          // 예고 보여 주기(색 변화 · 위험 구역 · 예고음)
        public double ApproachMul = -1, ApproachStop = -1, EndPad = -1;                    // 접근 · 끝
        public double ReachMul = -1, LungeMul = -1, MeleeActive = -1, BoxBottom = -1;      // 근접 판정
        public double LeapHeight = -1, LeapBeats = -1;                                     // 내려찍기
        public double ShotRadius = -1, ShotLife = -1, ShotAimY = -1, ShotOriginX = -1, ShotOriginY = -1;   // 탄
        public double ChargeHalf = -1;                                                     // 돌진
        public double LastBeat => Steps.Count == 0 ? 2 : Steps.Max(s => s.Beat);
        public bool Far => Ranged || Charge || Leap;

        /// <summary>기본 세트 8종.</summary>
        public static readonly List<Pattern> BASIC = new List<Pattern>
        {
            new Pattern { Id = "slash", Name = "베기", Hint = "3박 뒤 한 번 — 물러나거나 카운터", MinD = 0 * FightUnit.S, MaxD = 4.5 * FightUnit.S, Pref = 1.9 * FightUnit.S, Weight = 1.65, ZoneShow = 1, ZoneHot = 0,
                Steps = { new PatStep { Beat = 3, Reach = 2.3 * FightUnit.S, Lunge = 1.0 * FightUnit.S, Damage = 10 } } },
            new Pattern { Id = "double", Name = "2연격", Hint = "2박 · 2박 반", MinD = 0 * FightUnit.S, MaxD = 4.5 * FightUnit.S, Pref = 1.9 * FightUnit.S, Weight = 1,
                Steps = { new PatStep { Beat = 2, Reach = 2.2 * FightUnit.S, Lunge = 0.6 * FightUnit.S, Damage = 8 }, new PatStep { Beat = 2.5, Reach = 2.3 * FightUnit.S, Lunge = 0.7 * FightUnit.S, Damage = 9 } } },
            new Pattern { Id = "triple", Name = "3연격", Hint = "2 · 3 · 4박 — 밀고 들어온다", MinD = 0 * FightUnit.S, MaxD = 5 * FightUnit.S, Pref = 2.0 * FightUnit.S, Weight = 1, MinPhase = 1,
                Steps = { new PatStep { Beat = 2, Reach = 2.2 * FightUnit.S, Lunge = 0.7 * FightUnit.S, Damage = 8 }, new PatStep { Beat = 3, Reach = 2.2 * FightUnit.S, Lunge = 0.7 * FightUnit.S, Damage = 8 }, new PatStep { Beat = 4, Reach = 2.6 * FightUnit.S, Lunge = 1.1 * FightUnit.S, Damage = 12 } } },
            new Pattern { Id = "sweep", Name = "바닥 쓸기", Hint = "2박 뒤 낮게 — 점프 또는 카운터", MinD = 0 * FightUnit.S, MaxD = 5 * FightUnit.S, Pref = 2.4 * FightUnit.S, Weight = 1,
                Steps = { new PatStep { Beat = 2, Kind = HitKind.Low, Reach = 3.4 * FightUnit.S, Lunge = 0.4 * FightUnit.S, Height = 0.75 * FightUnit.S, Damage = 8 } } },
            new Pattern { Id = "charge", Name = "돌진", Hint = "2박 뒤 돌진 — 대쉬로 빠지기 · 뛰어넘기 · 카운터", MinD = 4 * FightUnit.S, MaxD = 16 * FightUnit.S, Pref = 7 * FightUnit.S, Weight = 0.9, Charge = true,
                Steps = { new PatStep { Beat = 2, Kind = HitKind.Charge, Reach = 9 * FightUnit.S, Height = 1.5 * FightUnit.S, Damage = 12, Speed = 20 * FightUnit.S } } },
            new Pattern { Id = "shots", Name = "에너지탄", Hint = "2 · 3박에 탄 — 무빙(카운터는 무효화만)", MinD = 4 * FightUnit.S, MaxD = 40 * FightUnit.S, Pref = 8 * FightUnit.S, Weight = 1, Ranged = true, ShotRadius = -2,
                Steps = { new PatStep { Beat = 2, Kind = HitKind.Projectile, Damage = 7, Speed = 10 * FightUnit.S }, new PatStep { Beat = 3, Kind = HitKind.Projectile, Damage = 7, Speed = 10 * FightUnit.S } } },
            new Pattern { Id = "leap", Name = "내려찍기", Hint = "뛰어올라 2박에 착지 — 점프 · 무빙 · 카운터", MinD = 2.5 * FightUnit.S, MaxD = 16 * FightUnit.S, Pref = 6 * FightUnit.S, Weight = 0.9, MinPhase = 1, Leap = true,
                Steps = { new PatStep { Beat = 2, Reach = 1.3 * FightUnit.S, Lunge = 0 * FightUnit.S, Damage = 13 }, new PatStep { Beat = 2, Kind = HitKind.Low, Reach = 3.8 * FightUnit.S, Lunge = 0 * FightUnit.S, Height = 0.6 * FightUnit.S, Damage = 7 } } },
            new Pattern { Id = "counter", Name = "카운터", Hint = "2박째에 카운터 — 그때 때리면 반격당함 · 같이 카운터하면 크래시", MinD = 0 * FightUnit.S, MaxD = 4.5 * FightUnit.S, Pref = 2.2 * FightUnit.S, Weight = 0.7, Counter = true },
        };

        /// <summary>결투자 카운터 성공 뒤 추격: 쫓아가 1 · 2박에 친다(경직된 플레이어를 노림).</summary>
        public static readonly Pattern PUNISH = new Pattern { Id = "punish", Name = "추격", Hint = "반격당했다 — 경직이 풀리면 바로 빠지기", MinD = 0 * FightUnit.S, MaxD = 99 * FightUnit.S, Pref = 1.2 * FightUnit.S,
            Steps = { new PatStep { Beat = 1, Reach = 2.2 * FightUnit.S, Lunge = 1.2 * FightUnit.S, Damage = 10 }, new PatStep { Beat = 2, Reach = 2.6 * FightUnit.S, Lunge = 1.5 * FightUnit.S, Damage = 16 } } };

        /// <summary>헛친 틈 찌르기: 플레이어 공격 · 카운터가 빗나가면 1박 뒤 찌른다.</summary>
        public static readonly Pattern JAB = new Pattern { Id = "jab", Name = "빈틈 찌르기", Hint = "헛쳤다 — 1박 뒤 찌르기", MinD = 0 * FightUnit.S, MaxD = 99 * FightUnit.S, Pref = 1.6 * FightUnit.S,
            Steps = { new PatStep { Beat = 1, Reach = 2.0 * FightUnit.S, Lunge = 1.0 * FightUnit.S, Damage = 9 } } };

        /// <summary>연타 대응(카운터가 없을 때): 1박 뒤 밀쳐내기.</summary>
        public static readonly Pattern SHOVE = new Pattern { Id = "shove", Name = "밀쳐내기", Hint = "연타 대응 — 1박 뒤", MinD = 0 * FightUnit.S, MaxD = 99 * FightUnit.S, Pref = 1.6 * FightUnit.S,
            Steps = { new PatStep { Beat = 1, Reach = 2.0 * FightUnit.S, Lunge = 0.5 * FightUnit.S, Damage = 8 } } };
    }

    /// <summary>결투에서 화면이 연출할 사건.</summary>
    public enum DuelEv
    {
        PatternStart, Hit, PlayerHurt, Nullified, CounterSuccess, NearCounter, EnemyCounterStart, EnemyCounter, CounterCrash,
        WallBounce, NearWallBounce, PlayerWallBounce, Damage, Shot, Land, KO, PlayerDown, SpamReact, EnemyDash, EnemyJump,
        MapStart, MapStrike, MapEnd, Immune, Evade, CounterTell, CounterBounce, PhaseBreak, PhaseStart,
        UltStart, UltStrike, UltHit, UltEnd,
        AllyShot, AllyStrike, AllyCutIn, AllyFly          // 동료: 사격(Act = 콤보) · 강한 사격 · 여주인공 컷인(Value = 보이는 시간) · 드론 비행 시작
    }

    public struct DuelEvent
    {
        public DuelEv Kind;
        public double X, Y, Value;
        public int Act;          // Damage: 0 검기 · 2 기계 · 1 카운터 성공 · 3 카운터 공격 / Hit: HitKind
        public string Text;
    }

    /// <summary>맵 패턴(전역 공격) 종류.</summary>
    public enum MapKind { None, Missiles, Columns, Lasers, Walls }

    /// <summary>
    /// 맵 패턴 하나의 위험물. 미사일 = 유도 탄(카운터로 무효화 가능), 기둥 · 레이저 · 장벽 = 환경 공격(카운터 불가 — 무빙 · 점프 · 대쉬 무적으로 피함).
    /// ArmT = 실제로 때리는 시각(박), 그 전은 예고. Half · HalfH = 가로 · 세로 반폭(레이저는 Y = 띠 가운데).
    /// </summary>
    public sealed class Hazard
    {
        public MapKind Kind;
        public double X, Y, Vx, Vy, Half, HalfH, SpawnT, ArmT, EndT, Life, HomeT;
        public int Dir;
        public bool Hit, Struck, Dead;
    }

    public sealed class Shot
    {
        public double X, Y, Vx, Vy, Life, Damage, Radius = 0.3 * FightUnit.S;
        public bool Dead;
    }

    /// <summary>
    /// 결투자 한 명 = 플레이어와 같은 전투 규칙(Fighter): 경직 · 넉백 · 월 바운스 · 카운터(반격 상태 → 카운터 공격, 스택 · 충전 · 쿨타임).
    /// 박 시계를 따라 패턴을 고르고, 이동 속도로 거리를 조절한다(플레이어보다 느림 → 카이팅).
    /// </summary>
    public sealed class Duelist : Fighter
    {
        public const double HALF = 0.45 * FightUnit.S, HEIGHT = 2.0 * FightUnit.S;
        public double Hp, MaxHp, FlinchT;
        public bool KO;
        public Pattern Cur;
        public double CurStart = -1;        // 패턴 시작 박(절대 박 번호)
        public double LeapFromX, LeapToX, LeapToY, ChargeLeft, ChargeSpeed;
        public double CtrAtkT = -1;
        public bool Invuln;                     // 일부 맵 패턴 동안 무적
        public double StepT, StepVx, DropT;     // 스텝 · 회피 같은 짧은 이동, 발판에서 내려가기
        /// <summary>자연 이동: 대쉬 남은 시간(그림용) · 남은 공중 점프 · 이번 프레임 스스로 걷는 중(바닥 감속 안 함).</summary>
        public double DashT, TurnT;
        public int AirJumps;
        public bool Driven;
        public readonly HashSet<int> DoneSteps = new HashSet<int>();

        public Duelist(FightTune t, Arena map) : base(t, map, FightTune.Pos(t.EnemyHalf, 0.05 * FightUnit.S), FightTune.Pos(t.EnemyHeight, 0.2 * FightUnit.S)) { Facing = -1; X = 5 * FightUnit.S; }

        protected override double OwnGravity => T.EnemyGravityMul;
        public PatStep ChargeStep;

        public double CounterLeft => ctrStateT;

        protected override void Interrupt()
        {
            base.Interrupt();
            CtrAtkT = -1;
            Cur = null;
            ChargeLeft = 0;
            StepT = 0;
        }

        /// <summary>카운터 시전: 반격 상태 + 잠시 뒤 카운터 공격(플레이어와 같은 구조, 수치는 결투자 것).</summary>
        /// <summary>atkDelay = 자세 → 카운터 공격까지(초). 그동안 반격 상태(때리면 반격당함), 카운터 공격 순간에 같이 카운터하면 크래시.</summary>
        public void StartCounter(double atkDelay)
        {
            CtrStack -= 1;
            CtrCd = T.EnemyCtrCooldown;
            ctrStateT = atkDelay + Math.Max(0, T.EnemyCtrStatePad);     // 카운터 공격 뒤에는 잠깐만 남는다(늦은 카운터가 억울하지 않게)
            CtrAtkT = Math.Max(0.02, atkDelay);
            Vx = 0;
            StepT = 0;
        }

        public void EndCounter() { ctrStateT = 0; CtrAtkT = -1; }

        /// <summary>반격 상태 · 카운터 공격 타이머. fire = 이번 프레임에 카운터 공격.</summary>
        public void TickCounter(double dt, out bool fire)
        {
            fire = false;
            if (ctrStateT > 0) ctrStateT -= dt;
            if (CtrAtkT > 0) { CtrAtkT -= dt; if (CtrAtkT <= 0) { CtrAtkT = -1; fire = true; } }
        }

        /// <summary>중력 · 착지(가로는 바깥에서 직접 옮김).</summary>
        public void Settle(double dt)
        {
            if (DropT > 0) DropT -= dt;
            if (DashT > 0) DashT -= dt;
            if (TurnT > 0) TurnT -= dt;
            Body(dt, 1, DropT > 0, out _);
            if (Grounded && !Driven) Vx = Toward(Vx, 0, T.Decel * dt);
            Driven = false;
        }

        public Box CounterBox() => new Box(X - T.EnemyCtrHalfW, X + T.EnemyCtrHalfW, Cy - T.EnemyCtrHalfH, Cy + T.EnemyCtrHalfH);
    }

    /// <summary>
    /// 1:1 결투 한 판. 박 시계(BPM · 박자표 · 시작 보정) → 결투자 패턴 · 이동 · 타격, 플레이어 공격 · 카운터 판정, 데미지 · 경직 · 넉백, 그루브.
    ///  · 패턴은 마디 첫 박에 시작(연타 대응만 다음 박 · 반 박). 곡이 조용해도 쉬지 않는다 — 조용한 마디는 견제(탄 · 단타) 위주.
    ///  · 플레이어 카운터(반격 상태)에 투사체가 아닌 결투자 공격이 닿으면 카운터 성공: 무효 + 데미지 + 경직 + 넉백(+근접 카운터 · 월 바운스).
    ///  · 결투자 카운터(반격 상태)를 때리면 결투자의 카운터 성공: 플레이어 데미지 + 넉백 + 경직. 둘 다 반격 상태에서 부딪히면 카운터 크래시(데미지 없이 둘 다).
    ///  · 플레이어 넉백 · 경직은 결투자 카운터 · 크래시에만. 보통 공격은 데미지 + 짧은 무적.
    ///  · 연타 대응: 짧은 시간에 여러 대 맞으면 결투자가 카운터(반 박 뒤)나 밀쳐내기(다음 박).
    ///  · 박에 맞춘 행동 = 그루브 상승 → 데미지 · 카운터 충전 가속. 박에 맞춘 공격 · 카운터는 추가 데미지.
    /// </summary>
    public sealed class Duel
    {
        public readonly FightTune T;
        public readonly Arena Map;
        public readonly PlayerFsm P;
        public readonly Duelist E;
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<DuelEvent> Events = new List<DuelEvent>();
        public readonly List<Pattern> Library;
        public double Beat, Offset, SongT;
        public int BeatsPerBar;
        public int PlayerHp, MaxPlayerHp;
        public double Dealt, Taken, Groove;
        public int Counters, EnemyCounters, Crashes, OnBeats, MaxCombo, Combo;
        public bool LastOnBeat;
        public readonly double[] BarEnergy;
        int lastBar = int.MinValue, restBars;
        ulong rng;
        bool atkOnBeat, ctrOnBeat, allyOnBeat;
        /// <summary>동료(기계 공격 담당 여주인공 · 땅에 선 인간형). 켜져 있을 때만 움직이고 쏨.</summary>
        public Ally A { get; private set; }
        public bool AllyOn => T.AllyOn >= 0.5;
        double pendingReact = -1, aiCd;
        string reactText;
        /// <summary>반응 카운터(습관 읽기 · 연타 대응 · 경직 회복) 준비 중: 방패를 드는 중 — 아직 받아치지 않는다.</summary>
        public bool CounterPrepping => pendingReact >= 0;
        double ReactPrep => Math.Max(0, T.EnemyCtrPrepBeats) * Beat;

        /// <summary>
        /// 반응 카운터 예약: 하던 공격을 멈추고 방패를 든다(신호) → 최소 ‘반응 카운터 준비 시간’ 뒤에 자세.
        /// 짤딜을 넣던 플레이어가 보고 멈출 틈 — 준비 중에 때리면 보통 타격.
        /// </summary>
        void ScheduleCounter(double t, double stanceAt, bool quant, DuelEv tell, string tellText, string stanceText = null)
        {
            double at = Math.Max(stanceAt, t + ReactPrep);
            if (quant) at = TimeOfBeat(QuantUp(BeatOf(at)));
            pendingReact = at;
            reactText = stanceText;
            var e = E;
            e.Cur = null; active.Clear(); e.ChargeLeft = 0; Face();
            Ev(tell, e.X, e.Y + 2.4 * FightUnit.S, 0, 0, tellText);
        }
        readonly HashSet<int> stepHit = new HashSet<int>();
        // 맵 패턴
        public readonly List<Hazard> Hazards = new List<Hazard>();
        public MapKind CurMap = MapKind.None;
        MapKind lastMap = MapKind.None;
        double mapStart;
        int mapIndex, lastMapBar = int.MinValue, mapSide = 1, lastBeatI = int.MinValue;
        bool ctrConnected, wasInCounter;
        // 결투자 카운터 심리전: 카운터 공격 시각(크래시 판정), 플레이어 카운터 시작 시각, 공격 간격(습관 읽기)
        double enemyAtkTime = double.MinValue, ctrPressT = double.MinValue, habitAt = -1, habitCd;
        /// <summary>가만히 있던 플레이어에게 들어간 결투자 카운터 공격: 판정 창(늦은 쪽)이 닫힐 때 맞힌다 — 그 사이 카운터를 시작하면 크래시.</summary>
        double ctrHitAt = -1;
        readonly List<double> atkTimes = new List<double>();
        readonly List<double> spam = new List<double>();

        public Duel(RhythmChart chart, int seed = 1, Arena map = null, FightTune tune = null)
        {
            T = tune ?? new FightTune();
            if (map == null)
            {
                // 결투장 크기도 튜닝(기둥 · 발판은 벽 밖으로 나가면 뺀다)
                map = Arena.Standard();
                map.HalfW = FightTune.Pos(T.ArenaHalfW, 4 * FightUnit.S);
                map.Ceil = FightTune.Pos(T.ArenaCeil, 3 * FightUnit.S);
                map.Solids.RemoveAll(q => q.X1 > map.HalfW - 0.5 * FightUnit.S || q.X0 < -map.HalfW + 0.5 * FightUnit.S || q.Y1 > map.Ceil - 1 * FightUnit.S);
                map.Platforms.RemoveAll(q => q.X1 > map.HalfW - 0.5 * FightUnit.S || q.X0 < -map.HalfW + 0.5 * FightUnit.S || q.Y1 > map.Ceil - 2 * FightUnit.S);
            }
            Map = map;
            Beat = chart?.Beat ?? 0.5;
            Offset = (chart?.OffsetMs ?? 0) / 1000.0;
            BeatsPerBar = chart?.BeatsPerBar ?? 4;
            P = new PlayerFsm(T, Map) { X = T.PlayerStartX };
            E = new Duelist(T, Map) { X = T.EnemyStartX };
            A = new Ally(T);
            A.Snap(P, E);
            restBars = Math.Max(0, (int)Math.Round(T.FirstRestBars));
            E.Hp = E.MaxHp = T.EnemyHp;
            E.CtrStack = T.EnemyCtrCapI;
            P.Ult = Math.Min(T.UltMax, Math.Max(0, T.UltStartGauge));
            PlayerHp = MaxPlayerHp = Math.Max(1, (int)Math.Round(T.PlayerHp));
            Library = Pattern.BASIC;
            rng = (ulong)(uint)seed * 2654435761UL + 0x7F4A7C15UL;
            // 마디별 세기(차트 노트 밀도) — 패턴 고르기에만 쓴다(조용한 마디 = 견제, 큰 마디 = 연타)
            int bars = chart == null ? 64 : Math.Max(1, (int)Math.Ceiling((chart.Length - Offset) / chart.Bar));
            BarEnergy = new double[bars];
            if (chart != null && chart.Notes.Count > 0)
            {
                foreach (var n in chart.Notes) { int b = (int)Math.Floor((n.T - Offset) / chart.Bar); if (b >= 0 && b < bars) BarEnergy[b] += 1; }
                double mx = BarEnergy.Max();
                for (int i = 0; i < bars; i++) BarEnergy[i] = mx > 0 ? BarEnergy[i] / mx : 0.5;
            }
            else for (int i = 0; i < bars; i++) BarEnergy[i] = 0.5;
        }

        double Rnd()
        {
            rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17;
            return (rng >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>결투자 체력으로 정하는 단계: 0 기본 · 1 격해짐(66% 아래) · 2 격노(33% 아래).</summary>
        public int Phase => Math.Min(2, HpStage + (PhaseNo == 1 ? (int)Math.Round(T.Phase2StageAdd) : PhaseNo == 2 ? (int)Math.Round(T.Phase3StageAdd) : 0));
        int HpStage => E.Hp > E.MaxHp * T.Phase1Hp ? 0 : E.Hp > E.MaxHp * T.Phase2Hp ? 1 : 2;

        // ───────── 페이즈(체력바) ─────────
        /// <summary>지금 페이즈(0 = 1페이즈). 체력바를 다 깎으면 다음 페이즈 — 마지막 페이즈에서만 K.O.</summary>
        public int PhaseNo { get; private set; }
        /// <summary>허수아비(작업실 미리보기): 결투자가 공격 · 반응을 하지 않고 맞기만 한다(넉백 · 경직은 그대로), 체력은 다 깎이면 다시 참.</summary>
        public bool Dummy;
        public int PhaseCount => Math.Max(1, Math.Min(3, (int)Math.Round(T.PhaseCount)));
        /// <summary>페이즈 전환 중(무적 · 가운데로 도약).</summary>
        public bool PhaseBreaking => breakT > 0;
        double breakT, breakLen, breakFromX, breakToX, cornerCd;
        double PM(double p2, double p3) => PhaseNo == 0 ? 1 : PhaseNo == 1 ? p2 : p3;
        double DmgMul => T.EnemyDamageMul * PM(T.Phase2DmgMul, T.Phase3DmgMul);
        double TellMul => Math.Max(0, PM(T.Phase2TellMul, T.Phase3TellMul));
        double RestMul => Math.Max(0, PM(T.Phase2RestMul, T.Phase3RestMul));

        void BeginPhaseBreak()
        {
            var e = E;
            PhaseNo++;
            e.Hp = e.MaxHp = Math.Max(1, T.EnemyHp * PM(T.Phase2HpMul, T.Phase3HpMul));
            e.Cur = null; active.Clear(); Shots.Clear(); e.ChargeLeft = 0; e.EndCounter();
            e.Knock = null; e.Bouncing = false; e.StunT = 0; e.StepT = 0;
            ctrHitAt = -1; pendingReact = -1; reactText = null; habitAt = -1;
            if (CurMap != MapKind.None) EndMap();
            breakLen = breakT = Math.Max(0.05, T.PhaseBreakBeats * Beat);
            breakFromX = e.X;
            double c = 0;
            if (Math.Abs(P.X - c) < 4 * FightUnit.S) c = P.X > 0 ? -7 * FightUnit.S : 7 * FightUnit.S;      // 플레이어가 가운데에 있으면 옆으로
            breakToX = T.PhaseToCenter >= 0.5 ? c : e.X;
            e.Invuln = true;
            PlayerHp = Math.Min(MaxPlayerHp, PlayerHp + (int)Math.Round(MaxPlayerHp * Math.Max(0, T.PhaseHealPlayer)));
            Ev(DuelEv.PhaseBreak, e.X, e.Y + 2.6 * FightUnit.S, PhaseNo + 1, PhaseCount, $"PHASE {PhaseNo + 1}");
        }

        /// <summary>전환 중: 떠올라 가운데로 날아가 착지. 끝나면 무적이 풀리고 새 페이즈 시작.</summary>
        void TickPhaseBreak(double dt)
        {
            var e = E;
            breakT -= dt;
            double k = Math.Max(0, Math.Min(1, 1 - breakT / breakLen));
            double m = Math.Min(1, k / 0.6);                           // 앞 60% 동안 이동, 뒤는 착지 후 자세
            e.X = breakFromX + (breakToX - breakFromX) * (m * m * (3 - 2 * m));
            e.Y = Math.Sin(m * Math.PI) * 4.0 * FightUnit.S;
            e.Vx = e.Vy = 0;
            e.Grounded = m >= 1;
            Face();
            if (breakT <= 0)
            {
                breakT = 0;
                e.Y = 0; e.Grounded = true;
                e.Invuln = false;
                restBars = 0;
                Ev(DuelEv.PhaseStart, e.X, e.Y + 2.6 * FightUnit.S, PhaseNo + 1, PhaseCount, PhaseNo + 1 >= PhaseCount ? "마지막 페이즈" : $"PHASE {PhaseNo + 1}");
            }
        }
        public bool Over => E.KO || PlayerHp <= 0;
        public double BeatOf(double t) => (t - Offset) / Beat;
        public double TimeOfBeat(double b) => Offset + b * Beat;
        public double GrooveMul => 1 + Groove * T.GrooveDamage;

        // ───────── 패턴별 값(−1 이면 공통값) ─────────
        static double O(double v, double common) => v >= 0 ? v : common;
        public double TelegraphOf(Pattern p) => O(p?.Telegraph ?? -1, T.TelegraphBeats) * TellMul;
        public double ZoneShowOf(Pattern p) => O(p?.ZoneShow ?? -1, T.ZoneShowBeats) * TellMul;
        public double ZoneHotOf(Pattern p) => O(p?.ZoneHot ?? -1, T.ZoneHotBeats) * TellMul;
        public double CueLeadOf(Pattern p) => O(p?.CueLead ?? -1, T.CueLeadBeats) * TellMul;
        public double ReachMulOf(Pattern p) => O(p?.ReachMul ?? -1, T.EnemyReachMul);
        public double LungeMulOf(Pattern p) => O(p?.LungeMul ?? -1, T.EnemyLungeMul);
        public double BoxBottomOf(Pattern p) => O(p?.BoxBottom ?? -1, T.MeleeBoxBottom);
        double ApproachStopOf(Pattern p) => O(p?.ApproachStop ?? -1, T.ApproachStopBeats);
        double LeapBeatsOf(Pattern p) => O(p?.LeapBeats ?? -1, T.LeapBeats);
        public double ShotOriginXOf(Pattern p) => O(p?.ShotOriginX ?? -1, T.ShotOriginX);
        public double ShotOriginYOf(Pattern p) => O(p?.ShotOriginY ?? -1, T.ShotOriginY);
        public double ShotAimYOf(Pattern p) => O(p?.ShotAimY ?? -1, T.ShotAimY);

        /// <summary>t 가 박에 얼마나 가까운지(초).</summary>
        public double BeatError(double t)
        {
            double b = BeatOf(t);
            return Math.Abs(b - Math.Round(b)) * Beat;
        }

        void Ev(DuelEv k, double x, double y, double v = 0, int act = 0, string text = null) => Events.Add(new DuelEvent { Kind = k, X = x, Y = y, Value = v, Act = act, Text = text });

        // ───────── 플레이어 입력(누른 실제 시각 t → 박 판정 · 그루브) ─────────
        bool Beatish(double t)
        {
            bool on = BeatError(t) <= T.BeatWindow;
            LastOnBeat = on;
            Groove = Math.Max(0, Math.Min(1, Groove + (on ? T.GrooveGain : -T.GrooveLoss)));
            return on;
        }

        public bool PressAttack(double t)
        {
            int div = QuantDiv;
            if (div <= 0) { if (!P.Attack()) return false; atkOnBeat = Beatish(t); NoteRhythm(t); return true; }
            // 박에 붙이기: 다음 박 칸에 낸다(이미 기다리는 공격이 있으면 그 하나로 — 연타해도 칸마다 한 번)
            if (!P.AttackAccept) return false;
            atkOnBeat = Beatish(t); NoteRhythm(t);
            if (pendingAtk < 0) pendingAtk = NextGrid(t, div);
            if (pendingAtk <= t) FirePending(t, div);
            return true;
        }

        /// <summary>박에 붙이기 칸 수(박을 몇으로 나눌지): 0 끔 · 1 · 2 · 4.</summary>
        int QuantDiv { get { int q = (int)Math.Round(T.AtkQuant); return q <= 0 ? 0 : q == 1 ? 1 : q == 2 ? 2 : 4; } }
        /// <summary>기다리는 공격이 나갈 시각(음수 = 없음).</summary>
        double pendingAtk = -1;

        /// <summary>t 다음(또는 살짝 지난) 박 칸 시각.</summary>
        double NextGrid(double t, int div)
        {
            double step = Math.Max(0.01, Beat / div);
            double k = Math.Ceiling((t - Offset - Math.Max(0, T.AtkQuantLate)) / step - 1e-9);
            return Offset + k * step;
        }

        /// <summary>기다리던 공격: 바로 나갈 수 있으면 내고, 위치 고정 · 재사용 대기가 남았으면 그 뒤 첫 박 칸으로 미룸(언제나 박 칸에서만 나가게).</summary>
        void FirePending(double now, int div)
        {
            if (!P.AttackAccept) { pendingAtk = -1; return; }
            if (P.AttackFree) { P.Attack(); pendingAtk = -1; return; }
            double step = Math.Max(0.01, Beat / div);
            pendingAtk = Offset + Math.Floor((now - Offset) / step + 1 + 1e-9) * step;
        }
        public bool PressCounter(double t) { if (!P.Counter()) return false; pendingAtk = -1; ctrOnBeat = Beatish(t); ctrConnected = false; ctrPressT = t; return true; }

        /// <summary>
        /// 습관 읽기: 공격을 같은 간격으로 계속 누르면(HabitCount 번, 오차 HabitTol 안) 결투자가 다음 타이밍을 예상해 그 직전에 카운터를 건다.
        /// 박마다 기계적으로 치면 읽힌다 — 박을 쪼개거나 쉬어 가며 리듬을 바꾸면 안 읽힌다.
        /// </summary>
        void NoteRhythm(double t)
        {
            atkTimes.Add(t);
            int need = Math.Max(2, (int)Math.Round(T.HabitCount));
            while (atkTimes.Count > need) atkTimes.RemoveAt(0);
            if (T.HabitOn < 0.5 || Phase < (int)Math.Round(T.HabitMinPhase) || atkTimes.Count < need || habitCd > 0 || habitAt >= 0) return;
            double first = atkTimes[1] - atkTimes[0];
            if (first < 0.08 || first > 1.5) return;
            for (int i = 2; i < atkTimes.Count; i++) if (Math.Abs(atkTimes[i] - atkTimes[i - 1] - first) > T.HabitTol) return;
            habitAt = t + first - T.HabitLead;
            atkTimes.Clear();
        }

        /// <summary>결투자 카운터 시작: 카운터 공격까지 ‘카운터 공격까지(박)’, 단계가 되면 가끔 엇박으로 밀린다. 크래시 판정용으로 공격 시각을 기억.</summary>
        void BeginEnemyCounter(double t, string text = "카운터 자세")
        {
            double delay = CtrAtkDelay;
            if (Phase >= (int)Math.Round(T.OffbeatMinPhase) && Rnd() < T.OffbeatChance) delay += T.OffbeatShift * Beat;
            E.StartCounter(delay);
            enemyAtkTime = t + Math.Max(0.02, delay);
            Ev(DuelEv.EnemyCounterStart, E.X, E.Y + 2.4 * FightUnit.S, 0, 0, text);
        }

        /// <summary>저스트 크래시: 플레이어가 결투자 카운터 공격 시각 ± 크래시 창 안에서 카운터를 ‘시작’ 했는지. 끄면 둘 다 반격 상태이기만 하면 크래시.</summary>
        double CrashErr => Math.Abs(ctrPressT - enemyAtkTime);
        /// <summary>다가오는 결투자 카운터 공격 시각(카운터 자세 중일 때, 아니면 음수) — 타이밍 링 표시용.</summary>
        public double CrashAt => E.InCounter && !E.KO && !E.Disabled ? enemyAtkTime : -1;
        /// <summary>크래시 굿 판정 창(초).</summary>
        public double CrashWindow => GoodWin;
        double GoodWin => Math.Max(0, T.CrashGoodBeats) * Beat;
        bool JustCrash => T.JustCrashOn < 0.5 || CrashErr <= GoodWin;
        bool PerfectCrash => CrashErr <= Math.Max(0, T.CrashPerfectBeats) * Beat;
        /// <summary>위장 예고가 지금 단계에서 켜져 있는지(‘위장 시작 단계’ 전에는 처음부터 카운터 자세로 보임).</summary>
        public bool Disguised => T.CounterDisguise >= 0.5 && Phase >= (int)Math.Round(T.CounterDisguiseMinPhase);
        public bool PressDash(double t, int dx, int dy) { if (!P.Dash(dx, dy)) return false; Beatish(t); return true; }
        public bool PressJump(double t, int dir, bool down) { if (!P.Jump(dir, down)) return false; Beatish(t); return true; }

        double Bonus(bool onBeat) => (onBeat ? 1 + T.BeatBonus : 1) * GrooveMul;

        // ───────── 매 프레임 ─────────
        public void Tick(double dt, double t, int dir)
        {
            SongT = t;
            P.EventLock = t < 0 || E.KO || PlayerHp <= 0 || P.UltLockT > 0;      // 궁극기 위치 고정 동안 = 캐릭터 고정
            TickUlt(dt);
            Groove = Math.Max(0, Groove - T.GrooveDecay * dt);
            P.CtrRechargeSpeed = 1 + Groove * T.GrooveCtrRecharge;
            if (pendingAtk >= 0) { int qd = QuantDiv; if (qd <= 0) { if (P.AttackAccept) P.Attack(); pendingAtk = -1; } else if (t >= pendingAtk) FirePending(t, qd); }
            P.Tick(dt, dir);
            foreach (var (ev, combo) in P.Events) OnPlayerEv(ev, combo, t);
            if (AllyOn)
            {
                A.Tick(dt, P, E);
                for (int i = 0; i < A.Events.Count; i++) OnAllyEv(A.Events[i].ev, A.Events[i].combo, t);
            }
            A.Events.Clear();
            TickEnemy(dt, t);
            TickShots(dt);
            TickHazards(dt, t);
            if (aiCd > 0) aiCd -= dt;
            // 카운터가 아무것도 받지 못하고 끝남 = 헛친 틈
            if (wasInCounter && !P.InCounter && !P.Disabled && !ctrConnected) NoteWhiff(t, true);
            wasInCounter = P.InCounter;
            if (Dummy && E.Hp <= 0) E.Hp = E.MaxHp;
            if (Dummy && PlayerHp <= 0) PlayerHp = MaxPlayerHp;
            if (E.Hp <= 0 && !E.KO)
            {
                if (PhaseNo < PhaseCount - 1) BeginPhaseBreak();
                else { E.Hp = 0; E.KO = true; E.Cur = null; E.EndCounter(); Ev(DuelEv.KO, E.X, E.Y + 1 * FightUnit.S); }
            }
            if (PlayerHp <= 0) PlayerHp = 0;
        }

        /// <summary>화면이 사건을 다 쓴 뒤 비운다.</summary>
        public void ClearFrame() { Events.Clear(); P.Events.Clear(); }

        // ───────── 결투자 ─────────
        double Speed => (T.EnemySpeed + T.EnemySpeedPhase * Phase) * PM(T.Phase2SpeedMul, T.Phase3SpeedMul);

        void TickEnemy(double dt, double t)
        {
            var e = E;
            if (breakT > 0) { TickPhaseBreak(dt); return; }
            if (cornerCd > 0) cornerCd -= dt;
            e.ChargeCounter(dt, T.EnemyCtrCapI, T.EnemyCtrRecharge * PM(T.Phase2CtrRechargeMul, T.Phase3CtrRechargeMul));
            if (e.FlinchT > 0) e.FlinchT -= dt;
            if (e.TickReaction(dt, out bool b, out bool near, out bool recovered))
            {
                if (b) { Ev(DuelEv.WallBounce, e.X, e.Y + 1 * FightUnit.S, 0, 0, near ? "근접 월 바운스!" : "월 바운스!"); if (near) Ev(DuelEv.NearWallBounce, e.X, e.Y + 1 * FightUnit.S); }
                if (recovered) OnRecovered(t);
                return;
            }
            if (e.KO || Dummy) { e.Settle(dt); return; }
            e.TickCounter(dt, out bool fire);
            if (fire) EnemyCounterAttack();
            if (ctrHitAt >= 0 && t >= ctrHitAt) ResolveCounterHit();
            // 연타 대응 카운터(반 박 단위로 박자에 맞춰)
            if (pendingReact >= 0 && t >= pendingReact)
            {
                pendingReact = -1;
                string txt = reactText ?? "카운터 자세";
                reactText = null;
                if (e.CounterReady && !e.InCounter) { e.Cur = null; active.Clear(); e.ChargeLeft = 0; Face(); BeginEnemyCounter(t, txt); }
            }
            // 습관 읽기 카운터: 예상 타이밍 직전에 자세 — 그 전에 준비 시간만큼 방패를 드는 모습을 먼저 보인다
            if (habitCd > 0) habitCd -= dt;
            if (habitAt >= 0 && t >= habitAt - ReactPrep)
            {
                double at = habitAt;
                habitAt = -1;
                if (e.CounterReady && !e.InCounter && pendingReact < 0 && CurMap == MapKind.None && !e.Invuln)
                {
                    habitCd = T.HabitCooldown;
                    ScheduleCounter(t, at, false, DuelEv.CounterTell, "읽혔다!", "리듬을 읽었다!");
                }
            }
            if (CurMap != MapKind.None) { TickMap(dt, t); return; }
            double bt = BeatOf(t);
            int bar = (int)Math.Floor(bt / BeatsPerBar + 1e-9);
            if (bar != lastBar)
            {
                lastBar = bar;
                // 마디 첫 박 근처에서만 시작(경직이 마디 중간에 풀리면 다음 마디까지 기다렸다가 박자에 합류 = 그동안이 공격 기회)
                if (e.Cur == null && !e.InCounter && pendingReact < 0 && t >= Offset && bt - bar * (double)BeatsPerBar < T.PatternStartWindow)
                {
                    if (MapDue(bar)) StartMap(bar); else PickPattern(bar);
                }
            }
            if (CurMap != MapKind.None) { TickMap(dt, t); return; }
            bool leaping = e.Cur != null && e.Cur.Leap && !e.Grounded;
            if (e.Cur != null) RunPattern(dt, t, bt);
            else if (!e.InCounter) { if (T.EnemyMoveMode >= 0.5) IdleNatural(dt, t, bt); else Idle(dt, t, bt); }
            if (e.StepT > 0) { e.StepT -= dt; MoveEnemy(dt, e.StepVx); }
            if (!(leaping && e.Cur != null && e.Cur.Leap)) e.Settle(dt);
        }

        void Face() { E.Facing = P.X < E.X ? -1 : 1; }

        void MoveEnemy(double dt, double vx)
        {
            var e = E;
            double nx = Map.MoveX(e.X, e.Y, vx * dt, E.Half, E.Height, out bool wall);
            if (wall && e.Grounded && Math.Abs(vx) > 0.1 * FightUnit.S)
            {
                // 기둥에 막히면 뛰어넘는다
                double top = 0;
                foreach (var s in Map.Solids) if (Math.Abs(s.Cx - (e.X + Math.Sign(vx) * T.VaultLook)) < T.VaultRange) top = Math.Max(top, s.Y1);
                if (top > e.Y) { e.Vy = Math.Sqrt(2 * T.Gravity * Math.Max(0.01, T.EnemyGravityMul) * (top - e.Y + T.VaultMargin)); e.Grounded = false; }
            }
            e.X = nx;
        }

        double Energy(int bar)
        {
            if (BarEnergy.Length == 0) return 0.5;
            int i = ((bar % BarEnergy.Length) + BarEnergy.Length) % BarEnergy.Length;
            return BarEnergy[i];
        }

        /// <summary>마디 첫 박에 패턴을 고른다(쉬는 마디 · 거리 · 단계 · 카운터 스택 · 곡의 세기).</summary>
        void PickPattern(int bar)
        {
            if (restBars > 0) { restBars--; return; }
            double en = Energy(bar);
            double restP = T.EnemyRest * (Phase == 0 ? 1 : Phase == 1 ? T.EnemyRestPhase2 : T.EnemyRestPhase3) * RestMul;
            if (Rnd() < restP) return;
            double dist = Math.Abs(P.X - E.X);
            var cand = new List<(Pattern p, double w)>();
            foreach (var p in Library)
            {
                double rm = ReachMulOf(p);
                if (p.MinPhase > Phase) continue;
                double minD = p.Far ? p.MinD : p.MinD * rm, maxD = p.Far ? p.MaxD : p.MaxD * rm + T.MeleeMaxExtra;
                if (dist < minD || dist > maxD) continue;
                if (p.Counter && !E.CounterReady) continue;
                if (!p.Far && Math.Abs(P.Y - E.Y) > T.MeleeMaxHeightDiff) continue;          // 높이 차가 크면 근접 패턴 안 씀
                double w = p.Weight;
                if (en > T.EnergyLoud && p.Steps.Count >= 2) w *= T.EnergyLoudMul;                 // 곡이 커지면 연타
                if (en < T.EnergyQuiet && (p.Far || p.Steps.Count == 1)) w *= T.EnergyQuietMul;     // 조용하면 견제
                if (Phase == 2 && p.Counter) w *= T.RageCounterMul;                                  // 격노: 카운터 자세 자주
                cand.Add((p, w));
            }
            if (cand.Count == 0) return;
            double tot = cand.Sum(c => c.w), r = Rnd() * tot;
            var pick = cand[cand.Count - 1].p;
            foreach (var c in cand) { if (r < c.w) { pick = c.p; break; } r -= c.w; }
            StartPattern(pick, bar * (double)BeatsPerBar);
        }

        public void StartPattern(Pattern p, double startBeat)
        {
            var e = E;
            e.Cur = p;
            e.CurStart = startBeat;
            e.DoneSteps.Clear();
            stepHit.Clear();
            active.Clear();
            Face();
            if (p.Leap) { e.LeapFromX = e.X; e.LeapToX = P.X; }
            bool disguise = p.Counter && Disguised;
            Ev(DuelEv.PatternStart, e.X, e.Y + 2.4 * FightUnit.S, 0, disguise ? 1 : 0, disguise ? Pattern.BASIC[0].Name : p.Name);
        }

        void EndPattern()
        {
            E.Cur = null;
            E.ChargeLeft = 0;
            restBars = Rnd() < (Phase == 0 ? T.RestAfter1 : Phase == 1 ? T.RestAfter2 : T.RestAfter3) * RestMul ? 1 : 0;   // 패턴 뒤 숨 쉴 마디
        }

        void RunPattern(double dt, double t, double bt)
        {
            var e = E;
            var p = e.Cur;
            double rel = bt - e.CurStart;
            double rm = ReachMulOf(p);
            // 카운터 자세: 2박째에 카운터(반격 상태 → 카운터 공격)
            if (p.Counter)
            {
                Face();
                if (rel < T.CounterStanceBeats - ApproachStopOf(p)) Approach(dt, p.Pref * rm, O(p.ApproachMul, T.CounterApproachMul));
                // 위장: 처음엔 보통 베기처럼 보이다가 반격 자세 직전에만 진짜 신호(방패 번쩍임 · 금속음)
                if (Disguised && rel >= T.CounterStanceBeats - T.CounterTellBeats && !e.DoneSteps.Contains(-2))
                {
                    e.DoneSteps.Add(-2);
                    Ev(DuelEv.CounterTell, e.X, e.Y + 2.4 * FightUnit.S, 0, 0, "카운터!");
                }
                if (rel >= T.CounterStanceBeats && !e.DoneSteps.Contains(-1))
                {
                    e.DoneSteps.Add(-1);
                    if (e.CounterReady) BeginEnemyCounter(t);
                }
                if (e.DoneSteps.Contains(-1) && !e.InCounter && e.CtrAtkT < 0) EndPattern();
                return;
            }
            double firstHit = p.Steps.Count > 0 ? p.Steps.Min(s => s.Beat) : 1;
            if (rel < firstHit - ApproachStopOf(p))
            {
                if (p.Leap)
                {
                    // 1박에 목표를 정하고 첫 타격 박에 착지하는 포물선
                    if (rel < firstHit - LeapBeatsOf(p)) { e.LeapToX = P.X; e.LeapToY = Map.SupportBelow(P.X, P.Y + 0.1 * FightUnit.S, E.Half); e.LeapFromX = e.X; return; }
                    LeapArc(rel, firstHit);
                    return;
                }
                Face();
                if (!p.Charge) Approach(dt, p.Far ? p.Pref : p.Pref * (p == Pattern.PUNISH ? 1 : rm), p == Pattern.PUNISH ? O(p.ApproachMul, T.EnemyPunishChase) : O(p.ApproachMul, T.ApproachMul));
            }
            else if (p.Leap && rel < firstHit) { LeapArc(rel, firstHit); return; }
            // 타격
            for (int i = 0; i < p.Steps.Count; i++)
            {
                var s = p.Steps[i];
                if (rel < s.Beat || e.DoneSteps.Contains(i)) continue;
                e.DoneSteps.Add(i);
                FireStep(i, s);
                if (e.Cur == null) return;          // 카운터당해 끊김
            }
            // 돌진 진행
            if (e.ChargeLeft > 0)
            {
                double step = Math.Min(e.ChargeLeft, e.ChargeSpeed * dt);
                double nx = Map.MoveX(e.X, e.Y, e.Facing * step, E.Half, E.Height, out bool wall);
                e.ChargeLeft = wall ? 0 : e.ChargeLeft - step;
                e.X = nx;
                var cs = e.ChargeStep ?? new PatStep { Kind = HitKind.Charge, Damage = 12, Height = 1.5 * FightUnit.S };
                double chh = O(p.ChargeHalf, T.ChargeHalf);
                CheckHit(100, cs, new Box(e.X - chh, e.X + chh, e.Y, e.Y + cs.Height));
                if (e.Cur == null) return;
            }
            // 근접 판정은 짧게 유지(0.12초)
            foreach (var a in active.ToList())
            {
                a.left -= dt;
                if (a.left <= 0) active.Remove(a);
                else { CheckHit(a.id, a.step, a.box); if (e.Cur == null) return; }
            }
            if (rel >= p.LastBeat + O(p.EndPad, T.PatternEndPad) && e.ChargeLeft <= 0 && active.Count == 0) EndPattern();
        }

        void Approach(double dt, double pref, double mul)
        {
            var e = E;
            double dist = Math.Abs(P.X - e.X), gap = dist - pref;
            MoveEnemy(dt, Math.Abs(gap) < T.ApproachDeadZone ? 0 : Math.Sign(gap) * Math.Sign(P.X - e.X) * Speed * mul);
        }

        void LeapArc(double rel, double firstHit)
        {
            var e = E;
            double lb = FightTune.Pos(LeapBeatsOf(e.Cur));
            double k = Math.Max(0, Math.Min(1, (rel - (firstHit - lb)) / lb));
            e.X = e.LeapFromX + (e.LeapToX - e.LeapFromX) * k;
            e.Y = e.LeapToY * k + Math.Sin(k * Math.PI) * O(e.Cur?.LeapHeight ?? -1, T.LeapHeight);
            e.Grounded = false; e.Vy = 0;
        }

        sealed class Active { public int id; public PatStep step; public Box box; public double left; }
        readonly List<Active> active = new List<Active>();
        /// <summary>지금 켜져 있는 결투자 근접 판정(판정 보기용).</summary>
        public IEnumerable<Box> ActiveBoxes { get { foreach (var x in active) yield return x.box; } }

        double StepDamage(PatStep s) => s.Damage * DmgMul * (Phase == 2 ? T.RageDamageMul : 1);

        void FireStep(int i, PatStep s)
        {
            var e = E;
            var cp = e.Cur;
            double rm = ReachMulOf(cp);
            switch (s.Kind)
            {
                case HitKind.Projectile:
                {
                    double rad = O(cp?.ShotRadius ?? -1, T.ShotRadius);
                    double sx = e.X + e.Facing * ShotOriginXOf(cp), sy = e.Y + ShotOriginYOf(cp);
                    var spawn = new Box(sx - rad, sx + rad, sy - rad, sy + rad);
                    foreach (var w in Map.Solids) if (w.Overlaps(spawn)) { sx = e.X; break; }   // 기둥에 붙어 서면 몸 중심에서 쏜다
                    double dx = P.X - sx, dy = P.Y + ShotAimYOf(cp) - sy, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < 1e-6) { dx = e.Facing; dy = 0; d = 1; }
                    Shots.Add(new Shot { X = sx, Y = sy, Vx = dx / d * s.Speed, Vy = dy / d * s.Speed, Life = O(cp?.ShotLife ?? -1, T.ShotLife), Damage = StepDamage(s), Radius = rad });
                    Ev(DuelEv.Shot, sx, sy);
                    break;
                }
                case HitKind.Charge:
                    Face();
                    e.ChargeLeft = s.Reach;
                    e.ChargeStep = s;
                    e.ChargeSpeed = s.Speed;
                    break;
                default:
                {
                    bool leap = e.Cur != null && e.Cur.Leap;
                    if (leap && i == 0) { e.X = e.LeapToX; e.Y = e.LeapToY; e.Grounded = true; Ev(DuelEv.Land, e.X, e.Y); }
                    if (!leap) Face();
                    if (s.Lunge > 0) e.X = Map.MoveX(e.X, e.Y, e.Facing * s.Lunge * LungeMulOf(cp), E.Half, E.Height, out _);
                    double reach = s.Reach * rm;
                    Box b;
                    if (leap) b = new Box(e.X - reach, e.X + reach, e.Y, e.Y + s.Height);
                    else
                    {
                        double y0 = e.Y + (s.Kind == HitKind.Low ? 0 : BoxBottomOf(cp));
                        b = e.Facing > 0 ? new Box(e.X, e.X + reach, y0, e.Y + s.Height) : new Box(e.X - reach, e.X, y0, e.Y + s.Height);
                    }
                    active.Add(new Active { id = i, step = s, box = b, left = O(cp?.MeleeActive ?? -1, T.MeleeActive) });
                    Ev(DuelEv.Hit, (b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, reach, (int)s.Kind);
                    CheckHit(i, s, b);
                    break;
                }
            }
        }

        /// <summary>결투자 타격이 플레이어에 닿았는지. 반격 상태 = 카운터 성공(투사체 제외), 무적 · 넉백 중 = 무시, 아니면 맞음.</summary>
        void CheckHit(int id, PatStep s, Box b)
        {
            if (stepHit.Contains(id) || P.EventLock || !b.Overlaps(P.Hurtbox)) return;
            if (P.InCounter && s.Counterable) { stepHit.Add(id); PlayerCounterSuccess(); return; }
            if (P.Invuln || P.Knocked || P.Bouncing) return;
            stepHit.Add(id);
            HurtPlayer(StepDamage(s), s.Kind == HitKind.Low ? "점프!" : s.Kind == HitKind.Charge ? "대쉬로 빠지기!" : null);
        }

        /// <summary>
        /// 결투자 카운터 공격 순간. 플레이어가
        ///  · 판정 창 안에서 카운터를 시작했으면 크래시(플레이어 승리),
        ///  · 그보다 일찍 시작해 자세로 버티고 있었으면 튕김(‘빨리 누르면 튕김’ 을 끄면 예전처럼 반격당함),
        ///  · 아무것도 안 했으면 판정 창(늦은 쪽)이 닫힐 때 맞는다 — 그 사이에 카운터를 시작하면 늦었어도 크래시.
        /// </summary>
        void EnemyCounterAttack()
        {
            var e = E;
            var box = e.CounterBox();
            Ev(DuelEv.Hit, e.X, e.Cy, T.EnemyCtrHalfW, -1);
            if (P.EventLock || !box.Overlaps(P.Hurtbox)) return;
            if (P.InCounter)
            {
                if (JustCrash) CounterCrash();
                else if (T.EarlyBounce >= 0.5) BounceOff();
                else EnemyCounterSuccess("빠른 카운터 — 반격당함!");
                return;
            }
            ctrHitAt = SongT + (T.JustCrashOn >= 0.5 ? GoodWin : 0);
        }

        /// <summary>미뤄 둔 결투자 카운터 공격: 그 사이 카운터를 시작했으면 크래시, 아니면 가만히 맞음(데미지 + 짧은 경직).</summary>
        void ResolveCounterHit()
        {
            ctrHitAt = -1;
            var e = E;
            if (e.KO || e.Disabled) return;
            bool late = P.InCounter && ctrPressT >= enemyAtkTime - 1e-9 && JustCrash;
            if (late && e.CounterBox().Overlaps(P.Hurtbox)) { CounterCrash(); return; }
            if (P.EventLock || P.Invuln || P.Knocked || P.Bouncing || !e.CounterBox().Overlaps(P.Hurtbox)) return;
            HurtPlayer(T.BaseDamage * T.EnemyCtrHitMul * DmgMul, "카운터로 받아치기!");
            if (T.EnemyCtrHitStun > 0 && PlayerHp > 0) P.KnockedBy(e.X, e.Cy, T.EnemyCtrHitStun);
        }

        /// <summary>튕김: 판정 창보다 빨리 카운터해서 서로의 자세가 부딪침 — 양쪽 다 짧게 밀려나 굳는다(결투자 카운터는 끝남).</summary>
        void BounceOff()
        {
            var e = E;
            ctrConnected = true;
            ctrHitAt = -1;
            active.Clear();
            e.EndCounter();
            double ex = e.X, ey = e.Cy, px = P.X, py = P.Cy;
            bool motion = T.OffCrashMotion >= 0.5;
            // 엇박 크래시 모션: 크래시처럼 둘이 마주 보게
            if (motion && Math.Abs(ex - px) > 1e-6) { P.Facing = ex > px ? 1 : -1; e.Facing = -P.Facing; }
            double dmg = T.BaseDamage * Math.Max(0, T.OffCrashDmgMul);
            if (dmg > 0) DamageEnemy(dmg, 1, false);
            e.StartReaction(px, py, Math.Max(0, T.BounceOffStun));
            P.KnockedBy(ex, ey, Math.Max(0, T.BounceOffStun));
            double db = (ctrPressT - enemyAtkTime) / Math.Max(0.01, Beat);          // 카운터 공격 순간보다 몇 박 빠른지(-) · 늦은지(+)
            string txt = T.CrashTimingText >= 0.5
                ? (db < 0 ? $"튕김 — {(-db):0.00}박 빨랐어요" : $"튕김 — {db:0.00}박 늦었어요")
                : (db < 0 ? "튕김 — 조금 빨랐어요" : "튕김 — 조금 늦었어요");
            if (dmg > 0) txt += $" · −{(int)dmg}";
            Ev(DuelEv.CounterBounce, (px + ex) / 2, Math.Max(P.Y, e.Y) + 1.4 * FightUnit.S, db, motion ? 1 : 0, txt);   // Act 1 = 크래시 모션 보여 줌
        }

        // ───────── 궁극기(궤도 폭격) ─────────
        /// <summary>시전 중인지 · 시작부터 지난 시간 · 레이저 가운데 X(결투자를 늦게 따라감).</summary>
        public bool UltOn => P.UltT > 0;
        public double UltElapsed, UltLaserX;
        public bool UltAiming => UltOn && UltElapsed < T.UltCut;
        double ultNextTick;

        void GainUlt(double v) { if (v > 0 && P.UltT <= 0) P.Ult = Math.Min(T.UltMax, P.Ult + v); }
        /// <summary>쓰는 데 필요한 양(최대보다 크면 최대).</summary>
        public double UltCost => Math.Max(1, Math.Min(T.UltMax, T.UltCost));

        /// <summary>궁극기 시작: 게이지가 가득 · 움직일 수 있을 때. 끝까지 일방적으로 시전(결투자 회피는 나중에).</summary>
        public bool PressUlt(double t)
        {
            double cost = UltCost;
            if (P.UltT > 0 || !P.CanAct || P.Ult < cost - 1e-6 || E.KO) return false;
            P.Ult = Math.Max(0, P.Ult - cost);
            P.UltT = Math.Max(0.1, T.UltTime);
            P.UltLockT = Math.Max(0, T.UltLock);
            P.UltInvT = Math.Max(0, T.UltInvuln);
            UltElapsed = 0;
            UltLaserX = E.X;
            ultNextTick = Math.Max(0, T.UltCut);
            pendingAtk = -1;
            Ev(DuelEv.UltStart, E.X, E.Y + 2 * FightUnit.S, 0, 0, "ORBITAL STRIKE");
            if (AllyOn && T.AllyCutIn >= 0.5) Ev(DuelEv.AllyCutIn, A.X, A.Y, Math.Max(0.2, T.AllyCutInTime));
            return true;
        }

        void TickUlt(double dt)
        {
            if (P.UltInvulnT > 0) P.UltInvulnT = Math.Max(0, P.UltInvulnT - dt);
            if (P.UltLockT > 0) P.UltLockT = Math.Max(0, P.UltLockT - dt);
            if (P.UltInvT > 0) P.UltInvT = Math.Max(0, P.UltInvT - dt);
            if (P.UltT <= 0) return;
            UltElapsed += dt;
            P.UltT -= dt;
            if (UltElapsed < T.UltCut) UltLaserX = E.X;                           // 조준 포착: 정확히 결투자 위치
            else
            {
                double k = 1 - Math.Exp(-dt / Math.Max(0.01, T.UltFollow));
                UltLaserX += (E.X - UltLaserX) * k;                                  // 폭격: 살짝 늦게 따라감
                double step = Math.Max(0.05, T.UltTickBeats * Beat);
                int guard = 0;
                while (UltElapsed >= ultNextTick && guard++ < 8)
                {
                    ultNextTick += step;
                    bool hit = !E.KO && Math.Abs(E.X - UltLaserX) <= T.UltWidth + E.Half;
                    Ev(DuelEv.UltStrike, UltLaserX, 0, hit ? 1 : 0, 0);
                    if (hit) { DamageEnemy(T.UltDmg * (T.UltBonus >= 0.5 ? Bonus(true) : 1), 4, false); Ev(DuelEv.UltHit, E.X, E.Y + 1.2 * FightUnit.S, T.UltDmg, 0); }
                }
            }
            if (P.UltT <= 0)
            {
                P.UltT = 0;
                P.UltInvulnT = Math.Max(0, T.UltAfter);                               // 끝난 뒤에도 잠깐 무적
                Ev(DuelEv.UltEnd, P.X, P.Y + 1 * FightUnit.S);
            }
        }

        void HurtPlayer(double dmg, string tip)
        {
            int d = (int)Math.Round(dmg);
            GainUlt(T.UltGainHurt);
            PlayerHp -= d;
            Taken += d;
            Combo = 0;
            Groove = Math.Max(0, Groove - T.GrooveHurt);
            P.Hurt();
            Ev(DuelEv.PlayerHurt, P.X, P.Y + 1 * FightUnit.S, d, 0, tip);
            if (PlayerHp <= 0) { PlayerHp = 0; Ev(DuelEv.PlayerDown, P.X, P.Y); }
        }

        void TickShots(double dt)
        {
            foreach (var s in Shots)
            {
                if (s.Dead) continue;
                s.X += s.Vx * dt; s.Y += s.Vy * dt; s.Life -= dt;
                if (s.Life <= 0 || Math.Abs(s.X) > Map.HalfW || s.Y < 0 || s.Y > Map.Ceil) { s.Dead = true; continue; }
                var sb = new Box(s.X - s.Radius, s.X + s.Radius, s.Y - s.Radius, s.Y + s.Radius);
                foreach (var w in Map.Solids) if (w.Overlaps(sb)) s.Dead = true;
                if (s.Dead || P.EventLock || !sb.Overlaps(P.Hurtbox)) continue;
                if (P.InCounter) { s.Dead = true; ctrConnected = true; Ev(DuelEv.Nullified, s.X, s.Y, 0, 0, "무효화"); continue; }   // 투사체 = 무효화만
                if (P.Invuln || P.Knocked || P.Bouncing) continue;
                s.Dead = true;
                HurtPlayer(s.Damage, "무빙으로 피하기!");
            }
            Shots.RemoveAll(s => s.Dead);
        }

        // ───────── 카운터 · 경직 · 넉백 ─────────
        double CenterDist() { double dx = E.X - P.X, dy = E.Cy - P.Cy; return Math.Sqrt(dx * dx + dy * dy); }

        void PlayerCounterSuccess()
        {
            var e = E;
            ctrConnected = true;
            if (Math.Abs(e.X - P.X) > 1e-6) { P.Facing = e.X > P.X ? 1 : -1; e.Facing = -P.Facing; }   // 카운터 성공: 둘이 서로 마주 보게
            bool near = CenterDist() <= T.NearCounterDist;              // 넉백 전 거리
            double dmg = T.BaseDamage * T.CtrSuccessMul * Bonus(ctrOnBeat);
            DamageEnemy(dmg, 1, ctrOnBeat);
            Counters++;
            Combo++;
            MaxCombo = Math.Max(MaxCombo, Combo);
            Groove = Math.Min(1, Groove + T.GrooveGain);
            active.Clear();
            restBars = 0;
            pendingReact = -1;
            spam.Clear();
            e.StartReaction(P.X, P.Cy, T.StunBase);                     // 무효 + 경직 + 넉백(패턴 끊김)
            if (near) e.AddStun(T.StunNear);
            Ev(DuelEv.CounterSuccess, e.X, e.Y + 1.2 * FightUnit.S, dmg, 1, near ? "근접 카운터!" : "COUNTER!");
            AllyCounter(false);
            GainUlt(T.UltGainCounter + (near ? T.UltGainNear : 0));
            if (near) Ev(DuelEv.NearCounter, e.X, e.Y + 1.2 * FightUnit.S);
        }

        void EnemyCounterSuccess(string why = null)
        {
            var e = E;
            bool near = CenterDist() <= T.NearCounterDist;
            int d = (int)Math.Round(T.BaseDamage * T.EnemyCtrSuccessMul * DmgMul * (Phase == 2 ? T.RageDamageMul : 1));
            PlayerHp -= d; Taken += d; Combo = 0; GainUlt(T.UltGainHurt);
            Groove = Math.Max(0, Groove - T.GrooveHurt);
            EnemyCounters++;
            e.EndCounter();
            P.KnockedBy(e.X, e.Cy, T.StunBase);
            if (near) P.AddStun(T.StunNear);
            Ev(DuelEv.EnemyCounter, P.X, P.Y + 1.2 * FightUnit.S, d, 0, why ?? (near ? "근접 반격당함!" : "반격당함!"));
            if (PlayerHp <= 0) { PlayerHp = 0; Ev(DuelEv.PlayerDown, P.X, P.Y); }
            // 프리딜 찬스: 바로 쫓아가 친다(반 박 뒤 시작)
            if (T.EnemyPunish >= 0.5 && PlayerHp > 0 && CurMap == MapKind.None) StartPattern(Pattern.PUNISH, QuantUp(BeatOf(SongT) + T.PunishLeadBeats));
        }

        void CounterCrash()
        {
            var e = E;
            ctrConnected = true;
            Crashes++;
            active.Clear();
            restBars = 0;
            pendingReact = -1;
            double ex = e.X, ey = e.Cy, px = P.X, py = P.Cy;
            // 크래시 = 정면 충돌: 둘이 서로 마주 보게
            if (Math.Abs(ex - px) > 1e-6) { P.Facing = ex > px ? 1 : -1; e.Facing = -P.Facing; }
            bool perfect = T.JustCrashOn >= 0.5 && PerfectCrash;
            ctrHitAt = -1;
            // 크래시 = 플레이어가 이김: 결투자는 오래 굳고 데미지, 플레이어는 짧게 밀렸다가 먼저 움직인다(퍼펙트면 더 크게)
            double dmg = T.BaseDamage * (perfect ? T.CrashPerfectDmgMul : T.CrashGoodDmgMul) * Bonus(ctrOnBeat);
            DamageEnemy(dmg, 1, ctrOnBeat);
            Combo++;
            MaxCombo = Math.Max(MaxCombo, Combo);
            Groove = Math.Min(1, Groove + T.GrooveGain);
            e.StartReaction(px, py, T.CrashStun + (perfect ? T.CrashWinBonus : 0));
            P.KnockedBy(ex, ey, T.CrashStun * (perfect ? T.CrashWinStunMul : T.CrashPlayerStunMul));
            // 크래시 뒤 휴식: 같은 장면이 연달아 반복되지 않게(결투자 쪽만 — 잘한 플레이어의 카운터는 그대로)
            e.CtrCd = Math.Max(e.CtrCd, T.CrashEnemyCooldown);
            if (T.CrashResetCharge >= 0.5) e.CtrRech = 0;
            habitAt = -1;
            Ev(DuelEv.CounterCrash, (px + ex) / 2, Math.Max(P.Y, e.Y) + 1.4 * FightUnit.S, dmg, perfect ? 1 : 0, perfect ? "PERFECT CRASH!" : "COUNTER CRASH!");
            AllyCounter(true);
            GainUlt(T.UltGainCrash);
        }

        // ───────── 동료 ─────────
        /// <summary>카운터 성공 · 크래시: 동료도 뒤에서 강한 사격(크래시면 컷인).</summary>
        void AllyCounter(bool crash)
        {
            if (!AllyOn) return;
            if (T.AllyOnCounter >= 0.5) A.Command(3, true, true, P, E);
            if (crash && T.AllyCutIn >= 0.5) Ev(DuelEv.AllyCutIn, A.X, A.Y, Math.Max(0.2, T.AllyCutInTime));
        }

        /// <summary>동료가 실제로 쏜 순간(보통 사격 · 강한 사격)의 판정.</summary>
        void OnAllyEv(AllyEv ev, int combo, double t)
        {
            var e = E;
            bool strike = ev == AllyEv.Strike, ctr = strike && A.CounterStrike;
            if (ev == AllyEv.Fly) { Ev(DuelEv.AllyFly, A.X, A.Y + Ally.HEIGHT, 0, 0); return; }
            Ev(strike ? DuelEv.AllyStrike : DuelEv.AllyShot, A.AimX, A.AimY, ctr ? 1 : 0, combo);
            if (strike && !ctr && T.AllyCutIn >= 0.5 && T.AllyCutIn3 >= 0.5) Ev(DuelEv.AllyCutIn, A.X, A.Y, Math.Max(0.2, T.AllyCutInTime));
            if (e.KO) return;
            bool hit = A.InRange(e, strike);
            A.AimHit = hit;
            if (!hit) return;
            if (e.Invuln) { if (!ctr) Ev(DuelEv.Immune, e.X, e.Y + 2 * FightUnit.S, 0, 0, "무적"); return; }
            if (ctr)
            {
                double cd = T.BaseDamage * Math.Max(0, T.AllyCtrDmgMul);
                if (cd > 0) DamageEnemy(cd, 2, false);
                return;
            }
            if (e.InCounter) { Ev(DuelEv.Immune, e.X, e.Y + 2 * FightUnit.S, 0, 0, "막힘"); return; }      // 결투자 반격 자세: 동료 사격은 막히기만(벌은 없음)
            double dmg = T.BaseDamage * T.AtkMul(combo) * Math.Max(0, T.AllyDmgMul) * (strike ? Math.Max(0, T.AllyStrikeMul) : 1) * Bonus(allyOnBeat);
            DamageEnemy(dmg, 2, allyOnBeat);
            P.HitLanded();
            e.FlinchT = Math.Max(e.FlinchT, strike ? Math.Max(0, T.AllyStrikeFlinch) : 0.12);
            NoteHit(t);
        }

        void DamageEnemy(double d, int act, bool onBeat)
        {
            if (E.KO || d <= 0) return;
            // 궁극기 게이지: 모든 데미지에서 조금씩(0 검기 · 2 기계 = 기본 공격, 3 = 카운터 공격 · 성공 · 크래시는 따로, 4 = 궁극기 자신은 안 참)
            if (act == 0 || act == 2) GainUlt(T.UltGainHit);
            else if (act == 3) GainUlt(T.UltGainCtrAtk);
            E.Hp -= d;
            Dealt += d;
            if (onBeat) OnBeats++;
            Ev(DuelEv.Damage, E.X, E.Y + 1.6 * FightUnit.S, d, act, onBeat ? "ON BEAT" : null);
        }

        /// <summary>연타 대응: 결투자가 멀쩡한 상태에서 짧은 시간에 여러 대 맞으면 카운터(반 박 뒤) 또는 밀쳐내기(다음 박).</summary>
        void NoteHit(double t)
        {
            if (Dummy || E.Disabled || E.KO || E.InCounter) return;
            spam.Add(t);
            spam.RemoveAll(x => x < t - T.SpamWindow);
            if (spam.Count < (int)Math.Round(T.SpamHits)) return;
            spam.Clear();
            if (E.CounterReady && pendingReact < 0)
                ScheduleCounter(t, TimeOfBeat(QuantUp(BeatOf(t) + T.ReactLeadBeats)), true, DuelEv.SpamReact, "!");
            else if (E.Cur == null && pendingReact < 0)
            {
                StartPattern(Pattern.SHOVE, QuantUp(BeatOf(t) + T.ShoveLeadBeats));
                Ev(DuelEv.SpamReact, E.X, E.Y + 2.4 * FightUnit.S, 0, 0, "밀쳐내기!");
            }
        }

        // ───────── 결투자 AI: 박 스텝 · 회피 · 빈틈 찌르기 · 경직 회복 ─────────
        double CtrAtkDelay => T.EnemyCtrAtkBeats * Beat;
        /// <summary>박 b 이후 첫 ‘반응 박 단위’ 자리(기본 반 박).</summary>
        double QuantUp(double b) { double q = FightTune.Pos(T.ReactQuantBeats, 0.0625); return Math.Ceiling(b / q - 1e-9) * q; }
        bool Free => !Dummy && breakT <= 0 && E.Cur == null && !E.InCounter && !E.Disabled && !E.KO && CurMap == MapKind.None && pendingReact < 0 && PlayerHp > 0;

        /// <summary>쉬는 동안: 박마다 스텝으로 거리 조절(가만히 서 있지 않는다), 발판 위 플레이어는 따라 점프, 아래로 내려가면 따라 내려간다.</summary>
        void Idle(double dt, double t, double bt)
        {
            var e = E;
            double dist = Math.Abs(P.X - e.X);
            double want = dist > T.EnemyFarDist ? T.EnemyFarWant : T.EnemyIdleReach * T.EnemyReachMul;
            Face();
            double gap = dist - want;
            int toward = Math.Sign(P.X - e.X);
            int beatI = (int)Math.Floor(bt);
            if (beatI != lastBeatI)
            {
                lastBeatI = beatI;
                // 구석 탈출: 등 뒤 벽이 가깝고 플레이어가 붙어 오면 머리 위로 넘어가 반대편으로
                if (InCorner() && dist < T.CornerTrigger && Rnd() < T.CornerChance && TryVault()) return;
                if (T.EnemyStepDist > 0 && e.Grounded && e.StepT <= 0 && Math.Abs(gap) > T.StepThreshold)
                {
                    // 박에 맞춘 스텝(가끔은 일부러 반대로 — 거리 흔들기)
                    int sgn = Math.Sign(gap) * toward;
                    if (Rnd() < T.StepFeint) sgn = -sgn;
                    e.StepVx = sgn * T.EnemyStepDist / FightTune.Pos(T.StepTime);
                    e.StepT = T.StepTime;
                }
                // 발판 위 플레이어 따라 점프 · 아래로 따라 내려가기
                if (T.EnemyPlatformJump >= 0.5 && e.Grounded)
                {
                    double up = P.Y - e.Y;
                    if (up > T.PlatformJumpHeight && P.Grounded && dist < T.PlatformJumpRange)
                    {
                        e.Vy = Math.Sqrt(2 * T.Gravity * Math.Max(0.01, T.EnemyGravityMul) * (up + T.PlatformJumpMargin));
                        e.Grounded = false;
                        e.StepVx = toward * Math.Max(Speed * T.PlatformJumpSpeedMul, dist / FightTune.Pos(T.PlatformJumpTime));
                        e.StepT = T.PlatformJumpTime;
                    }
                    else if (up < -T.PlatformJumpHeight && e.Y > 0.5 * FightUnit.S) e.DropT = Math.Max(0.05, T.DropThroughTime);
                }
            }
            double vx = Math.Abs(gap) < T.IdleDeadZone ? 0 : Math.Sign(gap) * toward * Speed * (T.EnemyStepDist > 0 ? T.IdleWalkMul : 1);
            // 가운데로 끌림: 결투장 가장자리일수록 가운데 쪽으로 걷는다(한쪽 벽에 몰려 있지 않게)
            double hw = Math.Max(1, Map.HalfW);
            vx += -Math.Sign(e.X) * Math.Min(1, Math.Abs(e.X) / hw) * Speed * Math.Max(0, T.CenterPull);
            if (e.StepT <= 0) MoveEnemy(dt, vx + Math.Sin(t * T.IdleWobbleFreq) * T.IdleWobble);
        }

        // ───────── 결투자 자연 이동 ─────────
        enum Intent { Hold, Approach, Retreat }
        Intent intent = Intent.Hold;
        double intentUntil, evadeAt = -1, punishAt = -1, evadeCd, punishCd, pIdleT;
        double faceHoldT, sameDirT, glanceAt, glanceUntil, lookUntil;
        int moveDir;
        bool evadeHit;

        int Agi => Math.Max(1, Math.Min(3, (int)Math.Round(T.EnemyAgility)));
        double ReactT => T.EnemyReact > 0 ? T.EnemyReact : Agi == 1 ? 0.35 : Agi == 2 ? 0.25 : 0.15;
        double EvadeChance => (Agi == 1 ? 0.15 : Agi == 2 ? 0.30 : 0.50) * Math.Max(0, T.EnemyEvadeMul);
        static double Tw(double v, double target, double step) => v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);

        /// <summary>플레이어가 방심했는지: 오래 가만히 · 카운터가 비었음 · 공격 위치 고정 중(헛친 틈은 NoteWhiff 가 따로).</summary>
        bool PlayerOpen => PlayerHp > 0 && (pIdleT >= T.EnemyOpenIdle || (!P.CounterReady && P.Grounded) || P.State == PState.Attack);

        /// <summary>
        /// 쉬는 동안(자연 이동): 차분하게 간격을 잰다 — 다가가기 · 물러나기 · 버티기를 몇 박씩 유지하고, 가속 · 감속으로 걷는다.
        /// 멀면 대쉬로 붙고, 발판 위 플레이어는 점프(필요하면 더블 점프)로 따라가고, 플레이어가 방심하면 대쉬로 붙어 찌른다.
        /// </summary>
        void IdleNatural(double dt, double t, double bt)
        {
            var e = E;
            double dist = Math.Abs(P.X - e.X);
            int toward = Math.Sign(P.X - e.X);
            if (toward == 0) toward = e.Facing;
            double want = dist > T.EnemyFarDist ? T.EnemyFarWant : T.EnemyIdleReach * T.EnemyReachMul;
            double band = Math.Max(0, T.EnemyHoldBand);
            Look(dt, t, toward, dist < (want + band) * Math.Max(1, T.EnemyEngageMul));
            // 플레이어가 가만히 있는 시간(방심 판단)
            pIdleT = P.State == PState.Idle || P.State == PState.Ready ? pIdleT + dt : 0;
            if (evadeCd > 0) evadeCd -= dt;
            if (punishCd > 0) punishCd -= dt;
            if (e.Grounded) e.AirJumps = Agi >= 2 ? 1 : 0;

            // 미뤄 둔 회피 · 몰아붙이기(사람 같은 반응 늦음)
            if (evadeAt >= 0 && t >= evadeAt) { evadeAt = -1; DoEvade(evadeHit); }
            if (punishAt >= 0 && t >= punishAt) { punishAt = -1; DoPunish(t); }

            // 구석 탈출 · 발판 따라가기는 박마다 판단
            int beatI = (int)Math.Floor(bt);
            if (beatI != lastBeatI)
            {
                lastBeatI = beatI;
                if (InCorner() && dist < T.CornerTrigger && Rnd() < T.CornerChance && TryVault()) return;
                if (T.EnemyPlatformJump >= 0.5 && e.Grounded)
                {
                    double up = P.Y - e.Y;
                    if (up > T.PlatformJumpHeight && P.Grounded && dist < T.PlatformJumpRange) { Jump(toward * Speed); }
                    else if (up < -T.PlatformJumpHeight && e.Y > 0.5 * FightUnit.S) e.DropT = Math.Max(0.05, T.DropThroughTime);
                }
                // 방심 노리기: 마디 안 어느 박에서든(쿨타임 · 거리 안)
                if (T.EnemyPunishOpen >= 0.5 && punishCd <= 0 && punishAt < 0 && PlayerOpen && dist < want + band + T.EnemyDashDist * 1.5)
                {
                    punishCd = Math.Max(0.5, T.EnemyPunishCd);
                    punishAt = t + ReactT;
                }
                // 멀면 대쉬로 붙기(위치 잡기 — 난이도별 확률)
                else if (e.Grounded && e.StepT <= 0 && dist > want + band * Math.Max(1, T.EnemyDashFarMul) && Rnd() < (Agi == 1 ? 0.15 : Agi == 2 ? 0.3 : 0.5))
                    Dash(toward);
            }

            // 의도: 정하면 몇 박 유지(경계에서 들썩이지 않게)
            if (t >= intentUntil)
            {
                var ni = dist > want + band ? Intent.Approach : dist < want - band ? Intent.Retreat : Intent.Hold;
                if (P.InCounter && dist < want + band) ni = Intent.Retreat;                 // 카운터 자세엔 섣불리 다가가지 않음(차분)
                intent = ni;
                intentUntil = t + Math.Max(0.1, T.EnemyCommitBeats * Beat);
            }
            double target = intent == Intent.Approach ? toward * Speed : intent == Intent.Retreat ? -toward * Speed * 0.8 : 0;
            // 버티기: 아주 살짝 흔들림(서 있는 사람처럼) · 가장자리면 가운데 쪽으로
            if (intent == Intent.Hold) target += Math.Sin(t * T.IdleWobbleFreq) * T.IdleWobble;
            double hw = Math.Max(1, Map.HalfW);
            target += -Math.Sign(e.X) * Math.Min(1, Math.Abs(e.X) / hw) * Speed * Math.Max(0, T.CenterPull);
            double acc = Math.Max(0.1, T.EnemyAccel) * (e.Grounded ? 1 : 0.5);
            e.Vx = Tw(e.Vx, target, acc * dt);
            e.Driven = true;
            if (e.StepT <= 0) MoveEnemy(dt, e.Vx);
        }

        /// <summary>
        /// 시선(자연): 교전 거리 안 · 멈춤 · 다가가기 = 플레이어를 봄. 교전 거리 밖에서 플레이어 반대쪽으로 계속 움직이면 돌아서서 가는 쪽을 보고,
        /// 그동안 가끔 뒤돌아봄. 한 번 돌아서면 잠깐은 그 방향(휙휙 돌지 않게) · 돌아설 때 foe-turn 그림 시간.
        /// </summary>
        void Look(double dt, double t, int toward, bool engaged)
        {
            var e = E;
            if (faceHoldT > 0) faceHoldT -= dt;
            if (T.EnemyLookMode < 0.5) { e.Facing = toward; return; }
            int md = Math.Abs(e.Vx) > 0.3 * Speed ? Math.Sign(e.Vx) : e.StepT > 0 ? Math.Sign(e.StepVx) : 0;
            if (md != 0 && md == moveDir) sameDirT += dt; else { moveDir = md; sameDirT = 0; }
            int want = toward;
            bool travelling = !engaged && md != 0 && md != toward && sameDirT >= Math.Max(0.05, T.EnemyTurnAfterBeats * Beat) && t >= lookUntil;
            if (travelling)
            {
                want = md;
                // 가는 쪽을 보며 걷는 동안 가끔 뒤돌아봄
                if (T.EnemyGlanceBeats > 0)
                {
                    if (glanceAt <= 0) glanceAt = t + T.EnemyGlanceBeats * Beat;
                    if (t >= glanceAt) { glanceUntil = t + Math.Max(0.1, T.EnemyGlanceTime); glanceAt = t + T.EnemyGlanceBeats * Beat; }
                    if (t < glanceUntil) want = toward;
                }
            }
            else glanceAt = 0;
            if (want != e.Facing && (faceHoldT <= 0 || (engaged && want == toward && md == 0)))
            {
                e.Facing = want;
                faceHoldT = Math.Max(0, T.EnemyFaceHold);
                e.TurnT = Math.Max(0, T.EnemyTurnTime);
            }
        }

        void Jump(double vx)
        {
            var e = E;
            e.Vy = Math.Max(0.1, T.EnemyJumpV);
            e.Grounded = false;
            e.Vx = vx;
            Ev(DuelEv.EnemyJump, e.X, e.Y, 0, 0);
        }

        bool DJump(double vx)
        {
            var e = E;
            if (e.Grounded || e.AirJumps <= 0) return false;
            e.AirJumps--;
            e.Vy = Math.Max(0.1, T.EnemyDJumpV);
            e.Vx = vx;
            Ev(DuelEv.EnemyJump, e.X, e.Y, 1, 0);
            return true;
        }

        void Dash(int dir)
        {
            var e = E;
            if (dir == 0) return;
            double tm = FightTune.Pos(T.EnemyDashTime, 0.02);
            e.StepVx = dir * Math.Max(0.1, T.EnemyDashDist) / tm;
            e.StepT = tm;
            e.DashT = tm;
            e.Vx = dir * Speed;
            Ev(DuelEv.EnemyDash, e.X, e.Y + 1 * FightUnit.S, dir, 0);
        }

        /// <summary>회피하고 싶음(맞았거나 · 플레이어가 대쉬로 붙음): 난이도 확률 → 반응 늦음 뒤 점프 · 더블 점프 · 백대쉬.</summary>
        void WantEvade(double t, bool fromDash)
        {
            if (!Free || evadeCd > 0 || evadeAt >= 0) return;
            if (Rnd() >= EvadeChance) return;
            evadeCd = Agi == 1 ? 2.0 : Agi == 2 ? 1.4 : 0.9;
            evadeHit = !fromDash;
            evadeAt = t + ReactT;
        }

        void DoEvade(bool afterHit)
        {
            var e = E;
            if (!Free) return;
            int away = P.X < e.X ? 1 : -1;
            bool roomBack = RoomBehind() > T.EnemyDashDist * 0.8;
            if (!e.Grounded)
            {
                // 공중: 더블 점프로 위로, 난이도 3 은 공중 대쉬로 빠지기
                if (Agi >= 3 && roomBack && Rnd() < 0.5) Dash(away);
                else DJump(away * Speed);
            }
            else if (!roomBack || (P.Grounded && Rnd() < 0.45))
            {
                // 등 뒤가 막혔거나 가끔: 뛰어오르며 피하기(벽 쪽이면 플레이어 머리 위로)
                Jump((roomBack ? away : -away) * Speed * 1.2);
            }
            else Dash(away);                                                          // 백대쉬
            e.Facing = -away; faceHoldT = 0; lookUntil = SongT + 0.5;                 // 피한 뒤엔 바로 플레이어를 다시 봄
            Ev(DuelEv.Evade, e.X, e.Y + 2.2 * FightUnit.S, 0, 0, "회피");
        }

        /// <summary>방심 노리기: 대쉬로 붙어서 짧게 찌름(빈틈 찌르기와 같은 단타).</summary>
        void DoPunish(double t)
        {
            var e = E;
            if (!Free || !PlayerOpen) return;
            double dist = Math.Abs(P.X - e.X);
            int toward = Math.Sign(P.X - e.X);
            double reach = T.WhiffBaseRange * T.EnemyReachMul;
            if (dist > reach && e.Grounded) Dash(toward);
            aiCd = Math.Max(aiCd, T.WhiffCooldown);
            StartPattern(Pattern.JAB, QuantUp(BeatOf(t) + 0.5));
            Ev(DuelEv.SpamReact, e.X, e.Y + 2.4 * FightUnit.S, 0, 0, "틈!");
        }

        /// <summary>플레이어가 대쉬로 붙어 오면(가까이 · 내 쪽으로) 회피를 생각함.</summary>
        void NoteDash(double t)
        {
            var e = E;
            if (Math.Abs(P.X - e.X) > T.EnemyDashDist * 2.2) return;
            if (Math.Sign(e.X - P.X) != P.Facing) return;
            WantEvade(t, true);
        }

        /// <summary>등 뒤(플레이어 반대쪽) 벽까지 거리.</summary>
        double RoomBehind()
        {
            var e = E;
            int away = P.X < e.X ? 1 : -1;
            return Map.HalfW - away * e.X;
        }
        bool InCorner() => T.CornerOn >= 0.5 && RoomBehind() < T.CornerDist;

        /// <summary>플레이어 머리 위로 뛰어넘어 반대편에 착지(구석 탈출). 쿨타임 중 · 공중이면 false.</summary>
        bool TryVault()
        {
            var e = E;
            if (cornerCd > 0 || !e.Grounded || e.StepT > 0) return false;
            int toward = P.X > e.X ? 1 : -1;
            double target = P.X + toward * Math.Max(0, T.CornerLand);
            double lim = Map.HalfW - E.Half - 0.5 * FightUnit.S;
            target = Math.Max(-lim, Math.Min(lim, target));
            if (Math.Sign(target - e.X) != toward || Math.Abs(target - P.X) < 1 * FightUnit.S) return false;   // 넘어갈 자리가 없음
            double g = T.Gravity * Math.Max(0.01, T.EnemyGravityMul);
            double h = Math.Max(0.5 * FightUnit.S, T.CornerJumpHeight + Math.Max(0, P.Y - e.Y));
            e.Vy = Math.Sqrt(2 * g * h);
            double air = 2 * e.Vy / g;
            e.Grounded = false;
            e.StepVx = (target - e.X) / air;
            e.StepT = air;
            cornerCd = T.CornerCooldown;
            Ev(DuelEv.Evade, e.X, e.Y + 2.2 * FightUnit.S, 0, 0, "넘어가기");
            return true;
        }

        /// <summary>뒤로 스텝(회피). 구석이라 뒤로 빠질 자리가 없으면 넘어가기로 바꾼다.</summary>
        void BackStep(double dist)
        {
            var e = E;
            if (T.CornerOnEvade >= 0.5 && T.CornerOn >= 0.5 && RoomBehind() < dist + 1 * FightUnit.S && TryVault()) return;
            int away = P.X < e.X ? 1 : -1;
            e.StepVx = away * dist / FightTune.Pos(T.StepTime + 0.02);
            e.StepT = T.StepTime + 0.02;
            Ev(DuelEv.Evade, e.X, e.Y + 2.2 * FightUnit.S, 0, 0, "회피");
        }

        /// <summary>플레이어 공격을 맞고 빠지기(이어지는 기계 무기 · 콤보를 피함).</summary>
        void MaybeDodge()
        {
            if (T.EnemyMoveMode >= 0.5) { WantEvade(SongT, false); return; }     // 자연 이동: 점프 · 대쉬로 회피(난이도별)
            if (aiCd > 0 || !Free || Rnd() >= T.EnemyDodge) return;
            aiCd = T.DodgeCooldown;
            BackStep(T.DodgeDist);
        }

        /// <summary>헛친 틈: 플레이어 공격 · 카운터가 빗나가면 1박 뒤 찌른다(반 박 단위로 시작).</summary>
        void NoteWhiff(double t, bool counter)
        {
            if (aiCd > 0 || !Free) return;
            double dist = Math.Abs(P.X - E.X);
            if (dist > T.WhiffBaseRange * T.EnemyReachMul + T.WhiffRange || Rnd() >= T.EnemyWhiffPunish) return;
            aiCd = T.WhiffCooldown;
            StartPattern(Pattern.JAB, QuantUp(BeatOf(t) + T.WhiffLeadBeats));
        }

        /// <summary>경직이 풀리면 가만히 있지 않는다: 가까우면 물러나거나, 카운터를 노린다.</summary>
        void OnRecovered(double t)
        {
            if (E.KO || CurMap != MapKind.None) return;
            double dist = Math.Abs(P.X - E.X);
            if (dist > T.RecoverDist) return;
            if (Rnd() < T.EnemyRecoverEvade) BackStep(T.RecoverEvadeDist);
            else if (E.CounterReady && pendingReact < 0) ScheduleCounter(t, TimeOfBeat(QuantUp(BeatOf(t) + T.ReactLeadBeats)), true, DuelEv.SpamReact, "!");
        }

        // ───────── 맵 패턴(전역 공격) ─────────
        static readonly string[] MAP_NAME = { "", "유도 미사일", "낙뢰 기둥", "가로 레이저", "충격파 장벽" };
        static readonly string[] MAP_HINT = { "", "쫓아오는 미사일 — 계속 움직이고, 기둥에 부딪히게 하거나 대쉬 · 카운터로", "발밑에 떨어진다 — 예고 줄을 보고 박마다 옆으로", "낮으면 점프, 높으면 바닥에", "양 끝에서 밀려온다 — 박에 맞춰 점프로 넘기" };
        public static string MapName(MapKind k) => MAP_NAME[(int)k];
        public static string MapHint(MapKind k) => MAP_HINT[(int)k];

        bool MapOn(MapKind k) => k == MapKind.Missiles ? T.MissileOn >= 0.5 : k == MapKind.Columns ? T.ColumnOn >= 0.5 : k == MapKind.Lasers ? T.LaserOn >= 0.5 : k == MapKind.Walls && T.WallOn >= 0.5;
        bool MapInvuln(MapKind k) => k == MapKind.Missiles ? T.MissileInvuln >= 0.5 : k == MapKind.Columns ? T.ColumnInvuln >= 0.5 : k == MapKind.Lasers ? T.LaserInvuln >= 0.5 : k == MapKind.Walls && T.WallInvuln >= 0.5;
        int MapCount(MapKind k) => Math.Max(1, (int)Math.Round(k == MapKind.Missiles ? T.MisCount : k == MapKind.Columns ? T.ColCount : k == MapKind.Lasers ? T.LasCount : T.WallCount));
        double MapInterval(MapKind k) => k == MapKind.Missiles ? T.MisInterval : k == MapKind.Columns ? T.ColInterval : k == MapKind.Lasers ? T.LasInterval : T.WallInterval;
        double MapLead(MapKind k) => k == MapKind.Columns ? T.ColTelegraph : k == MapKind.Lasers ? T.LasTelegraph : k == MapKind.Walls ? T.WallTelegraph : 0;

        bool MapDue(int bar)
        {
            if (T.MapEnable < 0.5 || bar < (int)Math.Round(T.MapStartBar)) return false;
            if (lastMapBar != int.MinValue && bar - lastMapBar < Math.Max(1, (int)Math.Round(T.MapEveryBars * PM(T.Phase2MapEveryMul, T.Phase3MapEveryMul)))) return false;
            for (int k = 1; k <= 4; k++) if (MapOn((MapKind)k)) return true;
            return false;
        }

        /// <summary>강제로 맵 패턴 시작(테스트 · 연습용). 마디 경계가 아니어도 다음 박부터.</summary>
        public void ForceMap(MapKind k) { StartMap(k, Math.Ceiling(BeatOf(SongT))); }

        void StartMap(int bar)
        {
            var list = new List<MapKind>();
            for (int k = 1; k <= 4; k++) if (MapOn((MapKind)k) && (MapKind)k != lastMap) list.Add((MapKind)k);
            if (list.Count == 0) for (int k = 1; k <= 4; k++) if (MapOn((MapKind)k)) list.Add((MapKind)k);
            if (list.Count == 0) return;
            lastMapBar = bar;
            StartMap(list[(int)(Rnd() * list.Count) % list.Count], bar * (double)BeatsPerBar);
        }

        void StartMap(MapKind k, double startBeat)
        {
            var e = E;
            CurMap = lastMap = k;
            mapStart = startBeat;
            mapIndex = 0;
            mapSide = P.X < 0 ? 1 : -1;
            e.Cur = null; e.ChargeLeft = 0; active.Clear();
            e.Invuln = MapInvuln(k);
            Ev(DuelEv.MapStart, e.X, e.Y + 2.6 * FightUnit.S, (int)k, e.Invuln ? 1 : 0, MapName(k));
        }

        void EndMap()
        {
            Ev(DuelEv.MapEnd, E.X, E.Y + 2 * FightUnit.S, (int)CurMap);
            CurMap = MapKind.None;
            E.Invuln = false;
            restBars = 0;
        }

        /// <summary>맵 패턴 진행: 박에 맞춰 위험물을 내보내고(예고 → 타격), 결투자는 무적이면 공중에 떠 있고 아니면 거리를 잰다.</summary>
        void TickMap(double dt, double t)
        {
            var e = E;
            var k = CurMap;
            double rel = BeatOf(t) - mapStart;
            int n = MapCount(k);
            double iv = Math.Max(0.0625, MapInterval(k)), lead = MapLead(k);
            while (mapIndex < n && rel >= T.MapIntroBeats + mapIndex * iv)
            {
                double strike = mapStart + T.MapIntroBeats + lead + mapIndex * iv;
                Spawn(k, mapIndex, TimeOfBeat(strike), t);
                mapIndex++;
            }
            if (e.Invuln)
            {
                // 무적: 결투장 위로 떠올라 플레이어 쪽을 내려다본다
                double tx = Math.Max(-Map.HalfW + 2 * FightUnit.S, Math.Min(Map.HalfW - 2 * FightUnit.S, P.X - mapSide * T.HoverOffset));
                e.X += (tx - e.X) * Math.Min(1, dt * T.HoverFollow);
                e.Y += (T.EnemyHoverY - e.Y) * Math.Min(1, dt * T.HoverRise);
                e.Vy = 0; e.Grounded = false;
                Face();
            }
            else
            {
                Idle(dt, t, BeatOf(t));
                if (e.StepT > 0) { e.StepT -= dt; MoveEnemy(dt, e.StepVx); }
                e.Settle(dt);
            }
            if (mapIndex >= n && Hazards.Count == 0) EndMap();
        }

        void Spawn(MapKind k, int i, double armT, double t)
        {
            var e = E;
            var h = new Hazard { Kind = k, SpawnT = t, ArmT = armT };
            switch (k)
            {
                case MapKind.Missiles:
                {
                    // 위로 퍼지듯 쏘아 올린 뒤 플레이어를 쫓는다
                    double ang = Math.PI / 2 + (i % 2 == 0 ? 1 : -1) * (T.MisSpread + T.MisSpreadStep * (i / 2 % 3));
                    h.X = e.X; h.Y = e.Y + T.MisOriginY;
                    h.Vx = Math.Cos(ang) * T.MisSpeed; h.Vy = Math.Sin(ang) * T.MisSpeed;
                    h.HomeT = T.MisHoming; h.Life = T.MisHoming + T.MisLife; h.Half = T.MisRadius; h.HalfH = T.MisRadius;
                    h.ArmT = t;
                    Ev(DuelEv.Shot, h.X, h.Y);
                    break;
                }
                case MapKind.Columns:
                {
                    // 예고 시점의 플레이어 발밑(조금 흔들어서) — 박마다 옆으로 움직이게
                    double jitter = (Rnd() - 0.5) * T.ColJitter;
                    h.X = Math.Max(-Map.HalfW + 0.5 * FightUnit.S, Math.Min(Map.HalfW - 0.5 * FightUnit.S, P.X + jitter));
                    h.Half = T.ColWidth / 2; h.Y = Map.Ceil / 2; h.HalfH = Map.Ceil / 2;
                    h.EndT = armT + T.ColActive;
                    break;
                }
                case MapKind.Lasers:
                {
                    bool low = i % 2 == 0;
                    h.X = 0; h.Half = Map.HalfW;
                    h.Y = low ? T.LasThick : T.LasHighY + T.LasThick;
                    h.HalfH = T.LasThick;
                    h.Dir = low ? 0 : 1;
                    h.EndT = armT + T.LasActive;
                    break;
                }
                case MapKind.Walls:
                {
                    int side = (i % 2 == 0 ? 1 : -1) * mapSide;
                    h.Dir = -side;
                    h.X = side * Map.HalfW; h.Half = T.WallThick / 2; h.HalfH = T.WallHeight; h.Y = T.WallHeight / 2;
                    h.Vx = -side * T.WallSpeed;
                    break;
                }
            }
            Hazards.Add(h);
        }

        double MapDmg => T.MapDamage * DmgMul;

        void TickHazards(double dt, double t)
        {
            foreach (var h in Hazards)
            {
                if (h.Dead) continue;
                switch (h.Kind)
                {
                    case MapKind.Missiles:
                    {
                        if (h.HomeT > 0)
                        {
                            h.HomeT -= dt;
                            double want = Math.Atan2(P.Cy - h.Y, P.X - h.X), cur = Math.Atan2(h.Vy, h.Vx);
                            double d = want - cur;
                            while (d > Math.PI) d -= 2 * Math.PI;
                            while (d < -Math.PI) d += 2 * Math.PI;
                            double turn = Math.Max(-T.MisTurn * dt, Math.Min(T.MisTurn * dt, d));
                            double sp = Math.Sqrt(h.Vx * h.Vx + h.Vy * h.Vy);
                            h.Vx = Math.Cos(cur + turn) * sp; h.Vy = Math.Sin(cur + turn) * sp;
                        }
                        h.X += h.Vx * dt; h.Y += h.Vy * dt; h.Life -= dt;
                        var mb = new Box(h.X - h.Half, h.X + h.Half, h.Y - h.HalfH, h.Y + h.HalfH);
                        if (h.Life <= 0 || Math.Abs(h.X) > Map.HalfW || h.Y < 0 || h.Y > Map.Ceil + 2 * FightUnit.S) { h.Dead = true; break; }
                        foreach (var w in Map.Solids) if (w.Overlaps(mb)) { h.Dead = true; Ev(DuelEv.MapStrike, h.X, h.Y, (int)h.Kind); }
                        if (h.Dead || P.EventLock || !mb.Overlaps(P.Hurtbox)) break;
                        if (P.InCounter) { h.Dead = true; ctrConnected = true; Ev(DuelEv.Nullified, h.X, h.Y, 0, 0, "무효화"); break; }
                        if (P.Invuln || P.Knocked || P.Bouncing) break;
                        h.Dead = true;
                        HurtPlayer(MapDmg, "계속 움직이기!");
                        break;
                    }
                    case MapKind.Columns:
                    case MapKind.Lasers:
                    {
                        if (t >= h.ArmT && !h.Struck) { h.Struck = true; Ev(DuelEv.MapStrike, h.X, h.Kind == MapKind.Columns ? 0 : h.Y, (int)h.Kind); }
                        if (t >= h.ArmT && t <= h.EndT && !h.Hit) HazardHit(h, new Box(h.X - h.Half, h.X + h.Half, h.Y - h.HalfH, h.Y + h.HalfH), h.Kind == MapKind.Columns ? "옆으로!" : h.Dir == 0 ? "점프!" : "바닥에!");
                        if (t > h.EndT + 0.25) h.Dead = true;
                        break;
                    }
                    case MapKind.Walls:
                    {
                        if (t < h.ArmT) break;
                        if (!h.Struck) { h.Struck = true; Ev(DuelEv.MapStrike, h.X, 0, (int)h.Kind); }
                        h.X += h.Vx * dt;
                        if (!h.Hit) HazardHit(h, new Box(h.X - h.Half, h.X + h.Half, 0, h.HalfH), "점프로 넘기!");
                        if (Math.Abs(h.X) > Map.HalfW + 1 * FightUnit.S) h.Dead = true;
                        break;
                    }
                }
            }
            Hazards.RemoveAll(h => h.Dead);
        }

        /// <summary>환경 공격(기둥 · 레이저 · 장벽): 카운터로 막을 수 없다. 대쉬 무적 · 맞은 뒤 무적이면 통과.</summary>
        void HazardHit(Hazard h, Box b, string tip)
        {
            if (P.EventLock || !b.Overlaps(P.Hurtbox)) return;
            if (P.Invuln || P.Knocked || P.Bouncing) return;
            h.Hit = true;
            HurtPlayer(MapDmg, tip);
        }

        // ───────── 플레이어 공격 ─────────
        void OnPlayerEv(FsmEv ev, int combo, double t)
        {
            var e = E;
            if (T.EnemyMoveMode >= 0.5 && (ev == FsmEv.Dash || ev == FsmEv.BackDash)) NoteDash(t);
            switch (ev)
            {
                case FsmEv.Slash:
                case FsmEv.Mech:
                {
                    if (e.KO) break;
                    // 동료가 있으면 기계 공격 = 동료의 엄호 사격(3타는 강한 사격) — 판정은 동료가 실제로 쏘는 순간(OnAllyEv)
                    if (ev == FsmEv.Mech && AllyOn) { allyOnBeat = atkOnBeat; A.Command(combo, combo >= 3 && T.AllyStrike3 >= 0.5, false, P, e); break; }
                    if (!P.AttackBox(combo).Overlaps(e.Hurtbox)) { if (ev == FsmEv.Slash) NoteWhiff(t, false); break; }
                    if (e.Invuln) { if (ev == FsmEv.Slash) Ev(DuelEv.Immune, e.X, e.Y + 2 * FightUnit.S, 0, 0, "무적"); break; }
                    if (e.InCounter) { EnemyCounterSuccess(); break; }      // 결투자 반격 상태에 때림 → 결투자의 카운터
                    double dmg = T.BaseDamage * T.AtkMul(combo) * Bonus(atkOnBeat);
                    DamageEnemy(dmg, ev == FsmEv.Slash ? 0 : 2, atkOnBeat);
                    P.HitLanded();
                    if (ev == FsmEv.Slash) { Combo++; MaxCombo = Math.Max(MaxCombo, Combo); }
                    e.FlinchT = 0.12;
                    NoteHit(t);
                    if (ev == FsmEv.Slash) MaybeDodge();
                    break;
                }
                case FsmEv.CounterAtk:
                {
                    if (e.KO || !P.CounterBox().Overlaps(e.Hurtbox)) break;
                    if (e.Invuln) { Ev(DuelEv.Immune, e.X, e.Y + 2 * FightUnit.S, 0, 0, "무적"); break; }
                    ctrConnected = true;
                    if (e.InCounter)
                    {
                        // 결투자 자세에 내 카운터 공격이 닿음: 판정 창 안 = 크래시, 너무 빠르면 튕김(끄면 반격당함), 카운터 공격이 이미 지나갔으면(늦음) 그냥 튕김
                        if (P.InCounter && JustCrash) CounterCrash();
                        else if (T.EarlyBounce >= 0.5 || ctrPressT > enemyAtkTime) BounceOff();
                        else EnemyCounterSuccess("빠른 카운터 — 반격당함!");
                        break;
                    }
                    // 공격하지 않는 상대에게 쓴 카운터 = 데미지만(검기 · 기계 각각)
                    double dmg = T.BaseDamage * T.CtrAtkMul * 2 * Bonus(ctrOnBeat);
                    DamageEnemy(dmg, 3, ctrOnBeat);
                    break;
                }
                case FsmEv.WallBounce:
                case FsmEv.NearWallBounce:
                    Ev(DuelEv.PlayerWallBounce, P.X, P.Y + 1 * FightUnit.S, 0, 0, ev == FsmEv.NearWallBounce ? "근접 월 바운스" : "월 바운스");
                    break;
            }
        }

        /// <summary>지금 결투자가 예고 중인 다음 타격(화면 예고용): 몇 박 남았는지, 종류, 카운터인지.</summary>
        public bool NextThreat(out double beatsLeft, out HitKind kind, out bool counter)
        {
            beatsLeft = 0; kind = HitKind.Melee; counter = false;
            if (E.KO || E.Disabled) return false;
            if (E.InCounter) { counter = true; beatsLeft = E.CtrAtkT > 0 ? E.CtrAtkT / Beat : 0; return true; }
            if (pendingReact >= 0) { counter = true; beatsLeft = Math.Max(0, BeatOf(pendingReact) - BeatOf(SongT)); return true; }
            var p = E.Cur;
            if (p == null) return false;
            double rel = BeatOf(SongT) - E.CurStart;
            if (p.Counter)
            {
                if (E.DoneSteps.Contains(-1)) return false;
                // 위장 중엔 보통 베기처럼(반격 자세 시점이 ‘타격’ 인 척), 신호 뒤엔 카운터
                if (Disguised && rel < T.CounterStanceBeats - T.CounterTellBeats) { beatsLeft = Math.Max(0, T.CounterStanceBeats - rel); return true; }
                counter = true; beatsLeft = Math.Max(0, T.CounterStanceBeats + T.EnemyCtrAtkBeats - rel); return true;
            }
            double best = double.MaxValue;
            for (int i = 0; i < p.Steps.Count; i++)
            {
                if (E.DoneSteps.Contains(i)) continue;
                double left = p.Steps[i].Beat - rel;
                if (left < best) { best = left; kind = p.Steps[i].Kind; }
            }
            if (best == double.MaxValue) return false;
            beatsLeft = Math.Max(0, best);
            return true;
        }
    }
}
