using System.Collections.Generic;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 화면에 쓰이던 텍스처를 바로 지우지 않고 몇 프레임 뒤에 지움.
    /// 그림을 다시 읽거나(저장 → 다시 읽기) 캔버스 · 프레임이 바뀔 때, 화면(UI 배경 · 스프라이트)이 아직 옛 텍스처를 들고 있는 그 프레임에
    /// 지워 버리면 그리기 도중 지워진 텍스처를 만나 에디터가 불안정해질 수 있음 → 다들 새 텍스처로 바꾼 뒤에 지움.
    /// </summary>
    public static class TexGrave
    {
        const int DELAY = 10;     // 프레임
        static readonly List<(Object o, int frame)> q = new List<(Object, int)>();

        public static void Kill(Object o) { if (o != null) q.Add((o, Time.frameCount)); }

        /// <summary>KitRoot 가 매 프레임 부름.</summary>
        public static void Tick()
        {
            if (q.Count == 0) return;
            int now = Time.frameCount;
            for (int i = q.Count - 1; i >= 0; i--)
            {
                if (now - q[i].frame < DELAY) continue;
                if (q[i].o != null) Object.Destroy(q[i].o);
                q.RemoveAt(i);
            }
        }

        /// <summary>킷이 닫힐 때 남은 것 모두.</summary>
        public static void Flush()
        {
            foreach (var (o, _) in q) if (o != null) Object.Destroy(o);
            q.Clear();
        }
    }
}
