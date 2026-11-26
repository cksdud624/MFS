using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoriFx
{
    /// <summary>카드를 어디서 · 누구 기준으로 재생하는지. 좌표는 게임 단위(U).</summary>
    public struct FxCtx
    {
        public Vector2 self, target, hit;  // 쓰는 쪽 · 맞는 쪽 · 타격 지점(쓰는 쪽 · 맞는 쪽 = 발 위치)
        /// <summary>쓰는 쪽 · 맞는 쪽의 히트박스(몸) 가운데 — ‘둘 사이’ 기준. hasBody 가 false 면 발 위치로 계산(예전 방식).</summary>
        public Vector2 selfBody, targetBody;
        public bool hasBody;
        public int facing;                 // 1 오른쪽 · -1 왼쪽
        public int owner;                  // 따라가기 기준(게임이 정하는 번호, 0 = 없음)
        public int targetOwner;
        public Vector2 dir;                // 움직이는 · 치는 방향(대쉬 8방향 · 타격 방향). 0 이면 바라보는 쪽

        public Vector2 Anchor(string a)
        {
            switch (a)
            {
                case "target": return target;
                case "hit": return hit;
                case "mid": return hasBody ? (selfBody + targetBody) * 0.5f : (self + target) * 0.5f;    // 두 히트박스 가운데의 가운데
                default: return self;
            }
        }

        public int OwnerOf(string a) => a == "target" ? targetOwner : a == "self" ? owner : 0;

        /// <summary>미리 계산용 기본값: 쓰는 쪽 1 · 맞는 쪽 2 · 오른쪽.</summary>
        public static FxCtx Default => new FxCtx { owner = 1, targetOwner = 2, facing = 1, dir = Vector2.right };

        /// <summary>방향(정규화). 없으면 바라보는 쪽.</summary>
        public Vector2 Dir => dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(facing >= 0 ? 1 : -1, 0);
    }

    /// <summary>화면 트랙을 합친 결과(매 프레임). 게임이 카메라 · 시간에 적용한다.</summary>
    public struct FxScreen
    {
        public Vector2 shake;              // px(1080p 기준) — 화면 높이 / 1080 을 곱해 쓰기
        public float zoom;                 // 1 = 그대로, 1.1 = 10% 당김
        public Color flash;                // a = 진하기
        public float timeScale;            // 히트스톱(모두): 1 = 보통, 0 = 멈춤
        public Vector2 zoomAt;             // 줌 중심(게임 단위) — zoomAnchored 일 때만
        public bool zoomAnchored;          // false = 화면 가운데(카메라 그대로)
        public int ownerA, ownerB;         // 히트스톱 ‘한쪽만’: 멈출 캐릭터 번호(0 = 없음)
        public float scaleA, scaleB;

        // 화면 효과(후처리) — 재생기가 할 수 있는 만큼 적용
        public float glow;                 // 빛 번짐 더하기(Bloom 세기 +)
        public float chroma;               // 색수차(px · 1080p 기준)
        public float blur;                 // 방사 블러(0 ~ 1)
        public Vector2 blurAt; public bool blurAnchored;
        public float waveStr, waveR, waveW; public Vector2 waveAt;     // 충격파: 세기(px@1080) · 반지름 · 두께(U) · 중심
        public float sat, invert;          // 채도(배) · 반전(0 ~ 1)
        public Color tint;                 // 색 입히기(a = 세기)
        public float tilt;                 // 화면 기울기(도)
        // 빛(주변 밝히기): 4개까지 — xy 중심(U) · z 반지름(U) · w 세기 / 색
        public int lightN;
        public Vector4 light0, light1, light2, light3;
        public Color lightCol0, lightCol1, lightCol2, lightCol3;
        // 일렁임(아지랑이): 중심 · 반지름(U) · 세기(px@1080) · 빠르기
        public Vector2 hazeAt; public float hazeR, hazeStr, hazeFreq;

        public Vector4 LightAt(int i) => i == 0 ? light0 : i == 1 ? light1 : i == 2 ? light2 : light3;
        public Color LightCol(int i) => i == 0 ? lightCol0 : i == 1 ? lightCol1 : i == 2 ? lightCol2 : lightCol3;

        internal void AddLight(Vector4 l, Color c)
        {
            if (lightN >= 4)
            {
                // 가장 약한 것과 바꿈
                int wk = 0; for (int i = 1; i < 4; i++) if (LightAt(i).w < LightAt(wk).w) wk = i;
                if (LightAt(wk).w >= l.w) return;
                Set(wk, l, c); return;
            }
            Set(lightN++, l, c);
        }

        void Set(int i, Vector4 l, Color c)
        {
            switch (i) { case 0: light0 = l; lightCol0 = c; break; case 1: light1 = l; lightCol1 = c; break; case 2: light2 = l; lightCol2 = c; break; default: light3 = l; lightCol3 = c; break; }
        }

        // 캐릭터 몸 효과(번쩍 · 색): 번호 2개까지
        public int bodyA, bodyB; public Color bodyColA, bodyColB; public bool bodyAddA, bodyAddB;

        /// <summary>이 캐릭터(번호)에 걸린 몸 효과(색 a = 세기). 없으면 false.</summary>
        public bool BodyOf(int owner, out Color c, out bool add)
        {
            if (owner != 0 && owner == bodyA) { c = bodyColA; add = bodyAddA; return c.a > 0.001f; }
            if (owner != 0 && owner == bodyB) { c = bodyColB; add = bodyAddB; return c.a > 0.001f; }
            c = default; add = false; return false;
        }

        internal void Body(int owner, Color c, bool add)
        {
            if (owner == 0) return;
            if (bodyA == 0 || bodyA == owner) { if (bodyA == 0 || c.a > bodyColA.a) { bodyA = owner; bodyColA = c; bodyAddA = add; } }
            else if (bodyB == 0 || bodyB == owner) { if (bodyB == 0 || c.a > bodyColB.a) { bodyB = owner; bodyColB = c; bodyAddB = add; } }
        }

        public static FxScreen None => new FxScreen { zoom = 1, timeScale = 1, scaleA = 1, scaleB = 1, sat = 1, flash = new Color(1, 1, 1, 0), tint = new Color(1, 1, 1, 0) };

        /// <summary>이 캐릭터(번호)의 시간 배율 = 모두 멈춤과 그 캐릭터만 멈춤 중 더 느린 쪽.</summary>
        public float ScaleOf(int owner)
        {
            float k = timeScale;
            if (owner != 0 && owner == ownerA) k = Mathf.Min(k, scaleA);
            if (owner != 0 && owner == ownerB) k = Mathf.Min(k, scaleB);
            return k;
        }

        internal void Slow(int owner, float k)
        {
            if (owner == 0) return;
            if (ownerA == 0 || ownerA == owner) { ownerA = owner; scaleA = Mathf.Min(scaleA, k); }
            else if (ownerB == 0 || ownerB == owner) { ownerB = owner; scaleB = Mathf.Min(scaleB, k); }
        }
    }

    /// <summary>
    /// 게임(또는 개발킷)이 구현하는 쪽: 이펙트 · 그림을 실제로 띄우고 지운다.
    /// 감독(FxDirector)은 시간표만 보고 “이 트랙을 나이 age 로 맞춰라”만 말한다.
    /// </summary>
    public interface IFxHost
    {
        /// <summary>트랙 하나를 띄움(age = 시작부터 지난 초). 못 띄우면 null.</summary>
        object Spawn(FxCard card, FxTrack track, FxCtx ctx, float age);
        /// <summary>매 프레임: 위치(따라가기) · 나이 맞추기. exact = 되감기 · 멈춤 · 한 칸 이동이라 나이를 정확히 다시 맞춰야 함.</summary>
        void Sync(object handle, FxCard card, FxTrack track, FxCtx ctx, float age, bool exact);
        void Despawn(object handle);
        /// <summary>길이가 자동(frames = 0)인 이펙트의 길이(초) 짐작. 모르면 2.</summary>
        float AutoLength(FxTrack track);
        /// <summary>따라가기: 이 번호의 지금 위치(못 찾으면 false).</summary>
        bool OwnerPos(int owner, out Vector2 pos);
    }

    /// <summary>재생 중인 카드 하나.</summary>
    public sealed class FxRun
    {
        public FxCard card;
        public FxCtx ctx;
        public float t;                    // 카드 시간(초)
        public float speed = 1;
        public bool paused, loop, hold;    // hold = 끝나도 지우지 않음(작업실 미리보기)
        public bool Done { get; internal set; }
        internal object[] h = Array.Empty<object>();
        internal FxTrack[] slot = Array.Empty<FxTrack>();   // 칸 = (트랙 × 개수), 무작위 · 방향을 적용한 트랙 사본
        internal int[] slotOf = Array.Empty<int>();         // 칸 → 원래 트랙 번호
        /// <summary>무작위 씨앗(재생마다 다름 · 같은 재생 안에선 되감아도 같음).</summary>
        public int seed;
        internal bool exact = true;
        internal float end;
        /// <summary>카드 끝(초): 카드 길이와 가장 늦게 끝나는 트랙 중 큰 쪽.</summary>
        public float End => end;
    }

    /// <summary>
    /// 연출 감독: 카드(시간표)를 재생하고 화면 값(흔들림 · 줌 · 섬광 · 히트스톱)을 계산한다.
    /// 화면 값은 카드 시간만의 함수라 되감기 · 한 칸 이동이 언제나 같은 결과.
    /// </summary>
    public sealed class FxDirector
    {
        readonly IFxHost host;
        readonly List<FxRun> runs = new List<FxRun>();
        public FxScreen Screen { get; private set; } = FxScreen.None;
        public IReadOnlyList<FxRun> Runs => runs;
        public int Max = 64;

        public FxDirector(IFxHost host) { this.host = host; }

        public FxRun Play(FxCard card, FxCtx ctx, float speed = 1)
        {
            if (card == null) return null;
            if (runs.Count >= Max) Stop(runs[0]);
            var r = new FxRun { card = card, ctx = ctx, speed = speed, seed = UnityEngine.Random.Range(1, int.MaxValue) };
            Slots(r);
            r.end = EndOf(card);
            runs.Add(r);
            Apply(r);
            return r;
        }

        public float Dur(FxCard c, FxTrack k) =>
            k.type == FxTrack.GHOST ? c.Sec(Mathf.Max(1, k.frames) + Mathf.Max(1, k.life))                // 잔상: 남기는 동안 + 마지막 것이 사라질 때까지
            : k.frames > 0 ? c.Sec(k.frames) : k.type == FxTrack.EFFECT ? Mathf.Max(0.05f, host.AutoLength(k)) : c.Sec(1);

        /// <summary>트랙 하나가 몇 칸(사본)을 쓰는지.</summary>
        public static int Copies(FxTrack k) => !k.IsSpawn ? 0 : k.type == FxTrack.GHOST ? 1 : Mathf.Clamp(k.count, 1, 64);

        public float EndOf(FxCard c)
        {
            float e = c.Sec(c.length);
            foreach (var k in c.tracks) if (k.on) e = Mathf.Max(e, c.Sec(k.at + LastStart(k)) + Dur(c, k));
            return Mathf.Max(e, 1f / 60);
        }

        /// <summary>여러 개를 띄울 때 마지막 것이 시작할 수 있는 가장 늦은 프레임(시작 프레임 기준).</summary>
        public static int LastStart(FxTrack k) => k.IsSpawn && k.type != FxTrack.GHOST ? Mathf.Max(0, k.count - 1) * Mathf.Max(0, k.gap) + Mathf.Max(0, k.jat) : 0;

        /// <summary>트랙 사본 만들기: 개수 · 간격 · 무작위(위치 · 회전 · 크기 · 시작) · 방향 맞추기. 씨앗이 같으면 언제나 같은 결과.</summary>
        void Slots(FxRun r)
        {
            var c = r.card;
            var list = new List<FxTrack>(); var of = new List<int>();
            for (int i = 0; i < c.tracks.Count; i++)
            {
                var k = c.tracks[i];
                if (!k.IsSpawn) continue;
                int n = Copies(k);
                for (int j = 0; j < n; j++)
                {
                    list.Add(Vary(k, r.ctx, r.seed, i, j));
                    of.Add(i);
                }
            }
            r.slot = list.ToArray(); r.slotOf = of.ToArray();
            r.h = new object[r.slot.Length];
        }

        /// <summary>한 칸의 트랙 사본: 무작위 값은 (씨앗, 트랙, 몇 번째)로만 정해짐.</summary>
        public static FxTrack Vary(FxTrack k, FxCtx ctx, int seed, int track, int copy)
        {
            var v = k.Clone();
            var rng = new System.Random(unchecked(seed * 31 + track * 7919 + copy * 104729));
            float R() => (float)(rng.NextDouble() * 2 - 1);           // −1 ~ 1
            if (k.type == FxTrack.GHOST) return v;                  // 잔상은 사본 · 무작위 없음(gap = 남기는 간격)
            v.at = k.at + copy * Mathf.Max(0, k.gap) + (k.jat > 0 ? rng.Next(0, k.jat + 1) : 0);
            v.x = k.x + R() * k.jx;
            v.y = k.y + R() * k.jy;
            v.rot = k.rot + R() * k.jrot;
            v.scale = Mathf.Max(0.01f, k.scale * (1 + R() * k.jscale));
            if (k.aim == "dir")
            {
                // 방향 맞추기: 오른쪽을 보는 그림 · 프리팹을 움직이는 방향으로 돌림(뒤집기가 켜져 있으면 왼쪽은 뒤집고 위아래만 돌림)
                var d = ctx.Dir;
                float ang = k.flip ? Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-4f ? 1e-4f : Mathf.Abs(d.x)) * Mathf.Rad2Deg : Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (k.flip && Mathf.Abs(d.x) < 1e-4f) ang = d.y >= 0 ? 90 : -90;
                var q = Quaternion.Euler(0, 0, ang);
                var off = q * new Vector3(v.x, v.y, 0);                // 비켜 놓기도 같이 돌림(대쉬 꼬리가 뒤에 남게)
                v.x = off.x; v.y = off.y;
                var mv = q * new Vector3(v.mx, v.my, 0);               // 이동도 같은 방향으로(참격이 치는 쪽으로 날아감)
                v.mx = mv.x; v.my = mv.y;
                v.rot += ang;
            }
            return v;
        }

        /// <summary>무작위 다시 굴리기(작업실: 다른 경우 보기).</summary>
        public void Reroll(FxRun r)
        {
            if (r == null) return;
            r.seed = UnityEngine.Random.Range(1, int.MaxValue);
            Refresh(r);
        }

        /// <summary>카드 시간을 옮김(되감기 · 한 칸 이동 · 끌기).</summary>
        public void Seek(FxRun r, float t)
        {
            if (r == null) return;
            r.t = Mathf.Max(0, t);
            r.exact = true;
            r.Done = false;
            if (!runs.Contains(r)) runs.Add(r);
            Apply(r);
        }

        /// <summary>카드 내용이 바뀜(작업실 편집): 띄운 것을 모두 지우고 지금 시간으로 다시 맞춤.</summary>
        public void Refresh(FxRun r)
        {
            if (r == null || !runs.Contains(r)) return;
            Clear(r);
            Slots(r);
            r.end = EndOf(r.card);
            r.exact = true;
            Apply(r);
        }

        public void Stop(FxRun r)
        {
            if (r == null) return;
            Clear(r);
            runs.Remove(r);
            r.Done = true;
        }

        public void StopAll() { for (int i = runs.Count - 1; i >= 0; i--) Stop(runs[i]); Screen = FxScreen.None; }

        void Clear(FxRun r)
        {
            for (int i = 0; i < r.h.Length; i++) if (r.h[i] != null) { host.Despawn(r.h[i]); r.h[i] = null; }
        }

        public void Tick(float dt)
        {
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var r = runs[i];
                if (!r.paused)
                {
                    r.t += dt * r.speed;
                    if (r.t >= r.end)
                    {
                        if (r.loop) { r.t = Mathf.Repeat(r.t, r.end); r.exact = true; Clear(r); }
                        else if (r.hold) { r.t = r.end; r.paused = true; }
                        else { Stop(r); continue; }
                    }
                }
                Apply(r);
            }
            Screen = Eval();
        }

        /// <summary>지금 카드 시간에 맞춰 트랙을 띄우거나 지움.</summary>
        void Apply(FxRun r)
        {
            var c = r.card;
            int need = 0;
            foreach (var t0 in c.tracks) need += Copies(t0);
            if (r.slot.Length != need) { Clear(r); Slots(r); r.exact = true; }
            for (int i = 0; i < r.slot.Length; i++)
            {
                var k = r.slot[i];
                if (!c.tracks[r.slotOf[i]].on) k.on = false;
                float a = c.Sec(k.at), d = Dur(c, k), age = r.t - a;
                bool live = k.on && age >= 0 && age < d;
                if (!live)
                {
                    if (r.h[i] != null) { host.Despawn(r.h[i]); r.h[i] = null; }
                    continue;
                }
                var ctx = r.ctx;
                if (k.follow)
                {
                    if (k.anchor == "mid")
                    {
                        // 둘 사이 따라가기: 두 캐릭터가 움직인 만큼(발 위치 변화) 몸 가운데도 같이 옮겨 계속 둘의 가운데에
                        if (ctx.owner != 0 && ctx.targetOwner != 0 && host.OwnerPos(ctx.owner, out var ps) && host.OwnerPos(ctx.targetOwner, out var pt))
                        {
                            ctx.selfBody += ps - ctx.self; ctx.targetBody += pt - ctx.target;
                            ctx.self = ps; ctx.target = pt;
                        }
                    }
                    else
                    {
                        int o = ctx.OwnerOf(k.anchor);
                        if (o != 0 && host.OwnerPos(o, out var p)) { if (k.anchor == "target") ctx.target = p; else ctx.self = p; }
                    }
                }
                if (r.h[i] == null) r.h[i] = host.Spawn(c, k, ctx, age);
                if (r.h[i] != null) host.Sync(r.h[i], c, k, ctx, age, r.exact || r.paused);
            }
            r.exact = false;
        }

        FxScreen Eval()
        {
            var s = FxScreen.None;
            float zoom = 0, best = 0;
            foreach (var r in runs) Add(ref s, ref zoom, ref best, r.card, r.t, r.ctx);
            s.zoom = Mathf.Max(0.2f, 1 + zoom);
            return s;
        }

        /// <summary>카드 하나만 시간 t(초)에서의 화면 값(작업실 · 미리 계산용).</summary>
        public static FxScreen EvalCard(FxCard c, float t) => EvalCard(c, t, FxCtx.Default);

        public static FxScreen EvalCard(FxCard c, float t, FxCtx ctx)
        {
            var s = FxScreen.None;
            float zoom = 0, best = 0;
            if (c != null) Add(ref s, ref zoom, ref best, c, t, ctx);
            s.zoom = Mathf.Max(0.2f, 1 + zoom);
            return s;
        }

        /// <summary>히트스톱을 반영한 캐릭터(번호 owner) 시간(초): 카드 시간 0 ~ t 동안 시간 배율을 1 프레임씩 더함.</summary>
        public static float SlowedTime(FxCard c, float t, int owner = 1)
        {
            if (c == null || t <= 0) return 0;
            float step = 1f / Mathf.Max(1, c.fps), acc = 0;
            var ctx = FxCtx.Default;
            for (float x = 0; x < t; x += step) acc += Mathf.Min(step, t - x) * EvalCard(c, x, ctx).ScaleOf(owner);
            return acc;
        }

        static void Add(ref FxScreen s, ref float zoom, ref float best, FxCard c, float t, FxCtx ctx)
        {
            foreach (var k in c.tracks)
            {
                if (!k.on || !k.IsScreen) continue;
                float a = c.Sec(k.at), d = Mathf.Max(c.Sec(1), c.Sec(k.frames)), tau = t - a;
                if (tau < 0 || tau >= d) continue;
                float v = FxCurve.Eval(k, tau / d);
                switch (k.type)
                {
                    case FxTrack.SHAKE:
                    {
                        float ph = tau * Mathf.Max(0.5f, k.freq) * Mathf.PI * 2;
                        float sx, sy;
                        if (k.dir == "hit")
                        {
                            // 타격 방향: 처음엔 맞은 쪽(바라보는 쪽)으로 밀렸다가 되돌아오며 떨림, 세로는 조금만
                            sx = Mathf.Cos(ph) * (ctx.facing >= 0 ? 1 : -1);
                            sy = Mathf.Sin(ph * 1.37f + 0.6f) * 0.2f;
                        }
                        else
                        {
                            sx = Mathf.Sin(ph) * 0.7f + Mathf.Sin(ph * 1.73f + 1.3f) * 0.3f;
                            sy = Mathf.Sin(ph * 1.11f + 2.1f) * 0.7f + Mathf.Sin(ph * 2.37f + 0.4f) * 0.3f;
                            if (k.dir == "x") sy = 0; else if (k.dir == "y") sx = 0;
                        }
                        s.shake += new Vector2(sx, sy) * (k.power * v);
                        break;
                    }
                    case FxTrack.ZOOM:
                    {
                        float z = k.power * v;
                        zoom += z;
                        if (k.anchor != "screen" && Mathf.Abs(z) > best) { best = Mathf.Abs(z); s.zoomAt = ctx.Anchor(k.anchor) + new Vector2(k.x * (ctx.facing >= 0 ? 1 : -1), k.y); s.zoomAnchored = true; }
                        break;
                    }
                    case FxTrack.FLASH:
                    {
                        var col = k.Col;
                        float al = Mathf.Clamp01(k.alpha * v);
                        if (al > s.flash.a) s.flash = new Color(col.r, col.g, col.b, al);
                        break;
                    }
                    case FxTrack.GLOW: s.glow += k.power * v; break;
                    case FxTrack.LIGHT:
                    {
                        var at = ctx.Anchor(k.anchor) + new Vector2(k.x * (ctx.facing >= 0 ? 1 : -1), k.y);
                        s.AddLight(new Vector4(at.x, at.y, Mathf.Max(0.1f, k.radius), k.power * v), k.Col);
                        break;
                    }
                    case FxTrack.HAZE:
                    {
                        float hs = k.power * v;
                        if (hs > s.hazeStr) { s.hazeStr = hs; s.hazeR = Mathf.Max(0.1f, k.radius); s.hazeFreq = k.freq; s.hazeAt = ctx.Anchor(k.anchor) + new Vector2(k.x * (ctx.facing >= 0 ? 1 : -1), k.y); }
                        break;
                    }
                    case FxTrack.CHROMA: s.chroma += k.power * v; break;
                    case FxTrack.TILT: s.tilt += k.power * v * (ctx.facing >= 0 ? 1 : -1); break;
                    case FxTrack.BLUR:
                    {
                        float b = k.power * v;
                        if (b > s.blur) { s.blurAnchored = k.anchor != "screen"; s.blurAt = ctx.Anchor(k.anchor) + new Vector2(k.x * (ctx.facing >= 0 ? 1 : -1), k.y); }
                        s.blur = Mathf.Max(s.blur, b);
                        break;
                    }
                    case FxTrack.WAVE:
                    {
                        float str = k.power * v;
                        if (str > s.waveStr)
                        {
                            float p = tau / d;
                            s.waveStr = str;
                            s.waveR = k.radius * (1 - (1 - p) * (1 - p));
                            s.waveW = Mathf.Max(0.05f, k.width);
                            s.waveAt = ctx.Anchor(k.anchor) + new Vector2(k.x * (ctx.facing >= 0 ? 1 : -1), k.y);
                        }
                        break;
                    }
                    case FxTrack.GRADE:
                    {
                        s.sat *= Mathf.Lerp(1, k.sat, v);
                        s.invert = Mathf.Max(s.invert, Mathf.Clamp01(k.invert * v));
                        var col = k.Col; float al = Mathf.Clamp01(k.alpha * v);
                        if (al > s.tint.a) s.tint = new Color(col.r, col.g, col.b, al);
                        break;
                    }
                    case FxTrack.BODY:
                    {
                        var col = k.Col; col.a = Mathf.Clamp01(k.alpha * v);
                        bool add = k.blend != "alpha";
                        if (k.who != "target") s.Body(ctx.owner, col, add);
                        if (k.who != "self") s.Body(ctx.targetOwner, col, add);
                        break;
                    }
                    case FxTrack.HITSTOP:
                    {
                        // 곡선 세기 1 = 설정한 시간 배율(power), 0 = 보통 속도
                        float ts = Mathf.Lerp(1, Mathf.Clamp01(k.power), v);
                        if (k.who == "self") s.Slow(ctx.owner, ts);
                        else if (k.who == "target") s.Slow(ctx.targetOwner, ts);
                        else s.timeScale = Mathf.Min(s.timeScale, ts);
                        break;
                    }
                }
            }
        }
    }
}
