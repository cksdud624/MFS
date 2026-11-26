using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    // Mori 결투 화면(StageScreen)의 코드 이펙트 층 — 킷 작업실 미리보기에서 그대로 씀(Rhythm/RhythmDraw.cs 에서 옮김)
    /// <summary>
    /// 이펙트 층(화면 전체): 불꽃 · 줄기 · 고리 · 마름모 · 가로 충격파 · 빛줄기 · X. 매 프레임 Tick 후 다시 그린다.
    /// </summary>
    public sealed class FxField : VisualElement
    {
        public enum Kind { Dot, Streak, Ring, Diamond, HLine, Ray, Cross }

        sealed class P
        {
            public Kind K;
            public Vector2 Pos, Vel;
            public float Life, Max, Size, Size1, Width, Rot, VRot, Grav, Drag, Delay;
            public Color Col;
        }

        readonly List<P> ps = new List<P>();
        static readonly System.Random rnd = new System.Random();
        static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        public FxField() { generateVisualContent += Draw; pickingMode = PickingMode.Ignore; }

        public int Count => ps.Count;
        public void Reset() { ps.Clear(); MarkDirtyRepaint(); }

        void Add(P p) { if (ps.Count < 650) ps.Add(p); }   // 너무 많으면 그리기가 무거워진다

        public void Ring(Vector2 at, Color c, float r0, float r1, float dur, float width, float delay = 0) =>
            Add(new P { K = Kind.Ring, Pos = at, Col = c, Size = r0, Size1 = r1, Max = dur, Width = width, Delay = delay });

        public void Diamond(Vector2 at, Color c, float s0, float s1, float dur, float width, float rot = 45) =>
            Add(new P { K = Kind.Diamond, Pos = at, Col = c, Size = s0, Size1 = s1, Max = dur, Width = width, Rot = rot, VRot = 90 });

        public void HLine(Vector2 at, Color c, float half0, float half1, float dur, float width) =>
            Add(new P { K = Kind.HLine, Pos = at, Col = c, Size = half0, Size1 = half1, Max = dur, Width = width });

        public void Cross(Vector2 at, Color c, float size, float dur) =>
            Add(new P { K = Kind.Cross, Pos = at, Col = c, Size = size, Size1 = size * 1.3f, Max = dur, Width = 4 });

        /// <summary>불꽃: dir(도) 쪽으로 spread 만큼 퍼짐. streak 이면 꼬리 있는 줄기.</summary>
        public void Sparks(Vector2 at, Color c, int n, float dir, float spread, float v0, float v1, float life, float size, bool streak, float grav = 600, float delay = 0)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (dir + R(-spread, spread)) * Mathf.Deg2Rad;
                float v = R(v0, v1);
                Add(new P
                {
                    K = streak ? Kind.Streak : Kind.Dot, Pos = at, Vel = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * v,
                    Col = c, Max = life * R(0.7f, 1.2f), Size = size * R(0.6f, 1.3f), Grav = grav, Drag = 1.6f, Delay = delay + R(0, 0.05f)
                });
            }
        }

        public void Rays(Vector2 at, Color c, int n, float len0, float len1, float dur, float width, float spin)
        {
            float off = R(0, 360);
            for (int i = 0; i < n; i++)
                Add(new P { K = Kind.Ray, Pos = at, Col = c, Size = len0, Size1 = len1 * R(0.7f, 1.15f), Max = dur, Width = width * R(0.6f, 1.3f), Rot = off + i * 360f / n, VRot = spin });
        }

        public void Tick(float dt)
        {
            for (int i = ps.Count - 1; i >= 0; i--)
            {
                var p = ps[i];
                if (p.Delay > 0) { p.Delay -= dt; continue; }
                p.Life += dt;
                if (p.Life >= p.Max) { ps.RemoveAt(i); continue; }
                p.Vel *= Mathf.Max(0, 1 - p.Drag * dt);
                p.Vel.y += p.Grav * dt;
                p.Pos += p.Vel * dt;
                p.Rot += p.VRot * dt;
            }
            MarkDirtyRepaint();
        }

        static float EOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);
        static Color A(Color c, float a) { c.a = a; return c; }

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var q in ps)
            {
                if (q.Delay > 0) continue;
                float t = q.Life / q.Max, a = 1 - t;
                switch (q.K)
                {
                    case Kind.Dot:
                    {
                        p.fillColor = A(q.Col, a);
                        float rr = Mathf.Max(0.5f, q.Size * (1 - t * 0.6f));
                        p.BeginPath();
                        if (rr < 3.5f)
                        {
                            // 작은 불꽃은 원 대신 마름모(점 4개) — 원은 잘게 쪼개져 수백 개면 프레임이 떨어진다
                            p.MoveTo(q.Pos + new Vector2(0, -rr)); p.LineTo(q.Pos + new Vector2(rr, 0));
                            p.LineTo(q.Pos + new Vector2(0, rr)); p.LineTo(q.Pos + new Vector2(-rr, 0)); p.ClosePath();
                        }
                        else p.Arc(q.Pos, rr, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
                        p.Fill();
                        break;
                    }
                    case Kind.Streak:
                        p.strokeColor = A(q.Col, a);
                        p.lineWidth = Mathf.Max(0.5f, q.Size * (1 - t * 0.5f));
                        p.BeginPath(); p.MoveTo(q.Pos); p.LineTo(q.Pos - q.Vel * 0.045f); p.Stroke();
                        break;
                    case Kind.Ring:
                    {
                        float r = Mathf.Lerp(q.Size, q.Size1, EOut(t));
                        p.strokeColor = A(q.Col, a * 0.95f);
                        p.lineWidth = Mathf.Max(0.5f, q.Width * (1 - t));
                        p.BeginPath(); p.Arc(q.Pos, r, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree)); p.Stroke();
                        break;
                    }
                    case Kind.Diamond:
                    {
                        float s = Mathf.Lerp(q.Size, q.Size1, EOut(t));
                        p.strokeColor = A(q.Col, a);
                        p.lineWidth = Mathf.Max(0.5f, q.Width * (1 - t));
                        p.BeginPath();
                        for (int k = 0; k <= 4; k++)
                        {
                            float ang = (q.Rot + k * 90) * Mathf.Deg2Rad;
                            var v = q.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * s;
                            if (k == 0) p.MoveTo(v); else p.LineTo(v);
                        }
                        p.Stroke();
                        break;
                    }
                    case Kind.HLine:
                    {
                        float h = Mathf.Lerp(q.Size, q.Size1, EOut(t));
                        p.strokeColor = A(q.Col, a);
                        p.lineWidth = Mathf.Max(0.5f, q.Width * (1 - t));
                        p.BeginPath(); p.MoveTo(q.Pos - new Vector2(h, 0)); p.LineTo(q.Pos + new Vector2(h, 0)); p.Stroke();
                        p.strokeColor = A(Color.white, a * 0.8f);
                        p.lineWidth = Mathf.Max(0.5f, q.Width * 0.35f * (1 - t));
                        p.BeginPath(); p.MoveTo(q.Pos - new Vector2(h * 0.8f, 0)); p.LineTo(q.Pos + new Vector2(h * 0.8f, 0)); p.Stroke();
                        break;
                    }
                    case Kind.Ray:
                    {
                        float ang = q.Rot * Mathf.Deg2Rad;
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float l0 = Mathf.Lerp(q.Size, q.Size1 * 0.4f, EOut(t)), l1 = Mathf.Lerp(q.Size * 1.5f, q.Size1, EOut(t));
                        p.strokeColor = A(q.Col, a * 0.9f);
                        p.lineWidth = Mathf.Max(0.5f, q.Width * (1 - t * 0.7f));
                        p.BeginPath(); p.MoveTo(q.Pos + d * l0); p.LineTo(q.Pos + d * l1); p.Stroke();
                        break;
                    }
                    case Kind.Cross:
                    {
                        float s = Mathf.Lerp(q.Size, q.Size1, EOut(t));
                        p.strokeColor = A(q.Col, a);
                        p.lineWidth = q.Width;
                        p.BeginPath(); p.MoveTo(q.Pos + new Vector2(-s, -s)); p.LineTo(q.Pos + new Vector2(s, s)); p.Stroke();
                        p.BeginPath(); p.MoveTo(q.Pos + new Vector2(s, -s)); p.LineTo(q.Pos + new Vector2(-s, s)); p.Stroke();
                        break;
                    }
                }
            }
        }
    }
}
