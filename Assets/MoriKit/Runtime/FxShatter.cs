using System.Collections.Generic;
using UnityEngine;

namespace MoriFx
{
    /// <summary>
    /// 조각내기: 그림(텍스처의 uv 영역)을 cols × rows 조각으로 쪼개 시간에 따라 날린다(메시 하나로 — 가벼움).
    /// 메시 좌표 = 그림 높이 1 기준(가로 = 폭/높이), 피벗 = 발밑(0.5, 0) 또는 가운데. 시간만의 함수라 되감아도 같다.
    /// </summary>
    public static class FxShatter
    {
        static readonly List<Vector3> v = new List<Vector3>();
        static readonly List<Vector2> uv = new List<Vector2>();
        static readonly List<Color> col = new List<Color>();
        static readonly List<int> tri = new List<int>();

        /// <param name="age">시작부터 지난 초</param>
        /// <param name="dur">길이(초)</param>
        /// <param name="aspect">그림 폭 / 높이</param>
        /// <param name="uvRect">그림이 텍스처에서 차지하는 uv 영역</param>
        /// <param name="feet">true = 발밑 피벗, false = 가운데</param>
        /// <param name="unitsPerU">메시 1 = 몇 U 인지(속도 · 중력을 메시 단위로 바꿀 때)</param>
        public static void Build(Mesh mesh, FxTrack k, float age, float dur, float aspect, Rect uvRect, bool feet, float unitsPerU, int seed, Color c0, Color c1, float alpha)
        {
            v.Clear(); uv.Clear(); col.Clear(); tri.Clear();
            int cx = Mathf.Clamp(k.cols, 1, 48), cy = Mathf.Clamp(k.rows, 1, 48);
            float p = dur > 0 ? Mathf.Clamp01(age / dur) : 0;
            float inv = 1 / Mathf.Max(0.0001f, unitsPerU);
            float w = aspect, h = 1;
            float x0 = -w / 2, y0 = feet ? 0 : -0.5f;
            var center = new Vector2(x0 + w / 2, y0 + h / 2);
            var rng = new System.Random(seed);
            float fade = 1;
            if (k.fadeOut > 0) fade = Mathf.Clamp01((dur - age) * 60f / k.fadeOut);
            var cc = Color.Lerp(c0, c1, p);
            for (int j = 0; j < cy; j++)
                for (int i = 0; i < cx; i++)
                {
                    float r1 = (float)rng.NextDouble(), r2 = (float)rng.NextDouble(), r3 = (float)rng.NextDouble() * 2 - 1, r4 = (float)rng.NextDouble();
                    float pw = w / cx, ph = h / cy;
                    var pc = new Vector2(x0 + (i + 0.5f) * pw, y0 + (j + 0.5f) * ph);
                    // 날아가는 방향: 가운데에서 바깥쪽 + 무작위, 세기 = power(U/s) × (0.5 ~ 1.5)
                    var dir = (pc - center);
                    dir = dir.sqrMagnitude < 1e-6f ? Random2(r1) : dir.normalized;
                    dir = (dir + Random2(r1) * Mathf.Clamp01(k.jx)).normalized;
                    float spd = k.power * (0.5f + r2) * inv;
                    float delay = r4 * Mathf.Max(0, k.jat) / 60f;                 // 조각마다 늦게 출발(시작 무작위)
                    float tt = Mathf.Max(0, age - delay);
                    var pos = pc + dir * spd * tt + new Vector2(0, 0.5f * k.grav * inv * tt * tt);
                    float ang = r3 * k.spin * tt * Mathf.Deg2Rad;
                    float s = Mathf.Lerp(1, 1 - Mathf.Clamp01(k.shrink), p);
                    float hw = pw * 0.5f * s, hh = ph * 0.5f * s;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    int b = v.Count;
                    foreach (var (ox, oy) in new[] { (-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh) })
                        v.Add(new Vector3(pos.x + ox * cs - oy * sn, pos.y + ox * sn + oy * cs, 0));
                    float u0 = uvRect.x + uvRect.width * i / cx, u1 = uvRect.x + uvRect.width * (i + 1) / cx;
                    float v0 = uvRect.y + uvRect.height * j / cy, v1 = uvRect.y + uvRect.height * (j + 1) / cy;
                    uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
                    var c = cc; c.a = alpha * fade;
                    col.Add(c); col.Add(c); col.Add(c); col.Add(c);
                    tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);
                }
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        static Vector2 Random2(float r) { float a = r * Mathf.PI * 2; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
    }
}
