using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mori.Core.Rhythm;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 노트 차트 보관: 내장 테스트 차트(Resources/Rhythm/test.json) + 내가 만든 차트(persistentDataPath/charts/*.json).
    /// 시장 이벤트 때 쓸 차트: charts 폴더에 이벤트 id 로 시작하는 파일(예: quad_witching-1.json)이 있으면 그중 하나, 없으면 테스트 차트.
    /// </summary>
    public static class ChartStore
    {
        public const string TEST = "(내장) 테스트 차트";
        public static string Dir => Path.Combine(KitPaths.Data, "charts");

        public static RhythmChart Test()
        {
            var ta = Resources.Load<TextAsset>("Rhythm/test");
            if (ta == null) { Debug.LogWarning("[Mori] Resources/Rhythm/test.json 이 없습니다"); return new RhythmChart(); }
            return RhythmChart.FromJson(ta.text);
        }

        /// <summary>내가 만든 차트 파일 이름 목록.</summary>
        public static List<string> List()
        {
            // 내 폴더 + 빌드에 담긴 차트(StreamingAssets/Bundled/charts — 이름이 같으면 내 것)
            try { Directory.CreateDirectory(Dir); } catch (Exception) { }
            return Bundled.Files(Dir, "charts", n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        }

        public static RhythmChart Load(string name)
        {
            if (string.IsNullOrEmpty(name) || name == TEST) return Test();
            return RhythmChart.FromJson(File.ReadAllText(Bundled.Pick(Path.Combine(Dir, name), Path.Combine("charts", name)), Encoding.UTF8));
        }

        public static string Save(string name, RhythmChart c)
        {
            Directory.CreateDirectory(Dir);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
            foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            c.Normalize();
            string p = Path.Combine(Dir, name), tmp = p + ".tmp";
            File.WriteAllText(tmp, c.ToJson(), Encoding.UTF8);
            KitBackup.Before(p);                     // 덮어쓰기 전 자동 백업
            if (File.Exists(p)) File.Delete(p);
            File.Move(tmp, p);
            return name;
        }

        /// <summary>이벤트에 쓸 차트(이벤트 id 로 시작하는 내 차트가 있으면 그중 하나).</summary>
        public static RhythmChart ForEvent(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
            {
                var mine = List().Where(n => n.StartsWith(eventId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (mine.Count > 0)
                {
                    try { return Load(mine[UnityEngine.Random.Range(0, mine.Count)]); }
                    catch (Exception e) { Debug.LogWarning("[Mori] 이벤트 차트를 읽지 못해 테스트 차트를 씁니다: " + e.Message); }
                }
            }
            return Test();
        }
    }
}
